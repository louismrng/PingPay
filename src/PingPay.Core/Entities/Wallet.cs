namespace PingPay.Core.Entities;

public class Wallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Solana keys
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted private key using envelope encryption.
    /// Format: Base64(IV + EncryptedDEK + EncryptedPrivateKey)
    /// </summary>
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Key version from KMS/Key Vault used for envelope encryption.
    /// </summary>
    public string KeyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Encryption algorithm used (e.g., AES-256-GCM).
    /// </summary>
    public string KeyAlgorithm { get; set; } = "AES-256-GCM";

    // Cached balances
    public decimal CachedUsdcBalance { get; set; }
    public decimal CachedUsdtBalance { get; set; }
    public decimal CachedSolBalance { get; set; }
    public DateTime BalanceLastUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
