# Solana Integration

## Services

### SolanaService
Core blockchain interactions via Solnet SDK.

```csharp
// Transfer tokens
var signature = await _solanaService.TransferTokenAsync(
    privateKey, recipientAddress, amount, TokenType.USDC);

// Get balance
var balance = await _solanaService.GetTokenBalanceAsync(publicKey, TokenType.USDC);

// Check confirmation
var confirmed = await _solanaService.IsTransactionConfirmedAsync(signature);
```

**Features:** Retry with backoff (1s, 2s, 4s), input validation, balance pre-check, ATA auto-creation.

### CachedSolanaBalanceService
Redis-cached balance queries (30s TTL for tokens, 60s for SOL).

```csharp
var balance = await _balanceService.GetTokenBalanceAsync(publicKey, TokenType.USDC);
var all = await _balanceService.GetAllBalancesAsync(publicKey);
await _balanceService.InvalidateBalanceCacheAsync(publicKey); // After transfer
```

### TransactionMonitorService
Monitors pending transactions, updates status on confirmation.

```csharp
var result = await _monitorService.ProcessPendingTransactionsAsync(batchSize: 50);
// result.Confirmed, result.Failed, result.StillPending
```

## Background Jobs

| Job | Schedule | Purpose |
|-----|----------|---------|
| `ProcessPendingTransactions` | 30s | Check confirmations |
| `MarkStaleTransactions` | 5m | Fail old pending txs |
| `RefreshActiveWalletBalances` | 5m | Warm cache |
| `CheckWalletFeeBalances` | Daily | Alert low SOL |

## Configuration

```json
{
  "Solana": {
    "RpcUrl": "https://api.devnet.solana.com",
    "UseDevnet": true
  }
}
```

Set `UseDevnet: false` and mainnet RPC URL for production.

## Tokens

| Token | Decimals | Mainnet Mint |
|-------|----------|--------------|
| USDC | 6 | `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v` |
| USDT | 6 | `Es9vMFrzaCERmJfrF4H2FYD4KCoNkY11McCe8BenwNYB` |

## Error Handling

| Exception | Cause |
|-----------|-------|
| `ValidationException` | Invalid amount/address |
| `InsufficientBalanceException` | Not enough tokens |
| `SolanaTransactionException` | Chain/network error |
