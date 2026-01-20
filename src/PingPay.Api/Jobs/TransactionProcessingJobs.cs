using Hangfire;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Services.Solana;

namespace PingPay.Api.Jobs;

/// <summary>
/// Hangfire background jobs for processing Solana transactions.
/// </summary>
public class TransactionProcessingJobs
{
    private readonly TransactionMonitorService _monitorService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<TransactionProcessingJobs> _logger;

    public TransactionProcessingJobs(
        TransactionMonitorService monitorService,
        ITransactionRepository transactionRepository,
        ILogger<TransactionProcessingJobs> logger)
    {
        _monitorService = monitorService;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Processes pending transactions and updates their status.
    /// Runs every 30 seconds.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessPendingTransactions(CancellationToken ct)
    {
        _logger.LogInformation("Starting pending transaction processing job");

        try
        {
            var result = await _monitorService.ProcessPendingTransactionsAsync(batchSize: 50, ct);

            _logger.LogInformation(
                "Pending transactions processed. Confirmed: {Confirmed}, Failed: {Failed}, Pending: {Pending}",
                result.Confirmed, result.Failed, result.StillPending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in pending transaction processing job");
            throw;
        }
    }

    /// <summary>
    /// Marks transactions that have been pending too long as failed.
    /// Runs every 5 minutes.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task MarkStaleTransactions(CancellationToken ct)
    {
        _logger.LogInformation("Starting stale transaction cleanup job");

        try
        {
            var staleCount = await _monitorService.MarkStaleTransactionsAsync(ct);

            if (staleCount > 0)
            {
                _logger.LogWarning("Marked {Count} stale transactions as failed", staleCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stale transaction cleanup job");
            throw;
        }
    }

    /// <summary>
    /// Monitors a specific transaction for confirmation.
    /// Called when a transaction is submitted to chain.
    /// </summary>
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 10, 30, 60, 120, 300 })]
    public async Task WaitForTransactionConfirmation(Guid transactionId, CancellationToken ct)
    {
        _logger.LogInformation("Waiting for confirmation of transaction {TransactionId}", transactionId);

        try
        {
            var confirmed = await _monitorService.WaitForTransactionConfirmationAsync(
                transactionId,
                timeout: TimeSpan.FromMinutes(2),
                ct);

            if (confirmed)
            {
                _logger.LogInformation("Transaction {TransactionId} confirmed", transactionId);
            }
            else
            {
                _logger.LogWarning("Transaction {TransactionId} not confirmed after timeout", transactionId);
                // Let it retry via Hangfire's retry mechanism
                throw new InvalidOperationException($"Transaction {transactionId} not confirmed");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error waiting for transaction {TransactionId} confirmation", transactionId);
            throw;
        }
    }

    /// <summary>
    /// Checks a single transaction's status.
    /// Used for manual retries or specific transaction monitoring.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task CheckTransactionStatus(Guid transactionId, CancellationToken ct)
    {
        _logger.LogDebug("Checking status of transaction {TransactionId}", transactionId);

        try
        {
            var status = await _monitorService.CheckTransactionStatusAsync(transactionId, ct);

            _logger.LogInformation(
                "Transaction {TransactionId} status: {Status}",
                transactionId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking transaction {TransactionId} status", transactionId);
            throw;
        }
    }
}
