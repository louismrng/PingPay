using PingPay.Api.Services;
using PingPay.Core.Interfaces;

namespace PingPay.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<JwtService>();

        services.AddHttpContextAccessor();

        return services;
    }
}
