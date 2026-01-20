using PingPay.Core.Enums;

namespace PingPay.Core.DTOs.Payments;

public class TransactionHistoryDto
{
    public Guid TransactionId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public TokenType TokenType { get; set; }
    public string? CounterpartyPhone { get; set; }
    public string? CounterpartyName { get; set; }
    public string? SolanaSignature { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
