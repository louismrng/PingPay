using Microsoft.EntityFrameworkCore;
using PingPay.Core.Entities;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;

namespace PingPay.Infrastructure.Data.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly PingPayDbContext _context;

    public TransactionRepository(PingPayDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(
        Guid userId,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .Where(t => t.SenderId == userId || t.ReceiverId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetPendingTransactionsAsync(
        int maxCount = 50,
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Processing)
            .OrderBy(t => t.CreatedAt)
            .Take(maxCount)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetStaleTransactionsAsync(
        DateTime olderThan,
        int maxCount = 100,
        CancellationToken ct = default)
    {
        return await _context.Transactions
            .Where(t => (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Processing)
                        && t.CreatedAt < olderThan)
            .OrderBy(t => t.CreatedAt)
            .Take(maxCount)
            .ToListAsync(ct);
    }

    public async Task<int> GetPendingTransactionCountAsync(CancellationToken ct = default)
    {
        return await _context.Transactions
            .CountAsync(t => t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Processing, ct);
    }

    public async Task<Transaction> CreateAsync(Transaction transaction, CancellationToken ct = default)
    {
        transaction.CreatedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);

        return transaction;
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetDailyTransferredAmountAsync(Guid userId, DateTime since, CancellationToken ct = default)
    {
        return await _context.Transactions
            .Where(t => t.SenderId == userId &&
                       t.CreatedAt >= since &&
                       t.Status != TransactionStatus.Failed &&
                       t.Status != TransactionStatus.Cancelled)
            .SumAsync(t => t.Amount, ct);
    }
}
