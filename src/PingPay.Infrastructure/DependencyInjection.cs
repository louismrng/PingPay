using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;
using PingPay.Infrastructure.Data;
using PingPay.Infrastructure.Data.Repositories;
using PingPay.Infrastructure.Services;
using PingPay.Infrastructure.Services.KeyManagement;
using PingPay.Infrastructure.Services.Sms;
using PingPay.Infrastructure.Services.Solana;
using StackExchange.Redis;

namespace PingPay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<SolanaOptions>(configuration.GetSection(SolanaOptions.SectionName));
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
        services.Configure<KeyManagementOptions>(configuration.GetSection(KeyManagementOptions.SectionName));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        // Database
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
        services.AddDbContext<PingPayDbContext>(options =>
        {
            options.UseNpgsql(dbOptions?.ConnectionString ?? "Host=localhost;Database=pingpay;",
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(dbOptions?.MaxRetryCount ?? 3);
                    npgsql.CommandTimeout(dbOptions?.CommandTimeout ?? 30);
                });
        });

        // Redis
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisOptions?.ConnectionString ?? "localhost:6379"));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Services
        services.AddScoped<ISolanaService, SolanaService>();
        services.AddScoped<ISmsService, TwilioSmsService>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IRateLimitService, RedisRateLimitService>();
        services.AddScoped<IOtpService, OtpService>();

        // Key Management - register based on configuration
        var kmOptions = configuration.GetSection(KeyManagementOptions.SectionName).Get<KeyManagementOptions>();
        switch (kmOptions?.Provider?.ToLowerInvariant())
        {
            case "azurekeyvault":
                services.AddScoped<IKeyManagementService, AzureKeyVaultService>();
                break;
            case "awskms":
                // TODO: Implement AWS KMS service
                throw new NotImplementedException("AWS KMS support not yet implemented");
            default:
                services.AddScoped<IKeyManagementService, LocalKeyManagementService>();
                break;
        }

        return services;
    }
}
