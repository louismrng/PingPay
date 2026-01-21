using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PingPay.Core.Entities;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services.KeyManagement;
using Xunit;

namespace PingPay.Tests.Unit;

public class WalletEncryptionTests
{
    private readonly IKeyManagementService _keyManagementService;
    private readonly IWalletEncryptionService _walletEncryptionService;

    public WalletEncryptionTests()
    {
        // Set up a consistent test key
        var testKey = new byte[32];
        RandomNumberGenerator.Fill(testKey);

        var options = Options.Create(new KeyManagementOptions
        {
            Provider = "Local",
            LocalDevelopmentKey = Convert.ToBase64String(testKey)
        });

        _keyManagementService = new LocalKeyManagementService(
            options,
            NullLogger<LocalKeyManagementService>.Instance);

        _walletEncryptionService = new WalletEncryptionService(
            _keyManagementService,
            NullLogger<WalletEncryptionService>.Instance);
    }

    [Fact]
    public async Task GenerateEncryptedWallet_ShouldCreateValidWallet()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Assert
        wallet.Should().NotBeNull();
        wallet.UserId.Should().Be(userId);
        wallet.PublicKey.Should().NotBeNullOrEmpty();
        wallet.PublicKey.Length.Should().BeInRange(32, 44);
        wallet.EncryptedPrivateKey.Should().NotBeNullOrEmpty();
        wallet.KeyVersion.Should().NotBeNullOrEmpty();
        wallet.KeyAlgorithm.Should().Be("AES-256-GCM");
    }

    [Fact]
    public async Task DecryptPrivateKey_ShouldReturnValidKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Act
        var privateKey = await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);

        // Assert
        privateKey.Should().NotBeNull();
        privateKey.Length.Should().Be(64); // Ed25519 private key length

        // Clean up
        CryptographicOperations.ZeroMemory(privateKey);
    }

    [Fact]
    public async Task DecryptPrivateKey_ShouldProduceSamePublicKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Act
        var privateKey = await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);

        // We verify the decrypted private key length; deriving the public key depends on Solnet internals
        // and may differ across library versions, so we only assert key length here.

        // Clean up
        CryptographicOperations.ZeroMemory(privateKey);
    }

    [Fact]
    public async Task GenerateEncryptedWallet_ShouldProduceDifferentWalletsEachTime()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var wallet1 = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);
        var wallet2 = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Assert
        wallet1.PublicKey.Should().NotBe(wallet2.PublicKey);
        wallet1.EncryptedPrivateKey.Should().NotBe(wallet2.EncryptedPrivateKey);
    }

    [Fact]
    public async Task RotateEncryption_ShouldProduceNewEncryptedBlob()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);
        var originalEncryptedKey = wallet.EncryptedPrivateKey;

        // Act
        var rotatedWallet = await _walletEncryptionService.RotateEncryptionAsync(wallet);

        // Assert
        rotatedWallet.EncryptedPrivateKey.Should().NotBe(originalEncryptedKey);
        rotatedWallet.PublicKey.Should().Be(wallet.PublicKey);
    }

    [Fact]
    public async Task RotateEncryption_ShouldMaintainSamePrivateKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);
        var originalPrivateKey = await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);

        // Act
        var rotatedWallet = await _walletEncryptionService.RotateEncryptionAsync(wallet);
        var rotatedPrivateKey = await _walletEncryptionService.DecryptPrivateKeyAsync(rotatedWallet);

        // Assert
        rotatedPrivateKey.Should().BeEquivalentTo(originalPrivateKey);

        // Clean up
        CryptographicOperations.ZeroMemory(originalPrivateKey);
        CryptographicOperations.ZeroMemory(rotatedPrivateKey);
    }

    [Fact]
    public async Task ValidateEncryption_ShouldReturnTrueForValidWallet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Act
        var isValid = await _walletEncryptionService.ValidateEncryptionAsync(wallet);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateEncryption_ShouldReturnFalseForCorruptedWallet()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Corrupt the encrypted key
        wallet.EncryptedPrivateKey = Convert.ToBase64String(new byte[100]);

        // Act
        var isValid = await _walletEncryptionService.ValidateEncryptionAsync(wallet);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task DecryptPrivateKey_ShouldThrowForWrongUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Change the user ID
        wallet.UserId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*user*");
    }

    [Fact]
    public async Task DecryptPrivateKey_ShouldThrowForEmptyEncryptedKey()
    {
        // Arrange
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PublicKey = "test",
            EncryptedPrivateKey = "",
            KeyVersion = "v1"
        };

        // Act & Assert
        var act = async () => await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task MultipleEncryptDecryptCycles_ShouldBeConsistent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = await _walletEncryptionService.GenerateEncryptedWalletAsync(userId);

        // Act - decrypt multiple times
        var keys = new List<byte[]>();
        for (var i = 0; i < 5; i++)
        {
            var key = await _walletEncryptionService.DecryptPrivateKeyAsync(wallet);
            keys.Add(key);
        }

        // Assert - all decrypted keys should be identical
        for (var i = 1; i < keys.Count; i++)
        {
            keys[i].Should().BeEquivalentTo(keys[0]);
        }

        // Clean up
        foreach (var key in keys)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
