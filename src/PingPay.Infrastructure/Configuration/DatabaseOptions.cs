namespace PingPay.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int MaxRetryCount { get; set; } = 3;
    public int CommandTimeout { get; set; } = 30;
}
