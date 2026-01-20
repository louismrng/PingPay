namespace PingPay.Core.Interfaces;

public interface IOtpService
{
    string GenerateCode();
    Task StoreOtpAsync(string phoneNumber, string hashedCode, CancellationToken ct = default);
    Task<bool> ValidateOtpAsync(string phoneNumber, string code, CancellationToken ct = default);
    Task InvalidateOtpAsync(string phoneNumber, CancellationToken ct = default);
}
