using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services.KeyManagement;
using Xunit;

namespace PingPay.Tests.Unit;

public class KeyManagementTests
{
    private readonly LocalKeyManagementService _service;

    public KeyManagementTests()
    {
        // Generate a consistent test key
        var testKey = Convert.ToBase64String(new byte[32]);
        var options = Options.Create(new KeyManagementOptions
        {
            Provider = "Local",
            LocalDevelopmentKey = testKey
        });

        _service = new LocalKeyManagementService(options, NullLogger<LocalKeyManagementService>.Instance);
    }

    [Fact]
    public async Task EncryptDecrypt_ShouldRoundTrip()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);
        var decrypted = await _service.DecryptAsync(encryptedBlob, keyVersion);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public async Task Encrypt_ShouldProduceDifferentOutputsForSameInput()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var (blob1, _) = await _service.EncryptAsync(plaintext);
        var (blob2, _) = await _service.EncryptAsync(plaintext);

        // Assert (different due to random IV)
        blob1.Should().NotBe(blob2);
    }

    [Fact]
    public async Task Encrypt_ShouldReturnLocalKeyVersion()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3 };

        // Act
        var (_, keyVersion) = await _service.EncryptAsync(plaintext);

        // Assert
        keyVersion.Should().Be("local-v1");
    }

    [Fact]
    public async Task EncryptDecrypt_ShouldHandleLargeData()
    {
        // Arrange
        var plaintext = new byte[64]; // Solana private key size
        Random.Shared.NextBytes(plaintext);

        // Act
        var (encryptedBlob, keyVersion) = await _service.EncryptAsync(plaintext);
        var decrypted = await _service.DecryptAsync(encryptedBlob, keyVersion);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }
}
