using System.Threading;
using System.Threading.Tasks;
using PingPay.Core.DTOs.WhatsApp;

namespace PingPay.Infrastructure.Services.Signal;

public interface ISignalBotService
{
    Task<WhatsAppResponse> ProcessMessageAsync(string phoneNumber, string message, CancellationToken ct = default);
}
