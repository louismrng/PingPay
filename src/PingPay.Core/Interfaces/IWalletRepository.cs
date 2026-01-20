using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Wallet?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default);
    Task<Wallet> CreateAsync(Wallet wallet, CancellationToken ct = default);
    Task UpdateAsync(Wallet wallet, CancellationToken ct = default);
}
