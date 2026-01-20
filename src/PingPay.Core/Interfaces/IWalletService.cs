using PingPay.Core.DTOs.Payments;
using PingPay.Core.DTOs.Wallet;
using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

public interface IWalletService
{
    Task<Wallet> CreateWalletForUserAsync(Guid userId, CancellationToken ct = default);
    Task<WalletBalanceDto> GetBalanceAsync(Guid userId, bool forceRefresh = false, CancellationToken ct = default);
    Task<PaymentResponseDto> WithdrawAsync(Guid userId, WithdrawDto request, CancellationToken ct = default);
}
