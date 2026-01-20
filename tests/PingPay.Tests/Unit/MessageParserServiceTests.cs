using FluentAssertions;
using PingPay.Core.DTOs.WhatsApp;
using PingPay.Infrastructure.Services.WhatsApp;
using Xunit;

namespace PingPay.Tests.Unit;

public class MessageParserServiceTests
{
    private readonly MessageParserService _parser = new();

    [Theory]
    [InlineData("help")]
    [InlineData("HELP")]
    [InlineData("?")]
    [InlineData("hi")]
    [InlineData("hello")]
    [InlineData("start")]
    public void Parse_HelpCommands_ShouldReturnHelpType(string input)
    {
        var result = _parser.Parse(input);

        result.Type.Should().Be(CommandType.Help);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("balance")]
    [InlineData("BALANCE")]
    [InlineData("bal")]
    [InlineData("b")]
    public void Parse_BalanceCommands_ShouldReturnBalanceType(string input)
    {
        var result = _parser.Parse(input);

        result.Type.Should().Be(CommandType.Balance);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("history")]
    [InlineData("HISTORY")]
    [InlineData("hist")]
    [InlineData("h")]
    [InlineData("transactions")]
    public void Parse_HistoryCommands_ShouldReturnHistoryType(string input)
    {
        var result = _parser.Parse(input);

        result.Type.Should().Be(CommandType.History);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("register")]
    [InlineData("signup")]
    [InlineData("join")]
    public void Parse_RegisterCommands_ShouldReturnRegisterType(string input)
    {
        var result = _parser.Parse(input);

        result.Type.Should().Be(CommandType.Register);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("send $10 to +14155551234", 10, "+14155551234", "USDC")]
    [InlineData("send 25 +14155551234", 25, "+14155551234", "USDC")]
    [InlineData("send $100.50 to +14155551234 usdt", 100.50, "+14155551234", "USDT")]
    [InlineData("send 50 +14155551234 USDC", 50, "+14155551234", "USDC")]
    [InlineData("SEND $5 TO +14155551234", 5, "+14155551234", "USDC")]
    public void Parse_SendCommands_ShouldParseCurrectly(
        string input,
        decimal expectedAmount,
        string expectedRecipient,
        string expectedToken)
    {
        var result = _parser.Parse(input);

        result.Type.Should().Be(CommandType.Send);
        result.IsValid.Should().BeTrue();
        result.Amount.Should().Be(expectedAmount);
        result.RecipientPhone.Should().Be(expectedRecipient);
        result.Token.Should().Be(expectedToken);
    }

    [Fact]
    public void Parse_SendWithoutPlus_ShouldNormalizePhone()
    {
        var result = _parser.Parse("send $10 to 14155551234");

        result.Type.Should().Be(CommandType.Send);
        result.IsValid.Should().BeTrue();
        result.RecipientPhone.Should().Be("+14155551234");
    }

    [Fact]
    public void Parse_SendZeroAmount_ShouldBeInvalid()
    {
        var result = _parser.Parse("send $0 to +14155551234");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("greater than zero");
    }

    [Fact]
    public void Parse_SendNegativeAmount_ShouldBeInvalid()
    {
        var result = _parser.Parse("send $-10 to +14155551234");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_SendOverLimit_ShouldBeInvalid()
    {
        var result = _parser.Parse("send $50000 to +14155551234");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public void Parse_StatusCommand_ShouldParseGuid()
    {
        var guid = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        var result = _parser.Parse($"status {guid}");

        result.Type.Should().Be(CommandType.Status);
        result.IsValid.Should().BeTrue();
        result.RawInput.Should().Be(guid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyInput_ShouldBeInvalid(string? input)
    {
        var result = _parser.Parse(input!);

        result.IsValid.Should().BeFalse();
        result.Type.Should().Be(CommandType.Unknown);
    }

    [Theory]
    [InlineData("invalid command")]
    [InlineData("random text")]
    [InlineData("sendmoney")]
    public void Parse_UnknownCommands_ShouldBeInvalid(string input)
    {
        var result = _parser.Parse(input);

        result.IsValid.Should().BeFalse();
        result.Type.Should().Be(CommandType.Unknown);
        result.ErrorMessage.Should().Contain("help");
    }

    [Theory]
    [InlineData("send $10 to abc")]
    [InlineData("send $10 to 123")]
    public void Parse_SendWithInvalidPhone_ShouldBeInvalid(string input)
    {
        var result = _parser.Parse(input);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("phone");
    }
}
