using Microsoft.AspNetCore.Mvc;
using PingPay.Infrastructure.Services.Telegram;
using Telegram.Bot.Types;

namespace PingPay.Api.Controllers;

/// <summary>
/// Webhook endpoint for Telegram messages.
/// </summary>
[ApiController]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramBotService _botService;
    private readonly ITelegramSenderService _senderService;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        ITelegramBotService botService,
        ITelegramSenderService senderService,
        ILogger<TelegramController> logger)
    {
        _botService = botService;
        _senderService = senderService;
        _logger = logger;
    }

    /// <summary>
    /// Telegram webhook for incoming messages.
    /// </summary>
    [HttpPost("webhook")]
    [Consumes("application/json")]
    public async Task<IActionResult> Webhook(
        [FromBody] Update update,
        CancellationToken ct)
    {
        try
        {
            if (update.Message == null || string.IsNullOrEmpty(update.Message.Text))
            {
                return Ok();
            }

            var chatId = update.Message.Chat.Id.ToString();
            var messageText = update.Message.Text;
            var userName = update.Message.From?.Username ?? update.Message.From?.FirstName ?? "User";

            _logger.LogInformation(
                "Telegram webhook received from {ChatId} ({UserName})",
                chatId, userName);

            var response = await _botService.ProcessMessageAsync(chatId, messageText, ct);

            // Send response back to user
            _ = _senderService.SendMessageAsync(chatId, response.Message, ct);

            return Ok(new { status = "processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram message");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check for the webhook.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "telegram-webhook" });
}
