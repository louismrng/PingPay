namespace PingPay.Core.DTOs.Wallet;

public class WalletBalanceDto
{
    public string PublicKey { get; set; } = string.Empty;
    public decimal UsdcBalance { get; set; }
    public decimal UsdtBalance { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
