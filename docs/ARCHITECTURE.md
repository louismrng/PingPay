# Architecture

## Overview

PingPay is a custodial payment platform enabling USDC/USDT transfers on Solana via phone number identity.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Clients                                     │
│                        (Mobile App / Web)                               │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ HTTPS
┌───────────────────────────────▼─────────────────────────────────────────┐
│                           API Layer                                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │ AuthController│ │WalletController│ │PaymentController│ │HealthController│   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └─────────────┘    │
└─────────┼────────────────┼────────────────┼─────────────────────────────┘
          │                │                │
┌─────────▼────────────────▼────────────────▼─────────────────────────────┐
│                        Service Layer                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                   │
│  │  OtpService  │  │WalletService │  │PaymentService│                   │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                   │
└─────────┼────────────────┼────────────────┼─────────────────────────────┘
          │                │                │
┌─────────▼────────────────▼────────────────▼─────────────────────────────┐
│                     Infrastructure Layer                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │ SolanaService│  │WalletEncryption│ │ CacheService │  │ SmsService  │    │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘    │
└─────────┼────────────────┼────────────────┼────────────────┼────────────┘
          │                │                │                │
          ▼                ▼                ▼                ▼
    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
    │  Solana  │    │ KMS/Vault│    │  Redis   │    │  Twilio  │
    │  (RPC)   │    │          │    │          │    │          │
    └──────────┘    └──────────┘    └──────────┘    └──────────┘
```

## Project Structure

```
PingPay/
├── src/
│   ├── PingPay.Core/              # Domain layer (no dependencies)
│   │   ├── Entities/              # User, Wallet, Transaction, AuditLog
│   │   ├── Interfaces/            # Repository & service contracts
│   │   ├── DTOs/                  # Request/response models
│   │   ├── Enums/                 # TokenType, TransactionStatus
│   │   └── Exceptions/            # Domain exceptions
│   │
│   ├── PingPay.Infrastructure/    # Data & external services
│   │   ├── Data/
│   │   │   ├── PingPayDbContext.cs
│   │   │   ├── Repositories/      # EF Core implementations
│   │   │   └── Migrations/
│   │   ├── Services/
│   │   │   ├── Solana/            # Blockchain integration
│   │   │   ├── KeyManagement/     # Encryption services
│   │   │   └── Sms/               # Twilio integration
│   │   └── Configuration/         # Options classes
│   │
│   └── PingPay.Api/               # Web API
│       ├── Controllers/           # HTTP endpoints
│       ├── Services/              # Application services
│       ├── Jobs/                  # Hangfire background jobs
│       └── Middleware/            # Auth, error handling
│
└── tests/
    └── PingPay.Tests/
        ├── Unit/                  # Isolated tests with mocks
        └── Integration/           # Tests against real services
```

## Core Entities

```
┌─────────────┐       ┌─────────────┐       ┌─────────────┐
│    User     │       │   Wallet    │       │ Transaction │
├─────────────┤       ├─────────────┤       ├─────────────┤
│ Id          │──1:1──│ UserId      │       │ SenderId    │
│ PhoneNumber │       │ PublicKey   │       │ ReceiverId  │
│ IsActive    │       │ EncryptedKey│       │ Amount      │
│ DailyLimit  │       │ KeyVersion  │       │ TokenType   │
│ LastLoginAt │       │ CachedBalance│      │ Status      │
└─────────────┘       └─────────────┘       │ Signature   │
                                            └─────────────┘
```

## Data Flow

### Authentication
```
1. User → POST /auth/request-otp { phoneNumber }
2. API → Twilio (send SMS)
3. User → POST /auth/verify-otp { phoneNumber, code }
4. API → Create/get user, generate JWT
5. User ← { accessToken, refreshToken }
```

### Wallet Creation
```
1. User registers (first OTP verify)
2. SolanaService.GenerateKeypair() → (publicKey, privateKey)
3. WalletEncryptionService.EncryptPrivateKey() → encryptedBlob
   └── KMS wraps DEK → DEK encrypts privateKey
4. Save Wallet { publicKey, encryptedBlob, keyVersion }
```

### Payment
```
1. User → POST /payments/send { recipient, amount, token }
2. PaymentService validates limits, balance
3. WalletEncryptionService.DecryptPrivateKey()
4. SolanaService.TransferTokenAsync() → signature
   └── Retry on transient errors (blockhash, timeout)
5. Save Transaction { status: Pending, signature }
6. Enqueue confirmation job
7. User ← { transactionId, signature }

Background:
8. TransactionMonitorService polls chain
9. Update Transaction { status: Completed }
10. Invalidate balance cache
```

## Key Components

### SolanaService
Handles all Solana RPC interactions via Solnet SDK.

| Method | Purpose |
|--------|---------|
| `GenerateKeypair()` | Create Ed25519 keypair |
| `TransferTokenAsync()` | SPL token transfer with retry |
| `GetTokenBalanceAsync()` | Query ATA balance |
| `GetSolBalanceAsync()` | Query SOL for fees |
| `IsTransactionConfirmedAsync()` | Check tx status |

### WalletEncryptionService
Envelope encryption for private keys.

```
Encrypt: KMS.Wrap(DEK) + AES-GCM(privateKey, DEK) → blob
Decrypt: KMS.Unwrap(encDEK) → DEK → AES-GCM.Decrypt → privateKey
```

### CachedSolanaBalanceService
Redis-cached balance queries to reduce RPC calls.

| Data | TTL |
|------|-----|
| Token balance | 30s |
| SOL balance | 60s |

### TransactionMonitorService
Background polling for transaction confirmations.

```
Every 30s:
  1. Get pending transactions
  2. Query chain for each signature
  3. Update status (Completed/Failed)
  4. Invalidate balance cache
```

## Background Jobs (Hangfire)

| Job | Schedule | Purpose |
|-----|----------|---------|
| `ProcessPendingTransactions` | */30s | Confirm pending txs |
| `MarkStaleTransactions` | */5m | Fail old pending txs |
| `RefreshActiveWalletBalances` | */5m | Warm cache |
| `CheckWalletFeeBalances` | Daily | Alert low SOL |
| `ValidateWalletEncryptions` | Weekly | Verify integrity |

## Security Layers

```
┌────────────────────────────────────────┐
│           Transport (HTTPS)            │
├────────────────────────────────────────┤
│         Authentication (JWT)           │
├────────────────────────────────────────┤
│         Rate Limiting (Redis)          │
├────────────────────────────────────────┤
│      Input Validation (FluentVal)      │
├────────────────────────────────────────┤
│    Encryption at Rest (AES-256-GCM)    │
├────────────────────────────────────────┤
│       Key Management (KMS/Vault)       │
└────────────────────────────────────────┘
```

## Database Schema

```sql
-- Core tables
users (id, phone_number, is_active, daily_limit, created_at)
wallets (id, user_id, public_key, encrypted_private_key, key_version)
transactions (id, sender_id, receiver_id, amount, token_type, status, signature)
audit_logs (id, user_id, action, entity_type, entity_id, old_values, new_values)

-- Key indexes
CREATE INDEX idx_users_phone ON users(phone_number);
CREATE INDEX idx_transactions_status ON transactions(status) WHERE status = 'pending';
CREATE INDEX idx_wallets_public_key ON wallets(public_key);
```

## Configuration

| Section | Purpose |
|---------|---------|
| `Database` | PostgreSQL connection |
| `Redis` | Cache & rate limiting |
| `Solana` | RPC URL, devnet flag |
| `KeyManagement` | KMS provider config |
| `Twilio` | SMS credentials |
| `Jwt` | Token settings |
| `RateLimit` | Request limits |

## Error Handling

| Exception | HTTP | When |
|-----------|------|------|
| `ValidationException` | 400 | Invalid input |
| `UnauthorizedException` | 401 | Bad/missing token |
| `InsufficientBalanceException` | 400 | Not enough funds |
| `RateLimitException` | 429 | Too many requests |
| `SolanaTransactionException` | 500 | Chain error |

## Scaling Considerations

- **Stateless API**: Horizontal scaling behind load balancer
- **Redis**: Centralized cache/rate limits across instances
- **Hangfire**: Single leader for recurring jobs, workers scale
- **Database**: Read replicas for query load
- **RPC**: Dedicated Solana RPC provider for mainnet
