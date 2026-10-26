# IB socket API — IBKR.Net

The app talks to IB Gateway through **[IBKR.Net](https://github.com/LimitlessSoft/IBKR.Net)**, our
own client for the TWS / IB Gateway socket API (MIT, clean-room: **no Interactive Brokers code**).
IB's own API client is GPL-3.0 from 10.50 and non-commercial before, so bundling it would bind
this app's license. How each message looks on the wire is in that repo's `docs/protocol.md`; this
file covers how the app uses it, IB's limits and the quirks we have hit. Read it before touching
`LT.Ibkr`.

`IBKR.Net` comes from nuget.org (0.2.0).

## Connecting

```csharp
await using var client = await IbkrClient.ConnectAsync(new IbkrClientOptions
{
    Host = "127.0.0.1", Port = 4002, ClientId = 1, ConnectTimeout = TimeSpan.FromSeconds(15),
}, ct);
```

`ConnectAsync` agrees on protocol version 187 (IBKR.Net caps it below the versions that switch
messages to protobuf) and completes when the Gateway sends `nextValidId`. `ManagedAccounts` is
known by then. `DisposeAsync` disconnects; `Disconnected` and `Notice` report drops and IB's
informational messages.

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

1. Validate, check `<IBC>/scripts/ibcstart.sh` (`StartIBC.bat` on Windows) exists. If the API port
   (4001 live / 4002 paper) is already open, a Gateway is running that this app run didn't start
   (typically left over after the app was killed): mark it Running with `IsExternal`, connect to it
   and stop here. Stop then only disconnects; the process is left alone.
2. Write IBC's `config.ini` to `<app-data>/ibc-config.ini`, owner-only (0600 on Unix). It holds
   `IbLoginId`/`IbPassword` — credentials never go on the command line (visible in the process list).
3. Run `ibcstart.sh <ver> --gateway --tws-path=… --ibc-path=… --ibc-ini=… --mode=… --on2fatimeout=exit`
   (optionally under `xvfb-run -a` on Linux to hide the window).
4. Poll the API port every second for up to 5 minutes. IBC output is parsed best-effort to show
   "approve in IB Key" (2FA) and to fail fast on a rejected login.
5. Delete the config file, mark Running, connect `IConnectionManager` to `127.0.0.1:<port>`.
6. Stop = disconnect, send `STOP` to IBC's command server (127.0.0.1:7462), kill the process tree
   after 20 s. On exit the Gateway keeps running by default and the next Log in reuses it (step 1); with
   Settings → Connection → "Stop IB Gateway when the app closes" on (off by default), the app stops
   the Gateway it started.

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

## What IBKR.Net covers

| `IbkrClient` | IB request | Notes |
|---|---|---|
| `GetServerTimeAsync` | `reqCurrentTime` | Cheap round trip — the heartbeat |
| `SetMarketDataTypeAsync` | `reqMarketDataType` | 1 live, 2 frozen, 3 delayed, 4 delayed-frozen |
| `GetContractDetailsAsync(Contract)` | `reqContractDetails` | Search by symbol, conId lookup, one expiration's options |
| `GetOptionChainParametersAsync` | `reqSecDefOptParams` | Expirations + strikes per exchange / trading class |
| `SubscribeMarketDataAsync` | `reqMktData` | Price, size, generic and string ticks |
| `GetHistoricalBarsAsync` | `reqHistoricalData` | IB strings: duration `"5 D"`, bar size `"5 mins"` |
| `SubscribeRealtimeBarsAsync` | `reqRealTimeBars` | 5-second bars |
| `GetPositionsAsync` / `SubscribePositionsAsync` | `reqPositions` | Snapshot / stream |
| `SubscribeAccountSummaryAsync` | `reqAccountSummary` | Stream — see error 322 below |
| `SubscribeAccountPnlAsync` / `SubscribePositionPnlAsync` | `reqPnL` / `reqPnLSingle` | |
| `GetOpenOrdersAsync` | `reqAllOpenOrders` | Every client's and TWS's orders (0.3.0); `reqOpenOrders` would only return the asking client id's, and LT places orders on the order connection's id |

### Not covered yet

- **Executions** (`reqExecutions`, commission reports): their frames have not been recorded yet
  (no fill when we recorded). Until then the Executions widget shows "not supported yet"
  (`ConnectionValidationCodes.CVC_007`).
- Market depth / Level 2 (`reqMktDepth`), tick-by-tick (`reqTickByTickData`), option greeks
  (`tickOptionComputation`).
- Scanners (`reqScannerSubscription`), news (`reqNewsProviders` / `reqHistoricalNews`).
- Matching symbols (`reqMatchingSymbols`): search is by exact symbol + type via
  `reqContractDetails`.
- `reqAccountUpdates` (portfolio with market value / P&L per position in one stream).
- Order modification, what-if previews, bracket / OCA orders; open order, execution and
  commission frames (no order was accepted while recording).

## Orders

- `IIbkrApiClient.PlaceOrderAsync` / `CancelOrderAsync`, used by `IOrderManager` and the Order
  ticket widget. IBKR.Net places orders only at protocol version **178** (the only order layout
  recorded), so `IbkrApiClient` opens a **second connection** for orders on the first order:
  same host and port, client id `OrderHelpers.GetOrderClientId` (main + 100), capped at 178.
- Cancelling: the Order ticket (orders sent this session) and the Orders widget (any working
  order whose client id is the order connection's, `OrderHelpers.CanCancel`). IB takes a cancel
  only from the client that placed the order, so TWS's and other clients' orders are not
  cancellable here; an order placed before an app restart still is, because the order connection
  reconnects with the same client id.
- An order is a quantity, or for a market order a **cash amount** (`cashQty`, e.g. $10 of BTC).
- Status comes from orderStatus only; a rejection is an IB error for the order id (e.g. 460 "No
  trading permissions", 435 "You must specify an account" — so the ticket always sends one).
- Crypto: GTC or IOC only (error 10052 otherwise); market = IOC. Before the first crypto order IB
  Gateway shows a "Cryptocurrency order confirmation" dialog and holds the order until clicked.

New calls are added to IBKR.Net from a wire recording first (`tools/IbkrWireRecorder` there).

## Quirks we have hit

- **Notices vs errors.** IBKR.Net treats codes 2100–2199 (farm status, 2150 on position P&L) and
  `10167` (delayed data shown) as notices; any other code with a request id fails that request.
  `10089/10090` (subscription needed) therefore end a quote stream on a `Live` market data type
  without a subscription. We default to `Delayed` (3), which returns live data where you are
  subscribed and delayed otherwise.
- **Unset numbers are `double.MaxValue`, not 0.** A contract sent with `Strike = 0` asks for
  strike 0 and fails with `200 No security definition` (seen 2026-10-24 loading an option
  expiration). `IbkrContractMapper` sends no strike (an empty field) when the DTO has none.
- **Error `2188` "Up-to-the-second historical data requires additional subscription" means no
  market data subscription for that listing's exchange at all**, not "history up to now needs
  live data": for SPY on ASX it fails even with the end a day back, while US SPY and its options
  load fine without a live subscription (verified 2026-10-24, protocol version 225). At version
  187, which IBKR.Net uses, the same ASX request returned bars. Usually it means the wrong
  listing was picked; search ranks `MarketDataConstants.PreferredCurrency` (USD) listings first.
- <a id="account-summary-error-322"></a>**Account summary: two requests per connection, ever.**
  IB allows two account summary requests per client at a time, and IB Gateway (server version 225)
  never frees a slot on `cancelAccountSummary`, in either the protobuf or the legacy encoding.
  The third request on a connection fails with `322 Maximum number of account summary requests
  exceeded`, even when the requests run one after another (verified with a probe on 2026-10-23).
  So snapshots can't be polled. `AccountSummaryFeed` in `LT.Ibkr` opens one
  `IbkrClient.SubscribeAccountSummaryAsync` per connection, keeps it until disconnect, and shares it:
  each subscriber gets the cached values, then every change (IB pushes changes about every 3
  minutes). Don't cancel and resubscribe it, e.g. on account change; filter by account instead.
- **Real-time bars need a live subscription** — there is no delayed `reqRealTimeBars`. The chart
  keeps historical bars and logs "real-time bars unavailable".

## How LT.Ibkr applies the rules

- `RequestThrottle` (sliding window) paces every request: 40 msg/s overall and 60 historical
  requests per 10 minutes. Requests wait instead of tripping IB's disconnect.
- `MarketDataManager` counts lines and refuses a subscription above 90 (headroom under IB's 100).
- `ChartManager` serves bars from the JSON cache while fresh (1 min intraday, 30 min daily+) and
  falls back to stale cache when IB is unreachable.
- `ConnectionManager.CheckHealthAsync` heartbeats with `GetServerTimeAsync`; a failure or
  `IsConnected == false` starts reconnecting with 2/5/10/30/60 s backoff.
- On start, before the login dialog, `GatewayManager.AttachRunningAsync` checks the last trading
  mode's API port: a Gateway still running from an earlier session is reused (marked external)
  and connected, so no login is asked for.

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
- **Threading:** IBKR.Net reads the socket on a background thread and hands us channels/tasks;
  continuations run off the UI thread. **Every UI update goes through
  `Dispatcher.UIThread`.** See [ui-patterns.md](ui-patterns.md).
- **Common message codes:** `502` can't connect, `504` not connected, `1100` connectivity lost,
  `1102` restored (data maintained), `2104/2106/2158` data farm OK (informational, not errors),
  `200` no security definition, `201` order rejected, `354` not subscribed, `10167` delayed data shown.

## Testing against IB

- **Paper account** (Gateway on 4002, usually no 2FA) for manual testing.
- `ghcr.io/gnzsnz/ib-gateway` (Docker, IBC auto-login, 2FA off on paper) — useful for
  integration tests later.
- Unit tests in `LT.Tests` never connect: managers depend on `IIbkrApiClient`, which tests fake.

## Alternatives considered

- **TwsApi (UtopleMan/tws-api-dotnet)** — used until 2026-10-24. It wraps IB's own C# client,
  which is GPL-3.0 from 10.50 and non-commercial before; bundling it would bind this app's
  license, and a fork can't build it (IB's repo is private).
- **IB's `CSharpAPI` directly** — same license problem.
- **Client Portal Web API only** — no Gateway socket needed, but higher latency, session
  tickling, weaker streaming.
