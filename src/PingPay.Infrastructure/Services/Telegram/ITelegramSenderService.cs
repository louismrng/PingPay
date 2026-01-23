using System.Threading;
using System.Threading.Tasks;

namespace PingPay.Infrastructure.Services.Telegram;

public interface ITelegramSenderService
{
    Task<bool> SendMessageAsync(string chatId, string message, CancellationToken ct = default);
}
