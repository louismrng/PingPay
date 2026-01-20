namespace PingPay.Infrastructure.Configuration;

public class SolanaOptions
{
    public const string SectionName = "Solana";

    /// <summary>
    /// RPC endpoint URL (e.g., Helius, QuickNode).
    /// </summary>
    public string RpcUrl { get; set; } = "https://api.mainnet-beta.solana.com";

    /// <summary>
    /// WebSocket endpoint for transaction subscriptions.
    /// </summary>
    public string? WsUrl { get; set; }

    /// <summary>
    /// Use devnet for testing.
    /// </summary>
    public bool UseDevnet { get; set; }

    /// <summary>
    /// Commitment level for transactions.
    /// </summary>
    public string Commitment { get; set; } = "confirmed";

    /// <summary>
    /// Maximum retries for failed transactions.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
