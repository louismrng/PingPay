# PingPay

Custodial Solana payment platform for USDC/USDT transfers with phone-number authentication.

## Tech Stack

- **.NET 8** + ASP.NET Core Web API
- **Solnet SDK** for Solana blockchain
- **PostgreSQL** + Entity Framework Core
- **Redis** for caching & rate limiting
- **Hangfire** for background jobs
- **Twilio** for SMS/OTP

## Project Structure

```
src/
├── PingPay.Core/           # Domain: entities, interfaces, DTOs, exceptions
├── PingPay.Infrastructure/ # Data access, external services, encryption
└── PingPay.Api/            # Web API, controllers, background jobs
tests/
└── PingPay.Tests/          # Unit & integration tests
```

## Quick Start

```bash
# Start dependencies
docker-compose up -d

# Run migrations
dotnet ef database update -p src/PingPay.Infrastructure

# Run API
dotnet run --project src/PingPay.Api
```

## Configuration

```json
{
  "Database": { "ConnectionString": "Host=localhost;Database=pingpay;..." },
  "Redis": { "ConnectionString": "localhost:6379" },
  "Solana": { "RpcUrl": "https://api.devnet.solana.com", "UseDevnet": true },
  "Twilio": { "AccountSid": "...", "AuthToken": "...", "FromNumber": "..." },
  "KeyManagement": { "Provider": "Local", "LocalDevelopmentKey": "<base64-key>" }
}
```

## Key Features

### Authentication
- Phone number + OTP via Twilio
- JWT tokens with refresh
- Rate limiting on OTP requests

### Wallet Management
- Auto-generated Solana keypairs
- Envelope encryption (AES-256-GCM + KMS)
- Key rotation support

### Transfers
- USDC/USDT SPL token transfers
- Retry with exponential backoff
- Cached balance queries (Redis)
- Background transaction monitoring

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/auth/request-otp` | Request OTP |
| `POST /api/auth/verify-otp` | Verify & get JWT |
| `GET /api/wallet` | Get wallet info |
| `GET /api/wallet/balance` | Get balances |
| `POST /api/payments/send` | Send payment |
| `GET /api/payments/{id}` | Get transaction |
| `GET /api/payments/history` | Transaction history |

## Background Jobs

| Job | Schedule |
|-----|----------|
| Process pending transactions | Every 30s |
| Mark stale transactions | Every 5m |
| Refresh active balances | Every 5m |
| Check SOL for fees | Daily |
| Validate encryptions | Weekly |

## Security

- Private keys encrypted with envelope encryption
- Supports AWS KMS, Azure Key Vault, or local key
- See [docs/SECURITY.md](docs/SECURITY.md)

## Testing

```bash
# Unit tests
dotnet test --filter "Category!=Integration"

# Integration tests (requires devnet)
dotnet test --filter "Category=Integration"
```

## Docs

- [Architecture](docs/ARCHITECTURE.md)
- [Running the Backend](docs/RUNNING.md)
- [Security & Encryption](docs/SECURITY.md)
- [Solana Integration](docs/SOLANA_INTEGRATION.md)
