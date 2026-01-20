namespace PingPay.Core.Entities;

public class WithdrawalWhitelist
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
