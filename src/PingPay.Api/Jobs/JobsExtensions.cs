using Hangfire;
using PingPay.Infrastructure.Services.KeyManagement;
using PingPay.Infrastructure.Services.Solana;

namespace PingPay.Api.Jobs;

/// <summary>
/// Extension methods for registering Hangfire jobs and services.
/// </summary>
public static class JobsExtensions
{
    /// <summary>
    /// Registers Solana-related services needed by background jobs.
    /// </summary>
    public static IServiceCollection AddSolanaJobServices(this IServiceCollection services)
    {
        services.AddScoped<CachedSolanaBalanceService>();
        services.AddScoped<TransactionMonitorService>();

        services.AddScoped<TransactionProcessingJobs>();
        services.AddScoped<BalanceRefreshJobs>();
        services.AddScoped<KeyRotationJobs>();

        return services;
    }

    /// <summary>
    /// Configures all recurring Hangfire jobs.
    /// Call this after Hangfire is fully configured.
    /// </summary>
    public static void ConfigureRecurringJobs(this IApplicationBuilder app)
    {
        // Transaction processing - every 30 seconds
        RecurringJob.AddOrUpdate<TransactionProcessingJobs>(
            "process-pending-transactions",
            job => job.ProcessPendingTransactions(CancellationToken.None),
            "*/30 * * * * *"); // Every 30 seconds

        // Stale transaction cleanup - every 5 minutes
        RecurringJob.AddOrUpdate<TransactionProcessingJobs>(
            "cleanup-stale-transactions",
            job => job.MarkStaleTransactions(CancellationToken.None),
            "*/5 * * * *"); // Every 5 minutes

        // Active wallet balance refresh - every 5 minutes
        RecurringJob.AddOrUpdate<BalanceRefreshJobs>(
            "refresh-active-wallet-balances",
            job => job.RefreshActiveWalletBalances(CancellationToken.None),
            "*/5 * * * *"); // Every 5 minutes

        // Check SOL balances for fees - daily at 6 AM UTC
        RecurringJob.AddOrUpdate<BalanceRefreshJobs>(
            "check-wallet-fee-balances",
            job => job.CheckWalletFeeBalances(CancellationToken.None),
            "0 6 * * *"); // Daily at 6 AM UTC

        // Wallet encryption validation - weekly on Sunday at 3 AM UTC
        RecurringJob.AddOrUpdate<KeyRotationJobs>(
            "validate-wallet-encryptions",
            job => job.ValidateWalletEncryptions(CancellationToken.None),
            "0 3 * * 0"); // Sunday at 3 AM UTC

        // Key version stats logging - daily at midnight UTC
        RecurringJob.AddOrUpdate<KeyRotationJobs>(
            "log-key-version-stats",
            job => job.LogKeyVersionStats(CancellationToken.None),
            "0 0 * * *"); // Daily at midnight UTC
    }

    /// <summary>
    /// Enqueues a job to wait for a specific transaction confirmation.
    /// </summary>
    public static string EnqueueTransactionConfirmation(Guid transactionId)
    {
        return BackgroundJob.Enqueue<TransactionProcessingJobs>(
            job => job.WaitForTransactionConfirmation(transactionId, CancellationToken.None));
    }

    /// <summary>
    /// Enqueues a job to refresh a wallet's balance.
    /// </summary>
    public static string EnqueueBalanceRefresh(string publicKey)
    {
        return BackgroundJob.Enqueue<BalanceRefreshJobs>(
            job => job.RefreshWalletBalance(publicKey, CancellationToken.None));
    }

    /// <summary>
    /// Enqueues a key rotation job for a specific key version.
    /// </summary>
    public static string EnqueueKeyRotation(string oldKeyVersion)
    {
        return BackgroundJob.Enqueue<KeyRotationJobs>(
            job => job.RotateWalletKeys(oldKeyVersion, CancellationToken.None));
    }
}
