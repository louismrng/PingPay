using System.Net;
using System.Text.Json;
using PingPay.Core.Exceptions;

namespace PingPay.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            ValidationException e => (HttpStatusCode.BadRequest, e.ErrorCode, e.Message),
            NotFoundException e => (HttpStatusCode.NotFound, e.ErrorCode, e.Message),
            InsufficientBalanceException e => (HttpStatusCode.BadRequest, e.ErrorCode, e.Message),
            DailyLimitExceededException e => (HttpStatusCode.BadRequest, e.ErrorCode, e.Message),
            RateLimitedException e => (HttpStatusCode.TooManyRequests, e.ErrorCode, e.Message),
            AccountFrozenException e => (HttpStatusCode.Forbidden, e.ErrorCode, e.Message),
            InvalidOtpException e => (HttpStatusCode.Unauthorized, e.ErrorCode, e.Message),
            SolanaTransactionException e => (HttpStatusCode.ServiceUnavailable, e.ErrorCode, e.Message),
            PingPayException e => (HttpStatusCode.BadRequest, e.ErrorCode, e.Message),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
        }
        else
        {
            _logger.LogWarning("Handled exception: {ErrorCode} - {Message}", errorCode, message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}
