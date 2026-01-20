using Microsoft.EntityFrameworkCore;
using PingPay.Core.Entities;
using PingPay.Core.Interfaces;

namespace PingPay.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PingPayDbContext _context;

    public UserRepository(PingPayDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, ct);
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string phoneNumber, CancellationToken ct = default)
    {
        return await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber, ct);
    }
}
