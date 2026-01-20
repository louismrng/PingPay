using FluentAssertions;
using PingPay.Core.Constants;
using Xunit;

namespace PingPay.Tests.Unit;

public class SolanaConstantsTests
{
    [Fact]
    public void UsdcMintAddress_ShouldBeValidBase58()
    {
        // Assert
        SolanaConstants.UsdcMintAddress.Should().NotBeNullOrEmpty();
        SolanaConstants.UsdcMintAddress.Should().HaveLength(44);
    }

    [Fact]
    public void UsdtMintAddress_ShouldBeValidBase58()
    {
        // Assert
        SolanaConstants.UsdtMintAddress.Should().NotBeNullOrEmpty();
        SolanaConstants.UsdtMintAddress.Should().HaveLength(43);
    }

    [Fact]
    public void TokenDecimals_ShouldBeSix()
    {
        // Assert
        SolanaConstants.TokenDecimals.Should().Be(6);
    }

    [Fact]
    public void LamportsPerSol_ShouldBeOneBillion()
    {
        // Assert
        SolanaConstants.LamportsPerSol.Should().Be(1_000_000_000);
    }
}
