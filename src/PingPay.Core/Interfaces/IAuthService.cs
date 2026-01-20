using PingPay.Core.DTOs.Auth;

namespace PingPay.Core.Interfaces;

public interface IAuthService
{
    Task<bool> RequestOtpAsync(RequestOtpDto request, CancellationToken ct = default);
    Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto request, CancellationToken ct = default);
}
