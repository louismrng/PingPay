using System.Text.Json;
using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using StackExchange.Redis;

namespace PingPay.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly RedisOptions _options;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options)
    {
        _database = redis.GetDatabase();
        _options = options.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await _database.StringGetAsync(GetKey(key));

        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value);
        var effectiveExpiry = expiry ?? TimeSpan.FromMinutes(_options.DefaultExpiryMinutes);

        await _database.StringSetAsync(GetKey(key), json, effectiveExpiry);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _database.KeyDeleteAsync(GetKey(key));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await _database.KeyExistsAsync(GetKey(key));
    }

    private string GetKey(string key) => $"{_options.InstanceName}{key}";
}
