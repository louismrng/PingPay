using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using StackExchange.Redis;

namespace PingPay.Infrastructure.Services;

public class RedisRateLimitService : IRateLimitService
{
    private readonly IDatabase _database;
    private readonly RedisOptions _options;

    public RedisRateLimitService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options)
    {
        _database = redis.GetDatabase();
        _options = options.Value;
    }

    public async Task<bool> IsAllowedAsync(
        string key,
        int maxAttempts,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var redisKey = GetKey(key);
        var count = await _database.StringIncrementAsync(redisKey);

        if (count == 1)
        {
            await _database.KeyExpireAsync(redisKey, window);
        }

        return count <= maxAttempts;
    }

    public async Task<int> GetRemainingAttemptsAsync(
        string key,
        int maxAttempts,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var redisKey = GetKey(key);
        var countStr = await _database.StringGetAsync(redisKey);

        if (countStr.IsNullOrEmpty)
            return maxAttempts;

        var count = (int)countStr;
        return Math.Max(0, maxAttempts - count);
    }

    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        await _database.KeyDeleteAsync(GetKey(key));
    }

    private string GetKey(string key) => $"{_options.InstanceName}rate:{key}";
}
