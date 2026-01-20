using PingPay.Core.Enums;

namespace PingPay.Core.DTOs.Payments;

public class PaymentResponseDto
{
    public Guid TransactionId { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public TokenType TokenType { get; set; }
    public string? SolanaSignature { get; set; }
    public DateTime CreatedAt { get; set; }
}
