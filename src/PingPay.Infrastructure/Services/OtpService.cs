using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PingPay.Core.Constants;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using StackExchange.Redis;

namespace PingPay.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IDatabase _database;
    private readonly OtpOptions _options;
    private readonly RedisOptions _redisOptions;

    public OtpService(
        IConnectionMultiplexer redis,
        IOptions<OtpOptions> options,
        IOptions<RedisOptions> redisOptions)
    {
        _database = redis.GetDatabase();
        _options = options.Value;
        _redisOptions = redisOptions.Value;
    }

    public string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var number = BitConverter.ToUInt32(bytes) % (uint)Math.Pow(10, _options.CodeLength);
        return number.ToString().PadLeft(_options.CodeLength, '0');
    }

    public async Task StoreOtpAsync(string phoneNumber, string hashedCode, CancellationToken ct = default)
    {
        var key = GetKey(CacheKeys.Otp(phoneNumber));
        var expiry = TimeSpan.FromMinutes(_options.ExpiryMinutes);

        var data = new Dictionary<string, string>
        {
            ["hash"] = hashedCode,
            ["attempts"] = "0",
            ["created"] = DateTime.UtcNow.ToString("O")
        };

        await _database.HashSetAsync(key, data.Select(x =>
            new HashEntry(x.Key, x.Value)).ToArray());
        await _database.KeyExpireAsync(key, expiry);
    }

    public async Task<bool> ValidateOtpAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        var key = GetKey(CacheKeys.Otp(phoneNumber));

        var data = await _database.HashGetAllAsync(key);
        if (data.Length == 0)
            return false;

        var storedHash = data.FirstOrDefault(x => x.Name == "hash").Value.ToString();
        var attempts = int.Parse(data.FirstOrDefault(x => x.Name == "attempts").Value.ToString() ?? "0");

        if (attempts >= _options.MaxAttempts)
            return false;

        // Increment attempts
        await _database.HashIncrementAsync(key, "attempts");

        var inputHash = HashCode(code);
        return storedHash == inputHash;
    }

    public async Task InvalidateOtpAsync(string phoneNumber, CancellationToken ct = default)
    {
        var key = GetKey(CacheKeys.Otp(phoneNumber));
        await _database.KeyDeleteAsync(key);
    }

    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    private string GetKey(string key) => $"{_redisOptions.InstanceName}{key}";
}
