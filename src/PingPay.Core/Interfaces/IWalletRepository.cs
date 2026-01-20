using PingPay.Core.Entities;

namespace PingPay.Core.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Wallet?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default);
    Task<Wallet> CreateAsync(Wallet wallet, CancellationToken ct = default);
    Task UpdateAsync(Wallet wallet, CancellationToken ct = default);

    /// <summary>
    /// Gets wallets that have had recent activity.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetActiveWalletsAsync(
        TimeSpan activityWindow,
        int maxCount = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active (non-suspended) wallets.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetAllActiveWalletsAsync(CancellationToken ct = default);
}
