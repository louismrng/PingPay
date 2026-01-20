using PingPay.Core.Enums;

namespace PingPay.Core.Entities;

public class Transaction
{
    public Guid Id { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate transactions.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }

    public decimal Amount { get; set; }
    public TokenType TokenType { get; set; }

    public TransactionStatus Status { get; set; }
    public TransactionType Type { get; set; }

    /// <summary>
    /// Solana transaction signature (hash).
    /// </summary>
    public string? SolanaSignature { get; set; }

    /// <summary>
    /// Error message if transaction failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? Sender { get; set; }
    public User? Receiver { get; set; }
}
