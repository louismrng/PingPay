using Microsoft.EntityFrameworkCore;
using PingPay.Core.Entities;
using PingPay.Core.Interfaces;

namespace PingPay.Infrastructure.Data.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly PingPayDbContext _context;

    public AuditLogRepository(PingPayDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(AuditLog log, CancellationToken ct = default)
    {
        log.CreatedAt = DateTime.UtcNow;
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(
        Guid userId,
        int limit = 100,
        CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
