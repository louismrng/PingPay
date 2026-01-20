using System.ComponentModel.DataAnnotations;

namespace PingPay.Core.DTOs.Auth;

public class RequestOtpDto
{
    [Required]
    [Phone]
    [StringLength(20, MinimumLength = 10)]
    public string PhoneNumber { get; set; } = string.Empty;
}
