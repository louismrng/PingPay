using System.ComponentModel.DataAnnotations;
using PingPay.Core.Enums;

namespace PingPay.Core.DTOs.Payments;

public class SendPaymentDto
{
    /// <summary>
    /// Recipient phone number.
    /// </summary>
    [Required]
    [Phone]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10000)]
    public decimal Amount { get; set; }

    [Required]
    public TokenType TokenType { get; set; }

    /// <summary>
    /// Client-generated idempotency key to prevent duplicate submissions.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 16)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
