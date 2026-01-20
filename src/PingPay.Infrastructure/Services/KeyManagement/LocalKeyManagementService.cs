using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;

namespace PingPay.Infrastructure.Services.KeyManagement;

/// <summary>
/// Local key management for development only. DO NOT USE IN PRODUCTION.
/// Uses a static master key from configuration.
/// </summary>
public class LocalKeyManagementService : IKeyManagementService
{
    private readonly byte[] _masterKey;
    private readonly ILogger<LocalKeyManagementService> _logger;
    private const int IvSize = 12; // GCM IV size
    private const int TagSize = 16; // GCM tag size
    private const int DekSize = 32; // 256-bit DEK

    public LocalKeyManagementService(
        IOptions<KeyManagementOptions> options,
        ILogger<LocalKeyManagementService> logger)
    {
        _logger = logger;

        var keyBase64 = options.Value.LocalDevelopmentKey;
        if (string.IsNullOrEmpty(keyBase64))
        {
            // Generate a random key for development (will change on restart)
            _masterKey = RandomNumberGenerator.GetBytes(32);
            _logger.LogWarning("No LocalDevelopmentKey configured. Generated ephemeral key. Data will be unreadable after restart.");
        }
        else
        {
            _masterKey = Convert.FromBase64String(keyBase64);
        }

        if (_masterKey.Length != 32)
        {
            throw new ArgumentException("Master key must be 32 bytes (256 bits)");
        }
    }

    public Task<(string EncryptedBlob, string KeyVersion)> EncryptAsync(
        byte[] plaintext,
        CancellationToken ct = default)
    {
        // Generate a random Data Encryption Key (DEK)
        var dek = RandomNumberGenerator.GetBytes(DekSize);

        // Encrypt the DEK with the master key
        var encryptedDek = EncryptWithKey(_masterKey, dek);

        // Encrypt the plaintext with the DEK
        var encryptedData = EncryptWithKey(dek, plaintext);

        // Clear DEK from memory
        CryptographicOperations.ZeroMemory(dek);

        // Combine: [4 bytes DEK length][encrypted DEK][encrypted data]
        var dekLengthBytes = BitConverter.GetBytes(encryptedDek.Length);
        var combined = new byte[4 + encryptedDek.Length + encryptedData.Length];

        Buffer.BlockCopy(dekLengthBytes, 0, combined, 0, 4);
        Buffer.BlockCopy(encryptedDek, 0, combined, 4, encryptedDek.Length);
        Buffer.BlockCopy(encryptedData, 0, combined, 4 + encryptedDek.Length, encryptedData.Length);

        var blob = Convert.ToBase64String(combined);

        return Task.FromResult((blob, "local-v1"));
    }

    public Task<byte[]> DecryptAsync(
        string encryptedBlob,
        string keyVersion,
        CancellationToken ct = default)
    {
        var combined = Convert.FromBase64String(encryptedBlob);

        // Extract DEK length
        var dekLength = BitConverter.ToInt32(combined, 0);

        // Extract encrypted DEK
        var encryptedDek = new byte[dekLength];
        Buffer.BlockCopy(combined, 4, encryptedDek, 0, dekLength);

        // Extract encrypted data
        var encryptedData = new byte[combined.Length - 4 - dekLength];
        Buffer.BlockCopy(combined, 4 + dekLength, encryptedData, 0, encryptedData.Length);

        // Decrypt the DEK
        var dek = DecryptWithKey(_masterKey, encryptedDek);

        // Decrypt the data
        var plaintext = DecryptWithKey(dek, encryptedData);

        // Clear DEK from memory
        CryptographicOperations.ZeroMemory(dek);

        return Task.FromResult(plaintext);
    }

    private static byte[] EncryptWithKey(byte[] key, byte[] plaintext)
    {
        using var aes = new AesGcm(key, TagSize);

        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        aes.Encrypt(iv, plaintext, ciphertext, tag);

        // Combine: [IV][ciphertext][tag]
        var result = new byte[IvSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, IvSize + ciphertext.Length, TagSize);

        return result;
    }

    private static byte[] DecryptWithKey(byte[] key, byte[] combined)
    {
        using var aes = new AesGcm(key, TagSize);

        var iv = new byte[IvSize];
        var ciphertext = new byte[combined.Length - IvSize - TagSize];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(combined, 0, iv, 0, IvSize);
        Buffer.BlockCopy(combined, IvSize, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, IvSize + ciphertext.Length, tag, 0, TagSize);

        var plaintext = new byte[ciphertext.Length];
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return plaintext;
    }
}
