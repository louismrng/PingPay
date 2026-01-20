using System.ComponentModel.DataAnnotations;
using PingPay.Core.Enums;

namespace PingPay.Core.DTOs.Wallet;

public class WithdrawDto
{
    /// <summary>
    /// Destination Solana wallet address.
    /// </summary>
    [Required]
    [StringLength(44, MinimumLength = 32)]
    public string DestinationAddress { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10000)]
    public decimal Amount { get; set; }

    [Required]
    public TokenType TokenType { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 16)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
