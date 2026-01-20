namespace PingPay.Core.Interfaces;

public interface IRateLimitService
{
    Task<bool> IsAllowedAsync(string key, int maxAttempts, TimeSpan window, CancellationToken ct = default);
    Task<int> GetRemainingAttemptsAsync(string key, int maxAttempts, TimeSpan window, CancellationToken ct = default);
    Task ResetAsync(string key, CancellationToken ct = default);
}
