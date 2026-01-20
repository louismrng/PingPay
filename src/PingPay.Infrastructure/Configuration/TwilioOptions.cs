namespace PingPay.Infrastructure.Configuration;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Use Twilio Verify service instead of raw SMS.
    /// </summary>
    public bool UseVerifyService { get; set; }
    public string? VerifyServiceSid { get; set; }
}
