namespace PingPay.Infrastructure.Configuration;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum OTP requests per phone number per hour.
    /// </summary>
    public int OtpRequestsPerHour { get; set; } = 5;

    /// <summary>
    /// Maximum login attempts per phone number per hour.
    /// </summary>
    public int LoginAttemptsPerHour { get; set; } = 10;

    /// <summary>
    /// Maximum transactions per user per hour.
    /// </summary>
    public int TransactionsPerHour { get; set; } = 20;

    /// <summary>
    /// Maximum API requests per IP per minute.
    /// </summary>
    public int ApiRequestsPerMinute { get; set; } = 60;
}
