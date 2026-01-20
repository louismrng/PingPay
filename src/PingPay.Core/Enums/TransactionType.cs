namespace PingPay.Core.Enums;

public enum TransactionType
{
    /// <summary>
    /// Internal transfer between PingPay users.
    /// </summary>
    Transfer = 0,

    /// <summary>
    /// Withdrawal to external Solana wallet.
    /// </summary>
    Withdrawal = 1,

    /// <summary>
    /// Deposit from external source.
    /// </summary>
    Deposit = 2
}
