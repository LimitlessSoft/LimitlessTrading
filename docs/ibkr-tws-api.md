# IBKR TWS API — `TwsApi` (UtopleMan/tws-api-dotnet)

The app talks to Interactive Brokers through **[UtopleMan/tws-api-dotnet](https://github.com/UtopleMan/tws-api-dotnet)**,
published on NuGet as **`TwsApi`**. This file is what we know about it and about the IB socket API
underneath it. Read it before touching `LT.Ibkr`.

Researched on 2026-10-23 against `TwsApi` **0.5.0** (repo head `b23ddf4`, 2026-08-13).

## What it is

A **.NET 10** async/await facade over IBKR's official C# TWS client (`CSharpAPI`, vendored as a git
submodule of `InteractiveBrokers/tws-api`). It hides the legacy `EWrapper` callbacks, `EReader`
thread and request ids behind:

- `Task<T>` for one-shot requests (contract details, historical bars, positions snapshot, …)
- `IAsyncEnumerable<T>` (backed by `System.Threading.Channels`) for streams (ticks, real-time bars,
  positions, P/L, order status). **Breaking out of `await foreach` / cancelling the token cancels
  the IB subscription.**

| Fact | Value |
|---|---|
| Package | `TwsApi` (NuGet), MIT |
| Target | `net10.0` only — so every project referencing it must be `net10.0`+ |
| Bundles | `CSharpAPI.dll` (IB's client) + `RestApi.dll` (Client Portal Web API client) |
| Dependencies | `Google.Protobuf`, `Microsoft.Extensions.Options/DependencyInjection.Abstractions/Http` |
| Maturity | `0.x` — API can break between minor versions; pin the exact version |

There is also a second, independent client in the same package — `RestApi.IRestClient` for the
**Client Portal Web API** (HTTPS gateway or OAuth 1.0a). It shares no session with the socket
client. We use the **socket client** (`ITwsClient`) for everything real-time and trading; the REST
client is an option later for portal-only features (PortfolioAnalyst, FYI, alerts).

## Connecting

```csharp
services.AddTwsApi(o => { o.Host = "127.0.0.1"; o.Port = 4002; o.ClientId = 1; });

// inject ITwsClientFactory
await using ITwsClient tws = await factory.ConnectAsync(new TwsConnectionOptions
{
    Host = "127.0.0.1", Port = 4002, ClientId = 1, ConnectTimeout = TimeSpan.FromSeconds(15),
}, ct);
```

`ConnectAsync` completes when TWS sends `nextValidId`. Each `ITwsClient` is a fully independent
connection (own socket, reader thread, id sequence). `DisposeAsync` disconnects.

### Ports

| Port | Endpoint |
|---|---|
| 4001 | IB Gateway — live |
| 4002 | IB Gateway — paper |

The socket API is a local bridge to a running Gateway, not a direct connection to IBKR. **The only
supported setup is the IB Gateway the app starts itself** (next section) — there is no manual
host/port connect and TWS (7496/7497) is not supported. The app *replaces* the TWS UI.

The API settings TWS keeps under *Global Configuration → API* (socket port, read-only API) are
set through IBC's `config.ini` instead, so the user never opens Gateway's own UI.

Gateway **restarts daily** (configurable time, auto-logoff) and require a re-login weekly.
The app must treat disconnects as normal, not exceptional.

### Starting IB Gateway from the app (IBC)

**Log in…** (menu, status bar, or the dialog shown on start) starts IB Gateway for the user with the username / password they
type, through **[IBC](https://github.com/IbcAlpha/IBC)** (GPL-3.0, run as a separate process — we
don't link it). Gateway has no official CLI or API for credentials; IBC drives its login dialog.

Flow (`GatewayManager` → `IIbcGatewayProcess`, implemented by `LT.Ibkr/Ibc/IbcGatewayProcess`):

1. Validate, check `<IBC>/scripts/ibcstart.sh` (`StartIBC.bat` on Windows) exists and the API port
   (4001 live / 4002 paper) is **not** already in use.
2. Write IBC's `config.ini` to `<app-data>/ibc-config.ini`, owner-only (0600 on Unix). It holds
   `IbLoginId`/`IbPassword` — credentials never go on the command line (visible in the process list).
3. Run `ibcstart.sh <ver> --gateway --tws-path=… --ibc-path=… --ibc-ini=… --mode=… --on2fatimeout=exit`
   (optionally under `xvfb-run -a` on Linux to hide the window).
4. Poll the API port every second for up to 5 minutes. IBC output is parsed best-effort to show
   "approve in IB Key" (2FA) and to fail fast on a rejected login.
5. Delete the config file, mark Running, connect `IConnectionManager` to `127.0.0.1:<port>`.
6. Stop = disconnect, send `STOP` to IBC's command server (127.0.0.1:7462), kill the process tree
   after 20 s. The app stops the Gateway it started on exit.

What we set in `config.ini`: `OverrideTwsApiPort`, `ReadOnlyApi=no`,
`AcceptIncomingConnectionAction=reject` (localhost is trusted anyway),
`ExistingSessionDetectedAction=primary` (our session wins — this logs out another session of the
same user, e.g. a desktop TWS), `MinimizeMainWindow=yes`, command server bound to 127.0.0.1.

Limits / caveats:

- **2FA**: live accounts still need the user to approve the IB Key push; paper usually doesn't.
  IB forces a full re-login (with 2FA) about weekly.
- **Not truly headless**: Gateway is a Swing app. Linux needs a display or Xvfb (`xvfb-run`);
  on Windows/macOS the window exists, minimized.
- **Install**: the user installs the *offline/stable* Gateway (IBC needs the versioned folder,
  e.g. `~/Jts/ibgateway/1050`); we don't redistribute Gateway. IBC: the log-in dialog's
  **Install IBC** downloads the latest release for the OS from the GitHub API
  (`IbcAlpha/IBC`, assets `IBCLinux-/IBCMacos-/IBCWin-x.y.z.zip`, ~300 KB) into
  `<app-data>/ibc` and marks its `*.sh` executable (the zip carries no permissions).
- **Locate**: on open the dialog checks the saved folders, else searches the usual IBC / Gateway
  locations and a bounded (depth 4, hidden folders skipped) part of home / Downloads / `/opt`.
  **Browse…** accepts the IBC folder, its `scripts` folder, `Jts`, `ibgateway` or a version folder.
- Verified against IBC 3.24.2: `ibcstart.sh` arguments, all `config.ini` keys we write, and the
  log strings we parse (`Login has completed`, `Second Factor Authentication`, `Login failed`).
- The password is **not persisted** — typed per start. A keychain-backed "remember" is future work.
- The output patterns (`Second Factor Authentication`, `Login has completed`,
  `Unrecognized Username or Password`) are not verified against a real IBC run yet; readiness only
  depends on the port, so a mismatch just means a less specific status / a timeout instead of a
  fast failure.
- Windows: `cmd.exe /c StartIBC.bat` with paths containing spaces is untested.



- Every concurrent connection to the **same** TWS/Gateway needs a **distinct** `ClientId`.
- `ClientId = 0` is special: it also receives orders placed manually in TWS and can bind them.
- A different gateway (different login) has its own client id namespace.

## Public surface (`ITwsClient`, 0.5.0)

| Member | IB call | Notes |
|---|---|---|
| `ManagedAccounts`, `IsConnected` | `managedAccounts` | Comma-separated account ids |
| `SetMarketDataType(int)` | `reqMarketDataType` | 1 live, 2 frozen, 3 delayed, 4 delayed-frozen |
| `GetServerTimeAsync` | `reqCurrentTime` | Cheap round trip — usable as a heartbeat |
| `ResolveContractAsync(Contract)` | `reqContractDetails` | Disambiguation / conId lookup |
| `GetHistoricalBarsAsync(...)` | `reqHistoricalData` | IB string formats: `duration "5 D"`, `barSize "1 day"` |
| `GetFundamentalDataAsync(...)` | `reqFundamentalData` | Raw XML |
| `GetAccountSummaryAsync(tags, group)` | `reqAccountSummary` | Snapshot |
| `GetPositionsAsync` / `SubscribePositionsAsync` | `reqPositions` | Snapshot / stream |
| `GetOpenOrdersAsync` | `reqOpenOrders` | Snapshot |
| `GetExecutionsAsync(filter)` | `reqExecutions` | Fills joined with commission reports |
| `PlaceOrderAsync(Contract, Order)` | `placeOrder` | Completes on first acknowledged state |
| `CancelOrder(id)`, `NextOrderId()` | `cancelOrder` | |
| `StreamOrderStatusAsync(orderId)` | `orderStatus` | |
| `SubscribeMarketDataAsync(Contract, ticks)` | `reqMktData` | Level 1 ticks |
| `SubscribeRealtimeBarsAsync(...)` | `reqRealTimeBars` | 5-second bars only |
| `SubscribeAccountPnlAsync` / `SubscribePositionPnlAsync` | `reqPnL` / `reqPnLSingle` | |

`Contracts.Stock("AAPL")` etc. build `IBApi.Contract`s. Note that `Contract`, `Order`,
`ContractDetails`, `Bar`, `ExecutionFilter` are the **vendored `IBApi` types** — they leak through
the facade. That is the main reason all of it is confined to `LT.Ibkr` (see
[architecture.md](architecture.md)).

### Not covered yet (we will need these for "TWS but better")

The facade is young. Missing compared to the raw IB API — we either contribute upstream or drop to
the vendored `EClientSocket` inside `LT.Ibkr`:

- **Connection-lost notification** — there is no public event. `ConnectionManager` internally
  listens to `connectionClosed`/`error`, but `ITwsClient` only exposes `IsConnected`. We need a
  heartbeat (`GetServerTimeAsync`) or an upstream event to detect drops and error codes 1100/1102.
- **No fast failure on refused connections** — with nothing listening on the port, `ConnectAsync`
  still waits the whole `ConnectTimeout` (15 s) and then reports a `nextValidId` timeout
  (observed 2026-10-23). The UI shows "Connecting…" for that long.
- **Error/info message stream** — IB error callbacks (farm status, pacing, order rejects) are not
  surfaced as a stream.
- Market depth / Level 2 (`reqMktDepth`), tick-by-tick (`reqTickByTickData`).
- Option chains (`reqSecDefOptParams`). We build chains from `reqContractDetails` on
  `OPT` + expiry month instead — slower and heavier, but works. Option greeks
  (`tickOptionComputation`) are not routed at all.
- Scanners (`reqScannerSubscription`), news (`reqNewsProviders`/`reqHistoricalNews`).
- Matching symbols / search (`reqMatchingSymbols`). Search is by exact symbol + type via
  `reqContractDetails`.
- `reqAccountUpdates` (portfolio with market value/P&L per position in one stream).
- Order modification (re-`placeOrder` with the same id), what-if orders, bracket/OCA helpers.
- Historical tick data, head timestamp, histogram data.

- **Some "warnings" kill streams.** `TwsException.IsInformational` doesn't include `10167`
  (delayed data shown) or `10089/10090` (subscription needed), so on a `Live` market data type
  without a subscription the quote stream ends with that error. We default to `Delayed` (3),
  which returns live data where you are subscribed and delayed otherwise.
- **Account summary: at most two requests at a time** (error `322`, seen live on 2026-10-23 while
  polling every 15 s). IB only refreshes the values every 3 minutes, so we poll at that rate and
  `IbkrApiClient` lets only one request run at a time.
- **Real-time bars need a live subscription** — there is no delayed `reqRealTimeBars`. The chart
  keeps historical bars and logs "real-time bars unavailable".

## How LT.Ibkr applies the rules

- `RequestThrottle` (sliding window) paces every request: 40 msg/s overall and 60 historical
  requests per 10 minutes. Requests wait instead of tripping IB's disconnect.
- `MarketDataManager` counts lines and refuses a subscription above 90 (headroom under IB's 100).
- `ChartManager` serves bars from the JSON cache while fresh (1 min intraday, 30 min daily+) and
  falls back to stale cache when IB is unreachable.
- `ConnectionManager.CheckHealthAsync` heartbeats with `GetServerTimeAsync`; a failure or
  `IsConnected == false` starts reconnecting with 2/5/10/30/60 s backoff. `ResumeAsync` (run by `ConnectionWatchdog` on start) reconnects when `ConnectOnStartup` is set — connect sets it, Disconnect clears it, app exit (`ShutdownAsync`) keeps it; a failed resume falls into the same backoff.

## IB API rules that shape our design

- **Pacing:** max ~**50 messages/second** from client to TWS; exceeding it disconnects. Batch
  subscriptions on startup (e.g. a 200-symbol watchlist) must be throttled.
- **Market data lines:** default 100 simultaneous `reqMktData` subscriptions per account (grows
  with commissions/quote boosters). Watchlists larger than that need virtualization — only
  subscribe what's visible.
- **Historical data pacing:** no more than 60 requests per 10 minutes; identical requests within 15
  seconds are rejected. Cache bars locally.
- **Market data subscriptions:** without paid subscriptions you get error 354/10167; call
  `SetMarketDataType(3)` to fall back to delayed data (default for paper accounts).
- **Threading:** callbacks arrive on the `EReader` thread; the facade hands us channels/tasks, but
  continuations still run off the UI thread. **Every UI update goes through
  `Dispatcher.UIThread`.** See [ui-patterns.md](ui-patterns.md).
- **Common message codes:** `502` can't connect, `504` not connected, `1100` connectivity lost,
  `1102` restored (data maintained), `2104/2106/2158` data farm OK (informational, not errors),
  `200` no security definition, `201` order rejected, `354` not subscribed, `10167` delayed data shown.

## Testing against IB

- **Paper account** (Gateway on 4002, usually no 2FA) for manual testing.
- The upstream repo ships `docker-compose.yml` with `ghcr.io/gnzsnz/ib-gateway` (IBC auto-login,
  2FA off on paper) — useful for integration tests later.
- Unit tests in `LT.Tests` never connect: managers depend on `IIbkrApiClient`, which tests fake.

## Alternatives considered

- **Raw `CSharpAPI`** — complete but callback/request-id based; we'd rewrite what `TwsApi` already does.
- **Client Portal Web API only** — no TWS needed, but higher latency, session tickling, weaker
  streaming. Possible secondary transport via the bundled `RestApi` client.
