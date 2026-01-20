using System.Text.RegularExpressions;
using PingPay.Core.DTOs.WhatsApp;

namespace PingPay.Infrastructure.Services.WhatsApp;

/// <summary>
/// Parses WhatsApp messages into commands.
/// </summary>
public class MessageParserService
{
    // Patterns for parsing commands
    private static readonly Regex SendPattern = new(
        @"^send\s+\$?(?<amount>\d+(?:\.\d{1,2})?)\s+(?:to\s+)?(?<recipient>\+?\d{10,15})(?:\s+(?<token>usdc|usdt))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StatusPattern = new(
        @"^status\s+(?<id>[a-f0-9\-]{36})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"^\+?[1-9]\d{9,14}$",
        RegexOptions.Compiled);

    public ParsedCommand Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Invalid("Empty message");
        }

        var input = message.Trim().ToLowerInvariant();

        // Help commands
        if (input is "help" or "?" or "hi" or "hello" or "start")
        {
            return new ParsedCommand { Type = CommandType.Help, IsValid = true, RawInput = message };
        }

        // Balance check
        if (input is "balance" or "bal" or "b")
        {
            return new ParsedCommand { Type = CommandType.Balance, IsValid = true, RawInput = message };
        }

        // Transaction history
        if (input is "history" or "hist" or "h" or "transactions")
        {
            return new ParsedCommand { Type = CommandType.History, IsValid = true, RawInput = message };
        }

        // Register/signup
        if (input is "register" or "signup" or "join")
        {
            return new ParsedCommand { Type = CommandType.Register, IsValid = true, RawInput = message };
        }

        // Send command: "send $10 to +1234567890" or "send 10 +1234567890 usdc"
        var sendMatch = SendPattern.Match(input);
        if (sendMatch.Success)
        {
            return ParseSendCommand(sendMatch, message);
        }

        // Status check: "status <transaction-id>"
        var statusMatch = StatusPattern.Match(input);
        if (statusMatch.Success)
        {
            return new ParsedCommand
            {
                Type = CommandType.Status,
                RawInput = statusMatch.Groups["id"].Value,
                IsValid = true
            };
        }

        return Invalid($"Unknown command. Reply 'help' for available commands.");
    }

    private ParsedCommand ParseSendCommand(Match match, string rawInput)
    {
        var amountStr = match.Groups["amount"].Value;
        var recipient = match.Groups["recipient"].Value;
        var token = match.Groups["token"].Success ? match.Groups["token"].Value.ToUpperInvariant() : "USDC";

        if (!decimal.TryParse(amountStr, out var amount))
        {
            return Invalid("Invalid amount");
        }

        if (amount <= 0)
        {
            return Invalid("Amount must be greater than zero");
        }

        if (amount > 10000)
        {
            return Invalid("Amount exceeds maximum ($10,000)");
        }

        // Normalize phone number
        var normalizedPhone = NormalizePhone(recipient);
        if (!PhonePattern.IsMatch(normalizedPhone))
        {
            return Invalid("Invalid phone number format");
        }

        return new ParsedCommand
        {
            Type = CommandType.Send,
            Amount = amount,
            RecipientPhone = normalizedPhone,
            Token = token,
            IsValid = true,
            RawInput = rawInput
        };
    }

    private static string NormalizePhone(string phone)
    {
        // Remove all non-digits except leading +
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        // Ensure it starts with +
        if (!digits.StartsWith('+'))
        {
            digits = "+" + digits;
        }

        return digits;
    }

    private static ParsedCommand Invalid(string error) => new()
    {
        Type = CommandType.Unknown,
        IsValid = false,
        ErrorMessage = error
    };
}
