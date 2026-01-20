using System.Security.Cryptography;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;

namespace PingPay.Infrastructure.Services.KeyManagement;

/// <summary>
/// AWS KMS implementation for production key management.
/// Uses envelope encryption with AES-256-GCM for data and KMS for DEK wrapping.
/// </summary>
public class AwsKmsService : IKeyManagementService
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly string _keyId;
    private readonly ILogger<AwsKmsService> _logger;
    private const int IvSize = 12;
    private const int TagSize = 16;
    private const int DekSize = 32;

    public AwsKmsService(
        IOptions<KeyManagementOptions> options,
        ILogger<AwsKmsService> logger)
    {
        _logger = logger;

        _keyId = options.Value.AwsKmsKeyId
            ?? throw new ArgumentNullException(nameof(options), "AwsKmsKeyId is required");

        var region = options.Value.AwsRegion ?? "us-east-1";

        _kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<(string EncryptedBlob, string KeyVersion)> EncryptAsync(
        byte[] plaintext,
        CancellationToken ct = default)
    {
        // Generate a Data Encryption Key using KMS
        var generateRequest = new GenerateDataKeyRequest
        {
            KeyId = _keyId,
            KeySpec = DataKeySpec.AES_256
        };

        var generateResponse = await _kmsClient.GenerateDataKeyAsync(generateRequest, ct);

        var dek = generateResponse.Plaintext.ToArray();
        var encryptedDek = generateResponse.CiphertextBlob.ToArray();

        // Extract key version from the response metadata
        var keyVersion = ExtractKeyVersion(generateResponse.KeyId);

        try
        {
            // Encrypt the plaintext with the DEK using AES-GCM
            var encryptedData = EncryptWithDek(dek, plaintext);

            // Combine: [4 bytes DEK length][encrypted DEK][encrypted data]
            var dekLengthBytes = BitConverter.GetBytes(encryptedDek.Length);
            var combined = new byte[4 + encryptedDek.Length + encryptedData.Length];

            Buffer.BlockCopy(dekLengthBytes, 0, combined, 0, 4);
            Buffer.BlockCopy(encryptedDek, 0, combined, 4, encryptedDek.Length);
            Buffer.BlockCopy(encryptedData, 0, combined, 4 + encryptedDek.Length, encryptedData.Length);

            var blob = Convert.ToBase64String(combined);

            _logger.LogDebug("Encrypted data with KMS key {KeyId}", _keyId);

            return (blob, keyVersion);
        }
        finally
        {
            // Clear DEK from memory
            CryptographicOperations.ZeroMemory(dek);
        }
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

        // Decrypt the DEK using KMS
        var decryptRequest = new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(encryptedDek),
            KeyId = _keyId
        };

        var decryptResponse = await _kmsClient.DecryptAsync(decryptRequest, ct);
        var dek = decryptResponse.Plaintext.ToArray();

        try
        {
            // Decrypt the data with the DEK
            var plaintext = DecryptWithDek(dek, encryptedData);
            return plaintext;
        }
        finally
        {
            // Clear DEK from memory
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    private static string ExtractKeyVersion(string keyArn)
    {
        // ARN format: arn:aws:kms:region:account:key/key-id
        var parts = keyArn.Split('/');
        return parts.Length > 1 ? parts[^1] : keyArn;
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
