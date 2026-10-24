# LimitlessTrading

A cross-platform (Windows / macOS / Linux) desktop trading front-end for **Interactive Brokers**:
TWS, but better. Built with **Avalonia 12** on **.NET 10**, talking to TWS / IB Gateway through
[`TwsApi`](https://github.com/UtopleMan/tws-api-dotnet).

## What it does (read-only build)

- Connection status in the status bar, heartbeat, automatic reconnect, and reconnect on next start if you were connected when you closed the app
- Watchlist with contract search and streaming quotes (live or delayed)
- Candlestick chart (1m–1W) with real-time 5 s bar updates and a local bar cache
- Account panel: net liquidation, margin, buying power, daily / unrealized / realized P&L
- Positions with per-position P&L, open orders, today's executions
- Option chain around the money with live quotes
- Message log, settings dialog, saved layout, keyboard shortcuts
  ([docs/ui-patterns.md](docs/ui-patterns.md#keyboard-shortcuts))

There is no order entry: nothing in the app can place, modify or cancel an order.

## Run

Requirements: .NET SDK 10.0.4xx, [IBC](https://github.com/IbcAlpha/IBC) and the offline/stable
IB Gateway installer. The app starts IB Gateway itself when you log in — TWS is not supported
(see [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md#starting-ib-gateway-from-the-app-ibc)).

```bash
dotnet run --project src/LT.App      # the app
dotnet build LimitlessTrading.slnx   # everything
dotnet test                          # unit tests; never connect to IB
csharpier format .                   # formatting (CSharpier 1.3, tabs, width 100)
```

The log-in dialog opens on start. After login the app connects to `127.0.0.1:4002` (paper) or
`4001` (live), client id `1`. Username, mode and install folders are saved in `settings.json` in the
app-data folder; the password never is.

## Docs

| Doc | What |
|---|---|
| [docs/vision.md](docs/vision.md) | Why, principles, roadmap |
| [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md) | The `TwsApi` library, TWS setup, IB limits, gaps |
| [docs/architecture.md](docs/architecture.md) | Layers (mapped from the Termodom ecosystem), DI, rules |
| [docs/ui-patterns.md](docs/ui-patterns.md) | Avalonia: pages/widgets, MVVM, UI threading |
