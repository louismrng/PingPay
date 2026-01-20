using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Constants;
using PingPay.Core.Enums;
using PingPay.Core.Exceptions;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;

namespace PingPay.Infrastructure.Services.Solana;

public class SolanaService : ISolanaService
{
    private readonly IRpcClient _rpcClient;
    private readonly SolanaOptions _options;
    private readonly ILogger<SolanaService> _logger;

    // Retry configuration
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    };

    public SolanaService(
        IOptions<SolanaOptions> options,
        ILogger<SolanaService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _rpcClient = ClientFactory.GetClient(_options.RpcUrl);
    }

    public (string PublicKey, byte[] PrivateKey) GenerateKeypair()
    {
        var wallet = new Wallet(WordCount.TwentyFour);
        var account = wallet.Account;

        return (account.PublicKey.Key, account.PrivateKey.KeyBytes);
    }

    public async Task<string> TransferTokenAsync(
        byte[] senderPrivateKey,
        string recipientPublicKey,
        decimal amount,
        TokenType tokenType,
        CancellationToken ct = default)
    {
        var senderAccount = new Account(senderPrivateKey, string.Empty);
        var senderPublicKey = senderAccount.PublicKey;

        _logger.LogInformation(
            "Initiating transfer: {Amount} {Token} from {Sender} to {Recipient}",
            amount, tokenType, senderPublicKey.Key[..8] + "...", recipientPublicKey[..8] + "...");

        try
        {
            // Validate inputs
            if (amount <= 0)
                throw new ValidationException("Amount must be greater than zero");

            if (!IsValidSolanaAddress(recipientPublicKey))
                throw new ValidationException("Invalid recipient address");

            var mintAddress = GetMintAddress(tokenType);
            var tokenAmount = DecimalToTokenAmount(amount);

            // Derive ATAs
            var senderAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                senderPublicKey,
                new PublicKey(mintAddress));

            var recipientAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                new PublicKey(recipientPublicKey),
                new PublicKey(mintAddress));

            // Check sender balance first
            var senderBalance = await GetTokenBalanceRawAsync(senderAta.Key, ct);
            if (senderBalance < tokenAmount)
            {
                throw new InsufficientBalanceException(
                    amount,
                    TokenAmountToDecimal(senderBalance));
            }

            // Check if recipient ATA exists
            var recipientAtaExists = await DoesAccountExistAsync(recipientAta.Key, ct);

            // Build and send transaction with retry
            return await ExecuteWithRetryAsync(async () =>
            {
                var blockHash = await GetRecentBlockhashAsync(ct);

                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockHash)
                    .SetFeePayer(senderPublicKey);

                // Create recipient ATA if needed (sender pays rent)
                if (!recipientAtaExists)
                {
                    _logger.LogDebug("Creating ATA for recipient {Recipient}", recipientPublicKey[..8] + "...");
                    txBuilder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        senderPublicKey,
                        new PublicKey(recipientPublicKey),
                        new PublicKey(mintAddress)));
                }

                // Add transfer instruction
                txBuilder.AddInstruction(TokenProgram.Transfer(
                    senderAta,
                    recipientAta,
                    tokenAmount,
                    senderPublicKey));

                var tx = txBuilder.Build(senderAccount);

                // Send with preflight checks
                var result = await _rpcClient.SendTransactionAsync(
                    tx,
                    skipPreflight: false,
                    commitment: Commitment.Confirmed);

                if (!result.WasSuccessful)
                {
                    var errorMsg = ParseTransactionError(result);
                    _logger.LogError("Transaction failed: {Error}", errorMsg);
                    throw new SolanaTransactionException(errorMsg);
                }

                _logger.LogInformation(
                    "Transaction sent. Signature: {Signature}",
                    result.Result);

                return result.Result;
            }, ct);
        }
        catch (SolanaTransactionException)
        {
            throw;
        }
        catch (InsufficientBalanceException)
        {
            throw;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token transfer");
            throw new SolanaTransactionException("Transfer failed: " + ex.Message, ex);
        }
    }

    public async Task<decimal> GetTokenBalanceAsync(
        string publicKey,
        TokenType tokenType,
        CancellationToken ct = default)
    {
        try
        {
            var mintAddress = GetMintAddress(tokenType);
            var walletPublicKey = new PublicKey(publicKey);

            var ata = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                walletPublicKey,
                new PublicKey(mintAddress));

            var rawBalance = await GetTokenBalanceRawAsync(ata.Key, ct);
            return TokenAmountToDecimal(rawBalance);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get {Token} balance for {PublicKey}",
                tokenType, publicKey[..8] + "...");
            return 0m;
        }
    }

    public async Task<string> EnsureAssociatedTokenAccountAsync(
        string walletPublicKey,
        TokenType tokenType,
        byte[]? payerPrivateKey = null,
        CancellationToken ct = default)
    {
        var mintAddress = GetMintAddress(tokenType);
        var walletPubKey = new PublicKey(walletPublicKey);

        var ata = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
            walletPubKey,
            new PublicKey(mintAddress));

        // Check if already exists
        if (await DoesAccountExistAsync(ata.Key, ct))
        {
            return ata.Key;
        }

        if (payerPrivateKey == null)
        {
            throw new SolanaTransactionException("Payer required to create ATA");
        }

        var payerAccount = new Account(payerPrivateKey, string.Empty);

        return await ExecuteWithRetryAsync(async () =>
        {
            var blockHash = await GetRecentBlockhashAsync(ct);

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(payerAccount.PublicKey)
                .AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    payerAccount.PublicKey,
                    walletPubKey,
                    new PublicKey(mintAddress)))
                .Build(payerAccount);

            var result = await _rpcClient.SendTransactionAsync(tx);

            if (!result.WasSuccessful)
            {
                throw new SolanaTransactionException($"Failed to create ATA: {ParseTransactionError(result)}");
            }

            _logger.LogInformation("Created ATA {Ata} for wallet {Wallet}",
                ata.Key, walletPublicKey[..8] + "...");

            return ata.Key;
        }, ct);
    }

    public async Task<bool> IsTransactionConfirmedAsync(string signature, CancellationToken ct = default)
    {
        try
        {
            var result = await _rpcClient.GetSignatureStatusesAsync(
                new List<string> { signature },
                searchTransactionHistory: true);

            if (!result.WasSuccessful || result.Result?.Value == null || result.Result.Value.Count == 0)
            {
                return false;
            }

            var status = result.Result.Value[0];
            if (status == null)
            {
                return false;
            }

            // Check if confirmed and no error
            return status.ConfirmationStatus == "confirmed" ||
                   status.ConfirmationStatus == "finalized";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking transaction status for {Signature}", signature);
            return false;
        }
    }

    /// <summary>
    /// Gets detailed transaction information including slot and block time.
    /// </summary>
    public async Task<SolanaTransactionDetails?> GetTransactionDetailsAsync(
        string signature,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _rpcClient.GetTransactionAsync(
                signature,
                Commitment.Confirmed);

            if (!result.WasSuccessful || result.Result == null)
            {
                return null;
            }

            return new SolanaTransactionDetails
            {
                Signature = signature,
                Slot = result.Result.Slot,
                BlockTime = result.Result.BlockTime.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(result.Result.BlockTime.Value).UtcDateTime
                    : null,
                Fee = result.Result.Meta?.Fee ?? 0,
                IsSuccess = result.Result.Meta?.Err == null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting transaction details for {Signature}", signature);
            return null;
        }
    }

    /// <summary>
    /// Gets the SOL balance for a wallet (needed for transaction fees).
    /// </summary>
    public async Task<decimal> GetSolBalanceAsync(string publicKey, CancellationToken ct = default)
    {
        try
        {
            var result = await _rpcClient.GetBalanceAsync(publicKey, Commitment.Confirmed);

            if (!result.WasSuccessful)
            {
                return 0m;
            }

            return (decimal)result.Result.Value / SolanaConstants.LamportsPerSol;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get SOL balance for {PublicKey}", publicKey[..8] + "...");
            return 0m;
        }
    }

    /// <summary>
    /// Estimates the fee for a token transfer transaction.
    /// </summary>
    public async Task<ulong> EstimateTransferFeeAsync(
        string senderPublicKey,
        string recipientPublicKey,
        TokenType tokenType,
        CancellationToken ct = default)
    {
        try
        {
            var mintAddress = GetMintAddress(tokenType);
            var senderPubKey = new PublicKey(senderPublicKey);

            var senderAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                senderPubKey,
                new PublicKey(mintAddress));

            var recipientAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                new PublicKey(recipientPublicKey),
                new PublicKey(mintAddress));

            var recipientAtaExists = await DoesAccountExistAsync(recipientAta.Key, ct);
            var blockHash = await GetRecentBlockhashAsync(ct);

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(senderPubKey);

            if (!recipientAtaExists)
            {
                txBuilder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    senderPubKey,
                    new PublicKey(recipientPublicKey),
                    new PublicKey(mintAddress)));
            }

            txBuilder.AddInstruction(TokenProgram.Transfer(
                senderAta,
                recipientAta,
                1, // Minimal amount for estimation
                senderPubKey));

            var message = txBuilder.CompileMessage();
            var feeResult = await _rpcClient.GetFeeForMessageAsync(Convert.ToBase64String(message));

            if (!feeResult.WasSuccessful || feeResult.Result?.Value == null)
            {
                // Default estimate: 5000 lamports base + 2039280 if creating ATA
                return recipientAtaExists ? 5000UL : 2044280UL;
            }

            return feeResult.Result.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error estimating transfer fee");
            return 5000UL; // Conservative default
        }
    }

    /// <summary>
    /// Waits for a transaction to be confirmed with polling.
    /// </summary>
    public async Task<bool> WaitForConfirmationAsync(
        string signature,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var pollInterval = TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (await IsTransactionConfirmedAsync(signature, ct))
            {
                return true;
            }

            await Task.Delay(pollInterval, ct);
        }

        return false;
    }

    #region Private Helpers

    private string GetMintAddress(TokenType tokenType)
    {
        if (_options.UseDevnet)
        {
            return tokenType switch
            {
                TokenType.USDC => SolanaConstants.UsdcMintAddressDevnet,
                TokenType.USDT => SolanaConstants.UsdcMintAddressDevnet,
                _ => throw new ArgumentOutOfRangeException(nameof(tokenType))
            };
        }

        return tokenType switch
        {
            TokenType.USDC => SolanaConstants.UsdcMintAddress,
            TokenType.USDT => SolanaConstants.UsdtMintAddress,
            _ => throw new ArgumentOutOfRangeException(nameof(tokenType))
        };
    }

    private async Task<string> GetRecentBlockhashAsync(CancellationToken ct)
    {
        var result = await _rpcClient.GetLatestBlockHashAsync(Commitment.Confirmed);

        if (!result.WasSuccessful || result.Result?.Value == null)
        {
            throw new SolanaTransactionException("Failed to get recent blockhash");
        }

        return result.Result.Value.Blockhash;
    }

    private async Task<ulong> GetTokenBalanceRawAsync(string ataAddress, CancellationToken ct)
    {
        var result = await _rpcClient.GetTokenAccountBalanceAsync(ataAddress, Commitment.Confirmed);

        if (!result.WasSuccessful || result.Result?.Value == null)
        {
            return 0;
        }

        return ulong.Parse(result.Result.Value.Amount);
    }

    private async Task<bool> DoesAccountExistAsync(string address, CancellationToken ct)
    {
        var result = await _rpcClient.GetAccountInfoAsync(address, Commitment.Confirmed);
        return result.WasSuccessful && result.Result?.Value != null;
    }

    private static ulong DecimalToTokenAmount(decimal amount)
    {
        return (ulong)(amount * (decimal)Math.Pow(10, SolanaConstants.TokenDecimals));
    }

    private static decimal TokenAmountToDecimal(ulong amount)
    {
        return (decimal)amount / (decimal)Math.Pow(10, SolanaConstants.TokenDecimals);
    }

    private static bool IsValidSolanaAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 32 || address.Length > 44)
            return false;

        try
        {
            _ = new PublicKey(address);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ParseTransactionError<T>(RequestResult<T> result)
    {
        if (!string.IsNullOrEmpty(result.Reason))
        {
            return result.Reason;
        }

        if (result.ServerErrorCode.HasValue)
        {
            return $"Server error {result.ServerErrorCode}";
        }

        return "Unknown error";
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SolanaTransactionException ex) when (IsRetryableError(ex) && attempt < MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning(
                    "Transaction attempt {Attempt} failed, retrying in {Delay}ms: {Error}",
                    attempt + 1, RetryDelays[attempt].TotalMilliseconds, ex.Message);

                await Task.Delay(RetryDelays[attempt], ct);
            }
            catch (Exception ex) when (ex is not SolanaTransactionException && attempt < MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning(
                    "Unexpected error on attempt {Attempt}, retrying: {Error}",
                    attempt + 1, ex.Message);

                await Task.Delay(RetryDelays[attempt], ct);
            }
        }

        throw lastException ?? new SolanaTransactionException("Transaction failed after retries");
    }

    private static bool IsRetryableError(SolanaTransactionException ex)
    {
        var message = ex.Message.ToLowerInvariant();

        // Retryable errors
        return message.Contains("blockhash") ||
               message.Contains("timeout") ||
               message.Contains("rate limit") ||
               message.Contains("connection") ||
               message.Contains("network");
    }

    #endregion
}
