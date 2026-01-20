namespace PingPay.Core.Entities;

/// <summary>
/// OTP record stored in Redis (not PostgreSQL).
/// This class is for documentation/typing purposes.
/// </summary>
public class OtpRecord
{
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the OTP code.
    /// </summary>
    public string HashedCode { get; set; } = string.Empty;

    public int AttemptCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
