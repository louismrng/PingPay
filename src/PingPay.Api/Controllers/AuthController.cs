using Microsoft.AspNetCore.Mvc;
using PingPay.Core.DTOs.Auth;
using PingPay.Core.Interfaces;

namespace PingPay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Request an OTP code to be sent via SMS.
    /// </summary>
    [HttpPost("request-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpDto request, CancellationToken ct)
    {
        _logger.LogInformation("OTP requested for phone number ending in {PhoneSuffix}",
            request.PhoneNumber[^4..]);

        await _authService.RequestOtpAsync(request, ct);

        return Ok(new { message = "OTP sent successfully" });
    }

    /// <summary>
    /// Verify OTP code and receive JWT token.
    /// </summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> VerifyOtp([FromBody] VerifyOtpDto request, CancellationToken ct)
    {
        var response = await _authService.VerifyOtpAsync(request, ct);
        return Ok(response);
    }
}
