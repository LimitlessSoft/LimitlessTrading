# LimitlessTrading

A cross-platform (Windows / macOS / Linux) desktop trading front-end for **Interactive Brokers**:
TWS, but better. Built with **Avalonia 12** on **.NET 10**, talking to TWS / IB Gateway through
[`TwsApi`](https://github.com/UtopleMan/tws-api-dotnet).

## Run

Requirements: .NET SDK 10.0.4xx, and TWS or IB Gateway running with the API enabled
(see [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md#tws--gateway-side-setup)).

```bash
dotnet run --project src/LT.App      # the app
dotnet build LimitlessTrading.slnx   # everything
dotnet test                          # unit tests; never connect to IB
csharpier format .                   # formatting (CSharpier 1.3, tabs, width 100)
```

Default endpoint is `127.0.0.1:7497` (TWS paper), client id `1`. The last endpoint you connected
to is saved in `settings.json` in the app-data folder.

## Docs

| Doc | What |
|---|---|
| [docs/vision.md](docs/vision.md) | Why, principles, roadmap |
| [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md) | The `TwsApi` library, TWS setup, IB limits, gaps |
| [docs/architecture.md](docs/architecture.md) | Layers (mapped from the Termodom ecosystem), DI, rules |
| [docs/ui-patterns.md](docs/ui-patterns.md) | Avalonia: pages/widgets, MVVM, UI threading |
