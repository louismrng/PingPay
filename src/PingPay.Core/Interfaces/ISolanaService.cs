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
}
