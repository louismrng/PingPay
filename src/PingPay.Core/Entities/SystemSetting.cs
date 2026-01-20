namespace PingPay.Core.Entities;

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

// Strongly-typed system settings keys
public static class SystemSettingKeys
{
    public const string MaintenanceMode = "maintenance_mode";
    public const string NewRegistrationsEnabled = "new_registrations_enabled";
    public const string WithdrawalsEnabled = "withdrawals_enabled";
    public const string DefaultDailyLimit = "default_daily_limit";
    public const string DefaultMonthlyLimit = "default_monthly_limit";
    public const string MinTransferAmount = "min_transfer_amount";
    public const string MaxTransferAmount = "max_transfer_amount";
    public const string BalanceCacheTtlSeconds = "balance_cache_ttl_seconds";
}
