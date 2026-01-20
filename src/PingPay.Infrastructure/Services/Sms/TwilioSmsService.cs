using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PingPay.Infrastructure.Services.Sms;

public class TwilioSmsService : ISmsService
{
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(
        IOptions<TwilioOptions> options,
        ILogger<TwilioSmsService> logger)
    {
        _options = options.Value;
        _logger = logger;

        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public async Task SendOtpAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        try
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_options.FromPhoneNumber),
                body: $"Your PingPay verification code is: {code}. Valid for 5 minutes.");

            _logger.LogInformation(
                "OTP sent successfully. SID: {MessageSid}, To: {PhoneNumber}",
                message.Sid,
                MaskPhoneNumber(phoneNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            throw;
        }
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (phoneNumber.Length <= 4)
            return "****";

        return phoneNumber[..^4] + "****";
    }
}
