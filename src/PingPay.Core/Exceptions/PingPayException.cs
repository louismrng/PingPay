namespace PingPay.Core.Exceptions;

public class PingPayException : Exception
{
    public string ErrorCode { get; }

    public PingPayException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public PingPayException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class ValidationException : PingPayException
{
    public ValidationException(string message) : base("VALIDATION_ERROR", message) { }
}

public class NotFoundException : PingPayException
{
    public NotFoundException(string entityType, string identifier)
        : base("NOT_FOUND", $"{entityType} not found: {identifier}") { }
}

public class InsufficientBalanceException : PingPayException
{
    public InsufficientBalanceException(decimal requested, decimal available)
        : base("INSUFFICIENT_BALANCE", $"Insufficient balance. Requested: {requested}, Available: {available}") { }
}

public class DailyLimitExceededException : PingPayException
{
    public DailyLimitExceededException(decimal limit, decimal attempted)
        : base("DAILY_LIMIT_EXCEEDED", $"Daily transfer limit exceeded. Limit: {limit}, Attempted: {attempted}") { }
}

public class RateLimitedException : PingPayException
{
    public RateLimitedException(string resource)
        : base("RATE_LIMITED", $"Too many requests for: {resource}") { }
}

public class AccountFrozenException : PingPayException
{
    public AccountFrozenException()
        : base("ACCOUNT_FROZEN", "Account is frozen. Contact support.") { }
}

public class InvalidOtpException : PingPayException
{
    public InvalidOtpException()
        : base("INVALID_OTP", "Invalid or expired OTP code.") { }
}

public class SolanaTransactionException : PingPayException
{
    public SolanaTransactionException(string message)
        : base("SOLANA_ERROR", message) { }

    public SolanaTransactionException(string message, Exception innerException)
        : base("SOLANA_ERROR", message, innerException) { }
}
