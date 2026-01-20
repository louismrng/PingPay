using PingPay.Core.DTOs.Payments;

namespace PingPay.Core.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponseDto> SendPaymentAsync(
        Guid senderId,
        SendPaymentDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<TransactionHistoryDto>> GetHistoryAsync(
        Guid userId,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);
}
