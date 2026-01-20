namespace PingPay.Core.Constants;

public static class SolanaConstants
{
    /// <summary>
    /// USDC token mint address on Solana mainnet.
    /// </summary>
    public const string UsdcMintAddress = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    /// <summary>
    /// USDt token mint address on Solana mainnet.
    /// </summary>
    public const string UsdtMintAddress = "Es9vMFrzaCERmJfrF4H2FUD9qjSJdaQD4jXi9VVJr6P";

    /// <summary>
    /// USDC token mint address on Solana devnet (for testing).
    /// </summary>
    public const string UsdcMintAddressDevnet = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";

    /// <summary>
    /// Token decimals for USDC/USDt.
    /// </summary>
    public const int TokenDecimals = 6;

    /// <summary>
    /// Lamports per SOL.
    /// </summary>
    public const long LamportsPerSol = 1_000_000_000;
}
