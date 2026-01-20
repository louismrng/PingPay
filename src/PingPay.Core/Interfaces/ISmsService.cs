namespace PingPay.Core.Interfaces;

public interface ISmsService
{
    Task SendOtpAsync(string phoneNumber, string code, CancellationToken ct = default);
}
