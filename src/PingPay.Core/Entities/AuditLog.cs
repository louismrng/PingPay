namespace PingPay.Core.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    // Change tracking
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    // Request context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }

    public DateTime CreatedAt { get; set; }
}

// Audit action constants
public static class AuditActions
{
    public const string UserRegistered = "user_registered";
    public const string UserLogin = "user_login";
    public const string UserFrozen = "user_frozen";
    public const string UserUnfrozen = "user_unfrozen";
    public const string WalletCreated = "wallet_created";
    public const string PaymentSent = "payment_sent";
    public const string PaymentReceived = "payment_received";
    public const string WithdrawalRequested = "withdrawal_requested";
    public const string WithdrawalCompleted = "withdrawal_completed";
    public const string WithdrawalFailed = "withdrawal_failed";
    public const string LimitChanged = "limit_changed";
    public const string WhitelistAdded = "whitelist_added";
    public const string WhitelistRemoved = "whitelist_removed";
}
