using Microsoft.Extensions.Logging;
using PingPay.Core.Entities;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PingPay.Infrastructure.Services.KeyManagement;

/// <summary>
/// Service for rotating wallet encryption keys.
/// Should be run as a background job when master key is rotated.
/// </summary>
public class KeyRotationService
{
    private readonly PingPayDbContext _dbContext;
    private readonly IWalletEncryptionService _walletEncryptionService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<KeyRotationService> _logger;

    public KeyRotationService(
        PingPayDbContext dbContext,
        IWalletEncryptionService walletEncryptionService,
        IAuditLogRepository auditLogRepository,
        ILogger<KeyRotationService> logger)
    {
        _dbContext = dbContext;
        _walletEncryptionService = walletEncryptionService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Rotates encryption for all wallets using a specific key version.
    /// </summary>
    /// <param name="oldKeyVersion">The key version to rotate from</param>
    /// <param name="batchSize">Number of wallets to process per batch</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of wallets rotated</returns>
    public async Task<int> RotateWalletsWithKeyVersionAsync(
        string oldKeyVersion,
        int batchSize = 100,
        CancellationToken ct = default)
    {
        var totalRotated = 0;
        var failedWalletIds = new List<Guid>();

        _logger.LogInformation(
            "Starting key rotation for wallets with key version {OldKeyVersion}",
            oldKeyVersion);

        while (!ct.IsCancellationRequested)
        {
            // Get batch of wallets with the old key version
            var wallets = await _dbContext.Wallets
                .Where(w => w.KeyVersion == oldKeyVersion)
                .Take(batchSize)
                .ToListAsync(ct);

            if (wallets.Count == 0)
                break;

            foreach (var wallet in wallets)
            {
                try
                {
                    var updatedWallet = await _walletEncryptionService.RotateEncryptionAsync(wallet, ct);

                    _dbContext.Wallets.Update(updatedWallet);
                    await _dbContext.SaveChangesAsync(ct);

                    await _auditLogRepository.CreateAsync(new AuditLog
                    {
                        UserId = wallet.UserId,
                        Action = "key_rotation",
                        EntityType = "Wallet",
                        EntityId = wallet.Id.ToString(),
                        OldValues = System.Text.Json.JsonSerializer.Serialize(new { wallet.KeyVersion }),
                        NewValues = System.Text.Json.JsonSerializer.Serialize(new { updatedWallet.KeyVersion })
                    }, ct);

                    totalRotated++;

                    _logger.LogDebug(
                        "Rotated wallet {WalletId} from {OldVersion} to {NewVersion}",
                        wallet.Id, oldKeyVersion, updatedWallet.KeyVersion);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rotate wallet {WalletId}", wallet.Id);
                    failedWalletIds.Add(wallet.Id);

                    // Mark wallet for manual review (don't update version so it gets retried)
                    await _auditLogRepository.CreateAsync(new AuditLog
                    {
                        UserId = wallet.UserId,
                        Action = "key_rotation_failed",
                        EntityType = "Wallet",
                        EntityId = wallet.Id.ToString(),
                        NewValues = System.Text.Json.JsonSerializer.Serialize(new { Error = ex.Message })
                    }, ct);
                }
            }

            // Small delay between batches to reduce load
            await Task.Delay(100, ct);
        }

        _logger.LogInformation(
            "Key rotation completed. Rotated: {RotatedCount}, Failed: {FailedCount}",
            totalRotated, failedWalletIds.Count);

        if (failedWalletIds.Count > 0)
        {
            _logger.LogWarning(
                "Failed wallet IDs: {FailedIds}",
                string.Join(", ", failedWalletIds));
        }

        return totalRotated;
    }

    /// <summary>
    /// Validates that all wallets can be decrypted.
    /// Use this to verify encryption integrity.
    /// </summary>
    public async Task<(int Valid, int Invalid, List<Guid> InvalidIds)> ValidateAllWalletsAsync(
        int batchSize = 100,
        CancellationToken ct = default)
    {
        var valid = 0;
        var invalid = 0;
        var invalidIds = new List<Guid>();

        var offset = 0;

        while (!ct.IsCancellationRequested)
        {
            var wallets = await _dbContext.Wallets
                .OrderBy(w => w.CreatedAt)
                .Skip(offset)
                .Take(batchSize)
                .ToListAsync(ct);

            if (wallets.Count == 0)
                break;

            foreach (var wallet in wallets)
            {
                var isValid = await _walletEncryptionService.ValidateEncryptionAsync(wallet, ct);

                if (isValid)
                {
                    valid++;
                }
                else
                {
                    invalid++;
                    invalidIds.Add(wallet.Id);
                }
            }

            offset += batchSize;
        }

        _logger.LogInformation(
            "Wallet validation completed. Valid: {ValidCount}, Invalid: {InvalidCount}",
            valid, invalid);

        return (valid, invalid, invalidIds);
    }

    /// <summary>
    /// Gets statistics about key versions in use.
    /// </summary>
    public async Task<Dictionary<string, int>> GetKeyVersionStatsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Wallets
            .GroupBy(w => w.KeyVersion)
            .Select(g => new { KeyVersion = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.KeyVersion, x => x.Count, ct);
    }
}
