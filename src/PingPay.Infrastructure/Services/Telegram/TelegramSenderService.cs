using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Infrastructure.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PingPay.Infrastructure.Services.Telegram;

/// <summary>
/// Sends Telegram messages via Telegram Bot API.
/// </summary>
public class TelegramSenderService : ITelegramSenderService
{
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramSenderService> _logger;
    private readonly ITelegramBotClient _botClient;

    public TelegramSenderService(
        IOptions<TelegramOptions> options,
        ILogger<TelegramSenderService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _botClient = new TelegramBotClient(_options.BotToken);
    }

    /// <summary>
    /// Send a Telegram message to a chat ID.
    /// </summary>
    public async Task<bool> SendMessageAsync(
        string chatId,
        string message,
        CancellationToken ct = default)
    {
        try
        {
            if (!long.TryParse(chatId, out var parsedChatId))
            {
                _logger.LogWarning("Invalid chat ID format: {ChatId}", chatId);
                return false;
            }

            // The Telegram.Bot library marked older async method names obsolete.
            // Suppress CS0618 for this call to remain compatible with multiple versions.
#pragma warning disable CS0618
            var result = await _botClient.SendTextMessageAsync(
                chatId: parsedChatId,
                text: message,
                parseMode: global::Telegram.Bot.Types.Enums.ParseMode.MarkdownV2,
                cancellationToken: ct);
#pragma warning restore CS0618

            _logger.LogInformation(
                "Telegram message sent: {MessageId} to {ChatId}",
                result.MessageId, chatId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to {ChatId}", chatId);
            return false;
        }
    }

    /// <summary>
    /// Get the bot client instance.
    /// </summary>
    public ITelegramBotClient GetBotClient() => _botClient;
}
