namespace PingPay.Core.DTOs.WhatsApp;

/// <summary>
/// Incoming message from Twilio WhatsApp webhook.
/// </summary>
public class TwilioWhatsAppMessage
{
    public string MessageSid { get; set; } = string.Empty;
    public string AccountSid { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;  // whatsapp:+1234567890
    public string To { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int NumMedia { get; set; }
    public string? ProfileName { get; set; }
}

/// <summary>
/// Parsed command from user message.
/// </summary>
public class ParsedCommand
{
    public CommandType Type { get; set; }
    public string? RecipientPhone { get; set; }
    public decimal? Amount { get; set; }
    public string? Token { get; set; }
    public string? RawInput { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum CommandType
{
    Unknown,
    Help,
    Balance,
    Send,
    History,
    Status,
    Register
}

/// <summary>
/// Response to send back to user.
/// </summary>
public class WhatsAppResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}
