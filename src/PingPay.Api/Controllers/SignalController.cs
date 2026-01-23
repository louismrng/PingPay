using Microsoft.AspNetCore.Mvc;
using PingPay.Infrastructure.Services.Signal;

namespace PingPay.Api.Controllers;

/// <summary>
/// Webhook endpoint for Signal messages.
/// </summary>
[ApiController]
[Route("api/signal")]
public class SignalController : ControllerBase
{
    private readonly ISignalBotService _botService;
    private readonly ISignalSenderService _senderService;
    private readonly ILogger<SignalController> _logger;

    public SignalController(
        ISignalBotService botService,
        ISignalSenderService senderService,
        ILogger<SignalController> logger)
    {
        _botService = botService;
        _senderService = senderService;
        _logger = logger;
    }

    /// <summary>
    /// Signal webhook for incoming messages.
    /// </summary>
    [HttpPost("webhook")]
    [Consumes("application/json")]
    public async Task<IActionResult> Webhook(
        [FromBody] SignalMessage signalMessage,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(signalMessage.Source) || string.IsNullOrEmpty(signalMessage.Message))
            {
                return BadRequest();
            }

            _logger.LogInformation(
                "Signal webhook received from {Source}",
                signalMessage.Source[..4] + "****");

            var response = await _botService.ProcessMessageAsync(
                signalMessage.Source,
                signalMessage.Message,
                ct);

            // Send response back to user
            _ = _senderService.SendMessageAsync(signalMessage.Source, response.Message, ct);

            return Ok(new { status = "processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Signal message");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check for the webhook.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "signal-webhook" });
}

/// <summary>
/// Signal incoming message format.
/// </summary>
public class SignalMessage
{
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? GroupId { get; set; }
}
