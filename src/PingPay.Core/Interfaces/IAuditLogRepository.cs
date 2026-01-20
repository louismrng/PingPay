using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

public interface IAuditLogRepository
{
    Task CreateAsync(AuditLog log, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(
        Guid userId,
        int limit = 100,
        CancellationToken ct = default);
}
