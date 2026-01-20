namespace PingPay.Core.Constants;

public static class CacheKeys
{
    public static string Otp(string phoneNumber) => $"otp:{phoneNumber}";
    public static string RateLimit(string resource, string identifier) => $"rate:{resource}:{identifier}";
    public static string Idempotency(string key) => $"idempotency:{key}";
    public static string WalletBalance(Guid userId) => $"balance:{userId}";
    public static string UserSession(Guid userId) => $"session:{userId}";
}
