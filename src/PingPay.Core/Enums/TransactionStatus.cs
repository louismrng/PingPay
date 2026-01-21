namespace PingPay.Core.Enums;

public enum TransactionStatus
{
    Pending = 0,
    Processing = 1,
    Confirmed = 2,
    // historical alias used in some code paths
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
