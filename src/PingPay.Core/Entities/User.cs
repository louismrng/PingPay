namespace PingPay.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string PhoneCountryCode { get; set; } = "+1";
    public string? DisplayName { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFrozen { get; set; }
    public string? FrozenReason { get; set; }
    public DateTime? FrozenAt { get; set; }
    public Guid? FrozenBy { get; set; }

    // Transfer limits
    public decimal DailyTransferLimit { get; set; } = 1000m;
    public decimal DailyTransferredAmount { get; set; }
    public DateTime DailyLimitResetAt { get; set; }
    public decimal MonthlyTransferLimit { get; set; } = 10000m;
    public decimal MonthlyTransferredAmount { get; set; }
    public DateTime MonthlyLimitResetAt { get; set; }

    // Metadata
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Wallet? Wallet { get; set; }
    public ICollection<Transaction> SentTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> ReceivedTransactions { get; set; } = new List<Transaction>();
    public ICollection<WithdrawalWhitelist> WithdrawalWhitelist { get; set; } = new List<WithdrawalWhitelist>();
    public ICollection<DailyTransferSummary> DailyTransferSummaries { get; set; } = new List<DailyTransferSummary>();

    // Computed properties
    public string FullPhoneNumber => $"{PhoneCountryCode}{PhoneNumber}";
    public decimal DailyRemaining => Math.Max(0, DailyTransferLimit - DailyTransferredAmount);
    public decimal MonthlyRemaining => Math.Max(0, MonthlyTransferLimit - MonthlyTransferredAmount);
}
