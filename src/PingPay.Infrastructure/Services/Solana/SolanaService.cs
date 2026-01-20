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
using Solnet.Rpc.Types;
using Solnet.Wallet;

namespace PingPay.Infrastructure.Services.Solana;

public class SolanaService : ISolanaService
{
    private readonly IRpcClient _rpcClient;
    private readonly SolanaOptions _options;
    private readonly ILogger<SolanaService> _logger;

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
        try
        {
            var senderAccount = new Account(senderPrivateKey, string.Empty);
            var senderPublicKey = senderAccount.PublicKey;

            var mintAddress = GetMintAddress(tokenType);
            var tokenAmount = (ulong)(amount * (decimal)Math.Pow(10, SolanaConstants.TokenDecimals));

            // Get or derive Associated Token Accounts
            var senderAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                senderPublicKey,
                new PublicKey(mintAddress));

            var recipientAta = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(
                new PublicKey(recipientPublicKey),
                new PublicKey(mintAddress));

            // Check if recipient ATA exists
            var recipientAtaInfo = await _rpcClient.GetAccountInfoAsync(recipientAta.Key);

            var blockHash = await _rpcClient.GetLatestBlockHashAsync();
            if (!blockHash.WasSuccessful)
            {
                throw new SolanaTransactionException("Failed to get latest blockhash");
            }

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(senderPublicKey);

            // Create recipient ATA if it doesn't exist
            if (recipientAtaInfo.Result?.Value == null)
            {
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
            var result = await _rpcClient.SendTransactionAsync(tx);

            if (!result.WasSuccessful)
            {
                _logger.LogError("Solana transfer failed: {Error}", result.Reason);
                throw new SolanaTransactionException($"Transfer failed: {result.Reason}");
            }

            _logger.LogInformation(
                "Solana transfer successful. Signature: {Signature}, Amount: {Amount}, Token: {Token}",
                result.Result, amount, tokenType);

            return result.Result;
        }
        catch (SolanaTransactionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solana transfer error");
            throw new SolanaTransactionException("Transfer failed", ex);
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

            var balanceResult = await _rpcClient.GetTokenAccountBalanceAsync(ata.Key);

            if (!balanceResult.WasSuccessful || balanceResult.Result?.Value == null)
            {
                return 0m;
            }

            return decimal.Parse(balanceResult.Result.Value.UiAmountString ?? "0");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token balance for {PublicKey}", publicKey);
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

        var ataInfo = await _rpcClient.GetAccountInfoAsync(ata.Key);

        if (ataInfo.Result?.Value != null)
        {
            return ata.Key;
        }

        if (payerPrivateKey == null)
        {
            throw new SolanaTransactionException("Payer required to create ATA");
        }

        var payerAccount = new Account(payerPrivateKey, string.Empty);
        var blockHash = await _rpcClient.GetLatestBlockHashAsync();

        var tx = new TransactionBuilder()
            .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
            .SetFeePayer(payerAccount.PublicKey)
            .AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                payerAccount.PublicKey,
                walletPubKey,
                new PublicKey(mintAddress)))
            .Build(payerAccount);

        var result = await _rpcClient.SendTransactionAsync(tx);

        if (!result.WasSuccessful)
        {
            throw new SolanaTransactionException($"Failed to create ATA: {result.Reason}");
        }

        return ata.Key;
    }

    public async Task<bool> IsTransactionConfirmedAsync(string signature, CancellationToken ct = default)
    {
        try
        {
            var result = await _rpcClient.GetTransactionAsync(
                signature,
                Commitment.Confirmed);

            return result.WasSuccessful && result.Result != null;
        }
        catch
        {
            return false;
        }
    }

    private string GetMintAddress(TokenType tokenType)
    {
        if (_options.UseDevnet)
        {
            return tokenType switch
            {
                TokenType.USDC => SolanaConstants.UsdcMintAddressDevnet,
                TokenType.USDT => SolanaConstants.UsdcMintAddressDevnet, // Use same for devnet
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
}
