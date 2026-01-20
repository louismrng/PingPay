using PingPay.Core.Constants;
using PingPay.Core.DTOs.Payments;
using PingPay.Core.DTOs.Wallet;
using PingPay.Core.Entities;
using PingPay.Core.Enums;
using PingPay.Core.Exceptions;
using PingPay.Core.Interfaces;

namespace PingPay.Api.Services;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISolanaService _solanaService;
    private readonly IKeyManagementService _keyManagementService;
    private readonly ICacheService _cacheService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IWalletRepository walletRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        ISolanaService solanaService,
        IKeyManagementService keyManagementService,
        ICacheService cacheService,
        IAuditLogRepository auditLogRepository,
        ILogger<WalletService> logger)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _solanaService = solanaService;
        _keyManagementService = keyManagementService;
        _cacheService = cacheService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<Wallet> CreateWalletForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var existingWallet = await _walletRepository.GetByUserIdAsync(userId, ct);
        if (existingWallet != null)
        {
            return existingWallet;
        }

        // Generate new Solana keypair
        var (publicKey, privateKey) = _solanaService.GenerateKeypair();

        // Encrypt private key using envelope encryption
        var (encryptedBlob, keyVersion) = await _keyManagementService.EncryptAsync(privateKey, ct);

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PublicKey = publicKey,
            EncryptedPrivateKey = encryptedBlob,
            KeyVersion = keyVersion
        };

        await _walletRepository.CreateAsync(wallet, ct);

        _logger.LogInformation(
            "Wallet created for user {UserId}. PublicKey: {PublicKey}",
            userId, publicKey);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Action = "WALLET_CREATED",
            EntityType = "Wallet",
            EntityId = wallet.Id.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { wallet.PublicKey })
        }, ct);

        return wallet;
    }

    public async Task<WalletBalanceDto> GetBalanceAsync(
        Guid userId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("Wallet", userId.ToString());

        // Check cache unless force refresh
        if (!forceRefresh)
        {
            var cached = await _cacheService.GetAsync<WalletBalanceDto>(
                CacheKeys.WalletBalance(userId), ct);

            if (cached != null)
            {
                return cached;
            }
        }

        // Fetch fresh balances from Solana
        var usdcBalance = await _solanaService.GetTokenBalanceAsync(
            wallet.PublicKey, TokenType.USDC, ct);

        var usdtBalance = await _solanaService.GetTokenBalanceAsync(
            wallet.PublicKey, TokenType.USDT, ct);

        // Update cached balances in database
        wallet.CachedUsdcBalance = usdcBalance;
        wallet.CachedUsdtBalance = usdtBalance;
        wallet.BalanceLastUpdatedAt = DateTime.UtcNow;

        await _walletRepository.UpdateAsync(wallet, ct);

        var balanceDto = new WalletBalanceDto
        {
            PublicKey = wallet.PublicKey,
            UsdcBalance = usdcBalance,
            UsdtBalance = usdtBalance,
            LastUpdatedAt = wallet.BalanceLastUpdatedAt
        };

        // Cache for 30 seconds
        await _cacheService.SetAsync(
            CacheKeys.WalletBalance(userId),
            balanceDto,
            TimeSpan.FromSeconds(30),
            ct);

        return balanceDto;
    }

    public async Task<PaymentResponseDto> WithdrawAsync(
        Guid userId,
        WithdrawDto request,
        CancellationToken ct = default)
    {
        // Check idempotency
        var existingTx = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existingTx != null)
        {
            return MapToResponse(existingTx);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId.ToString());

        if (user.IsFrozen)
        {
            throw new AccountFrozenException();
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct)
            ?? throw new NotFoundException("Wallet", userId.ToString());

        // Check balance
        var balance = request.TokenType == TokenType.USDC
            ? wallet.CachedUsdcBalance
            : wallet.CachedUsdtBalance;

        if (balance < request.Amount)
        {
            throw new InsufficientBalanceException(request.Amount, balance);
        }

        // Create transaction record (receiver is self for withdrawals)
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = request.IdempotencyKey,
            SenderId = userId,
            ReceiverId = userId,
            Amount = request.Amount,
            TokenType = request.TokenType,
            Type = TransactionType.Withdrawal,
            Status = TransactionStatus.Processing
        };

        await _transactionRepository.CreateAsync(transaction, ct);

        try
        {
            // Decrypt private key
            var privateKey = await _keyManagementService.DecryptAsync(
                wallet.EncryptedPrivateKey,
                wallet.KeyVersion,
                ct);

            // Execute withdrawal to external address
            var signature = await _solanaService.TransferTokenAsync(
                privateKey,
                request.DestinationAddress,
                request.Amount,
                request.TokenType,
                ct);

            transaction.SolanaSignature = signature;
            transaction.Status = TransactionStatus.Confirmed;
            transaction.ConfirmedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Withdrawal completed. TxId: {TransactionId}, Signature: {Signature}, Destination: {Destination}",
                transaction.Id, signature, request.DestinationAddress[..8] + "...");
        }
        catch (Exception ex)
        {
            transaction.Status = TransactionStatus.Failed;
            transaction.ErrorMessage = ex.Message;

            _logger.LogError(ex,
                "Withdrawal failed. TxId: {TransactionId}, Error: {Error}",
                transaction.Id, ex.Message);
        }

        await _transactionRepository.UpdateAsync(transaction, ct);

        // Invalidate balance cache
        await _cacheService.RemoveAsync(CacheKeys.WalletBalance(userId), ct);

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Action = "WITHDRAWAL",
            EntityType = "Transaction",
            EntityId = transaction.Id.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                transaction.Amount,
                transaction.TokenType,
                DestinationAddress = request.DestinationAddress[..8] + "...",
                transaction.Status
            })
        }, ct);

        return MapToResponse(transaction);
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
