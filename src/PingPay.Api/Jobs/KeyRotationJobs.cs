using Hangfire;
using PingPay.Infrastructure.Services.KeyManagement;

namespace PingPay.Api.Jobs;

/// <summary>
/// Hangfire background jobs for key rotation operations.
/// </summary>
public class KeyRotationJobs
{
    private readonly KeyRotationService _keyRotationService;
    private readonly ILogger<KeyRotationJobs> _logger;

    public KeyRotationJobs(
        KeyRotationService keyRotationService,
        ILogger<KeyRotationJobs> logger)
    {
        _keyRotationService = keyRotationService;
        _logger = logger;
    }

    /// <summary>
    /// Rotates encryption keys for wallets using a specific old key version.
    /// This job should be triggered when a new master key is deployed.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RotateWalletKeys(string oldKeyVersion, CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting key rotation job for key version {OldKeyVersion}",
            oldKeyVersion);

        try
        {
            var rotatedCount = await _keyRotationService.RotateWalletsWithKeyVersionAsync(
                oldKeyVersion,
                batchSize: 50,
                ct);

            _logger.LogInformation(
                "Key rotation completed. Rotated {Count} wallets from {OldKeyVersion}",
                rotatedCount, oldKeyVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key rotation for {OldKeyVersion}", oldKeyVersion);
            throw;
        }
    }

    /// <summary>
    /// Validates that all wallet encryptions can be decrypted.
    /// Runs weekly to detect any data integrity issues.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task ValidateWalletEncryptions(CancellationToken ct)
    {
        _logger.LogInformation("Starting wallet encryption validation job");

        try
        {
            var (valid, invalid, invalidIds) = await _keyRotationService.ValidateAllWalletsAsync(
                batchSize: 100,
                ct);

            if (invalid > 0)
            {
                _logger.LogError(
                    "Wallet validation found {Invalid} invalid wallets out of {Total}. IDs: {InvalidIds}",
                    invalid, valid + invalid, string.Join(", ", invalidIds));
            }
            else
            {
                _logger.LogInformation(
                    "Wallet validation completed successfully. All {Count} wallets are valid",
                    valid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during wallet encryption validation");
            throw;
        }
    }

    /// <summary>
    /// Gets statistics on key versions in use.
    /// Useful for monitoring key rotation progress.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task LogKeyVersionStats(CancellationToken ct)
    {
        _logger.LogInformation("Gathering key version statistics");

        try
        {
            var stats = await _keyRotationService.GetKeyVersionStatsAsync(ct);

            foreach (var (version, count) in stats)
            {
                _logger.LogInformation(
                    "Key version {Version}: {Count} wallets",
                    version, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error gathering key version statistics");
            throw;
        }
    }
}
