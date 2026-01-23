using System.Threading;
using System.Threading.Tasks;

namespace PingPay.Infrastructure.Services.Signal;

public interface ISignalSenderService
{
    Task<bool> SendMessageAsync(string toPhoneNumber, string message, CancellationToken ct = default);
}
