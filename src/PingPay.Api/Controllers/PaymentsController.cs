using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PingPay.Api.Extensions;
using PingPay.Core.DTOs.Payments;
using PingPay.Core.Interfaces;

namespace PingPay.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Send a payment to another user.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaymentResponseDto>> SendPayment(
        [FromBody] SendPaymentDto request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();

        _logger.LogInformation(
            "Payment initiated by user {UserId} to {RecipientPhone} for {Amount} {Token}",
            userId, MaskPhone(request.RecipientPhone), request.Amount, request.TokenType);

        var response = await _paymentService.SendPaymentAsync(userId, request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Get payment history for the authenticated user.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TransactionHistoryDto>>> GetHistory(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var history = await _paymentService.GetHistoryAsync(userId, limit, offset, ct);
        return Ok(history);
    }

    private static string MaskPhone(string phone) =>
        phone.Length > 4 ? phone[..^4] + "****" : "****";
}
