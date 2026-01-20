using System.Security.Claims;

namespace PingPay.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token");
        }

        return userId;
    }

    public static string GetPhoneNumber(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.MobilePhone)?.Value
            ?? throw new UnauthorizedAccessException("Phone number not found in token");
    }
}
