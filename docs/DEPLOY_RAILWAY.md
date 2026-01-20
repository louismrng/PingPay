# Deploy to Railway

## Prerequisites

- Railway account (https://railway.app)
- Railway CLI installed

## Setup

### 1. Install CLI & Login

```bash
npm install -g @railway/cli
railway login
```

### 2. Create Project

```bash
railway init
# Select "Empty Project"
```

### 3. Add PostgreSQL

```bash
railway add
# Select "PostgreSQL"
```

Railway auto-sets `DATABASE_URL`. Map it in your app:

```bash
railway variables set Database__ConnectionString='${{Postgres.DATABASE_URL}}'
```

### 4. Add Redis

```bash
railway add
# Select "Redis"
```

```bash
railway variables set Redis__ConnectionString='${{Redis.REDIS_URL}}'
```

### 5. Set Environment Variables

```bash
# Generate secrets
railway variables set KeyManagement__Provider=Local
railway variables set KeyManagement__LocalDevelopmentKey=$(openssl rand -base64 32)
railway variables set Jwt__Secret=$(openssl rand -base64 32)

# Solana (use mainnet RPC for production)
railway variables set Solana__RpcUrl=https://api.devnet.solana.com
railway variables set Solana__UseDevnet=true

# Twilio (get from twilio.com/console)
railway variables set Twilio__AccountSid=ACxxxxxxxxx
railway variables set Twilio__AuthToken=your-token
railway variables set Twilio__FromNumber=+14155238886
```

### 6. Deploy

```bash
railway up
```

Or connect GitHub for auto-deploy:
```bash
railway link
# Then push to GitHub - Railway auto-deploys
```

### 7. Get URL

```bash
railway domain
# Returns: pingpay-production.up.railway.app
```

## Configure Twilio Webhook

Set your WhatsApp webhook URL in Twilio Console:

```
https://your-app.up.railway.app/api/whatsapp/webhook
```

## Production Checklist

| Setting | Value |
|---------|-------|
| `KeyManagement__Provider` | `AwsKms` or `AzureKeyVault` |
| `Solana__UseDevnet` | `false` |
| `Solana__RpcUrl` | Helius/QuickNode mainnet URL |

## Environment Variables Reference

| Variable | Required | Description |
|----------|----------|-------------|
| `Database__ConnectionString` | Yes | PostgreSQL URL |
| `Redis__ConnectionString` | Yes | Redis URL |
| `KeyManagement__Provider` | Yes | `Local`, `AwsKms`, `AzureKeyVault` |
| `KeyManagement__LocalDevelopmentKey` | If Local | Base64 32-byte key |
| `Solana__RpcUrl` | Yes | Solana RPC endpoint |
| `Solana__UseDevnet` | Yes | `true` or `false` |
| `Twilio__AccountSid` | Yes | Twilio account SID |
| `Twilio__AuthToken` | Yes | Twilio auth token |
| `Twilio__FromNumber` | Yes | WhatsApp-enabled number |
| `Jwt__Secret` | Yes | JWT signing key |

## Monitoring

View logs:
```bash
railway logs
```

Open dashboard:
```bash
railway open
```

## Costs

Railway pricing (as of 2024):
- Hobby: $5/month (includes $5 credit)
- Pro: $20/month + usage

Estimated for PingPay:
- API: ~$5-10/month
- PostgreSQL: ~$5/month
- Redis: ~$5/month

## Troubleshooting

**Build fails:**
```bash
railway logs --build
```

**App crashes:**
```bash
railway logs
# Check for missing env vars
```

**Database connection fails:**
- Ensure `DATABASE_URL` is mapped correctly
- Check PostgreSQL service is running in Railway dashboard
