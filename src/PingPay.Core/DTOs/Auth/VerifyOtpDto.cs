using System.ComponentModel.DataAnnotations;

namespace PingPay.Core.DTOs.Auth;

public class VerifyOtpDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}
