using Microsoft.Extensions.Logging;
using PingPay.Core.DTOs.WhatsApp;
using PingPay.Core.Entities;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Services.Solana;

namespace PingPay.Infrastructure.Services.WhatsApp;

/// <summary>
/// Handles WhatsApp bot commands and orchestrates responses.
/// </summary>
public class WhatsAppBotService
{
    private readonly IUserRepository _userRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISolanaService _solanaService;
    private readonly IWalletEncryptionService _walletEncryption;
    private readonly CachedSolanaBalanceService _balanceService;
    private readonly MessageParserService _parser;
    private readonly ILogger<WhatsAppBotService> _logger;

    public WhatsAppBotService(
        IUserRepository userRepository,
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        ISolanaService solanaService,
        IWalletEncryptionService walletEncryption,
        CachedSolanaBalanceService balanceService,
        MessageParserService parser,
        ILogger<WhatsAppBotService> logger)
    {
        _userRepository = userRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _solanaService = solanaService;
        _walletEncryption = walletEncryption;
        _balanceService = balanceService;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Process an incoming WhatsApp message and return a response.
    /// </summary>
    public async Task<WhatsAppResponse> ProcessMessageAsync(
        string phoneNumber,
        string message,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Processing message from {Phone}", phoneNumber[..4] + "****");

        var command = _parser.Parse(message);

        if (!command.IsValid)
        {
            return Error(command.ErrorMessage ?? "Invalid command");
        }

        // Get or create user
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, ct);

        return command.Type switch
        {
            CommandType.Help => Help(),
            CommandType.Register => await RegisterAsync(phoneNumber, user, ct),
            CommandType.Balance => await BalanceAsync(user, ct),
            CommandType.Send => await SendAsync(user, command, ct),
            CommandType.History => await HistoryAsync(user, ct),
            CommandType.Status => await StatusAsync(user, command.RawInput!, ct),
            _ => Error("Unknown command. Reply 'help' for options.")
        };
    }

    private WhatsAppResponse Help()
    {
        return Success(
            """
            *PingPay Commands*

            💰 *balance* - Check your balance
            📤 *send $10 to +1234567890* - Send payment
            📜 *history* - Recent transactions
            🔍 *status <id>* - Check transaction status
            📝 *register* - Create account

            Examples:
            • send $25 to +14155551234
            • send 100 +14155551234 usdt
            """);
    }

    private async Task<WhatsAppResponse> RegisterAsync(
        string phoneNumber,
        User? existingUser,
        CancellationToken ct)
    {
        if (existingUser != null)
        {
            var wallet = await _walletRepository.GetByUserIdAsync(existingUser.Id, ct);
            return Success($"You're already registered!\n\nYour wallet: `{wallet?.PublicKey[..8]}...`\n\nReply 'balance' to check funds.");
        }

        // Create new user
        var user = await _userRepository.CreateAsync(new User
        {
            PhoneNumber = phoneNumber,
            IsActive = true,
            IsPhoneVerified = true // Verified via WhatsApp
        }, ct);

        // Generate and encrypt wallet
        var wallet = await _walletEncryption.GenerateEncryptedWalletAsync(user.Id, ct);
        await _walletRepository.CreateAsync(wallet, ct);

        _logger.LogInformation("New user registered via WhatsApp: {UserId}", user.Id);

        return Success(
            $"""
            ✅ *Welcome to PingPay!*

            Your wallet: `{wallet.PublicKey}`

            To receive funds, share your wallet address or phone number.

            Reply 'help' for commands.
            """);
    }

    private async Task<WhatsAppResponse> BalanceAsync(User? user, CancellationToken ct)
    {
        if (user == null)
        {
            return Error("You're not registered. Reply 'register' to create an account.");
        }

        var wallet = await _walletRepository.GetByUserIdAsync(user.Id, ct);
        if (wallet == null)
        {
            return Error("Wallet not found. Contact support.");
        }

        var balances = await _balanceService.GetAllBalancesAsync(wallet.PublicKey, ct: ct);

        return Success(
            $"""
            💰 *Your Balance*

            USDC: ${balances.UsdcBalance:F2}
            USDT: ${balances.UsdtBalance:F2}
            SOL: {balances.SolBalance:F4}

            Wallet: `{wallet.PublicKey[..12]}...`
            """);
    }

    private async Task<WhatsAppResponse> SendAsync(
        User? user,
        ParsedCommand command,
        CancellationToken ct)
    {
        if (user == null)
        {
            return Error("You're not registered. Reply 'register' first.");
        }

        var senderWallet = await _walletRepository.GetByUserIdAsync(user.Id, ct);
        if (senderWallet == null)
        {
            return Error("Wallet not found.");
        }

        var tokenType = command.Token == "USDT" ? TokenType.USDT : TokenType.USDC;
        var amount = command.Amount!.Value;

        // Check balance
        var (hasSufficient, currentBalance) = await _balanceService
            .CheckSufficientBalanceAsync(senderWallet.PublicKey, amount, tokenType, ct);

        if (!hasSufficient)
        {
            return Error($"Insufficient balance. You have ${currentBalance:F2} {tokenType}.");
        }

        // Find recipient
        var recipient = await _userRepository.GetByPhoneNumberAsync(command.RecipientPhone!, ct);
        string recipientAddress;

        if (recipient != null)
        {
            var recipientWallet = await _walletRepository.GetByUserIdAsync(recipient.Id, ct);
            if (recipientWallet == null)
            {
                return Error("Recipient has no wallet.");
            }
            recipientAddress = recipientWallet.PublicKey;
        }
        else
        {
            return Error($"Recipient {command.RecipientPhone} is not on PingPay. Invite them to join!");
        }

        // Execute transfer
        try
        {
            var privateKey = await _walletEncryption.DecryptPrivateKeyAsync(senderWallet, ct);

            try
            {
                var signature = await _solanaService.TransferTokenAsync(
                    privateKey,
                    recipientAddress,
                    amount,
                    tokenType,
                    ct);

                // Record transaction
                var transaction = await _transactionRepository.CreateAsync(new Transaction
                {
                    SenderId = user.Id,
                    SenderWalletId = senderWallet.Id,
                    ReceiverId = recipient?.Id,
                    RecipientAddress = recipientAddress,
                    Amount = amount,
                    TokenType = tokenType,
                    Type = TransactionType.Transfer,
                    Status = TransactionStatus.Pending,
                    SolanaSignature = signature,
                    IdempotencyKey = Guid.NewGuid().ToString()
                }, ct);

                // Invalidate cache
                await _balanceService.InvalidateBalanceCacheAsync(senderWallet.PublicKey, tokenType, ct);

                _logger.LogInformation(
                    "WhatsApp transfer initiated: {TxId} from {Sender} to {Recipient}",
                    transaction.Id, user.Id, recipient?.Id);

                return Success(
                    $"""
                    ✅ *Payment Sent!*

                    Amount: ${amount:F2} {tokenType}
                    To: {command.RecipientPhone}

                    Transaction ID: `{transaction.Id}`

                    Reply 'status {transaction.Id}' to check.
                    """);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(privateKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp transfer failed");
            return Error($"Transfer failed: {ex.Message}");
        }
    }

    private async Task<WhatsAppResponse> HistoryAsync(User? user, CancellationToken ct)
    {
        if (user == null)
        {
            return Error("You're not registered. Reply 'register' first.");
        }

        var transactions = await _transactionRepository.GetByUserIdAsync(user.Id, limit: 5, ct: ct);

        if (!transactions.Any())
        {
            return Success("No transactions yet. Send your first payment with 'send $10 to +1234567890'");
        }

        var lines = transactions.Select(t =>
        {
            var direction = t.SenderId == user.Id ? "📤" : "📥";
            var sign = t.SenderId == user.Id ? "-" : "+";
            var status = t.Status switch
            {
                TransactionStatus.Completed => "✅",
                TransactionStatus.Pending => "⏳",
                TransactionStatus.Failed => "❌",
                _ => "❓"
            };
            return $"{direction} {sign}${t.Amount:F2} {t.TokenType} {status}";
        });

        return Success($"*Recent Transactions*\n\n{string.Join("\n", lines)}");
    }

    private async Task<WhatsAppResponse> StatusAsync(
        User? user,
        string transactionId,
        CancellationToken ct)
    {
        if (user == null)
        {
            return Error("You're not registered.");
        }

        if (!Guid.TryParse(transactionId, out var id))
        {
            return Error("Invalid transaction ID.");
        }

        var tx = await _transactionRepository.GetByIdAsync(id, ct);

        if (tx == null || (tx.SenderId != user.Id && tx.ReceiverId != user.Id))
        {
            return Error("Transaction not found.");
        }

        var statusEmoji = tx.Status switch
        {
            TransactionStatus.Completed => "✅ Completed",
            TransactionStatus.Pending => "⏳ Pending",
            TransactionStatus.Processing => "🔄 Processing",
            TransactionStatus.Failed => "❌ Failed",
            _ => "❓ Unknown"
        };

        return Success(
            $"""
            *Transaction Status*

            ID: `{tx.Id}`
            Amount: ${tx.Amount:F2} {tx.TokenType}
            Status: {statusEmoji}
            Date: {tx.CreatedAt:MMM dd, HH:mm}

            Signature: `{tx.SolanaSignature?[..16]}...`
            """);
    }

    private static WhatsAppResponse Success(string message) => new() { Message = message, Success = true };
    private static WhatsAppResponse Error(string message) => new() { Message = $"❌ {message}", Success = false };
}
