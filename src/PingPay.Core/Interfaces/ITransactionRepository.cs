using PingPay.Core.Entities;
using PingPay.Core.Enums;

namespace PingPay.Core.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(
        Guid userId,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetPendingTransactionsAsync(CancellationToken ct = default);
    Task<Transaction> CreateAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
    Task<decimal> GetDailyTransferredAmountAsync(Guid userId, DateTime since, CancellationToken ct = default);
}
