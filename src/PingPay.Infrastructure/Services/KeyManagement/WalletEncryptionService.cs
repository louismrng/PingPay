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

    public async Task<Wallet> GenerateEncryptedWalletAsync(Guid userId, CancellationToken ct = default)
    {
        // Generate a new Solana keypair using a cryptographically secure method
        var wallet = new Wallet(WordCount.TwentyFour);
        var account = wallet.Account;

        var publicKey = account.PublicKey.Key;
        var privateKeyBytes = account.PrivateKey.KeyBytes;

        try
        {
            // Add magic header and timestamp for validation
            var payload = CreatePayload(privateKeyBytes, userId);

            // Encrypt using envelope encryption
            var (encryptedBlob, keyVersion) = await _keyManagementService.EncryptAsync(payload, ct);

            _logger.LogInformation(
                "Generated encrypted wallet for user {UserId}. PublicKey: {PublicKey}",
                userId, publicKey);

            return new Wallet
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

    public async Task<byte[]> DecryptPrivateKeyAsync(Wallet wallet, CancellationToken ct = default)
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
            // Validate and extract the private key from payload
            var privateKey = ExtractPrivateKey(payload, wallet.UserId);

            // Validate that the private key matches the public key
            if (!ValidateKeyPair(privateKey, wallet.PublicKey))
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

    public async Task<Wallet> RotateEncryptionAsync(Wallet wallet, CancellationToken ct = default)
    {
        // First decrypt with the old key
        var privateKey = await DecryptPrivateKeyAsync(wallet, ct);

        try
        {
            // Create new payload
            var payload = CreatePayload(privateKey, wallet.UserId);

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

    public async Task<bool> ValidateEncryptionAsync(Wallet wallet, CancellationToken ct = default)
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
    /// Creates a payload with magic header, version, timestamp, and user ID for validation.
    /// Format: [4 bytes magic][1 byte version][8 bytes timestamp][16 bytes userId][64 bytes privateKey]
    /// </summary>
    private static byte[] CreatePayload(byte[] privateKey, Guid userId)
    {
        const int PayloadSize = 4 + 1 + 8 + 16 + 64;
        var payload = new byte[PayloadSize];
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

        // Private key (64 bytes for Solana ed25519)
        Buffer.BlockCopy(privateKey, 0, payload, offset, privateKey.Length);

        return payload;
    }

    /// <summary>
    /// Extracts and validates the private key from the payload.
    /// </summary>
    private static byte[] ExtractPrivateKey(byte[] payload, Guid expectedUserId)
    {
        if (payload.Length < 93) // Minimum size: 4 + 1 + 8 + 16 + 64
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

        // Extract private key
        var privateKey = new byte[64];
        Buffer.BlockCopy(payload, offset, privateKey, 0, 64);

        return privateKey;
    }

    /// <summary>
    /// Validates that the private key corresponds to the expected public key.
    /// </summary>
    private static bool ValidateKeyPair(byte[] privateKey, string expectedPublicKey)
    {
        try
        {
            var account = new Account(privateKey, string.Empty);
            return account.PublicKey.Key == expectedPublicKey;
        }
        catch
        {
            return false;
        }
    }
}
