using Hangfire;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Services.Solana;

namespace PingPay.Api.Jobs;

/// <summary>
/// Hangfire background jobs for refreshing wallet balances.
/// </summary>
public class BalanceRefreshJobs
{
    private readonly CachedSolanaBalanceService _balanceService;
    private readonly IWalletRepository _walletRepository;
    private readonly ILogger<BalanceRefreshJobs> _logger;

    public BalanceRefreshJobs(
        CachedSolanaBalanceService balanceService,
        IWalletRepository walletRepository,
        ILogger<BalanceRefreshJobs> logger)
    {
        _balanceService = balanceService;
        _walletRepository = walletRepository;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes balances for active wallets.
    /// Runs every 5 minutes to keep cache warm for active users.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task RefreshActiveWalletBalances(CancellationToken ct)
    {
        _logger.LogInformation("Starting active wallet balance refresh");

        try
        {
            // Get wallets with recent activity (last 24 hours)
            var activeWallets = await _walletRepository.GetActiveWalletsAsync(
                TimeSpan.FromHours(24),
                maxCount: 100,
                ct);

            var refreshed = 0;
            var errors = 0;

            foreach (var wallet in activeWallets)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    // Force refresh the balance
                    await _balanceService.GetAllBalancesAsync(
                        wallet.PublicKey,
                        forceRefresh: true,
                        ct);

                    refreshed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to refresh balance for wallet {WalletId}",
                        wallet.Id);
                    errors++;
                }

                // Small delay to avoid rate limiting
                await Task.Delay(100, ct);
            }

            _logger.LogInformation(
                "Balance refresh completed. Refreshed: {Refreshed}, Errors: {Errors}",
                refreshed, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in active wallet balance refresh job");
            throw;
        }
    }

    /// <summary>
    /// Refreshes balance for a specific wallet.
    /// Called after a transfer completes.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task RefreshWalletBalance(string publicKey, CancellationToken ct)
    {
        _logger.LogDebug("Refreshing balance for wallet {PublicKey}", publicKey[..8] + "...");

        try
        {
            var balances = await _balanceService.GetAllBalancesAsync(
                publicKey,
                forceRefresh: true,
                ct);

            _logger.LogDebug(
                "Wallet {PublicKey} balances: USDC={Usdc}, USDT={Usdt}, SOL={Sol}",
                publicKey[..8] + "...",
                balances.UsdcBalance,
                balances.UsdtBalance,
                balances.SolBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing wallet {PublicKey} balance", publicKey[..8] + "...");
            throw;
        }
    }

    /// <summary>
    /// Checks SOL balance for fee sufficiency on all wallets.
    /// Alerts if wallets are low on SOL for transaction fees.
    /// Runs daily.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task CheckWalletFeeBalances(CancellationToken ct)
    {
        _logger.LogInformation("Starting wallet fee balance check");

        const decimal minimumSol = 0.01m;
        var lowBalanceWallets = new List<(Guid WalletId, string PublicKey, decimal SolBalance)>();

        try
        {
            var wallets = await _walletRepository.GetAllActiveWalletsAsync(ct);

            foreach (var wallet in wallets)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var (hasSufficientSol, currentSol) = await _balanceService
                        .CheckSufficientSolForFeesAsync(wallet.PublicKey, minimumSol, ct);

                    if (!hasSufficientSol)
                    {
                        lowBalanceWallets.Add((wallet.Id, wallet.PublicKey, currentSol));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check SOL balance for wallet {WalletId}", wallet.Id);
                }

                await Task.Delay(50, ct);
            }

            if (lowBalanceWallets.Count > 0)
            {
                _logger.LogWarning(
                    "Found {Count} wallets with insufficient SOL for fees",
                    lowBalanceWallets.Count);

                foreach (var (walletId, publicKey, solBalance) in lowBalanceWallets)
                {
                    _logger.LogWarning(
                        "Wallet {WalletId} ({PublicKey}) has only {SolBalance} SOL",
                        walletId, publicKey[..8] + "...", solBalance);
                }
            }
            else
            {
                _logger.LogInformation("All wallets have sufficient SOL for fees");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in wallet fee balance check job");
            throw;
        }
    }
}
