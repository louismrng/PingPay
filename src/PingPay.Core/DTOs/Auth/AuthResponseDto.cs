namespace PingPay.Core.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid UserId { get; set; }
    public bool IsNewUser { get; set; }
}
