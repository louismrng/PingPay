using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services.Solana;
using Xunit;

namespace PingPay.Tests.Unit;

public class CachedSolanaBalanceServiceTests
{
    private readonly Mock<ISolanaService> _solanaServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly CachedSolanaBalanceService _service;

    public CachedSolanaBalanceServiceTests()
    {
        _solanaServiceMock = new Mock<ISolanaService>();
        _cacheServiceMock = new Mock<ICacheService>();

        var options = Options.Create(new SolanaOptions
        {
            RpcUrl = "https://api.devnet.solana.com",
            UseDevnet = true
        });

        _service = new CachedSolanaBalanceService(
            _solanaServiceMock.Object,
            _cacheServiceMock.Object,
            options,
            NullLogger<CachedSolanaBalanceService>.Instance);
    }

    [Fact]
    public async Task GetTokenBalance_WhenCached_ShouldReturnCachedValue()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";
        var cachedBalance = new { Balance = 100.5m, FetchedAt = DateTime.UtcNow };

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBalance);

        // Act
        var balance = await _service.GetTokenBalanceAsync(publicKey, TokenType.USDC);

        // Assert
        balance.Should().Be(100.5m);
        _solanaServiceMock.Verify(
            s => s.GetTokenBalanceAsync(It.IsAny<string>(), It.IsAny<TokenType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTokenBalance_WhenNotCached_ShouldFetchFromChain()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()))
            .ReturnsAsync(250.75m);

        // Act
        var balance = await _service.GetTokenBalanceAsync(publicKey, TokenType.USDC);

        // Assert
        balance.Should().Be(250.75m);
        _solanaServiceMock.Verify(
            s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheServiceMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenBalance_WithForceRefresh_ShouldBypassCache()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);

        // Act
        var balance = await _service.GetTokenBalanceAsync(publicKey, TokenType.USDC, forceRefresh: true);

        // Assert
        balance.Should().Be(300m);
        _cacheServiceMock.Verify(
            c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSolBalance_WhenNotCached_ShouldFetchAndCache()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetSolBalanceAsync(publicKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.5m);

        // Act
        var balance = await _service.GetSolBalanceAsync(publicKey);

        // Assert
        balance.Should().Be(1.5m);
    }

    [Fact]
    public async Task GetAllBalances_ShouldFetchAllBalancesInParallel()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);
        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDT, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m);
        _solanaServiceMock
            .Setup(s => s.GetSolBalanceAsync(publicKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.5m);

        // Act
        var balances = await _service.GetAllBalancesAsync(publicKey, forceRefresh: true);

        // Assert
        balances.PublicKey.Should().Be(publicKey);
        balances.UsdcBalance.Should().Be(100m);
        balances.UsdtBalance.Should().Be(50m);
        balances.SolBalance.Should().Be(0.5m);
        balances.TotalStablecoinBalance.Should().Be(150m);
    }

    [Fact]
    public async Task InvalidateBalanceCache_ShouldRemoveCacheEntries()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        // Act
        await _service.InvalidateBalanceCacheAsync(publicKey);

        // Assert
        _cacheServiceMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2)); // At least USDC, USDT, and SOL
    }

    [Fact]
    public async Task InvalidateBalanceCache_WithSpecificToken_ShouldRemoveOnlyThatToken()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        // Act
        await _service.InvalidateBalanceCacheAsync(publicKey, TokenType.USDC);

        // Assert
        _cacheServiceMock.Verify(
            c => c.RemoveAsync(It.Is<string>(s => s.Contains("USDC")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckSufficientBalance_WhenSufficient_ShouldReturnTrue()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        // Act
        var (hasSufficient, currentBalance) = await _service.CheckSufficientBalanceAsync(
            publicKey, 50m, TokenType.USDC);

        // Assert
        hasSufficient.Should().BeTrue();
        currentBalance.Should().Be(100m);
    }

    [Fact]
    public async Task CheckSufficientBalance_WhenInsufficient_ShouldReturnFalse()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetTokenBalanceAsync(publicKey, TokenType.USDC, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25m);

        // Act
        var (hasSufficient, currentBalance) = await _service.CheckSufficientBalanceAsync(
            publicKey, 50m, TokenType.USDC);

        // Assert
        hasSufficient.Should().BeFalse();
        currentBalance.Should().Be(25m);
    }

    [Fact]
    public async Task CheckSufficientSolForFees_WhenSufficient_ShouldReturnTrue()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetSolBalanceAsync(publicKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.05m);

        // Act
        var (hasSufficient, currentSol) = await _service.CheckSufficientSolForFeesAsync(publicKey);

        // Assert
        hasSufficient.Should().BeTrue();
        currentSol.Should().Be(0.05m);
    }

    [Fact]
    public async Task CheckSufficientSolForFees_WhenInsufficient_ShouldReturnFalse()
    {
        // Arrange
        var publicKey = "TestPublicKey123456789012345678901234567890";

        _cacheServiceMock
            .Setup(c => c.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        _solanaServiceMock
            .Setup(s => s.GetSolBalanceAsync(publicKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.001m);

        // Act
        var (hasSufficient, currentSol) = await _service.CheckSufficientSolForFeesAsync(publicKey);

        // Assert
        hasSufficient.Should().BeFalse();
        currentSol.Should().Be(0.001m);
    }
}
