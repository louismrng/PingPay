namespace PingPay.Infrastructure.Configuration;

/// <summary>
/// Telegram bot configuration options.
/// </summary>
public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public bool UsePolling { get; set; } = false;
}
