namespace PingPay.Infrastructure.Configuration;

public class OtpOptions
{
    public const string SectionName = "Otp";

    public int CodeLength { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 3;
    public int ResendCooldownSeconds { get; set; } = 60;
}
