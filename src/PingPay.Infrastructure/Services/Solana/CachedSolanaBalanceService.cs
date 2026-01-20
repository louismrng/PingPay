using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingPay.Core.Enums;
using PingPay.Core.Interfaces;
using PingPay.Infrastructure.Configuration;

namespace PingPay.Infrastructure.Services.Solana;

/// <summary>
/// Provides cached access to Solana balance queries.
/// Token balances are cached briefly to reduce RPC calls while maintaining reasonable accuracy.
/// </summary>
public class CachedSolanaBalanceService
{
    private readonly ISolanaService _solanaService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedSolanaBalanceService> _logger;
    private readonly SolanaOptions _options;

    // Cache key prefixes
    private const string TokenBalanceCachePrefix = "balance:token:";
    private const string SolBalanceCachePrefix = "balance:sol:";

    // Default cache durations
    private static readonly TimeSpan TokenBalanceCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SolBalanceCacheDuration = TimeSpan.FromSeconds(60);

    public CachedSolanaBalanceService(
        ISolanaService solanaService,
        ICacheService cacheService,
        IOptions<SolanaOptions> options,
        ILogger<CachedSolanaBalanceService> logger)
    {
        _solanaService = solanaService;
        _cacheService = cacheService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets the token balance for a wallet with caching.
    /// </summary>
    /// <param name="publicKey">The wallet public key</param>
    /// <param name="tokenType">The token type (USDC/USDT)</param>
    /// <param name="forceRefresh">If true, bypasses cache and fetches from chain</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The token balance</returns>
    public async Task<decimal> GetTokenBalanceAsync(
        string publicKey,
        TokenType tokenType,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var cacheKey = GetTokenBalanceCacheKey(publicKey, tokenType);

        if (!forceRefresh)
        {
            var cached = await _cacheService.GetAsync<CachedBalance>(cacheKey, ct);
            if (cached != null)
            {
                _logger.LogDebug(
                    "Cache hit for {Token} balance of {PublicKey}",
                    tokenType, publicKey[..8] + "...");
                return cached.Balance;
            }
        }

        // Fetch from chain
        var balance = await _solanaService.GetTokenBalanceAsync(publicKey, tokenType, ct);

        // Cache the result
        await _cacheService.SetAsync(
            cacheKey,
            new CachedBalance { Balance = balance, FetchedAt = DateTime.UtcNow },
            TokenBalanceCacheDuration,
            ct);

        _logger.LogDebug(
            "Fetched and cached {Token} balance for {PublicKey}: {Balance}",
            tokenType, publicKey[..8] + "...", balance);

        return balance;
    }

    /// <summary>
    /// Gets the SOL balance for a wallet with caching.
    /// </summary>
    public async Task<decimal> GetSolBalanceAsync(
        string publicKey,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var cacheKey = GetSolBalanceCacheKey(publicKey);

        if (!forceRefresh)
        {
            var cached = await _cacheService.GetAsync<CachedBalance>(cacheKey, ct);
            if (cached != null)
            {
                _logger.LogDebug("Cache hit for SOL balance of {PublicKey}", publicKey[..8] + "...");
                return cached.Balance;
            }
        }

        var balance = await _solanaService.GetSolBalanceAsync(publicKey, ct);

        await _cacheService.SetAsync(
            cacheKey,
            new CachedBalance { Balance = balance, FetchedAt = DateTime.UtcNow },
            SolBalanceCacheDuration,
            ct);

        return balance;
    }

    /// <summary>
    /// Gets all token balances for a wallet (USDC and USDT).
    /// </summary>
    public async Task<WalletBalances> GetAllBalancesAsync(
        string publicKey,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        // Fetch all balances in parallel
        var usdcTask = GetTokenBalanceAsync(publicKey, TokenType.USDC, forceRefresh, ct);
        var usdtTask = GetTokenBalanceAsync(publicKey, TokenType.USDT, forceRefresh, ct);
        var solTask = GetSolBalanceAsync(publicKey, forceRefresh, ct);

        await Task.WhenAll(usdcTask, usdtTask, solTask);

        return new WalletBalances
        {
            PublicKey = publicKey,
            UsdcBalance = await usdcTask,
            UsdtBalance = await usdtTask,
            SolBalance = await solTask,
            FetchedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Invalidates the cached balance for a wallet.
    /// Call this after a transfer to ensure fresh data on next query.
    /// </summary>
    public async Task InvalidateBalanceCacheAsync(
        string publicKey,
        TokenType? tokenType = null,
        CancellationToken ct = default)
    {
        if (tokenType.HasValue)
        {
            await _cacheService.RemoveAsync(GetTokenBalanceCacheKey(publicKey, tokenType.Value), ct);
        }
        else
        {
            // Invalidate all token balances
            await _cacheService.RemoveAsync(GetTokenBalanceCacheKey(publicKey, TokenType.USDC), ct);
            await _cacheService.RemoveAsync(GetTokenBalanceCacheKey(publicKey, TokenType.USDT), ct);
        }

        await _cacheService.RemoveAsync(GetSolBalanceCacheKey(publicKey), ct);

        _logger.LogDebug("Invalidated balance cache for {PublicKey}", publicKey[..8] + "...");
    }

    /// <summary>
    /// Checks if a wallet has sufficient balance for a transfer.
    /// Uses cached balance if available.
    /// </summary>
    public async Task<(bool HasSufficientBalance, decimal CurrentBalance)> CheckSufficientBalanceAsync(
        string publicKey,
        decimal requiredAmount,
        TokenType tokenType,
        CancellationToken ct = default)
    {
        var balance = await GetTokenBalanceAsync(publicKey, tokenType, forceRefresh: false, ct);
        return (balance >= requiredAmount, balance);
    }

    /// <summary>
    /// Checks if a wallet has sufficient SOL for transaction fees.
    /// Requires approximately 0.01 SOL for most operations.
    /// </summary>
    public async Task<(bool HasSufficientSol, decimal CurrentSol)> CheckSufficientSolForFeesAsync(
        string publicKey,
        decimal minimumSol = 0.01m,
        CancellationToken ct = default)
    {
        var balance = await GetSolBalanceAsync(publicKey, forceRefresh: false, ct);
        return (balance >= minimumSol, balance);
    }

    private static string GetTokenBalanceCacheKey(string publicKey, TokenType tokenType)
        => $"{TokenBalanceCachePrefix}{tokenType}:{publicKey}";

    private static string GetSolBalanceCacheKey(string publicKey)
        => $"{SolBalanceCachePrefix}{publicKey}";

    private class CachedBalance
    {
        public decimal Balance { get; set; }
        public DateTime FetchedAt { get; set; }
    }
}

/// <summary>
/// Represents all balances for a wallet.
/// </summary>
public class WalletBalances
{
    public string PublicKey { get; set; } = string.Empty;
    public decimal UsdcBalance { get; set; }
    public decimal UsdtBalance { get; set; }
    public decimal SolBalance { get; set; }
    public DateTime FetchedAt { get; set; }

    public decimal TotalStablecoinBalance => UsdcBalance + UsdtBalance;
}
