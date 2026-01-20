using Microsoft.AspNetCore.Mvc;
using PingPay.Infrastructure.Services.WhatsApp;

namespace PingPay.Api.Controllers;

/// <summary>
/// Webhook endpoint for WhatsApp messages via Twilio.
/// </summary>
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly WhatsAppBotService _botService;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        WhatsAppBotService botService,
        ILogger<WhatsAppController> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    /// <summary>
    /// Twilio webhook for incoming WhatsApp messages.
    /// </summary>
    [HttpPost("webhook")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/xml")]
    public async Task<IActionResult> Webhook(
        [FromForm] string From,
        [FromForm] string Body,
        [FromForm] string? MessageSid,
        [FromForm] string? ProfileName,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "WhatsApp webhook received from {From}, MessageSid: {Sid}",
            From?[..12] + "****", MessageSid);

        if (string.IsNullOrEmpty(From) || string.IsNullOrEmpty(Body))
        {
            return BadRequest();
        }

        // Extract phone number from "whatsapp:+1234567890"
        var phoneNumber = From.Replace("whatsapp:", "");

        try
        {
            var response = await _botService.ProcessMessageAsync(phoneNumber, Body, ct);

            // Return TwiML response
            var twiml = WhatsAppSenderService.GenerateTwimlResponse(response.Message);
            return Content(twiml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WhatsApp message");

            var errorTwiml = WhatsAppSenderService.GenerateTwimlResponse(
                "Sorry, something went wrong. Please try again later.");
            return Content(errorTwiml, "application/xml");
        }
    }

    /// <summary>
    /// Status callback for message delivery updates.
    /// </summary>
    [HttpPost("status")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult StatusCallback(
        [FromForm] string? MessageSid,
        [FromForm] string? MessageStatus,
        [FromForm] string? To)
    {
        _logger.LogInformation(
            "WhatsApp status update: {Sid} -> {Status}",
            MessageSid, MessageStatus);

        return Ok();
    }

    /// <summary>
    /// Health check for the webhook.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "whatsapp-webhook" });
}
