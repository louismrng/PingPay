using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Infrastructure.Configuration;
using System.Net.Http.Json;

namespace PingPay.Infrastructure.Services.Signal;

/// <summary>
/// Sends Signal messages via Signal REST API.
/// </summary>
public class SignalSenderService : ISignalSenderService
{
    private readonly SignalOptions _options;
    private readonly ILogger<SignalSenderService> _logger;
    private readonly HttpClient _httpClient;

    public SignalSenderService(
        IOptions<SignalOptions> options,
        ILogger<SignalSenderService> logger,
        HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.ApiEndpoint);
    }

    /// <summary>
    /// Send a Signal message to a phone number.
    /// </summary>
    public async Task<bool> SendMessageAsync(
        string toPhoneNumber,
        string message,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                number = _options.PhoneNumber,
                recipients = new[] { toPhoneNumber },
                message = message
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/send",
                payload,
                cancellationToken: ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Signal message sent to {To}",
                    toPhoneNumber[..4] + "****");
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "Signal API returned {StatusCode} for message to {To}",
                    response.StatusCode, toPhoneNumber[..4] + "****");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Signal message to {To}", toPhoneNumber[..4] + "****");
            return false;
        }
    }
}
