using PingPay.Core.Constants;
using PingPay.Core.DTOs.Payments;
using PingPay.Core.Entities;
using PingPay.Core.Enums;
using PingPay.Core.Exceptions;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace PingPay.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IUserRepository _userRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISolanaService _solanaService;
    private readonly IKeyManagementService _keyManagementService;
    private readonly ICacheService _cacheService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IUserRepository userRepository,
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ISolanaService solanaService,
        IKeyManagementService keyManagementService,
        ICacheService cacheService,
        IRateLimitService rateLimitService,
        IAuditLogRepository auditLogRepository,
        IOptions<RateLimitOptions> rateLimitOptions,
        ILogger<PaymentService> logger)
    {
        _userRepository = userRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _solanaService = solanaService;
        _keyManagementService = keyManagementService;
        _cacheService = cacheService;
        _rateLimitService = rateLimitService;
        _auditLogRepository = auditLogRepository;
        _rateLimitOptions = rateLimitOptions.Value;
        _logger = logger;
    }

    public async Task<PaymentResponseDto> SendPaymentAsync(
        Guid senderId,
        SendPaymentDto request,
        CancellationToken ct = default)
    {
        // Check idempotency
        var existingTx = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existingTx != null)
        {
            return MapToResponse(existingTx);
        }

        // Rate limiting
        var rateLimitKey = CacheKeys.RateLimit("transfer", senderId.ToString());
        var isAllowed = await _rateLimitService.IsAllowedAsync(
            rateLimitKey,
            _rateLimitOptions.TransactionsPerHour,
            TimeSpan.FromHours(1),
            ct);

        if (!isAllowed)
        {
            throw new RateLimitedException("Transfers");
        }

        // Get sender
        var sender = await _userRepository.GetByIdAsync(senderId, ct)
            ?? throw new NotFoundException("User", senderId.ToString());

        if (sender.IsFrozen)
        {
            throw new AccountFrozenException();
        }

        // Check daily limit
        var dailyTransferred = await _transactionRepository.GetDailyTransferredAmountAsync(
            senderId,
            sender.DailyLimitResetAt.AddDays(-1),
            ct);

        if (dailyTransferred + request.Amount > sender.DailyTransferLimit)
        {
            throw new DailyLimitExceededException(sender.DailyTransferLimit, dailyTransferred + request.Amount);
        }

        // Get receiver
        var receiver = await _userRepository.GetByPhoneNumberAsync(request.RecipientPhone, ct)
            ?? throw new NotFoundException("Recipient", request.RecipientPhone);

        if (receiver.Id == senderId)
        {
            throw new ValidationException("Cannot send payment to yourself");
        }

        // Get wallets
        var senderWallet = await _walletRepository.GetByUserIdAsync(senderId, ct)
            ?? throw new NotFoundException("Wallet", senderId.ToString());

        var receiverWallet = await _walletRepository.GetByUserIdAsync(receiver.Id, ct)
            ?? throw new NotFoundException("Wallet", receiver.Id.ToString());

        // Check balance
        var balance = request.TokenType == TokenType.USDC
            ? senderWallet.CachedUsdcBalance
            : senderWallet.CachedUsdtBalance;

        if (balance < request.Amount)
        {
            throw new InsufficientBalanceException(request.Amount, balance);
        }

        // Create transaction record
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = request.IdempotencyKey,
            SenderId = senderId,
            ReceiverId = receiver.Id,
            Amount = request.Amount,
            TokenType = request.TokenType,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Processing
        };

        await _transactionRepository.CreateAsync(transaction, ct);

        try
        {
            // Decrypt sender's private key
            var privateKey = await _keyManagementService.DecryptAsync(
                senderWallet.EncryptedPrivateKey,
                senderWallet.KeyVersion,
                ct);

            // Execute Solana transfer
            var signature = await _solanaService.TransferTokenAsync(
                privateKey,
                receiverWallet.PublicKey,
                request.Amount,
                request.TokenType,
                ct);

            transaction.SolanaSignature = signature;
            transaction.Status = TransactionStatus.Confirmed;
            transaction.ConfirmedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Payment completed. TxId: {TransactionId}, Signature: {Signature}",
                transaction.Id, signature);
        }
        catch (Exception ex)
        {
            transaction.Status = TransactionStatus.Failed;
            transaction.ErrorMessage = ex.Message;
            transaction.RetryCount++;

            _logger.LogError(ex,
                "Payment failed. TxId: {TransactionId}, Error: {Error}",
                transaction.Id, ex.Message);
        }

        await _transactionRepository.UpdateAsync(transaction, ct);

        // Invalidate balance cache
        await _cacheService.RemoveAsync(CacheKeys.WalletBalance(senderId), ct);
        await _cacheService.RemoveAsync(CacheKeys.WalletBalance(receiver.Id), ct);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = senderId,
            Action = "PAYMENT_SENT",
            EntityType = "Transaction",
            EntityId = transaction.Id.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                transaction.Amount,
                transaction.TokenType,
                RecipientId = receiver.Id,
                transaction.Status
            })
        }, ct);

        return MapToResponse(transaction);
    }

    public async Task<IReadOnlyList<TransactionHistoryDto>> GetHistoryAsync(
        Guid userId,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var transactions = await _transactionRepository.GetByUserIdAsync(userId, limit, offset, ct);

        return transactions.Select(t => new TransactionHistoryDto
        {
            TransactionId = t.Id,
            Type = t.Type,
            Status = t.Status,
            Amount = t.Amount,
            TokenType = t.TokenType,
            CounterpartyPhone = t.SenderId == userId
                ? t.Receiver?.PhoneNumber
                : t.Sender?.PhoneNumber,
            CounterpartyName = t.SenderId == userId
                ? t.Receiver?.DisplayName
                : t.Sender?.DisplayName,
            SolanaSignature = t.SolanaSignature,
            CreatedAt = t.CreatedAt,
            ConfirmedAt = t.ConfirmedAt
        }).ToList();
    }

    private static PaymentResponseDto MapToResponse(Transaction transaction) => new()
    {
        TransactionId = transaction.Id,
        Status = transaction.Status,
        Amount = transaction.Amount,
        TokenType = transaction.TokenType,
        SolanaSignature = transaction.SolanaSignature,
        CreatedAt = transaction.CreatedAt
    };
}
