namespace PingPay.Infrastructure.Configuration;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "pingpay:";
    public int DefaultExpiryMinutes { get; set; } = 60;
}
