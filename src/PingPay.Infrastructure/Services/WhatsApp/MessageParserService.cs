using System.Text.RegularExpressions;
using PingPay.Core.DTOs.WhatsApp;

namespace PingPay.Infrastructure.Services.WhatsApp;

/// <summary>
/// Parses WhatsApp messages into commands.
/// </summary>
public class MessageParserService
{
    // Patterns for parsing commands
    // Accept a broader recipient token so we can parse the send command and validate the phone separately
    private static readonly Regex SendPattern = new(
        @"^send\s+\$?(?<amount>\d+(?:[.,]\d{1,2})?)\s+(?:to\s+)?(?<recipient>\S{1,50})(?:\s+(?<token>usdc|usdt))?$",
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

        // Send command: try to parse manually to be more forgiving than a single regex
        if (input.StartsWith("send "))
        {
            var parts = message.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // amount is typically parts[1]
                var amountPart = parts.Length >= 2 ? parts[1].TrimStart('$') : string.Empty;
                if (decimal.TryParse(amountPart.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amt))
                {
                    // determine recipient: look for token that looks like a phone (contains digits or +)
                    string recipient = string.Empty;
                    string token = "USDC";

                    // search subsequent parts for a phone-like token
                    for (int i = 2; i < parts.Length; i++)
                    {
                        var p = parts[i];
                        var lowered = p.ToLowerInvariant();
                        if (lowered == "to") continue;
                        if (lowered == "usdc" || lowered == "usdt")
                        {
                            token = lowered.ToUpperInvariant();
                            continue;
                        }

                        // candidate recipient
                        recipient = p;
                        // check if next part is a token
                        if (i + 1 < parts.Length)
                        {
                            var maybeToken = parts[i + 1].ToLowerInvariant();
                            if (maybeToken == "usdc" || maybeToken == "usdt") token = maybeToken.ToUpperInvariant();
                        }
                        break;
                    }

                    if (string.IsNullOrEmpty(recipient) && parts.Length >= 3)
                    {
                        recipient = parts[2];
                    }

                    // Build normalized recipient and validate
                    var normalizedPhone = NormalizePhone(recipient ?? string.Empty);
                    if (!PhonePattern.IsMatch(normalizedPhone))
                    {
                        return Invalid("Invalid phone number format");
                    }

                    // validate amount and limits
                    if (amt <= 0) return Invalid("Amount must be greater than zero");
                    if (amt > 10000) return Invalid("Amount exceeds maximum ($10,000)");

                    return new ParsedCommand
                    {
                        Type = CommandType.Send,
                        Amount = amt,
                        RecipientPhone = normalizedPhone,
                        Token = token,
                        IsValid = true,
                        RawInput = message
                    };
                }
            }
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
