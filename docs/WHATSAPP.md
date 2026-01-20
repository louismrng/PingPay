# WhatsApp Integration

PingPay supports WhatsApp as a user interface via Twilio.

## Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   WhatsApp   │────▶│    Twilio    │────▶│   PingPay    │
│     User     │◀────│   Webhook    │◀────│     API      │
└──────────────┘     └──────────────┘     └──────────────┘
                            │
                     POST /api/whatsapp/webhook
```

## Commands

| Command | Description | Example |
|---------|-------------|---------|
| `help` | Show commands | `help` |
| `balance` | Check balance | `balance` or `bal` |
| `send` | Send payment | `send $10 to +14155551234` |
| `history` | Recent transactions | `history` or `h` |
| `status` | Check transaction | `status <tx-id>` |
| `register` | Create account | `register` |

### Send Formats
```
send $10 to +14155551234
send 25 +14155551234
send $100 +14155551234 usdt
send 50 to +14155551234 usdc
```

## Setup

### 1. Twilio Configuration

1. Get a Twilio account at https://twilio.com
2. Enable WhatsApp Sandbox or get approved WhatsApp number
3. Add credentials to config:

```json
{
  "Twilio": {
    "AccountSid": "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "AuthToken": "your-auth-token",
    "FromNumber": "+14155238886"
  }
}
```

### 2. Webhook Setup

Configure Twilio to send webhooks to your API:

**Webhook URL:** `https://your-domain.com/api/whatsapp/webhook`
**Method:** POST
**Content-Type:** application/x-www-form-urlencoded

For local development, use ngrok:
```bash
ngrok http 5000
# Then set webhook to: https://abc123.ngrok.io/api/whatsapp/webhook
```

### 3. Twilio Sandbox (Development)

1. Go to Twilio Console → Messaging → WhatsApp Sandbox
2. Send "join <sandbox-code>" to +1 415 523 8886
3. Set webhook URL in sandbox settings

## Services

### MessageParserService
Parses user messages into commands.

```csharp
var command = _parser.Parse("send $10 to +14155551234");
// command.Type = CommandType.Send
// command.Amount = 10
// command.RecipientPhone = "+14155551234"
// command.Token = "USDC"
```

### WhatsAppBotService
Handles commands and returns responses.

```csharp
var response = await _botService.ProcessMessageAsync(
    phoneNumber: "+14155551234",
    message: "balance");
// response.Message = "💰 Your Balance..."
```

### WhatsAppSenderService
Sends outbound messages via Twilio.

```csharp
await _senderService.SendMessageAsync("+14155551234", "Hello!");
```

## User Flow

### Registration
```
User: "register"
Bot: "✅ Welcome to PingPay! Your wallet: 7xK9..."
```

### Check Balance
```
User: "balance"
Bot: "💰 Your Balance
     USDC: $125.00
     USDT: $0.00
     SOL: 0.0500"
```

### Send Payment
```
User: "send $25 to +14155559999"
Bot: "✅ Payment Sent!
     Amount: $25.00 USDC
     To: +14155559999
     Transaction ID: abc-123..."
```

## Security

- Phone number = identity (verified by WhatsApp)
- No passwords stored
- Same encryption for wallets
- Rate limiting on commands

## Testing

```bash
# Run parser tests
dotnet test --filter "MessageParserService"

# Manual test via curl (simulating Twilio)
curl -X POST http://localhost:5000/api/whatsapp/webhook \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "From=whatsapp:+14155551234&Body=balance"
```

## Webhook Payload

Twilio sends form data:

| Field | Description |
|-------|-------------|
| `From` | `whatsapp:+1234567890` |
| `To` | Your Twilio number |
| `Body` | Message text |
| `MessageSid` | Unique message ID |
| `ProfileName` | User's WhatsApp name |

## Response Format

Returns TwiML:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Response>
    <Message>Your response here</Message>
</Response>
```
