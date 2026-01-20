# Running the Backend

## Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- PostgreSQL 15+ (or use Docker)
- Redis 7+ (or use Docker)

## Quick Start (Docker)

```bash
# Start all dependencies
docker-compose up -d

# Run the API
dotnet run --project src/PingPay.Api
```

API available at `https://localhost:5001` (or `http://localhost:5000`).

## Manual Setup

### 1. Database

```bash
# Start PostgreSQL
docker run -d --name pingpay-db \
  -e POSTGRES_USER=pingpay \
  -e POSTGRES_PASSWORD=pingpay \
  -e POSTGRES_DB=pingpay \
  -p 5432:5432 \
  postgres:15

# Run migrations
dotnet ef database update -p src/PingPay.Infrastructure -s src/PingPay.Api
```

### 2. Redis

```bash
docker run -d --name pingpay-redis -p 6379:6379 redis:7-alpine
```

### 3. Configuration

Create `src/PingPay.Api/appsettings.Development.json`:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Database=pingpay;Username=pingpay;Password=pingpay"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "pingpay:",
    "DefaultExpiryMinutes": 60
  },
  "Solana": {
    "RpcUrl": "https://api.devnet.solana.com",
    "UseDevnet": true
  },
  "KeyManagement": {
    "Provider": "Local",
    "LocalDevelopmentKey": "YOUR_BASE64_32_BYTE_KEY"
  },
  "Jwt": {
    "Secret": "your-256-bit-secret-key-for-jwt-tokens",
    "Issuer": "PingPay",
    "Audience": "PingPay",
    "ExpiryMinutes": 60
  },
  "Twilio": {
    "AccountSid": "your-account-sid",
    "AuthToken": "your-auth-token",
    "FromNumber": "+1234567890"
  }
}
```

Generate encryption key:
```bash
openssl rand -base64 32
```

### 4. Run

```bash
# Development mode
dotnet run --project src/PingPay.Api

# Or with watch
dotnet watch run --project src/PingPay.Api
```

## Environment Variables

Override config via environment variables:

```bash
export Database__ConnectionString="Host=..."
export Redis__ConnectionString="localhost:6379"
export Solana__RpcUrl="https://api.devnet.solana.com"
export KeyManagement__Provider="Local"
export KeyManagement__LocalDevelopmentKey="..."
```

## Endpoints

| URL | Description |
|-----|-------------|
| `http://localhost:5000` | API (HTTP) |
| `https://localhost:5001` | API (HTTPS) |
| `http://localhost:5000/swagger` | Swagger UI |
| `http://localhost:5000/hangfire` | Hangfire Dashboard |
| `http://localhost:5000/health` | Health Check |

## Docker Compose (Full Stack)

```bash
# Start everything (DB, Redis, API)
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# View logs
docker-compose logs -f api

# Stop
docker-compose down
```

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter "Category!=Integration"

# Integration tests (needs devnet)
dotnet test --filter "Category=Integration"
```

## Troubleshooting

**Database connection failed:**
```bash
# Check PostgreSQL is running
docker ps | grep postgres
# Check connection
psql -h localhost -U pingpay -d pingpay
```

**Redis connection failed:**
```bash
# Check Redis is running
docker ps | grep redis
# Test connection
redis-cli ping
```

**Migrations not applied:**
```bash
dotnet ef database update -p src/PingPay.Infrastructure -s src/PingPay.Api
```

**Port already in use:**
```bash
# Find process
lsof -i :5000
# Kill it
kill -9 <PID>
```
