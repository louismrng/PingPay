namespace PingPay.Core.DTOs.Messaging;

/// <summary>
/// Generic message DTO for messaging platforms.
/// </summary>
public class IncomingMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // "whatsapp", "telegram", "signal"
    public string Body { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public string? ProfileName { get; set; }
}

/// <summary>
/// Response DTO for outgoing messages.
/// </summary>
public class OutgoingMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
