namespace PingPay.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFrozen { get; set; }
    public decimal DailyTransferLimit { get; set; } = 1000m;
    public decimal DailyTransferredAmount { get; set; }
    public DateTime DailyLimitResetAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Wallet? Wallet { get; set; }
    public ICollection<Transaction> SentTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> ReceivedTransactions { get; set; } = new List<Transaction>();
}
