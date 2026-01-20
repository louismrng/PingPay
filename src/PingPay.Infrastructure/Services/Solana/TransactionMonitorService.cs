using Microsoft.Extensions.Logging;
using PingPay.Core.Entities;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;

namespace PingPay.Infrastructure.Services.Solana;

/// <summary>
/// Monitors pending Solana transactions and updates their status.
/// </summary>
public class TransactionMonitorService
{
    private readonly ISolanaService _solanaService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly CachedSolanaBalanceService _balanceService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<TransactionMonitorService> _logger;

    // Configuration
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StaleTransactionThreshold = TimeSpan.FromMinutes(10);

    public TransactionMonitorService(
        ISolanaService solanaService,
        ITransactionRepository transactionRepository,
        CachedSolanaBalanceService balanceService,
        IAuditLogRepository auditLogRepository,
        ILogger<TransactionMonitorService> logger)
    {
        _solanaService = solanaService;
        _transactionRepository = transactionRepository;
        _balanceService = balanceService;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Checks a single transaction for confirmation status.
    /// </summary>
    /// <param name="transactionId">The internal transaction ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated transaction status</returns>
    public async Task<TransactionStatus> CheckTransactionStatusAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId, ct);
        if (transaction == null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found", transactionId);
            return TransactionStatus.Failed;
        }

        if (string.IsNullOrEmpty(transaction.SolanaSignature))
        {
            _logger.LogWarning("Transaction {TransactionId} has no Solana signature", transactionId);
            return transaction.Status;
        }

        return await CheckAndUpdateTransactionAsync(transaction, ct);
    }

    /// <summary>
    /// Processes all pending transactions and updates their status.
    /// </summary>
    /// <param name="batchSize">Maximum transactions to process</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Summary of processed transactions</returns>
    public async Task<TransactionMonitorResult> ProcessPendingTransactionsAsync(
        int batchSize = 50,
        CancellationToken ct = default)
    {
        var result = new TransactionMonitorResult();

        var pendingTransactions = await _transactionRepository.GetPendingTransactionsAsync(batchSize, ct);

        _logger.LogInformation(
            "Processing {Count} pending transactions",
            pendingTransactions.Count);

        foreach (var transaction in pendingTransactions)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var newStatus = await CheckAndUpdateTransactionAsync(transaction, ct);

                switch (newStatus)
                {
                    case TransactionStatus.Completed:
                        result.Confirmed++;
                        break;
                    case TransactionStatus.Failed:
                        result.Failed++;
                        break;
                    default:
                        result.StillPending++;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing transaction {TransactionId}",
                    transaction.Id);
                result.Errors++;
            }
        }

        _logger.LogInformation(
            "Transaction monitor completed. Confirmed: {Confirmed}, Failed: {Failed}, Pending: {Pending}, Errors: {Errors}",
            result.Confirmed, result.Failed, result.StillPending, result.Errors);

        return result;
    }

    /// <summary>
    /// Marks stale transactions as failed.
    /// Transactions that have been pending for too long are likely failed.
    /// </summary>
    public async Task<int> MarkStaleTransactionsAsync(CancellationToken ct = default)
    {
        var staleThreshold = DateTime.UtcNow - StaleTransactionThreshold;
        var staleCount = 0;

        var staleTransactions = await _transactionRepository.GetStaleTransactionsAsync(
            staleThreshold,
            maxCount: 100,
            ct);

        foreach (var transaction in staleTransactions)
        {
            try
            {
                // One final check on chain
                if (!string.IsNullOrEmpty(transaction.SolanaSignature))
                {
                    var isConfirmed = await _solanaService.IsTransactionConfirmedAsync(
                        transaction.SolanaSignature, ct);

                    if (isConfirmed)
                    {
                        await UpdateTransactionStatusAsync(
                            transaction,
                            TransactionStatus.Completed,
                            "Confirmed on final check",
                            ct);
                        continue;
                    }
                }

                // Mark as failed
                await UpdateTransactionStatusAsync(
                    transaction,
                    TransactionStatus.Failed,
                    "Transaction timed out",
                    ct);

                staleCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error marking stale transaction {TransactionId}",
                    transaction.Id);
            }
        }

        if (staleCount > 0)
        {
            _logger.LogWarning(
                "Marked {Count} stale transactions as failed",
                staleCount);
        }

        return staleCount;
    }

    /// <summary>
    /// Waits for a specific transaction to be confirmed.
    /// </summary>
    public async Task<bool> WaitForTransactionConfirmationAsync(
        Guid transactionId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId, ct);
        if (transaction == null || string.IsNullOrEmpty(transaction.SolanaSignature))
        {
            return false;
        }

        var effectiveTimeout = timeout ?? ConfirmationTimeout;

        var confirmed = await _solanaService.WaitForConfirmationAsync(
            transaction.SolanaSignature,
            effectiveTimeout,
            ct);

        if (confirmed)
        {
            await UpdateTransactionStatusAsync(
                transaction,
                TransactionStatus.Completed,
                "Confirmed via polling",
                ct);
        }

        return confirmed;
    }

    private async Task<TransactionStatus> CheckAndUpdateTransactionAsync(
        Transaction transaction,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(transaction.SolanaSignature))
        {
            return transaction.Status;
        }

        // Get detailed transaction info from chain
        var details = await _solanaService.GetTransactionDetailsAsync(
            transaction.SolanaSignature, ct);

        if (details == null)
        {
            // Transaction not found - might still be processing or dropped
            var age = DateTime.UtcNow - transaction.CreatedAt;

            if (age > StaleTransactionThreshold)
            {
                await UpdateTransactionStatusAsync(
                    transaction,
                    TransactionStatus.Failed,
                    "Transaction not found on chain after timeout",
                    ct);
                return TransactionStatus.Failed;
            }

            return TransactionStatus.Pending;
        }

        if (details.IsSuccess)
        {
            await UpdateTransactionStatusAsync(
                transaction,
                TransactionStatus.Completed,
                $"Confirmed at slot {details.Slot}",
                ct);

            // Invalidate balance cache for sender and recipient
            await InvalidateRelatedBalancesAsync(transaction, ct);

            return TransactionStatus.Completed;
        }
        else
        {
            await UpdateTransactionStatusAsync(
                transaction,
                TransactionStatus.Failed,
                "Transaction failed on chain",
                ct);
            return TransactionStatus.Failed;
        }
    }

    private async Task UpdateTransactionStatusAsync(
        Transaction transaction,
        TransactionStatus newStatus,
        string reason,
        CancellationToken ct)
    {
        var oldStatus = transaction.Status;
        transaction.Status = newStatus;

        if (newStatus == TransactionStatus.Completed)
        {
            transaction.CompletedAt = DateTime.UtcNow;
        }

        transaction.UpdatedAt = DateTime.UtcNow;

        await _transactionRepository.UpdateAsync(transaction, ct);

        // Audit log
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = transaction.SenderUserId,
            Action = "transaction_status_update",
            EntityType = "Transaction",
            EntityId = transaction.Id.ToString(),
            OldValues = System.Text.Json.JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = newStatus.ToString(),
                Reason = reason
            })
        }, ct);

        _logger.LogInformation(
            "Transaction {TransactionId} status updated: {OldStatus} -> {NewStatus} ({Reason})",
            transaction.Id, oldStatus, newStatus, reason);
    }

    private async Task InvalidateRelatedBalancesAsync(Transaction transaction, CancellationToken ct)
    {
        try
        {
            // Get sender wallet
            if (transaction.SenderWalletId.HasValue)
            {
                // We'd need the wallet public key here - this is a simplification
                // In a real impl, you'd fetch the wallet or store the public key on the transaction
            }

            // Recipient public key is stored on the transaction
            if (!string.IsNullOrEmpty(transaction.RecipientAddress))
            {
                await _balanceService.InvalidateBalanceCacheAsync(
                    transaction.RecipientAddress,
                    transaction.TokenType,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate balance cache for transaction {Id}", transaction.Id);
        }
    }
}

/// <summary>
/// Result of a transaction monitoring batch.
/// </summary>
public class TransactionMonitorResult
{
    public int Confirmed { get; set; }
    public int Failed { get; set; }
    public int StillPending { get; set; }
    public int Errors { get; set; }

    public int Total => Confirmed + Failed + StillPending + Errors;
}
