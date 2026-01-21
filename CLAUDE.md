# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Start dependencies (PostgreSQL, Redis)
docker-compose up -d

# Run database migrations
dotnet ef database update -p src/PingPay.Infrastructure -s src/PingPay.Api

# Run the API
dotnet run --project src/PingPay.Api

# Run with hot reload
dotnet watch run --project src/PingPay.Api

# Build all projects
dotnet build

# Run all tests
dotnet test

# Run unit tests only
dotnet test --filter "Category!=Integration"

# Run integration tests (requires Solana devnet)
dotnet test --filter "Category=Integration"

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

## Architecture

PingPay is a custodial Solana payment platform for USDC/USDT transfers using phone number authentication.

### Project Structure

- **PingPay.Core** - Domain layer with no external dependencies. Contains entities (User, Wallet, Transaction), interfaces, DTOs, enums, and exceptions.
- **PingPay.Infrastructure** - Data access and external service integrations. Contains EF Core repositories, Solana/Twilio services, and encryption services.
- **PingPay.Api** - Web API layer with controllers, application services, Hangfire background jobs, and middleware.
- **PingPay.Tests** - Unit and integration tests.

### Key Service Flows

**Authentication**: Phone number + OTP via Twilio → JWT token generation

**Wallet Creation**: On first login, generates Ed25519 keypair → encrypts private key with envelope encryption (AES-256-GCM + KMS) → stores in database

**Payment Flow**:
1. Validate limits and balance
2. Decrypt sender's private key
3. Execute SPL token transfer via Solana RPC (with retry)
4. Save transaction as Pending
5. Background job monitors chain for confirmation

### Key Components

- `SolanaService` - Solana RPC interactions via Solnet SDK (keypair generation, token transfers, balance queries)
- `WalletEncryptionService` - Envelope encryption for private keys using KMS-wrapped DEKs
- `CachedSolanaBalanceService` - Redis-cached balance queries (30s TTL for tokens, 60s for SOL)
- `TransactionMonitorService` - Background polling for transaction confirmations

### Background Jobs (Hangfire)

- `ProcessPendingTransactions` - Every 30s, confirms pending transactions
- `MarkStaleTransactions` - Every 5m, fails old pending transactions
- `RefreshActiveWalletBalances` - Every 5m, warms balance cache
- `CheckWalletFeeBalances` - Daily, alerts on low SOL
- `ValidateWalletEncryptions` - Weekly, verifies encryption integrity

## Configuration

Key configuration sections in appsettings:
- `Database` - PostgreSQL connection
- `Redis` - Cache and rate limiting
- `Solana` - RPC URL, devnet flag
- `KeyManagement` - KMS provider (Local/AWS/Azure)
- `Twilio` - SMS and WhatsApp credentials
- `Jwt` - Token settings

Environment variables override config using double underscore notation: `Database__ConnectionString`

## Code Style

- .NET 8 with C# file-scoped namespaces
- 4-space indentation
- `var` preferred when type is apparent
- Interfaces prefixed with `I`
- Allman brace style (opening brace on new line)
