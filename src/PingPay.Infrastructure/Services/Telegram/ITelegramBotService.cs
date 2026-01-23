using System.Threading;
using System.Threading.Tasks;
using PingPay.Core.DTOs.WhatsApp;

namespace PingPay.Infrastructure.Services.Telegram;

public interface ITelegramBotService
{
    Task<WhatsAppResponse> ProcessMessageAsync(string userId, string message, CancellationToken ct = default);
}
