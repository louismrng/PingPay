using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services.KeyManagement;
using Xunit;

namespace PingPay.Tests.Unit;

public class EnvelopeEncryptionTests
{
    private readonly LocalKeyManagementService _service;
    private readonly byte[] _testKey;

    public EnvelopeEncryptionTests()
    {
        _testKey = new byte[32];
        RandomNumberGenerator.Fill(_testKey);

        var options = Options.Create(new KeyManagementOptions
        {
            Provider = "Local",
            LocalDevelopmentKey = Convert.ToBase64String(_testKey)
        });

        _service = new LocalKeyManagementService(
            options,
            NullLogger<LocalKeyManagementService>.Instance);
    }

    [Fact]
    public async Task EncryptDecrypt_ShouldRoundTripCorrectly()
    {
        // Arrange
        var plaintext = RandomNumberGenerator.GetBytes(64);

        // Act
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);
        var decrypted = await _service.DecryptAsync(encryptedBlob, keyVersion);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public async Task Encrypt_ShouldProduceDifferentCiphertextEachTime()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var (blob1, _) = await _service.EncryptAsync(plaintext);
        var (blob2, _) = await _service.EncryptAsync(plaintext);

        // Assert (different due to random IV and DEK)
        blob1.Should().NotBe(blob2);
    }

    [Fact]
    public async Task Encrypt_ShouldReturnValidBase64()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3 };

        // Act
        var (encryptedBlob, _) = await _service.EncryptAsync(plaintext);

        // Assert
        var action = () => Convert.FromBase64String(encryptedBlob);
        action.Should().NotThrow();
    }

    [Fact]
    public async Task Decrypt_ShouldThrowForInvalidBlob()
    {
        // Arrange
        var invalidBlob = Convert.ToBase64String(new byte[] { 1, 2, 3 });

        // Act & Assert
        var act = async () => await _service.DecryptAsync(invalidBlob, "v1");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Decrypt_ShouldThrowForCorruptedData()
    {
        // Arrange
        var plaintext = RandomNumberGenerator.GetBytes(64);
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);

        // Corrupt the blob
        var bytes = Convert.FromBase64String(encryptedBlob);
        bytes[bytes.Length / 2] ^= 0xFF; // Flip some bits
        var corruptedBlob = Convert.ToBase64String(bytes);

        // Act & Assert
        var act = async () => await _service.DecryptAsync(corruptedBlob, keyVersion);
        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task EncryptDecrypt_ShouldHandleVariousSizes(int size)
    {
        // Arrange
        var plaintext = RandomNumberGenerator.GetBytes(size);

        // Act
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);
        var decrypted = await _service.DecryptAsync(encryptedBlob, keyVersion);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public async Task EncryptDecrypt_ShouldHandleEmptyArray()
    {
        // Arrange
        var plaintext = Array.Empty<byte>();

        // Act
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);
        var decrypted = await _service.DecryptAsync(encryptedBlob, keyVersion);

        // Assert
        decrypted.Should().BeEmpty();
    }

    [Fact]
    public async Task KeyVersion_ShouldBeLocalV1()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3 };

        // Act
        var (_, keyVersion) = await _service.EncryptAsync(plaintext);

        // Assert
        keyVersion.Should().Be("local-v1");
    }

    [Fact]
    public void Constructor_ShouldThrowForInvalidKeySize()
    {
        // Arrange
        var invalidKey = new byte[16]; // 128-bit instead of 256-bit
        var options = Options.Create(new KeyManagementOptions
        {
            Provider = "Local",
            LocalDevelopmentKey = Convert.ToBase64String(invalidKey)
        });

        // Act & Assert
        var action = () => new LocalKeyManagementService(
            options,
            NullLogger<LocalKeyManagementService>.Instance);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public async Task ConcurrentEncryption_ShouldBeThreadSafe()
    {
        // Arrange
        var plaintext = RandomNumberGenerator.GetBytes(64);
        var tasks = new List<Task<(string, string)>>();

        // Act - run 100 concurrent encryptions
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(_service.EncryptAsync(plaintext));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - all should succeed and be different
        results.Should().HaveCount(100);
        results.Select(r => r.Item1).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public async Task ConcurrentDecryption_ShouldBeThreadSafe()
    {
        // Arrange
        var plaintext = RandomNumberGenerator.GetBytes(64);
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);

        var tasks = new List<Task<byte[]>>();

        // Act - run 100 concurrent decryptions
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(_service.DecryptAsync(encryptedBlob, keyVersion));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - all should produce the same plaintext
        foreach (var result in results)
        {
            result.Should().BeEquivalentTo(plaintext);
        }
    }
}
