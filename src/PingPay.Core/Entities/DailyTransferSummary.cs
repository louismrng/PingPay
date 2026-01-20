namespace PingPay.Core.Entities;

public class DailyTransferSummary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    // USDC totals
    public int UsdcSentCount { get; set; }
    public decimal UsdcSentAmount { get; set; }
    public int UsdcReceivedCount { get; set; }
    public decimal UsdcReceivedAmount { get; set; }

    // USDT totals
    public int UsdtSentCount { get; set; }
    public decimal UsdtSentAmount { get; set; }
    public int UsdtReceivedCount { get; set; }
    public decimal UsdtReceivedAmount { get; set; }

    // Fees
    public decimal TotalFeesPaid { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? User { get; set; }

    // Computed
    public decimal TotalSentAmount => UsdcSentAmount + UsdtSentAmount;
    public decimal TotalReceivedAmount => UsdcReceivedAmount + UsdtReceivedAmount;
    public int TotalTransactionCount => UsdcSentCount + UsdcReceivedCount + UsdtSentCount + UsdtReceivedCount;
}
