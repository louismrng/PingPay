using PingPay.Core.Constants;
using PingPay.Core.DTOs.Auth;
using PingPay.Core.Entities;
using PingPay.Core.Exceptions;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace PingPay.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;
    private readonly ISmsService _smsService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IWalletService _walletService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly JwtService _jwtService;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IOtpService otpService,
        ISmsService smsService,
        IRateLimitService rateLimitService,
        IWalletService walletService,
        IAuditLogRepository auditLogRepository,
        JwtService jwtService,
        IOptions<RateLimitOptions> rateLimitOptions,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _smsService = smsService;
        _rateLimitService = rateLimitService;
        _walletService = walletService;
        _auditLogRepository = auditLogRepository;
        _jwtService = jwtService;
        _rateLimitOptions = rateLimitOptions.Value;
        _logger = logger;
    }

    public async Task<bool> RequestOtpAsync(RequestOtpDto request, CancellationToken ct = default)
    {
        var rateLimitKey = CacheKeys.RateLimit("otp", request.PhoneNumber);

        var isAllowed = await _rateLimitService.IsAllowedAsync(
            rateLimitKey,
            _rateLimitOptions.OtpRequestsPerHour,
            TimeSpan.FromHours(1),
            ct);

        if (!isAllowed)
        {
            throw new RateLimitedException("OTP requests");
        }

        var code = _otpService.GenerateCode();
        var hashedCode = OtpService.HashCode(code);

        await _otpService.StoreOtpAsync(request.PhoneNumber, hashedCode, ct);
        await _smsService.SendOtpAsync(request.PhoneNumber, code, ct);

        _logger.LogInformation("OTP sent to phone ending in {PhoneSuffix}", request.PhoneNumber[^4..]);

        return true;
    }

    public async Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto request, CancellationToken ct = default)
    {
        var rateLimitKey = CacheKeys.RateLimit("login", request.PhoneNumber);

        var isAllowed = await _rateLimitService.IsAllowedAsync(
            rateLimitKey,
            _rateLimitOptions.LoginAttemptsPerHour,
            TimeSpan.FromHours(1),
            ct);

        if (!isAllowed)
        {
            throw new RateLimitedException("Login attempts");
        }

        var isValid = await _otpService.ValidateOtpAsync(request.PhoneNumber, request.Code, ct);

        if (!isValid)
        {
            throw new InvalidOtpException();
        }

        await _otpService.InvalidateOtpAsync(request.PhoneNumber, ct);
        await _rateLimitService.ResetAsync(rateLimitKey, ct);

        var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber, ct);
        var isNewUser = user == null;

        if (isNewUser)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = request.PhoneNumber,
                IsVerified = true,
                DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1)
            };

            user = await _userRepository.CreateAsync(user, ct);

            // Create wallet for new user
            await _walletService.CreateWalletForUserAsync(user.Id, ct);

            _logger.LogInformation("New user created: {UserId}", user.Id);
        }
        else if (!user!.IsActive)
        {
            throw new AccountFrozenException();
        }

        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Action = isNewUser ? "USER_REGISTERED" : "USER_LOGIN",
            EntityType = "User",
            EntityId = user.Id.ToString()
        }, ct);

        var (token, expiresAt) = _jwtService.GenerateToken(user.Id, user.PhoneNumber);

        return new AuthResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            IsNewUser = isNewUser
        };
    }
}
