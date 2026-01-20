using System.Security.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;

namespace PingPay.Infrastructure.Services.KeyManagement;

/// <summary>
/// Azure Key Vault implementation for production key management.
/// Uses envelope encryption with AES-256-GCM for data and RSA for DEK.
/// </summary>
public class AzureKeyVaultService : IKeyManagementService
{
    private readonly KeyClient _keyClient;
    private readonly string _keyName;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private const int IvSize = 12;
    private const int TagSize = 16;
    private const int DekSize = 32;

    public AzureKeyVaultService(
        IOptions<KeyManagementOptions> options,
        ILogger<AzureKeyVaultService> logger)
    {
        _logger = logger;

        var vaultUri = options.Value.AzureKeyVaultUri
            ?? throw new ArgumentNullException(nameof(options), "AzureKeyVaultUri is required");

        _keyName = options.Value.AzureKeyName
            ?? throw new ArgumentNullException(nameof(options), "AzureKeyName is required");

        _keyClient = new KeyClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    public async Task<(string EncryptedBlob, string KeyVersion)> EncryptAsync(
        byte[] plaintext,
        CancellationToken ct = default)
    {
        // Get the latest key version
        var key = await _keyClient.GetKeyAsync(_keyName, cancellationToken: ct);
        var keyVersion = key.Value.Properties.Version;

        // Generate a random DEK
        var dek = RandomNumberGenerator.GetBytes(DekSize);

        // Encrypt the DEK with Key Vault
        var cryptoClient = new CryptographyClient(key.Value.Id, new DefaultAzureCredential());
        var encryptedDekResult = await cryptoClient.EncryptAsync(
            EncryptionAlgorithm.RsaOaep256,
            dek,
            ct);

        // Encrypt the data with the DEK using AES-GCM
        var encryptedData = EncryptWithDek(dek, plaintext);

        // Clear DEK from memory
        CryptographicOperations.ZeroMemory(dek);

        // Combine: [4 bytes DEK length][encrypted DEK][encrypted data]
        var dekLengthBytes = BitConverter.GetBytes(encryptedDekResult.Ciphertext.Length);
        var combined = new byte[4 + encryptedDekResult.Ciphertext.Length + encryptedData.Length];

        Buffer.BlockCopy(dekLengthBytes, 0, combined, 0, 4);
        Buffer.BlockCopy(encryptedDekResult.Ciphertext, 0, combined, 4, encryptedDekResult.Ciphertext.Length);
        Buffer.BlockCopy(encryptedData, 0, combined, 4 + encryptedDekResult.Ciphertext.Length, encryptedData.Length);

        var blob = Convert.ToBase64String(combined);

        _logger.LogDebug("Encrypted data with key version {KeyVersion}", keyVersion);

        return (blob, keyVersion);
    }

    public async Task<byte[]> DecryptAsync(
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

        // Get the specific key version
        var keyId = new Uri($"{_keyClient.VaultUri}keys/{_keyName}/{keyVersion}");
        var cryptoClient = new CryptographyClient(keyId, new DefaultAzureCredential());

        // Decrypt the DEK
        var decryptedDekResult = await cryptoClient.DecryptAsync(
            EncryptionAlgorithm.RsaOaep256,
            encryptedDek,
            ct);

        var dek = decryptedDekResult.Plaintext;

        // Decrypt the data
        var plaintext = DecryptWithDek(dek, encryptedData);

        // Clear DEK from memory
        CryptographicOperations.ZeroMemory(dek);

        return plaintext;
    }

    private static byte[] EncryptWithDek(byte[] key, byte[] plaintext)
    {
        using var aes = new AesGcm(key, TagSize);

        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        aes.Encrypt(iv, plaintext, ciphertext, tag);

        var result = new byte[IvSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(iv, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, IvSize + ciphertext.Length, TagSize);

        return result;
    }

    private static byte[] DecryptWithDek(byte[] key, byte[] combined)
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
