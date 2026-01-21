using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PingPay.Core.Entities;
using PingPay.Core.Exceptions;
using PingPay.Core.Interfaces;
using Solnet.Wallet;

namespace PingPay.Infrastructure.Services.KeyManagement;

/// <summary>
/// High-level service for secure wallet private key encryption.
/// Wraps the low-level IKeyManagementService with wallet-specific logic.
/// </summary>
public class WalletEncryptionService : IWalletEncryptionService
{
    private readonly IKeyManagementService _keyManagementService;
    private readonly ILogger<WalletEncryptionService> _logger;

    // Magic bytes to identify encrypted wallet keys (helps detect corruption)
    private static readonly byte[] MagicHeader = "PPWK"u8.ToArray(); // PingPay Wallet Key

    public WalletEncryptionService(
        IKeyManagementService keyManagementService,
        ILogger<WalletEncryptionService> logger)
    {
        _keyManagementService = keyManagementService;
        _logger = logger;
    }

    public async Task<PingPay.Core.Entities.Wallet> GenerateEncryptedWalletAsync(Guid userId, CancellationToken ct = default)
    {
        // Generate a new Solana keypair using Solnet Account (parameterless ctor provides a keypair)
        var account = new Solnet.Wallet.Account();

        var publicKey = account.PublicKey.Key;
        var privateKeyBytes = account.PrivateKey.KeyBytes;

        if (privateKeyBytes == null)
        {
            throw new PingPayException("KEY_GEN_FAILED", "Failed to generate private key");
        }

        // Ensure we have a 64-byte secret key blob; if only 32 bytes present, expand to 64
        if (privateKeyBytes.Length == 32)
        {
            var expanded = new byte[64];
            Buffer.BlockCopy(privateKeyBytes, 0, expanded, 0, 32);
            var tail = RandomNumberGenerator.GetBytes(32);
            Buffer.BlockCopy(tail, 0, expanded, 32, 32);
            privateKeyBytes = expanded;
        }

        try
        {
            // Add magic header, timestamp and public key for validation
            var payload = CreatePayload(privateKeyBytes, userId, publicKey);

            // Encrypt using envelope encryption
            var (encryptedBlob, keyVersion) = await _keyManagementService.EncryptAsync(payload, ct);

            _logger.LogInformation(
                "Generated encrypted wallet for user {UserId}. PublicKey: {PublicKey}",
                userId, publicKey);

            return new PingPay.Core.Entities.Wallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedBlob,
                KeyVersion = keyVersion,
                KeyAlgorithm = "AES-256-GCM",
                CreatedAt = DateTime.UtcNow,
                BalanceLastUpdatedAt = DateTime.UtcNow
            };
        }
        finally
        {
            // Securely clear the private key from memory
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    public async Task<byte[]> DecryptPrivateKeyAsync(PingPay.Core.Entities.Wallet wallet, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(wallet.EncryptedPrivateKey))
        {
            throw new PingPayException("WALLET_INVALID", "Wallet has no encrypted private key");
        }

        byte[] payload;
        try
        {
            payload = await _keyManagementService.DecryptAsync(
                wallet.EncryptedPrivateKey,
                wallet.KeyVersion,
                ct);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt wallet {WalletId} - possible key corruption or wrong key version", wallet.Id);
            throw new PingPayException("DECRYPTION_FAILED", "Failed to decrypt wallet key", ex);
        }

        try
        {
            // Validate and extract the private key and embedded public key from payload
            var (privateKey, embeddedPublicKey) = ExtractPrivateKey(payload, wallet.UserId);

            // Validate that the embedded public key matches the stored wallet public key
            if (!string.Equals(embeddedPublicKey, wallet.PublicKey, StringComparison.Ordinal))
            {
                throw new PingPayException("KEY_MISMATCH", "Decrypted private key does not match wallet public key");
            }

            _logger.LogDebug("Successfully decrypted private key for wallet {WalletId}", wallet.Id);

            return privateKey;
        }
        finally
        {
            // Clear the payload from memory
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public async Task<PingPay.Core.Entities.Wallet> RotateEncryptionAsync(PingPay.Core.Entities.Wallet wallet, CancellationToken ct = default)
    {
        // First decrypt with the old key
        var privateKey = await DecryptPrivateKeyAsync(wallet, ct);

        try
        {
            // Create new payload
            var payload = CreatePayload(privateKey, wallet.UserId, wallet.PublicKey);

            try
            {
                // Re-encrypt with the current master key
                var (encryptedBlob, keyVersion) = await _keyManagementService.EncryptAsync(payload, ct);

                _logger.LogInformation(
                    "Rotated encryption for wallet {WalletId}. Old version: {OldVersion}, New version: {NewVersion}",
                    wallet.Id, wallet.KeyVersion, keyVersion);

                // Return updated wallet (not persisted - caller should save)
                wallet.EncryptedPrivateKey = encryptedBlob;
                wallet.KeyVersion = keyVersion;

                return wallet;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    public async Task<bool> ValidateEncryptionAsync(PingPay.Core.Entities.Wallet wallet, CancellationToken ct = default)
    {
        try
        {
            var privateKey = await DecryptPrivateKeyAsync(wallet, ct);
            CryptographicOperations.ZeroMemory(privateKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wallet {WalletId} encryption validation failed", wallet.Id);
            return false;
        }
    }

    /// <summary>
    /// Creates a payload with magic header, version, timestamp, user ID and public key for validation.
    /// Format: [4 bytes magic][1 byte version][8 bytes timestamp][16 bytes userId][1 byte pubKeyLen][pubKey bytes][64 bytes privateKey]
    /// </summary>
    private static byte[] CreatePayload(byte[] privateKey, Guid userId, string publicKey)
    {
        var pubKeyBytes = System.Text.Encoding.UTF8.GetBytes(publicKey);
        if (pubKeyBytes.Length > 255) throw new ArgumentException("Public key too long");

        var payload = new byte[4 + 1 + 8 + 16 + 1 + pubKeyBytes.Length + 64];
        var offset = 0;

        // Magic header
        Buffer.BlockCopy(MagicHeader, 0, payload, offset, 4);
        offset += 4;

        // Version (for future format changes)
        payload[offset] = 1;
        offset += 1;

        // Timestamp (Unix seconds)
        var timestamp = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Buffer.BlockCopy(timestamp, 0, payload, offset, 8);
        offset += 8;

        // User ID (for additional validation)
        var userIdBytes = userId.ToByteArray();
        Buffer.BlockCopy(userIdBytes, 0, payload, offset, 16);
        offset += 16;

        // Public key length and bytes
        payload[offset] = (byte)pubKeyBytes.Length;
        offset += 1;
        Buffer.BlockCopy(pubKeyBytes, 0, payload, offset, pubKeyBytes.Length);
        offset += pubKeyBytes.Length;

        // Private key (64 bytes for Solana ed25519)
        Buffer.BlockCopy(privateKey, 0, payload, offset, privateKey.Length);

        return payload;
    }

    /// <summary>
    /// Extracts and validates the private key from the payload.
    /// </summary>
    private static (byte[] PrivateKey, string EmbeddedPublicKey) ExtractPrivateKey(byte[] payload, Guid expectedUserId)
    {
        if (payload.Length < 95) // Minimum size: 4 + 1 + 8 + 16 + 1 + 1 + 64
        {
            throw new PingPayException("INVALID_PAYLOAD", "Encrypted payload is too small");
        }

        var offset = 0;

        // Validate magic header
        for (var i = 0; i < 4; i++)
        {
            if (payload[i] != MagicHeader[i])
            {
                throw new PingPayException("INVALID_PAYLOAD", "Invalid payload magic header");
            }
        }
        offset += 4;

        // Check version
        var version = payload[offset];
        if (version != 1)
        {
            throw new PingPayException("UNSUPPORTED_VERSION", $"Unsupported payload version: {version}");
        }
        offset += 1;

        // Skip timestamp (could validate it's not too old if needed)
        offset += 8;

        // Validate user ID
        var userIdBytes = new byte[16];
        Buffer.BlockCopy(payload, offset, userIdBytes, 0, 16);
        var userId = new Guid(userIdBytes);
        if (userId != expectedUserId)
        {
            throw new PingPayException("USER_MISMATCH", "Payload user ID does not match wallet owner");
        }
        offset += 16;

        // Read public key length and bytes
        var pubKeyLen = payload[offset];
        offset += 1;
        if (payload.Length < offset + pubKeyLen + 64) throw new PingPayException("INVALID_PAYLOAD", "Encrypted payload truncated");
        var pubKeyBytes = new byte[pubKeyLen];
        Buffer.BlockCopy(payload, offset, pubKeyBytes, 0, pubKeyLen);
        var embeddedPubKey = System.Text.Encoding.UTF8.GetString(pubKeyBytes);
        // Skip over embedded public key bytes
        offset += pubKeyLen;

        // Extract private key
        var privateKey = new byte[64];
        Buffer.BlockCopy(payload, offset, privateKey, 0, 64);

        return (privateKey, embeddedPubKey);
    }

    /// <summary>
    /// Validates that the private key corresponds to the expected public key.
    /// </summary>
    private static bool ValidateKeyPair(byte[] privateKey, string expectedPublicKey)
    {
        try
        {
            // Try to construct an Account from the secret key bytes so we can derive the public key.
            var ctor = typeof(Account).GetConstructor(new[] { typeof(byte[]) });
            if (ctor != null)
            {
                var acct = (Account)ctor.Invoke(new object[] { privateKey });
                return acct.PublicKey.Key == expectedPublicKey;
            }

            // Fallback: try parameterless account (best-effort)
            var fallback = new Account();
            return fallback.PublicKey.Key == expectedPublicKey;
        }
        catch
        {
            return false;
        }
    }
}
