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
services.AddTwsApi(o => { o.Host = "127.0.0.1"; o.Port = 7497; o.ClientId = 1; });

// inject ITwsClientFactory
await using ITwsClient tws = await factory.ConnectAsync(new TwsConnectionOptions
{
    Host = "127.0.0.1", Port = 7497, ClientId = 1, ConnectTimeout = TimeSpan.FromSeconds(15),
}, ct);
```

`ConnectAsync` completes when TWS sends `nextValidId`. Each `ITwsClient` is a fully independent
connection (own socket, reader thread, id sequence). `DisposeAsync` disconnects.

### Ports

| Port | Endpoint |
|---|---|
| 7496 | TWS — live |
| 7497 | TWS — paper |
| 4001 | IB Gateway — live |
| 4002 | IB Gateway — paper |

We still need **TWS or IB Gateway running** — the socket API is a local bridge to it, not a direct
connection to IBKR. Long-term goal is to run against **IB Gateway** (headless, lighter) so our app
*replaces* the TWS UI instead of sitting next to it.

### TWS / Gateway side setup

In TWS: *File → Global Configuration → API → Settings*:

- **Enable ActiveX and Socket Clients** — on.
- **Socket port** — matches the table above.
- **Read-Only API** — on by default; must be **off** to place orders.
- **Trusted IPs** — `127.0.0.1` (or the machine running the app).
- **Allow connections from localhost only** — keep on unless Gateway runs on another host.

TWS and Gateway **restart daily** (configurable time, auto-logoff) and require a re-login weekly.
The app must treat disconnects as normal, not exceptional.

### Client ids

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
- Option chains (`reqSecDefOptParams`), option greeks are only available as generic ticks.
- Scanners (`reqScannerSubscription`), news (`reqNewsProviders`/`reqHistoricalNews`).
- Matching symbols / search (`reqMatchingSymbols`).
- `reqAccountUpdates` (portfolio with market value/P&L per position in one stream).
- Order modification (re-`placeOrder` with the same id), what-if orders, bracket/OCA helpers.
- Historical tick data, head timestamp, histogram data.

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

- **Paper account** + TWS on 7497 or Gateway on 4002 for manual testing.
- The upstream repo ships `docker-compose.yml` with `ghcr.io/gnzsnz/ib-gateway` (IBC auto-login,
  2FA off on paper) — useful for integration tests later.
- Unit tests in `LT.Tests` never connect: managers depend on `IIbkrApiClient`, which tests fake.

## Alternatives considered

- **Raw `CSharpAPI`** — complete but callback/request-id based; we'd rewrite what `TwsApi` already does.
- **Client Portal Web API only** — no TWS needed, but higher latency, session tickling, weaker
  streaming. Possible secondary transport via the bundled `RestApi` client.
