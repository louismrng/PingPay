# Messaging Integrations (WhatsApp, Telegram, Signal)

This document describes the messaging platform work added to the project, what functionality it adds, configuration values, and how to run and test locally.

Summary
- Kept existing WhatsApp/Twilio integration.
- Added Telegram bot integration (webhook + sender).
- Added Signal integration (webhook + HTTP sender assuming a signal-rest or signal-cli HTTP API).
- Reused existing message parsing and business logic so commands behave the same across platforms.
- Added unit tests for the new controllers.

What was added (files)
- Configuration
  - `src/PingPay.Infrastructure/Configuration/TelegramOptions.cs`
  - `src/PingPay.Infrastructure/Configuration/SignalOptions.cs`

- Telegram
  - `src/PingPay.Infrastructure/Services/Telegram/TelegramSenderService.cs` (sender)
  - `src/PingPay.Infrastructure/Services/Telegram/TelegramBotService.cs` (bot logic wrapper)
  - `src/PingPay.Infrastructure/Services/Telegram/ITelegramSenderService.cs`
  - `src/PingPay.Infrastructure/Services/Telegram/ITelegramBotService.cs`
  - `src/PingPay.Api/Controllers/TelegramController.cs` (webhook)

- Signal
  - `src/PingPay.Infrastructure/Services/Signal/SignalSenderService.cs` (HTTP sender)
  - `src/PingPay.Infrastructure/Services/Signal/SignalBotService.cs` (bot logic wrapper)
  - `src/PingPay.Infrastructure/Services/Signal/ISignalSenderService.cs`
  - `src/PingPay.Infrastructure/Services/Signal/ISignalBotService.cs`
  - `src/PingPay.Api/Controllers/SignalController.cs` (webhook)

- Generic DTOs
  - `src/PingPay.Core/DTOs/Messaging/MessageDto.cs`

- Tests
  - `tests/PingPay.Tests/Unit/TelegramControllerTests.cs`
  - `tests/PingPay.Tests/Unit/SignalControllerTests.cs`

- Dependency injection & config updates
  - `src/PingPay.Infrastructure/DependencyInjection.cs` (registered new services)
  - `src/PingPay.Api/appsettings.json` (placeholders)
  - `src/PingPay.Api/appsettings.Development.json` (examples)
  - `src/PingPay.Api/appsettings.Production.json` (placeholders)
  - `src/PingPay.Infrastructure/PingPay.Infrastructure.csproj` (added `Telegram.Bot` package)

Behavior / Functionality
- Commands supported: the same commands as WhatsApp (`help`, `balance`, `send`, `history`, `status`, `register`).
- Parsing logic is reused via `MessageParserService` so behavior is consistent across platforms.
- Telegram: incoming webhook receives updates, passes message text to bot service, and replies using the Telegram Bot API.
- Signal: incoming webhook expects a small JSON payload `{ "Source": "+123..", "Message": "..." }`, passes it to the bot service, and replies using an HTTP Signal API (assumes a running signal-rest or signal-cli HTTP endpoint).

Configuration
- Telegram (appsettings)
  - `Telegram:BotToken` — bot token from @BotFather
  - `Telegram:WebhookUrl` — used for documentation; set webhook with Telegram to `https://<host>/api/telegram/webhook` or set `UsePolling` to true and implement polling separately
  - `Telegram:UsePolling` — default false

- Signal (appsettings)
  - `Signal:PhoneNumber` — Signal-registered phone number used by your Signal gateway
  - `Signal:ApiEndpoint` — URL for your signal-rest or signal-cli HTTP API (e.g. `http://localhost:8080`)

- Example development config (edit `appsettings.Development.json`):
  - `Telegram:BotToken` = `YOUR_TELEGRAM_BOT_TOKEN`
  - `Telegram:WebhookUrl` = `https://your-host/api/telegram/webhook`
  - `Signal:PhoneNumber` = `+1234567890`
  - `Signal:ApiEndpoint` = `http://localhost:8080`

Running locally
1. Update configuration in `src/PingPay.Api/appsettings.Development.json` with real credentials.
2. Start the API:
   - dotnet run --project src/PingPay.Api
3. Telegram webhook setup (server must be reachable by Telegram):
   - Use `setWebhook` with your bot token:
     - curl -F "url=https://<your-host>/api/telegram/webhook" https://api.telegram.org/bot<YOUR_BOT_TOKEN>/setWebhook
   - Or, run a tunnel (ngrok) and set the webhook to the tunnel URL.
4. Signal setup (example options):
   - Run a signal REST gateway (e.g. `signal-cli` HTTP bridge) and set `Signal:ApiEndpoint` to its URL.
   - Configure the gateway to POST incoming messages to `POST https://<your-host>/api/signal/webhook` with JSON { "Source": "+<number>", "Message": "<text>" }.

Testing
- Unit tests added for Telegram and Signal controllers. Run them with:
  - dotnet test tests/PingPay.Tests

Notes and caveats
- The Signal sender uses an HTTP API to send messages; you must deploy or run a compatible signal-rest or signal-cli HTTP bridge.
- Telegram uses the official `Telegram.Bot` NuGet package. The code attempts to be version-tolerant; if you update the package, the API surface may require small changes.
- No credentials or secrets are committed. Add production credentials via your configuration provider or environment variables.
- The implementation intentionally reuses the existing parsing and transfer logic; production readiness (rate limits, robust webhook verification, retry/backoff) should be validated before use.

If you want extra items added:
- Telegram polling mode implementation.
- More unit tests (sender services, parser, end-to-end controller tests with WebApplicationFactory).
- Example scripts to register Telegram webhook automatically.

