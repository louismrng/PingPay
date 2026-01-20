using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PingPay.Api.Extensions;
using PingPay.Core.DTOs.Payments;
using PingPay.Core.DTOs.Wallet;
using PingPay.Core.Interfaces;

namespace PingPay.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ILogger<WalletController> _logger;

    public WalletController(IWalletService walletService, ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _logger = logger;
    }

    /// <summary>
    /// Get the wallet balance for the authenticated user.
    /// </summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(WalletBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WalletBalanceDto>> GetBalance(
        [FromQuery] bool refresh = false,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var balance = await _walletService.GetBalanceAsync(userId, refresh, ct);
        return Ok(balance);
    }

    /// <summary>
    /// Withdraw funds to an external Solana wallet.
    /// </summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaymentResponseDto>> Withdraw(
        [FromBody] WithdrawDto request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();

        _logger.LogInformation(
            "Withdrawal initiated by user {UserId} to {DestinationPrefix}... for {Amount} {Token}",
            userId, request.DestinationAddress[..8], request.Amount, request.TokenType);

        var response = await _walletService.WithdrawAsync(userId, request, ct);
        return Ok(response);
    }
}
