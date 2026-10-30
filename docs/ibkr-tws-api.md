# IB socket API — IBKR.Net

The app talks to IB Gateway through **[IBKR.Net](https://github.com/LimitlessSoft/IBKR.Net)**, our
own client for the TWS / IB Gateway socket API (MIT, clean-room: **no Interactive Brokers code**).
IB's own API client is GPL-3.0 from 10.50 and non-commercial before, so bundling it would bind
this app's license. How each message looks on the wire is in that repo's `docs/protocol.md`; this
file covers how the app uses it, IB's limits and the quirks we have hit. Read it before touching
`LT.Ibkr`.

`IBKR.Net` comes from nuget.org (0.10.0).

### Extending IBKR.Net

IBKR.Net is ours and is changed whenever LimitlessTrading needs an IB call it doesn't have yet —
don't work around a missing call in LT (no second IB client, no Client Portal side channel). The
one exception is account **history** (NAV, deposits, past and today's trades), which the socket
API doesn't have at all: it comes from IB's Flex Web Service, see [ibkr-flex.md](ibkr-flex.md). The order is: record the request and its answers against a real Gateway with
`tools/IbkrWireRecorder` (in the IBKR.Net repo), implement it there from the recording only
(clean-room: never from IB's own API source), publish a new IBKR.Net version to nuget.org, then
bump the package here and map the new types in `LT.Ibkr/DtoMappings/`. Until then LT is built
against its own DTO so the feature lights up with only the `LT.Ibkr` change (as executions did:
`ExecutionDto`, used by the Executions widget and the trade journal, landed with 0.5.0).

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

The API settings Gateway keeps under *Configure → Settings → API → Settings* (socket port,
Read-Only API) are stored encrypted in Gateway's own settings folder, so the app can't pre-set
them. The user sets them **once** in Gateway: keep the socket port at the default 4001 (live) /
4002 (paper) — the app only watches those two — and untick **Read-Only API** to trade from the
app (with it ticked, orders are rejected by Gateway).

Gateway **restarts daily** (configurable time, auto-logoff) and require a re-login weekly.
The app must treat disconnects as normal, not exceptional.

### Starting IB Gateway from the app

**Log in…** (menu, status bar, or the dialog shown on start) → **Start IB Gateway** runs Gateway's
own launcher from the installed version folder. Gateway opens its normal login window and the
user logs in there — username, password, live or paper, IB Key 2FA. The app never sees or stores
credentials and drives no UI: it only waits for an API port to open and then connects. Pure C#, no
IBC, no Java code of ours.

Flow (`GatewayManager` → `IGatewayProcess`, implemented by `LT.Ibkr/Gateway/GatewayProcess`):

1. Validate, then find the launcher of the chosen version (`GatewayInstallHelpers.FindLauncher`).
   If the live or paper API port is already open, a Gateway is running that this app run didn't
   start (left over after the app was closed or killed): mark it Running with `IsExternal`,
   connect to it and stop here. Stop then only disconnects; the process is left alone. The port
   of the mode last connected to is tried first (live on a fresh install).
2. Start the launcher in its own folder (`Starting` → `AwaitingLogin`). Its stdout / stderr are
   read and dropped.
3. Poll both API ports every second for up to 10 minutes (typing + 2FA). The first to open wins:
   its mode is remembered, the state goes Running and `IConnectionManager` connects to
   `127.0.0.1:<port>`.
4. Gateway exiting before a port opens (the user closed the login window) goes back to Stopped,
   no error; a non-zero exit code is shown as a hint. Cancel (Stop while waiting) closes Gateway
   and ends in Stopped. The timeout closes Gateway and fails with a hint about the API port.
5. Gateway exiting after login (window closed, crash) fails the session and shuts the connection.
6. Stop = disconnect and kill the Gateway process tree (Gateway has no stop command). On exit the
   Gateway keeps running by default and the next start reuses it (step 1); with Settings →
   Connection → "Stop IB Gateway when the app closes" on (off by default), the app stops the
   Gateway it started.

Launchers (`GatewayInstallHelpers.FindLauncher`):

| OS | Install root (default) | Launcher |
|---|---|---|
| Linux | `~/Jts` | `ibgateway/<ver>/ibgateway` or `ibgateway1` (10.50 offline installer, verified) — an install4j script that `exec`s java, so the child process is Gateway itself |
| Windows | `C:\Jts` | `ibgateway\<ver>\ibgateway.exe` (not verified on a real install) |
| macOS | `~/Applications` | `IB Gateway 10.37/*.app/Contents/MacOS/<executable>` (not verified on a real install) |

Limits / caveats:

- **Install**: the user installs the *offline/stable* Gateway (the versioned folder, e.g.
  `~/Jts/ibgateway/1050`); we don't redistribute Gateway. On open the dialog checks the saved
  folder, else the usual roots; **Browse…** accepts `Jts`, `ibgateway` or a version folder.
- Started with no arguments, Gateway uses its default settings folder (`~/Jts` on Linux).
- Only ports 4001 / 4002 are watched; a Gateway configured to another port times out.
- Stopping kills Gateway rather than closing it gracefully.
- Leftovers from the IBC era: `<app-data>/ibc-config.ini` (could hold a password after a crash)
  is deleted on start (`LegacyIbcCleanup`); `<app-data>/ibc` (IBC downloaded by the old Install
  IBC button) is left for the user to delete, since a Gateway started by IBC may still use it.
  Old `settings.json` files with `Username` / `IbcPath` / `UseVirtualDisplay` still load.


- Every concurrent connection to the **same** TWS/Gateway needs a **distinct** `ClientId`.
- `ClientId = 0` is special: it also receives orders placed manually in TWS and can bind them.
- A different gateway (different login) has its own client id namespace.

## What IBKR.Net covers

| `IbkrClient` | IB request | Notes |
|---|---|---|
| `GetServerTimeAsync` | `reqCurrentTime` | Cheap round trip — the heartbeat |
| `SetMarketDataTypeAsync` | `reqMarketDataType` | 1 live, 2 frozen, 3 delayed, 4 delayed-frozen |
| `GetContractDetailsAsync(Contract)` | `reqContractDetails` | Search by symbol, conId lookup, one expiration's options; `tradingHours` / `liquidHours` (a few days around today, in `timeZoneId`, which may be an old name like `US/Eastern`) mark a chart's pre- / post-market bars; the Overview widget shows the rest (long name, industry / category / subcategory, min tick, valid exchanges, market name, security ids such as ISIN) through `IContractManager.GetDetailsAsync`, cached 12 h per conId |
| `GetOptionChainParametersAsync` | `reqSecDefOptParams` | Expirations + strikes per exchange / trading class |
| `SubscribeMarketDataAsync` | `reqMktData` | Price, size, generic and string ticks; optional generic tick list (`106` = underlying option implied volatility, tick 24, used by the option chain; `165` = 90-day average daily volume, tick 21 as a size tick in shares, used by the watchlist's Avg Vol and Avg Vol/Min columns, and the same list's 52-week low / high, ticks 19 / 20 as price ticks, used by the Overview widget; `295` = trade rate and volume rate, ticks 55 / 56 as generic ticks per minute, the volume rate in shares, used by the watchlist's Vol/Min column — recorded live 2026-10-28: AAPL 39,944 / min against a 10-minute short-term volume (generic 595, tick 65) of 440,720) |
| `GetMarketDataSnapshotAsync` | `reqMktData` snapshot | One quote, ends on `tickSnapshotEnd` ~11 s later; never the paid regulatory snapshot, no generic ticks (0.9.0) — see [Quote snapshots](#quote-snapshots) |
| `GetHistoricalBarsAsync` | `reqHistoricalData` | IB strings: duration `"5 D"`, bar size `"5 mins"`; `endDateTime` empty (now) or `yyyyMMdd-HH:mm:ss` UTC (a chart's older pages; the bar starting at the end may come back). No bars in the span: error `162` "HMDS query returned no data" |
| `SubscribeRealtimeBarsAsync` | `reqRealTimeBars` | 5-second bars |
| `GetPositionsAsync` / `SubscribePositionsAsync` | `reqPositions` | Snapshot / stream |
| `SubscribeAccountSummaryAsync` | `reqAccountSummary` | Stream — see error 322 below |
| `SubscribeAccountPnlAsync` / `SubscribePositionPnlAsync` | `reqPnL` / `reqPnLSingle` | `reqPnL` is shared per account — see below |
| `ModifyOrderAsync` | `placeOrder` with the order's id | A working order's new price (0.11.0); built from the recorded placeOrder layout, never sent while recording — see [Orders](#orders) |
| `GetOpenOrdersAsync` | `reqAllOpenOrders` | Every client's and TWS's orders (0.3.0); `reqOpenOrders` would only return the asking client id's, and LT places orders on the order connection's id |
| `GetExecutionsAsync` | `reqExecutions` | The day's fills of every account, client and TWS, each with its commission report (0.5.0); side stays `BOT` / `SLD`, multiplier `null` for none |
| `GetNewsProvidersAsync` | `reqNewsProviders` | The account's API providers (0.4.0) |
| `GetHistoricalNewsAsync` | `reqHistoricalNews` | Newest first, max 300; `before` pages back (strictly older) |
| `GetNewsArticleAsync` | `reqNewsArticle` | Text / HTML, or base64 binary (PDF) |
| `SubscribeNewsAsync` | `reqMktData` `mdoff,292` | Live headlines of a contract (its providers) or a broad tape; one market data line |
| `GetBroadTapeContractsAsync` | `reqContractDetails` (`NEWS`) | A provider's broad tape contracts; IB fails 321 / 200 when it has none |
| `SubscribeMarketDepthAsync` | `reqMktDepth` | Order book changes (insert / update / delete per row and side), one exchange or SMART depth with each row's exchange (0.6.0) — see [Market depth](#market-depth) |
| `SubscribeTickByTickAsync` | `reqTickByTickData` | Last / AllLast / BidAsk / MidPoint ticks, IB time in whole seconds (0.7.0); used by the chart's tick-by-tick live updates — see [Tick-by-tick](#tick-by-tick) |
| `GetHistoricalTicksAsync` | `reqHistoricalTicks` | Past trades, bid / ask or midpoints, at most 1000 a request, oldest first, strictly before an end (0.10.0); the chart's footprint backfill — see [Historical ticks](#historical-ticks) |
| `GetMarketDepthExchangesAsync` | `reqMktDepthExchanges` | The exchanges IB serves depth for (306 on the live account); not used by the app yet |
| `GetWshMetaDataAsync` / `GetWshEventDataAsync` | `reqWshMetaData` / `reqWshEventData` | Wall Street Horizon corporate events as raw JSON (0.8.0); needs the WSH subscription, else 10276 — see [Wall Street Horizon](#wall-street-horizon-calendar) |

### Not covered yet

- Option greeks and per-option implied volatility (`tickOptionComputation`).
- Scanners (`reqScannerSubscription`); news bulletins (`reqNewsBulletins`).
- Wall Street Horizon answers (`104` / `105`): requests and the 10276 refusal are recorded, the
  data frames and their JSON are not (no subscription) — see
  [Wall Street Horizon](#wall-street-horizon-calendar).
- Matching symbols (`reqMatchingSymbols`): search is by exact symbol + type via
  `reqContractDetails`.
- `reqAccountUpdates` (portfolio with market value / P&L per position in one stream).
- What-if previews, bracket / OCA orders; open order, execution and
  commission frames (no order was accepted while recording).

## Orders

- `IIbkrApiClient.PlaceOrderAsync` / `CancelOrderAsync`, used by `IOrderManager` and the Order
  ticket widget (also the option chain's quick orders and the Positions quick exit, which send
  without a ticket). IBKR.Net places orders only at protocol version **178** (the only order layout
  recorded), so `IbkrApiClient` opens a **second connection** for orders on the first order:
  same host and port, client id `OrderHelpers.GetOrderClientId` (main + 100), capped at 178.
- Cancelling: the Order ticket (orders sent this session) and the Orders widget (any working
  order whose client id is the order connection's, `OrderHelpers.CanCancel`). IB takes a cancel
  only from the client that placed the order, so TWS's and other clients' orders are not
  cancellable here; an order placed before an app restart still is, because the order connection
  reconnects with the same client id.
- Changing a price: a chart's order line (this app's own limit orders only, after a
  confirmation) → `IOrderManager.ModifyOrderAsync` → `IIbkrApiClient.ModifyOrderAsync` →
  IBKR.Net `ModifyOrderAsync` (0.11.0): placeOrder again with the order's id on the order
  connection, the order as `reqAllOpenOrders` last reported it (contract by conId + exchange,
  side, total quantity, type, time in force, account) with the new limit price. Like a cancel,
  IB takes it only from the client that placed the order (`OrderHelpers.GetModifyBlock`): TWS's
  orders (client 0) and other clients' are drawn but not movable; binding them would need client
  id 0, which the app doesn't use. IB doesn't report the outside-hours flag in open orders, so
  the change goes out with it off — the app never sets it, but one ticked in TWS on this app's
  order would be cleared. The answer awaited is the first open order / order status frame for
  the id, an error for the id is a refusal (the order stays as it was); with none in 10 s
  (`OrderConstants.ModifyAnswerTimeout`) the open orders are read again. No modification has
  been sent to IB while developing it (it trades for real), so the answer is from IB's
  documentation, not a recording.
- An order is a quantity, or for a market order a **cash amount** (`cashQty`, e.g. $10 of BTC).
- Status comes from orderStatus only; a rejection is an IB error for the order id (e.g. 460 "No
  trading permissions", 435 "You must specify an account" — so the ticket always sends one).
- Crypto: GTC or IOC only (error 10052 otherwise); market = IOC. Before the first crypto order IB
  Gateway shows a "Cryptocurrency order confirmation" dialog and holds the order until clicked.

New calls are added to IBKR.Net from a wire recording first (`tools/IbkrWireRecorder` there).

## News

`IIbkrApiClient` exposes the IBKR.Net news calls as DTOs (`LT.Ibkr/DtoMappings/News`);
`IbkrNewsSource` (`LT.Domain/NewsSources`) turns them into an `INewsSource`, and `NewsManager`
merges every source for the News widget (see [ui-patterns.md](ui-patterns.md#workspace-ltappworkspace)).
The app's own source (SEC filings, press release wires, Fed, BLS …, no IB) and the local archive
every headline is kept in, IB's included, are in [news-feeds.md](news-feeds.md).

- **Providers** seen on the live account (2026-10-26, read-only): BRFG, BRFUPDN, DJ-N, DJ-RT,
  DJ-RTA, DJ-RTE, DJ-RTG, DJNL. A provider's codes are what history, articles and live ticks take.
- **History** is per contract (conId) and mixes the requested providers, newest first; IB returns
  at most 300 per request (`NewsConstants.MaxHeadlinesPerRequest`), the widget asks for 100 at a
  time. Paging passes the oldest headline's time: IB returns headlines strictly before it. Times
  are UTC at minute resolution. Continuation headlines ("-2-", "-3-") are separate articles.
- **Live headlines** are news ticks over `reqMktData` with quotes off (`mdoff,292`). Each
  subscription uses a **market data line**, taken through `IMarketDataManager.AcquireLine` (it
  returns the line's id) / `ReleaseLine(id)` like quotes, and listed under the owner "News": one per linked contract (all its providers in one request), one per
  broad tape. IB first replays recent headlines, mostly ones history already returned, so
  headlines are deduplicated on (source, provider, article id) in the manager and the widget.
- **Broad tapes** (every headline of a provider, not tied to a symbol): BRFG (15 sub-tapes; we
  use `BRFG:BRFG_ALL`), BRFUPDN (`BRFUPDN:BRF_ALL`) and DJNL (`DJNL:DJNL_ALL`) have their own,
  looked up by provider code. The Dow Jones API providers (DJ-N, DJ-RT, DJ-RTA, DJ-RTE, DJ-RTG)
  fail that lookup with 321 ("Valid are: BRFG, BRFUPDN, DJ, DJNL, BZ, DJTOP, FLY"): their
  headlines come on the ~7,200 topic tapes of exchange `DJ`, and `DJ:N_DJN` carries all five
  mixed on one stream (live probe 2026-10-27: 55/55 distinct headlines seen across 19 tapes).
  `DJ:S_ALL`, `DJ:M_ALL` and the DJTOP tapes miss headlines. `NewsConstants.SharedBroadTapes`
  maps the DJ-* codes to that tape, built without a lookup; `IbkrNewsSource` subscribes it once
  (one line) for any number of selected DJ-* providers and keeps only their headlines. Other
  providers without a tape fail with 321 or 200, which `IbkrApiClient` turns into "none".
  The subscription needs security type `NEWS` and the provider code as exchange, so
  `IbkrContractMapper` sends those for `NEWS` contracts (the lookup's reply says exchange `NEWS`,
  which IB then rejects with 321). `IbkrNewsSource` looks each provider up once per app run.
- **Articles** came as `<pre>` text with entities such as `&#10;` and as `<html><body><p>…`; both
  arrive with type 0 and are told apart by their tags (`NewsHelpers.GetTextKind`). The widget
  shows plain text only (`NewsHelpers.ToPlainText`). Type 1 (base64, e.g. PDF) has not been seen
  yet; the widget offers to open it with the OS. The last 50 articles are kept in memory for 30
  minutes, and every text article read is kept in the news archive (readable offline).
- News requests go through the message throttle only, not the historical data window (that is
  for bars; we have not seen IB pace `reqHistoricalNews`), and the widget pages only on "Load
  older". Not verified against IB's limits under heavy paging.

## Market depth

`IIbkrApiClient.SubscribeMarketDepthAsync` streams IB's depth updates as DTOs
(`LT.Ibkr/DtoMappings/MarketData`); `IMarketDepthManager` builds the book from them for the Depth
widget (see [ui-patterns.md](ui-patterns.md#workspace-ltappworkspace)). Recorded on the live
account on 2026-10-26 (read-only); the wire format is in IBKR.Net's `docs/protocol.md`.

- **Three at a time.** IB allows 3 depth subscriptions per account by default; the 4th fails
  with **309** "Max number (3) of market depth requests has been reached". TWS and other API
  clients count too. `MarketDepthManager` refuses a 4th of its own
  (`MarketDepthConstants.MaxSubscriptions`) and turns 309 into the same message. The quote line
  limit (`IMarketDataManager`) is separate and not used.
- **SMART depth** (`isSmartDepth`, contract on SMART) aggregates every exchange the account has
  depth for; each row names its exchange (`IEX`, `DRCTEDGE`, `MEMX` …). IB sends **2152**
  (informational) with the request: "Exchanges - Depth: IEX; Top: BYX; … Need additional market
  data permissions - Depth: BATS; NASDAQ; ARCA; BEX; NYSE". On the live account only IEX gave depth
  for US stocks, so SMART depth there is IEX's book mixed with top-of-book quotes from other
  venues. The widget asks for SMART depth by default and can switch to the listing exchange.
- **No subscription** for the exchange's depth: **354** (ARCA, IBIS — `VOW IBIS/DEEP`) or
  **10089** (NASDAQ TotalView — `AAPL NASDAQ.NMS/DEEP`); shown as a readable message. Depth that
  worked without asking: IEX, PAXOS (crypto), IDEALPRO (forex).
- **Book maintenance** (`OrderBookHelpers.Apply`): an insert pushes rows down, a delete pulls
  them up, an update replaces the row; a side keeps at most the requested rows (20, up to 50
  served). IB sends deletes and updates past the end of a side (seen on PAXOS, several in a row);
  they are ignored.
- Requests go through the message throttle like every other.

## Quote snapshots

`IbkrClient.GetMarketDataSnapshotAsync` (0.9.0) sends `reqMktData` with snapshot set and
regulatory snapshot **clear** (a regulatory snapshot costs $0.01 each; never sent) and returns the
ticks IB sends before `tickSnapshotEnd` (`57`). LT exposes it as
`IIbkrApiClient.GetMarketDataSnapshotAsync` (through `RequestThrottle`, ticks mapped by
`MarketDataTickDtoMapper`) and `IMarketDataManager.GetQuoteSnapshotAsync`, which folds them into a
`QuoteDto`, returns the latest quote of a line already streaming the contract instead of asking
IB, allows `MarketDataConstants.MaxConcurrentSnapshots` (20) at once and fails after
`SnapshotTimeout` (20 s). The watchlist uses it for rows off screen (see
[ui-patterns.md](ui-patterns.md#watchlists)).

Recorded read-only on IB Gateway 4001 (live, protocol 187), 2026-10-28 07:28–07:33 New York
(pre-market), client ids 77–80, for AAPL, MSFT, SPY (SMART) and VOW3 (IBIS):

- **Timing.** All ticks (bid, ask, last, volume, close, exchanges, last time, halted) arrive within
  ~5 ms of the request; `57` comes **10.9–12.0 s** after it, every time: alone, six at once, with
  the live or the delayed market data type. After `57` the request is gone (a cancel is answered
  `300 Can't find EId`).
- **Lines.** Streaming requests were opened until IB refused one with **`101` Max number of
  tickers has been reached**; then six snapshots at once were all served (ticks and `57`), and a
  stream opened while they ran was accepted. **A running snapshot does not hold a market data
  line**, so `GetQuoteSnapshotAsync` doesn't count against `MaxLines` (only its own concurrency
  cap). At the time the account had only **23 free lines** (IB refused the 24th stream every time,
  with no LT running): other sessions of the login, or a lower allowance than the documented 100,
  hold the rest. LT's own limit of 90 can therefore be above what IB grants; IB's `101` is handled
  like LT's limit (the watchlist falls back to snapshots).
- **Refusals.** Generic ticks with a snapshot: `321 … Snapshot market data subscription is not
  applicable to generic ticks` (so the snapshot has no generic tick list). No subscription (VOW3
  IBIS, live or delayed): `10090` then `354`, no ticks, no `57`; the call fails with 10090.

## Wall Street Horizon (calendar)

`IIbkrApiClient.GetWshMetaDataAsync` / `GetWshEventDataAsync` pass WSH's JSON through as text;
`WshCalendarSource` (a singleton `ICalendarSource`) turns it into events, which `ICalendarManager`
merges with the Calendar widget's other sources (`CalendarHelpers`, see
[ui-patterns.md](ui-patterns.md#workspace-ltappworkspace)). WSH is **off by default** (Settings →
Calendar); Nasdaq's public calendars are the default source
([architecture.md](architecture.md#external-data-nasdaq-calendar)). Recorded
read-only on the live account on 2026-10-26 (IBKR.Net `docs/protocol.md`, "Wall Street Horizon").

- **Subscription.** WSH data needs IB's **"Wall Street Horizon Corporate Event Data"** market
  data subscription (IBKR Client Portal → Settings → Market Data Subscriptions). The live account
  doesn't have it: every request — metadata, event data by conId or by filter — is answered
  within milliseconds by **10276 "News feed is not allowed."** for its request id
  (`CalendarConstants.NotSubscribedErrorCodes`). The source turns that into
  `CalendarStatus.NotSubscribed` with a message naming the subscription; the widget shows it in
  its warning line next to the other sources' events, or in place of the list when WSH is the
  only source on. Other refusals, and not being connected, are shown with IB's code the same way.
- **Market-wide filter.** Event data takes a JSON filter instead of a contract:
  `{"country":"All","limit":100,"wshe_ed":"true",…}` — every country, a result limit and one
  `"<tag>":"true"` per event type (`CalendarHelpers.BuildEventFilter`). This is the shape of IB's
  own examples (`wshe_ed` earnings dates, `wshe_bod` board meetings); IB sent it out unchanged
  and refused it only for the missing subscription, so **whether it returns market-wide events,
  and what the answer looks like, is not verified.** The other event type tags come from the
  metadata; without any there, earnings and board meetings are asked for.
- **Limits.** IB's documentation caps a WSH request at 100 events; unverified. The source asks
  one day per request (`MaxEventsPerRequest` = 100, start = end = the day), after the metadata,
  strictly one after another (a `SemaphoreSlim`; every request through the message throttle): a
  month is at most 32 requests (`MaxDaysPerLoad` = 31). A day returning 100 events is marked "may
  have more". Days are kept in memory for 30 minutes (`EventsTtl`); Refresh / F5 asks again. The
  metadata is asked for on every load, since WSH wants it before event data.
- **Answers not seen.** IBKR.Net reads the replies (`104` metadata, `105` events) as request id
  plus one JSON document, the layout the ib_async client decodes. The JSON is read leniently
  (`CalendarHelpers.ParseEvents`): events from the root array or the first list found in an
  object, symbol / company / conId / date / time by their usual names (also inside a nested
  `data` object), `BMO` / `AMC` / `DMH` or the words as before open / after close / during
  market; events without a date go on the day asked for. An answer it can't read is shown as
  "could not be read" with the start of IB's text (`CalendarStatus.Unreadable`), never as an
  empty list. **With a subscription, record the replies first** and tighten the reader to them.
- Dates are New York dates (`CalendarHelpers.GetToday`); weeks start on Monday.

## Trade journal

IB returns only the current day's executions, so the app keeps its own history
(`ITradeJournalManager`, `TradeJournalRecorder` hosted service, `TradeJournalRepository`), filled
from two sources: the socket API (below) and IB Flex statements (all history, and today's
trades a few minutes late — [ibkr-flex.md → Merge rules](ibkr-flex.md#merge-rules)). Both are
deduplicated by IB's execution id, so the same fill from either stays one:

- While connected it asks for executions on connect and every 30 s
  (`AccountConstants.ExecutionsRefreshInterval`, one paced request; there is no app-wide order
  status event to trigger on) and merges them into `trade-journal/fills/<day>.json`, deduplicated
  by ExecId; a commission that arrives later is merged into the saved fill.
- **Fill sound.** Each executions answer is also handed to `IFillAlertManager`
  (`ITradeJournalManager.ExecutionsReceived`, wired by the `FillAlertService` hosted service),
  which plays a short chime when an ExecId it hasn't seen comes in (one sound per answer, however
  many fills; each partial fill has its own ExecId). The first answer after the connection comes
  up or goes down is IB's replay of the day and stays silent, so fills made while disconnected
  never sound. There is no execution event to listen to. Fills of the app's own orders are asked
  for at once: `OrderManager` taps the statuses of the orders it places (`OrderHelpers.OnFillsAsync`:
  more filled quantity, or a first `Filled`) and raises `OrderFilled`, and the journal then asks
  for executions `TradeJournalConstants.FillRefreshDelay` (1 s) later instead of at the next poll
  (`RequestExecutionsRefresh`). Requests coalesce: one pending at most, those made before the
  request starts are covered by it, those made while it runs add one more. This only works while
  a widget still reads the order's statuses. Fills from TWS or other clients wait for the poll,
  up to 30 s. Setting *General → Sounds* (`PlayFillSound`, on by default). Playback is
  `ISoundPlayer` (`LT.App/Services/SoundPlayer.cs`): a generated WAV in the app data folder's
  `sounds/`, played by winmm `PlaySound` (Windows), `afplay` (macOS) or `paplay` / `pw-play` /
  `aplay` (Linux), off the calling thread and never throwing. The same player plays the price
  alert alarm (`PlayAlarm`, see *Price alerts* in ui-patterns.md).
- It follows each managed account's `reqPnL` (up to `TradeJournalConstants.MaxPnlAccounts`) on
  the shared subscription and keeps DailyPnl as a curve: at most one point per account every
  10 s (a newer value replaces the point inside that window, so short spikes can be missed),
  written to `trade-journal/pnl/<day>.json` every minute and on disconnect / exit. A restart the
  same day continues the curve.
- Days are New York dates (`TradeJournalConstants.TradeDayTimeZoneId`), so a US session and its
  after-hours stay on one day for a user in Europe. IB resets DailyPnl on its own schedule, which
  may not be New York midnight.
- Trades are flat-to-flat round trips per account and contract (FIFO lots,
  `TradeMatchingHelpers`), over every recorded day, so a position carried overnight closes against
  its recorded entry; fills from before recording started are unknown (a sell then looks like a
  short). Amounts are summed as reported, regardless of currency.

## Quirks we have hit

- **Notices vs errors.** IBKR.Net treats codes 2100–2199 (farm status, 2150 on position P&L) and
  `10167` (delayed data shown) as notices; any other code with a request id fails that request.
  `10089/10090` (subscription needed) therefore end a quote stream on a `Live` market data type
  without a subscription. Quote stream and snapshot errors reach LT as `IbkrRequestException`
  with IB's code (the message keeps the `[code] text` form); `101` (no free line) is what callers
  react to. We default to `Live` (1); `Delayed` (3), picked in Settings, returns
  live data where you are subscribed and delayed otherwise.
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
- **Account P&L (`reqPnL`) is one subscription per account**, shared by every caller
  (`AccountPnlFeed` in `LT.Ibkr`): the Account widget, the account performance recorder and the
  trade journal follow the same one. It opens with the first subscriber, is cancelled when the last leaves, and
  a late subscriber gets the latest value first.
- **No account history.** The socket API has no historical NAV or P&L. The Account performance
  widget records today live (`IAccountPerformanceManager`) and imports past days from IB Flex
  ([ibkr-flex.md](ibkr-flex.md)); without Flex it shows only what the app recorded while
  connected.
- **Real-time bars need a live subscription** — there is no delayed `reqRealTimeBars`. The chart
  keeps historical bars and logs "real-time bars unavailable".
- **`reqRealTimeBars` only does 5-second bars.** For TWS-like speed, the chart's "Every trade"
  live update modes (`ChartLiveUpdateMode.Quotes` / `QuotesThrottled`, see
  [ui-patterns.md](ui-patterns.md#charts)) also open a quote line through
  `IMarketDataManager.SubscribeQuotesAsync` (one of the 100 lines per chart) and move the last
  bar to each last-trade price (the midpoint for forex, which has no trades). The 5-second bars
  keep running: they carry the volume and keep high / low exact (IB's last-price ticks are
  sampled). Without a free line, or when the quote stream fails, the chart stays on 5-second bars
  and logs "quotes unavailable, using 5-second bars".
- <a id="tick-by-tick"></a>**Tick-by-tick (`reqTickByTickData`) is limited to 5 subscriptions
  per account** (error 10190 beyond). `IMarketDataManager.SubscribeTickByTickAsync` shares
  them like quote lines: subscriptions of the same contract (conId and routing exchange,
  `MarketDataShareHelpers.GetTickByTickShareKey`) and tick type read one IB subscription, opened
  by the first, cancelled when the last leaves, an IB error or end ending all of them; a joiner
  gets the ticks from then on (no replay). Only an IB subscription counts
  (`MarketDataConstants.MaxTickByTick`, `ActiveTickByTick`): a sixth contract / tick type is
  refused before asking IB, another chart of a streaming ticker still joins. So all charts of
  SPY on tick-by-tick take one, and Settings → General → *Ticker settings* picks which tickers
  get tick-by-tick ([ui-patterns.md](ui-patterns.md#charts)). The chart's
  tick-by-tick modes ask for `Last` (no odd lots; `AllLast` would add odd lots flagged
  unreported) and `MidPoint` for forex — `Last` on forex fails with 10189. Other refusals: 10189
  no permission, 354 not subscribed, 200 unknown contract; all end the stream with an
  `IbkrRequestException` carrying the code. Tick times are IB's, in whole seconds (many ticks
  share one). Any failure falls the chart back to quotes, then to 5-second bars, logged as
  "tick-by-tick unavailable, using quotes". A chart showing footprint bars takes two more
  (`AllLast` for every trade, odd lots included like the historical trades, and `BidAsk` to tell
  each trade's side; `IFootprintManager.SubscribeTradesAsync`, the quotes only once trades flow so
  a last free one goes to the trades); footprints of one contract share both, so two SPY
  footprint charts on tick-by-tick take three (`Last`, `AllLast`, `BidAsk`), not six. Without the trades the chart says "not live" and newer bars
  show as candles; without the quotes the sides go by the tick rule.
- <a id="historical-ticks"></a>**Historical ticks (`reqHistoricalTicks`) answer at most 1000 a
  request** (1001 → `10187` "Max length exceeded"; no permission → `10187` too, unknown contract
  `200`). IB returns whole seconds, so a page of 1000 holds 1000–1100 ticks; the end is
  exclusive, so paging back with the oldest tick's time as the next end neither repeats nor skips
  one. Trades include odd lots (flagged unreported, like `AllLast`) but carry no bid / ask, so the
  footprint backfill tells the aggressor by the tick rule. AAPL trades about 1000 times in 30 s
  at 10:50 New York; SPY 1000 times in 3–5 s in the last minutes before the close and in about a
  minute mid-afternoon (~220,000 trades, 213 pages, from 13:05 to 16:00 on 2026-10-29; its 15:55
  bar alone took 34 pages). Prints include late and out-of-sequence reports far from the market
  (40 shares at 769.65 in a 767.2–767.7 bar), which IB's bars leave out of their high / low.
  **Pacing, measured read-only on IB Gateway 4001 (live), 2026-10-29 after the close:** 213 SPY
  TRADES pages of 1000 sent back to back, one at a time, all answered, **no pacing error** (no
  162 / 420 / 366, no notice at all). The first ~10 come back in 0.2–0.5 s each; then IB holds the
  answers and releases them on a **6-second clock** (every held answer arrived at the same phase
  of a 6 s cycle): one to seven a tick, an occasional 12–18 s gap; sustained **~22 pages a
  minute** (p50 0.29 s, p90 6.0 s, max 18 s). Historical bar requests for other contracts sent
  meanwhile (1 D of 1 and 5 min, every 12 s) were answered in 0.17–1.5 s as usual: the holding
  is the ticks' own. **Several at once are worse:** four in flight (distinct ends) got 40 pages
  in 222 s, answers held up to ~100 s, against 40 in ~73 s one at a time. So `IbkrApiClient`
  sends historical ticks one at a time (`HistoricalPacing.WaitTicksAsync` / `ReleaseTicks`)
  with no pacing window of its own — the earlier 60 / 10 min window held each page ~10 s once 60
  were sent. A footprint chart pages at most `FootprintConstants.MaxBackfillRequests` (1200)
  per load (a whole SPY session is ~500). Because of that pace, every page (and every live
  trade) goes into the footprint trade cache, so a chart reopened later asks IB only for what
  it lacks — see [ui-patterns.md → Footprint bars](ui-patterns.md#charts). Footprint pages
  are asked for outside regular hours too (`useRTH` 0) whatever the chart shows, so what is
  cached is every trade and serves RTH and ETH charts alike; an RTH chart's page then reaches
  only the pre-market trades just before 9:30 (its next page goes straight to the previous
  session's last bar, not through the night).
- <a id="fundamentals-10358"></a>**Generic tick 258 (fundamental ratios) without the
  fundamentals entitlement rejects the whole `reqMktData`**: `10358 Fundamentals data is not
  allowed` comes back for the request id within milliseconds and no tick follows, not even
  bid/ask/last (recorded read-only on IB Gateway 4001, protocol 178, 2026-10-26, for AAPL, SPY,
  VOW and EUR.USD). `reqFundamentalData` (`52`) is refused the same way: every report type
  (`ReportSnapshot`, `ReportsFinSummary`, `ReportsFinStatements`, `ReportRatios`, `RESC`,
  `CalendarReport`, `ReportsOwnership`) got `10358` within a millisecond for AAPL (recorded
  read-only at protocol 187, 2026-10-29; request `52`, `2`, reqId, conId, symbol, secType,
  exchange, primaryExchange, currency, localSymbol, report type, `0`, `""`). The data reply was
  never seen, so IBKR.Net doesn't have the call; the Overview widget's Financials tab reads SEC
  filings instead (see [architecture.md](architecture.md#external-data-sec-edgar)). Never add `258` to a shared quote line. That is why the watchlist's market cap
  is **SEC shares outstanding × IB's last price** instead: `ISharesOutstandingManager` looks up
  US stocks (STK in USD) at SEC EDGAR (`LT.Sec`, see
  [architecture.md](architecture.md#external-data-sec-edgar)) and the row multiplies by every
  flushed price (last, or the previous close before the first trade).

## How LT.Ibkr applies the rules

- `RequestThrottle` (sliding window) paces every request: 40 msg/s overall, and
  `HistoricalPacing` holds historical requests for bars of 30 s or less to 60 per 10 minutes and
  sends historical ticks one at a time (IB paces those itself, see
  [Historical ticks](#historical-ticks)).
  Requests wait instead of tripping IB's disconnect. Chart bars (1 min and up) are not held to
  the 60 / 10 min window: IB doesn't enforce it for them (75 distinct 15-min requests in 70 s,
  recorded read-only on IB Gateway 4001, 2026-10-26, all answered; IB slowed its answers to
  ~6 s each after ~55). Holding them queued chart loads for minutes behind other charts.
- `MarketDataManager` counts lines and refuses a subscription above 90 (headroom under IB's 100).
  **Quote subscriptions share lines**: those of the same contract (conId + routing exchange),
  generic ticks and market data type (`MarketDataShareHelpers.GetShareKey`;
  `IIbkrApiClient.MarketDataType` is the one last set, so after a change new subscribers open a
  fresh line) read one IB stream. The first subscriber opens it and takes the line, later ones
  get the latest cumulative quote at once and then every update, cancelling one leaves the others
  streaming, and the last one leaving cancels the IB request and frees the line. An IB error (354,
  10089, 101 …) or the end of IB's stream ends every subscriber with it; the next subscriber opens
  a new one. So AAPL in a watchlist, a chart and an alert is one line, listed with every owner.
  Snapshots (`GetQuoteSnapshotAsync`) take no line, see [Quote snapshots](#quote-snapshots).
  Chart price alerts hold one line per contract with an active alert while connected (at most
  20, `PriceAlertConstants.MaxWatchedContracts`), shared with any other subscriber of it.
- The watchlist streams only the rows on screen plus a buffer and refreshes the others with
  snapshots, a few at a time, leaving `WatchlistReservedLines` (10) free; see
  [ui-patterns.md](ui-patterns.md#watchlists).
- `ChartManager` turns the chart's period into the IB duration (`ChartPeriodHelpers.GetRange`).
  The period is a zoom preset, not a filter: the request covers at least the timeframe's own
  span (Auto's: 1m 1 D, 5m 5 D, 15m 10 D, 1h 1 M, 1D 1 Y, 1W 5 Y), and a longer period its span
  plus a day of slack, rounded up to 2/8/32/93/185/365 D or whole years (end is always now),
  never longer than the timeframe's longest period (1m 7 days, 5m 1 month, 15m 3 months, 1h 1
  year — IB serves long spans of small bars slowly in one request). Every bar IB returns is kept;
  the period's start only says where the chart's view begins. The duration is part of the cache
  key (except the timeframe's own, Auto).
- **Older history is paged** (`IChartManager.GetOlderBarsAsync`): when a chart's view nears its
  oldest bar, one request ends there (`endDateTime`) for a page of
  `ChartTimeframeHelpers.GetPageDuration` (1m 1 W, 5m 1 M, 15m 2 M, 1h 6 M, 1D 2 Y, 1W 10 Y — a
  few thousand bars at most, never under a week so a weekend or holiday is never an empty page).
  One page at a time per chart, through the same throttle and the `ChartBars` cache (keyed by
  where the page ends; empty pages aren't cached). Bars at or after the end are dropped. `162`
  with "no data" (or an empty answer) is the start of the contract's history: paging stops until
  the chart reloads. The longest-period cap applies to a single request only; paging goes past it
  page by page, up to `ChartConstants.MaxLoadedBars` (50,000) per chart. A failed page is logged
  and retried no sooner than 10 s, and only when the view moves near the edge again.
- `ChartManager` serves bars from the data cache while fresh (1 min intraday, 30 min daily+) and
  falls back to stale cache when IB is unreachable. Contract searches (3 days) and option chain
  structure (1 day) are cached too, which saves contract-details requests; see
  [architecture.md → Data cache](architecture.md#data-cache).
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
  subscribe what's visible (done: on-screen rows stream, the rest get snapshots, which hold no
  line).
- **Historical data pacing:** for bars of 30 seconds or less, no more than 60 requests per 10
  minutes and identical requests within 15 seconds are rejected. Larger bars are only slowed down
  by IB (soft pacing). Cache bars locally.
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
- `ghcr.io/gnzsnz/ib-gateway` (Docker, auto-login via IBC, 2FA off on paper) — useful for
  integration tests later.
- Unit tests in `LT.Tests` never connect: managers depend on `IIbkrApiClient`, which tests fake.

## Alternatives considered

- **TwsApi (UtopleMan/tws-api-dotnet)** — used until 2026-10-24. It wraps IB's own C# client,
  which is GPL-3.0 from 10.50 and non-commercial before; bundling it would bind this app's
  license, and a fork can't build it (IB's repo is private).
- **IB's `CSharpAPI` directly** — same license problem.
- **Client Portal Web API only** — no Gateway socket needed, but higher latency, session
  tickling, weaker streaming.
