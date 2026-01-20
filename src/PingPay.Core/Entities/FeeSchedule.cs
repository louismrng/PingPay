using PingPay.Core.Enums;

namespace PingPay.Core.Entities;

public class FeeSchedule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public TokenType TokenType { get; set; }

    // Fee structure
    public decimal FlatFee { get; set; }
    public decimal PercentageFee { get; set; }
    public decimal MinFee { get; set; }
    public decimal? MaxFee { get; set; }

    // Thresholds
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    // Calculate fee for an amount
    public decimal CalculateFee(decimal amount)
    {
        var fee = FlatFee + (amount * PercentageFee);

        if (fee < MinFee) fee = MinFee;
        if (MaxFee.HasValue && fee > MaxFee.Value) fee = MaxFee.Value;

        return fee;
    }
}
