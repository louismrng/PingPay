using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

/// <summary>
/// High-level service for wallet private key encryption.
/// Handles secure key generation, encryption, and decryption with audit logging.
/// </summary>
public interface IWalletEncryptionService
{
    /// <summary>
    /// Generates a new Solana keypair and encrypts the private key.
    /// </summary>
    /// <returns>Wallet entity with encrypted private key (not persisted)</returns>
    Task<Wallet> GenerateEncryptedWalletAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Decrypts a wallet's private key for signing transactions.
    /// The returned key should be cleared from memory immediately after use.
    /// </summary>
    /// <param name="wallet">Wallet with encrypted private key</param>
    /// <returns>Decrypted private key bytes</returns>
    Task<byte[]> DecryptPrivateKeyAsync(Wallet wallet, CancellationToken ct = default);

    /// <summary>
    /// Re-encrypts a wallet's private key with the current/new master key.
    /// Used for key rotation.
    /// </summary>
    Task<Wallet> RotateEncryptionAsync(Wallet wallet, CancellationToken ct = default);

    /// <summary>
    /// Validates that a wallet's encrypted key can be decrypted.
    /// Does not return the key.
    /// </summary>
    Task<bool> ValidateEncryptionAsync(Wallet wallet, CancellationToken ct = default);
}
