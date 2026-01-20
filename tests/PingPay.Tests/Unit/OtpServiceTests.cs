using FluentAssertions;
using PingPay.Infrastructure.Services;
using Xunit;

namespace PingPay.Tests.Unit;

public class OtpServiceTests
{
    [Fact]
    public void HashCode_ShouldProduceDeterministicHash()
    {
        // Arrange
        var code = "123456";

        // Act
        var hash1 = OtpService.HashCode(code);
        var hash2 = OtpService.HashCode(code);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashCode_ShouldProduceDifferentHashesForDifferentCodes()
    {
        // Arrange
        var code1 = "123456";
        var code2 = "654321";

        // Act
        var hash1 = OtpService.HashCode(code1);
        var hash2 = OtpService.HashCode(code2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashCode_ShouldReturnBase64String()
    {
        // Arrange
        var code = "123456";

        // Act
        var hash = OtpService.HashCode(code);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        var action = () => Convert.FromBase64String(hash);
        action.Should().NotThrow();
    }
}
