using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<bool> ExistsAsync(string phoneNumber, CancellationToken ct = default);
}
