namespace PingPay.Infrastructure.Configuration;

/// <summary>
/// Signal bot configuration options.
/// </summary>
public class SignalOptions
{
    public const string SectionName = "Signal";

    public string PhoneNumber { get; set; } = string.Empty;
    public string SignalCliPath { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
}
