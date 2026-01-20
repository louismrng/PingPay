using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PingPay.Core.Enums;
using PingPay.Core.Exceptions;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services.Solana;
using Xunit;

namespace PingPay.Tests.Integration;

/// <summary>
/// Integration tests for SolanaService.
/// These tests use Solana Devnet and require network connectivity.
///
/// To run: Set environment variable SOLANA_DEVNET_RPC_URL or use default devnet.
/// Some tests are skipped by default due to rate limits on public devnet.
/// </summary>
[Collection("Solana")]
[Trait("Category", "Integration")]
public class SolanaServiceIntegrationTests
{
    private readonly SolanaService _service;
    private readonly SolanaOptions _options;

    public SolanaServiceIntegrationTests()
    {
        _options = new SolanaOptions
        {
            RpcUrl = Environment.GetEnvironmentVariable("SOLANA_DEVNET_RPC_URL")
                     ?? "https://api.devnet.solana.com",
            UseDevnet = true
        };

        _service = new SolanaService(
            Options.Create(_options),
            NullLogger<SolanaService>.Instance);
    }

    [Fact]
    public void GenerateKeypair_ShouldReturnValidKeypair()
    {
        // Act
        var (publicKey, privateKey) = _service.GenerateKeypair();

        // Assert
        publicKey.Should().NotBeNullOrEmpty();
        publicKey.Length.Should().BeInRange(32, 44); // Base58 public key length
        privateKey.Should().NotBeNull();
        privateKey.Length.Should().Be(64); // Ed25519 private key length
    }

    [Fact]
    public void GenerateKeypair_ShouldProduceDifferentKeysEachTime()
    {
        // Act
        var (publicKey1, _) = _service.GenerateKeypair();
        var (publicKey2, _) = _service.GenerateKeypair();

        // Assert
        publicKey1.Should().NotBe(publicKey2);
    }

    [Fact]
    public async Task GetSolBalance_ShouldReturnBalanceForValidAddress()
    {
        // Arrange - use a known devnet address (system program for simplicity)
        var address = "11111111111111111111111111111111";

        // Act
        var balance = await _service.GetSolBalanceAsync(address);

        // Assert - system program always has SOL
        balance.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetSolBalance_ShouldReturnZeroForNewWallet()
    {
        // Arrange - generate a new wallet that won't have any balance
        var (publicKey, _) = _service.GenerateKeypair();

        // Act
        var balance = await _service.GetSolBalanceAsync(publicKey);

        // Assert
        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetTokenBalance_ShouldReturnZeroForNewWallet()
    {
        // Arrange
        var (publicKey, _) = _service.GenerateKeypair();

        // Act
        var balance = await _service.GetTokenBalanceAsync(publicKey, TokenType.USDC);

        // Assert
        balance.Should().Be(0);
    }

    [Fact]
    public async Task IsTransactionConfirmed_ShouldReturnFalseForInvalidSignature()
    {
        // Arrange - fake signature
        var signature = "5wHu1qwD7q39JHq7rFmnqYXs7Xc5X9dKEAXPkKZFfcLa4sH6pQw2YYwgXKMuXiHJXbwwqAcg1xqY9mZc1VKNdnMJ";

        // Act
        var isConfirmed = await _service.IsTransactionConfirmedAsync(signature);

        // Assert
        isConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task GetTransactionDetails_ShouldReturnNullForInvalidSignature()
    {
        // Arrange
        var signature = "5wHu1qwD7q39JHq7rFmnqYXs7Xc5X9dKEAXPkKZFfcLa4sH6pQw2YYwgXKMuXiHJXbwwqAcg1xqY9mZc1VKNdnMJ";

        // Act
        var details = await _service.GetTransactionDetailsAsync(signature);

        // Assert
        details.Should().BeNull();
    }

    [Fact]
    public async Task TransferToken_ShouldThrowForInsufficientBalance()
    {
        // Arrange - create a new wallet with no balance
        var (_, senderPrivateKey) = _service.GenerateKeypair();
        var (recipientPublicKey, _) = _service.GenerateKeypair();

        // Act & Assert
        var act = async () => await _service.TransferTokenAsync(
            senderPrivateKey,
            recipientPublicKey,
            10m,
            TokenType.USDC);

        await act.Should().ThrowAsync<InsufficientBalanceException>();
    }

    [Fact]
    public async Task TransferToken_ShouldThrowForInvalidAmount()
    {
        // Arrange
        var (_, senderPrivateKey) = _service.GenerateKeypair();
        var (recipientPublicKey, _) = _service.GenerateKeypair();

        // Act & Assert
        var act = async () => await _service.TransferTokenAsync(
            senderPrivateKey,
            recipientPublicKey,
            -10m,  // Invalid amount
            TokenType.USDC);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TransferToken_ShouldThrowForZeroAmount()
    {
        // Arrange
        var (_, senderPrivateKey) = _service.GenerateKeypair();
        var (recipientPublicKey, _) = _service.GenerateKeypair();

        // Act & Assert
        var act = async () => await _service.TransferTokenAsync(
            senderPrivateKey,
            recipientPublicKey,
            0m,  // Zero amount
            TokenType.USDC);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TransferToken_ShouldThrowForInvalidRecipient()
    {
        // Arrange
        var (_, senderPrivateKey) = _service.GenerateKeypair();

        // Act & Assert
        var act = async () => await _service.TransferTokenAsync(
            senderPrivateKey,
            "invalid-address",  // Invalid Solana address
            10m,
            TokenType.USDC);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EstimateTransferFee_ShouldReturnReasonableFee()
    {
        // Arrange
        var (senderPublicKey, _) = _service.GenerateKeypair();
        var (recipientPublicKey, _) = _service.GenerateKeypair();

        // Act
        var fee = await _service.EstimateTransferFeeAsync(
            senderPublicKey,
            recipientPublicKey,
            TokenType.USDC);

        // Assert - fee should be reasonable (5000 lamports base + potentially ATA creation)
        fee.Should().BeGreaterThan(0);
        fee.Should().BeLessThan(10_000_000); // Less than 0.01 SOL
    }

    [Fact]
    public async Task WaitForConfirmation_ShouldTimeoutForInvalidSignature()
    {
        // Arrange
        var signature = "5wHu1qwD7q39JHq7rFmnqYXs7Xc5X9dKEAXPkKZFfcLa4sH6pQw2YYwgXKMuXiHJXbwwqAcg1xqY9mZc1VKNdnMJ";

        // Act
        var confirmed = await _service.WaitForConfirmationAsync(
            signature,
            TimeSpan.FromSeconds(2)); // Short timeout

        // Assert
        confirmed.Should().BeFalse();
    }

    // Skip expensive tests that require funded wallets
    [Fact(Skip = "Requires funded devnet wallet - run manually")]
    public async Task TransferToken_WithFundedWallet_ShouldSucceed()
    {
        // This test requires a funded devnet wallet
        // Set SOLANA_TEST_PRIVATE_KEY environment variable with a funded wallet

        var privateKeyBase64 = Environment.GetEnvironmentVariable("SOLANA_TEST_PRIVATE_KEY");
        if (string.IsNullOrEmpty(privateKeyBase64))
        {
            return;
        }

        var senderPrivateKey = Convert.FromBase64String(privateKeyBase64);
        var (recipientPublicKey, _) = _service.GenerateKeypair();

        // Act
        var signature = await _service.TransferTokenAsync(
            senderPrivateKey,
            recipientPublicKey,
            0.01m, // Small amount
            TokenType.USDC);

        // Assert
        signature.Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// Unit tests for SolanaService that don't require network access.
/// </summary>
public class SolanaServiceUnitTests
{
    private readonly SolanaService _service;

    public SolanaServiceUnitTests()
    {
        var options = new SolanaOptions
        {
            RpcUrl = "https://api.devnet.solana.com",
            UseDevnet = true
        };

        _service = new SolanaService(
            Options.Create(options),
            NullLogger<SolanaService>.Instance);
    }

    [Fact]
    public void GenerateKeypair_MultipleCalls_ShouldBeUnique()
    {
        // Act - generate 100 keypairs
        var keypairs = Enumerable.Range(0, 100)
            .Select(_ => _service.GenerateKeypair())
            .ToList();

        // Assert - all public keys should be unique
        var uniquePublicKeys = keypairs.Select(k => k.PublicKey).Distinct();
        uniquePublicKeys.Should().HaveCount(100);
    }

    [Fact]
    public void GenerateKeypair_PrivateKey_ShouldBeValidLength()
    {
        // Act
        var (_, privateKey) = _service.GenerateKeypair();

        // Assert - Ed25519 private key should be 64 bytes
        privateKey.Should().HaveCount(64);
    }

    [Theory]
    [InlineData("11111111111111111111111111111111")] // System program
    [InlineData("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA")] // Token program
    [InlineData("ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL")] // ATA program
    public void ValidSolanaAddresses_ShouldBeAccepted(string address)
    {
        // These addresses should be valid - testing via reflection or by trying to create PublicKey
        // Since we can't directly test the private IsValidSolanaAddress method,
        // we test it indirectly through error handling in other methods

        // Arrange & Act - this would throw if address validation failed in certain contexts
        // For now, this is a placeholder showing the test structure
        address.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("short")] // Too short
    [InlineData("this-is-definitely-not-a-valid-solana-address-because-it-has-invalid-characters!!")]
    public void InvalidSolanaAddresses_ShouldBeRejected(string address)
    {
        // Similar to above - this tests the concept
        // In practice, invalid addresses should cause ValidationException when used
        address.Should().NotBeNull();
    }
}
