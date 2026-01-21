using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Infrastructure.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PingPay.Infrastructure.Services.WhatsApp;

/// <summary>
/// Sends WhatsApp messages via Twilio.
/// </summary>
public class WhatsAppSenderService
{
    private readonly TwilioOptions _options;
    private readonly ILogger<WhatsAppSenderService> _logger;
    private readonly string _fromWhatsApp;

    public WhatsAppSenderService(
        IOptions<TwilioOptions> options,
        ILogger<WhatsAppSenderService> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Twilio options property renamed to FromPhoneNumber
        _fromWhatsApp = $"whatsapp:{_options.FromPhoneNumber}";

        // Initialize Twilio client
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    /// <summary>
    /// Send a WhatsApp message to a phone number.
    /// </summary>
    public async Task<bool> SendMessageAsync(
        string toPhoneNumber,
        string message,
        CancellationToken ct = default)
    {
        try
        {
            var toWhatsApp = toPhoneNumber.StartsWith("whatsapp:")
                ? toPhoneNumber
                : $"whatsapp:{toPhoneNumber}";

            var result = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_fromWhatsApp),
                to: new PhoneNumber(toWhatsApp));

            _logger.LogInformation(
                "WhatsApp message sent: {Sid} to {To}",
                result.Sid, toPhoneNumber[..4] + "****");

            return result.Status != MessageResource.StatusEnum.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp message to {To}", toPhoneNumber[..4] + "****");
            return false;
        }
    }

    /// <summary>
    /// Generate TwiML response for webhook.
    /// </summary>
    public static string GenerateTwimlResponse(string message)
    {
        // Escape XML special characters
        var escaped = System.Security.SecurityElement.Escape(message);

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Response>
                <Message>{escaped}</Message>
            </Response>
            """;
    }
}
