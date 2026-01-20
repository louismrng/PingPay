using PingPay.Core.Enums;

namespace PingPay.Core.Entities;

public class Transaction
{
    public Guid Id { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate transactions.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    // Participants
    public Guid SenderId { get; set; }
    public Guid? SenderWalletId { get; set; }
    public Guid? ReceiverId { get; set; }

    /// <summary>
    /// For external transfers - the recipient's Solana address.
    /// </summary>
    public string? RecipientAddress { get; set; }

    /// <summary>
    /// For withdrawals to external Solana addresses (legacy field, use RecipientAddress).
    /// </summary>
    public string? ExternalAddress { get; set; }

    // Transaction details
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public TokenType TokenType { get; set; }
    public TransactionStatus Status { get; set; }
    public TransactionType Type { get; set; }

    // Solana details
    public string? SolanaSignature { get; set; }
    public long? SolanaSlot { get; set; }
    public DateTime? SolanaBlockTime { get; set; }

    // Error handling
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime? NextRetryAt { get; set; }

    // Timestamps
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User? Sender { get; set; }
    public User? Receiver { get; set; }
    public Wallet? SenderWallet { get; set; }

    // Computed
    public decimal TotalAmount => Amount + FeeAmount;
    public bool CanRetry => RetryCount < MaxRetries && Status != TransactionStatus.Confirmed;

    // Alias for monitor service compatibility
    public Guid SenderUserId => SenderId;
}
