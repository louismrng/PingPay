using PingPay.Core.Enums;

namespace PingPay.Core.Interfaces;

public interface ISolanaService
{
    /// <summary>
    /// Generates a new Solana keypair.
    /// </summary>
    /// <returns>Tuple of (publicKey, privateKeyBytes)</returns>
    (string PublicKey, byte[] PrivateKey) GenerateKeypair();

    /// <summary>
    /// Transfers SPL tokens between wallets.
    /// </summary>
    Task<string> TransferTokenAsync(
        byte[] senderPrivateKey,
        string recipientPublicKey,
        decimal amount,
        TokenType tokenType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the token balance for a wallet.
    /// </summary>
    Task<decimal> GetTokenBalanceAsync(
        string publicKey,
        TokenType tokenType,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures an Associated Token Account exists for the wallet.
    /// </summary>
    Task<string> EnsureAssociatedTokenAccountAsync(
        string walletPublicKey,
        TokenType tokenType,
        byte[]? payerPrivateKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current transaction status.
    /// </summary>
    Task<bool> IsTransactionConfirmedAsync(string signature, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed transaction information including slot and block time.
    /// </summary>
    Task<SolanaTransactionDetails?> GetTransactionDetailsAsync(
        string signature,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the SOL balance for a wallet (needed for transaction fees).
    /// </summary>
    Task<decimal> GetSolBalanceAsync(string publicKey, CancellationToken ct = default);

    /// <summary>
    /// Estimates the fee for a token transfer transaction.
    /// </summary>
    Task<ulong> EstimateTransferFeeAsync(
        string senderPublicKey,
        string recipientPublicKey,
        TokenType tokenType,
        CancellationToken ct = default);

    /// <summary>
    /// Waits for a transaction to be confirmed with polling.
    /// </summary>
    Task<bool> WaitForConfirmationAsync(
        string signature,
        TimeSpan timeout,
        CancellationToken ct = default);
}

/// <summary>
/// Detailed transaction information from Solana.
/// </summary>
public class SolanaTransactionDetails
{
    public string Signature { get; set; } = string.Empty;
    public ulong Slot { get; set; }
    public DateTime? BlockTime { get; set; }
    public ulong Fee { get; set; }
    public bool IsSuccess { get; set; }
}
