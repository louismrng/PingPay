using Microsoft.EntityFrameworkCore;
using PingPay.Core.Entities;
using PingPay.Core.Interfaces;

namespace PingPay.Infrastructure.Data.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly PingPayDbContext _context;

    public WalletRepository(PingPayDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);
    }

    public async Task<Wallet?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.PublicKey == publicKey, ct);
    }

    public async Task<Wallet> CreateAsync(Wallet wallet, CancellationToken ct = default)
    {
        wallet.CreatedAt = DateTime.UtcNow;
        wallet.BalanceLastUpdatedAt = DateTime.UtcNow;

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync(ct);

        return wallet;
    }

    public async Task UpdateAsync(Wallet wallet, CancellationToken ct = default)
    {
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync(ct);
    }
}
