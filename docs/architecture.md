# Architecture & Project Structure

The solution follows the same layering as the **Termodom ecosystem** (`TD.[Domain].Api / Contracts /
Domain / Repository / Client / Fe`) and uses the in-house **LSCore** packages
(`LSCore.DependencyInjection`, `LSCore.Validation`). The only real difference: there is no HTTP
boundary — the "API" and the "FE" are the same Avalonia process, so the controller role is played
by ViewModels.

## Termodom → LimitlessTrading mapping

| Termodom | Role | LimitlessTrading |
|---|---|---|
| `TD.X.Contracts` | DTOs, Requests, Enums, Interfaces, Helpers, Constants | `LT.Contracts` |
| `TD.X.Domain` | Managers (business logic), Validators | `LT.Domain` |
| `TD.X.Repository` | Data access (EF + LSCore.Repository) | `LT.Repository` (local persistence — JSON for now) |
| `TD.X.Client` / `Domain/ApiClients` | Typed client for an external API | `LT.Ibkr` — the only project that references `IBKR.Net` |
| `TD.X.Api` | `Program.cs`, DI, controllers | `LT.App` — `Program.cs`, DI, ViewModels |
| `TD.X.Fe` (`pages/`, `widgets/`, `zStore/`, `helpers/`) | UI | `LT.App` (`Pages/`, `Widgets/`, `Helpers/`) |
| `TD.X.Tests` | Unit tests | `LT.Tests` |

## Layout

```
LimitlessTrading.slnx
src/
├── LT.Contracts/                  # no dependency on Avalonia or IBKR.Net
│   ├── Constants/                 # limits, defaults, paths (ConnectionConstants, AppConstants)
│   ├── Dtos/<Feature>/            # what managers return to the UI
│   ├── Entities/                  # what repositories persist
│   ├── Enums/                     # + Enums/ValidationCodes/
│   ├── Helpers/                   # static helper classes (no private helpers in managers)
│   ├── Interfaces/
│   │   ├── IApiClients/           # IIbkrApiClient — the broker boundary; IFlexApiClient — IB Flex
│   │   ├── IManagers/
│   │   ├── IProcesses/            # IGatewayProcess — child processes we own (IB Gateway)
│   │   └── IRepositories/
│   └── Requests/<Feature>/
├── LT.Domain/
│   ├── Managers/                  # IConnectionManager → ConnectionManager, …
│   ├── NewsSources/               # INewsSource implementations (IbkrNewsSource)
│   ├── CalendarSources/           # ICalendarSource implementations (WshCalendarSource, NasdaqCalendarSource)
│   └── Validators/                # LSCoreValidatorBase<TRequest>
├── LT.Repository/
│   └── Repositories/              # SettingsRepository (JSON in the user's app-data folder)
├── LT.Sec/                       # SecApiClient + SecJsonParser: SEC EDGAR (shares outstanding)
├── LT.Nasdaq/                    # NasdaqApiClient + NasdaqCalendarParser: nasdaq.com calendars
├── LT.Ibkr/
│   ├── DtoMappings/               # IBKR.Net types ↔ LT.Contracts DTOs
│   ├── Gateway/                   # GatewayProcess — starts / stops IB Gateway's launcher
│   ├── Flex/                      # FlexApiClient (HTTPS, no IBKR.Net) + FlexStatementParser
│   ├── RequestThrottle.cs         # IB pacing (messages/s, sliding window)
│   ├── HistoricalPacing.cs        # historical requests / 10 min, bars of 30 s or less only
│   └── IbkrApiClient.cs           # implements IIbkrApiClient over IBKR.Net (+ order connection)
├── LT.Indicators.Sdk/             # Series, Ta, Indicator — what indicators are written against;
│                                  #   references nothing (scripts compile against it alone)
├── LT.Indicators.Scripting/       # IndicatorEngine (IIndicatorEngine): built-ins, ScriptCompiler
│                                  #   (Roslyn, ban list, time guard), collectible load contexts
├── LT.App/                        # Avalonia (Fluent, dark)
│   ├── Program.cs                 # composition root (generic host + LSCore DI)
│   ├── App.axaml                  # themes, global styles
│   ├── Shell/                     # MainWindow: icon toolbar (hamburger menu), page host, status bar
│   ├── Pages/<Page>/              # a page: its widgets placed in a workspace (TradingPage)
│   ├── Workspace/                 # widget wrappers: center split layout, side strips, floating windows
│   ├── Widgets/<Part>/            # shell parts that aren't workspace widgets: ConnectionBar,
│   │                              #   Gateway and Settings windows
│   └── Services/                  # hosted services (ConnectionWatchdog)
├── Widgets/                       # "Widgets" solution folder
│   ├── LT.Widgets.Core/           # what widget projects build on (namespaces kept as LT.App.*)
│   │   ├── Stores/                # app-wide UI state shared by widgets (≈ Termodom zStore)
│   │   ├── ViewModels/            # ViewModelBase, ConnectedViewModelBase
│   │   ├── Controls/              # shared controls (ContractSearchBox, TickerTitle)
│   │   ├── Helpers/               # UI-only helpers (formatting, LatestValue, error messages)
│   │   └── Workspace/             # IWidgetDefinition, ILinkable, IWidgetState, WidgetTitles
│   └── LT.Widgets.<Widget>/       # one per workspace widget: <Widget>View.axaml,
│                                  #   <Widget>ViewModel.cs, <Widget>Widget.cs (IWidgetDefinition)
└── LT.Tests/                      # xUnit v3, never connects to IB
```

## Dependency direction

```
LT.App ──► LT.Domain ──► LT.Contracts ◄── LT.Repository
   │  │                       ▲  ▲
   │  └──► LT.Ibkr ───────────┘  │   (LT.Ibkr ──► IBKR.Net)
   ├──► LT.Indicators.Scripting ─┤   (──► LT.Indicators.Sdk, Microsoft.CodeAnalysis)
   └──► LT.Widgets.<Widget> ──► LT.Widgets.Core
```

- `LT.Contracts` references nothing of ours, and **never** `IBKR.Net` or Avalonia.
- `LT.Domain` depends only on `LT.Contracts` interfaces — it doesn't know IBKR is behind
  `IIbkrApiClient`, so managers are unit-testable with fakes.
- `LT.Indicators.Scripting` plays the same role for indicators: SDK types (`LT.Indicators.Sdk`)
  stay in it and the SDK; `IIndicatorEngine` speaks DTOs ([indicators.md](indicators.md)).
- `LT.Ibkr` maps every IB type to a DTO at the boundary. IB types never reach the Domain or UI.
  It also holds the Flex Web Service client (`LT.Ibkr/Flex`): IB's other API, plain HTTPS + XML,
  kept next to the socket client so every "talks to IB" piece is in one project; it uses no
  IBKR.Net type ([ibkr-flex.md](ibkr-flex.md)).
- Widget projects reference only `LT.Widgets.Core` (and through it `LT.Contracts`): they reach
  data through manager interfaces, never `LT.Domain`, `LT.Ibkr` or each other. App-wide styles
  and resources (`App.axaml`) still apply to them; their `StaticResource`s resolve at runtime.
  Code moved out of `LT.App` kept its `LT.App.*` namespaces (`LT.App.Widgets.<Widget>`,
  `LT.App.Stores`, …).
- `LT.Sec` is the typed client of SEC EDGAR (public JSON over HTTPS, no IB): the second external
  API, in its own project like `LT.Ibkr`, referencing only `LT.Contracts`. `LT.Nasdaq` (Nasdaq's
  calendar JSON) is the third, the same way.
- `LT.App` is the composition root: it references every layer only to wire DI. ViewModels inject
  **managers** (`IConnectionManager`), never repositories or `IIbkrApiClient` directly — same rule as
  Termodom controllers.

## Dependency injection

`Program.cs` builds a generic host (`Host.CreateApplicationBuilder`) and calls
`builder.AddLSCoreDependencyInjection("LT")`, which scans every `LT*.dll` and registers
`Foo : IFoo` as **transient** plus all `LSCoreValidatorBase<T>` validators. Then:

- Stateful services (`IIbkrApiClient`, `IConnectionManager`, `IMarketDataManager`,
  `IMarketDepthManager`, `ICalendarManager`, `IMessageLogManager`, `ISettingsManager`, `INewsManager`, both
  repositories) are re-registered as **singletons after** the scan — the last registration wins.
- Calendar sources (`ICalendarSource`) are registered the same way as news sources below, in the
  order the calendar merges them (`WshCalendarSource`, then `NasdaqCalendarSource`); the Nasdaq
  client is a singleton with its own `HttpClient` like the SEC one.
- News sources (`INewsSource`) don't match the `Foo : IFoo` convention, so each is registered
  explicitly (`AddSingleton<INewsSource, IbkrNewsSource>()`); `NewsManager` takes all of them
  (`IEnumerable<INewsSource>`). A new source (RSS, Benzinga …) is one class and one line there.
- `TimeProvider.System` is registered so managers that depend on time (reconnect backoff, bar
  cache TTL) are testable with `FakeTimeProvider`.
- The host is started (`host.Start()`) so `ConnectionWatchdog` (a `BackgroundService`) runs the
  heartbeat; it's stopped after the Avalonia lifetime ends. `TradeJournalRecorder` (also a
  `BackgroundService`) runs `ITradeJournalManager.RecordAsync` while connected.
  `FlexImportScheduler` runs `IFlexImportManager.RunAsync` for the app's lifetime (IB Flex
  back-fill, daily Activity and 5-minute Trade Confirmation imports).
- `IFlexApiClient` is registered with a factory (`FlexApiClient.CreateHttpClient()` — timeout and
  the User-Agent IB requires — plus `TimeProvider`) as one singleton, so one `HttpClient` and one
  pacing state serve every Flex request.
- `ISecApiClient` is registered the same way (`SecApiClient.CreateHttpClient()`: timeout, gzip,
  and the User-Agent with a contact address the SEC requires), one singleton so one request gate
  serves the app. `ISharesOutstandingManager` is a singleton so lookups are shared.
- Indicators: `IIndicatorEngine`, `IIndicatorScriptRepository` and `IIndicatorManager` are
  singletons; `IndicatorScriptHost` (hosted service) runs `IIndicatorManager.RunAsync` — sets up
  the scripts folder, compiles the scripts and recompiles them on change.
- `UpdateScheduler` (also a `BackgroundService`) clears the previous version's folder left by an
  update, then has `IUpdateManager` check GitHub Releases ~10 s after start and every 6 h.
  `AppUpdater` (`LT.App/Services`) does the platform work: download, SHA-256 check against
  GitHub's asset digest, unpack next to the app folder as `.LimitlessTrading.update`, swap the
  folders (the old one becomes `.LimitlessTrading.old`) and start the new executable from a shell
  that waits for this process to exit. While the update manager is `Restarting`, shutdown keeps IB
  Gateway running so the new version reuses it. Builds from source have version `0.0.0-dev`
  (`Directory.Build.props`; the release workflow passes `-p:Version`): they never check by
  themselves and can't install.
- Patch notes (menu → *Patch notes*) come from the same GitHub Releases: `IPatchNotesManager`
  (singleton) fetches the release list through `IAppUpdater.GetReleasesAsync`, parses each body
  with `PatchNotesHelpers.ParseEntries` (GitHub's generated "What's Changed" lines, author and
  link dropped, PR number kept) and caches the raw releases in `patch-notes.json`
  (`IPatchNotesRepository`), so the window opens at once and offline; it refreshes on every open.
  The same file holds the last version run: the first start on a newer one opens the patch notes
  after the log-in dialog (never on the very first start or from a source build).
- `host.UseLSCoreDependencyInjection()` must run right after `Build()` — `request.Validate()`
  resolves validators through it.
- ViewModels have no interfaces, so they're registered explicitly as transient.

> **Known issue — self-contained and Windows publish.** The LSCore scan calls `Assembly.LoadFrom`
> on *every* `*.dll` in the base directory before filtering by name. A self-contained publish puts
> the runtime's own DLLs there (`LoadFrom` of `System.Private.CoreLib.dll` throws
> `FileNotFoundException`), and a Windows publish adds native DLLs (`libSkiaSharp.dll` …:
> `BadImageFormatException`). `dotnet run` and a framework-dependent Linux publish are unaffected,
> so releases are framework-dependent Linux builds for now (`.github/workflows/release.yml`). Fix
> in LSCore (filter file names by the prefix before loading, or skip files that fail to load) to
> ship self-contained and Windows builds.

## Rules carried over from Termodom

1. **Update interfaces with implementations** — interface in `Contracts/Interfaces/...` first.
2. **No private helpers in managers** — reusable logic goes to `Contracts/Helpers/` as
   `public static class`, dependencies passed as parameters.
3. **Validation** — requests are validated in the manager with `request.Validate()`
   (LSCore.Validation). Messages live in `Enums/ValidationCodes/*ValidationCodes` with
   `[LSCoreValidationMessage]`; limits in `Constants/`. No inline user-facing strings in validators.
4. **Enums that get persisted: add values at the END only.**
5. **Mapping at boundaries** — entity ↔ DTO and IB type ↔ DTO conversions live in `DtoMappings/`.

## Stores

`src/Widgets/LT.Widgets.Core/Stores` are singleton `ObservableObject`s for UI state several widgets share — the role
Zustand stores play in Termodom FEs:

- `AccountStore` — managed accounts and the selected one (persisted); positions, orders,
  executions and the account panel follow it.
- `LinkGroupStore` — the contract of each link group (`LinkGroup`: ten colors). A
  linkable widget holds a `ContractLink` (its group, publish / receive); see
  [ui-patterns.md → Link groups](ui-patterns.md#link-groups).
- `DisplayTimeStore` — the zone every shown time is in (Settings → General → Time zone); see
  [ui-patterns.md → Display time zone](ui-patterns.md#display-time-zone-settings--general--time-zone).
- `SettingsNavigator` / `OrderTicketNavigator` — let a widget ask the main window to open the
  Settings dialog on a page / an order ticket popup pre-filled with an `OrderDraftDto`.

## Persistence

`LT.Repository` stores settings (connection, general settings, layout, watchlist, selected
account) in `settings.json`, the data cache in `cache/<CacheType>/<sha256 of key>.json`,
recorded account performance in `account-history/<account>.json` (one snapshot per trading day,
see [ui-patterns.md → Account performance](ui-patterns.md#account-performance)) and the trade
journal (recorded fills and intraday P&L, one file per trading day) in `trade-journal/fills/` and
`trade-journal/pnl/`. Indicator scripts are `indicators/*.cs` (plus the editor project and an
SDK copy, [indicators.md](indicators.md#script-files)). IB Flex imports go into those same files (account days marked
`Source = Flex`, fills carrying their Flex `TradeId`); the import's progress is in
`flex-import.json`, and the Flex token in `settings.json` (`SettingsEntity.Flex`, plain text).
Chart price alerts (active and fired) are in `price-alerts.json` (`PriceAlertRepository`, apart
from `settings.json` so an alert firing on a price stream never races a settings save). None of
these are part of the cache, so Clear cache and the size limit never touch them; nor do they touch
the Notes widgets' text, `notes.json` (one entry per note id). All live under the OS app-data folder
(`Environment.SpecialFolder.ApplicationData`: `%APPDATA%\LimitlessTrading` on Windows,
`~/.config/LimitlessTrading` on Linux and macOS). When we need queryable local data (layouts,
journal) the plan is SQLite + EF Core behind the same repository interfaces.

### External data: SEC EDGAR

The watchlist's market cap needs shares outstanding, which IB only has behind the fundamentals
entitlement (tick 258, [error 10358](ibkr-tws-api.md#fundamentals-10358)). `SharesOutstandingManager`
gets them from SEC EDGAR instead: `company_tickers.json` maps the ticker to a CIK (loaded once per
run; IB's `BRK B` is the SEC's `BRK-B`), then `companyconcept/…/dei/EntityCommonStockSharesOutstanding`
gives the cover-page counts; when that has nothing usable, `companyfacts` (several MB) adds
`us-gaap:CommonStockSharesOutstanding`, which multi-class companies (Alphabet) report undivided.
`MarketCapHelpers.PickSharesOutstanding` takes the newest count from a 10-K / 10-Q no older than
400 days: 20-F / 40-F counts are ordinary shares that an ADR's price doesn't match, and old counts
(Berkshire's cover page stops in 2011) would be wrong.

Results are kept in `shares-outstanding.json` (`SharesOutstandingRepository`, outside the data
cache, so the Cache settings don't apply) keyed by SEC ticker: a count is looked up again after
7 days, "nothing usable" after 3. One lookup per ticker runs at a time; `SecApiClient` sends one
request at a time, 125 ms apart (the SEC allows 10/s). After a failure no lookup starts for
5 minutes, the failure is logged once (source "SEC") and a stale count is used if there is one.

### External data: Nasdaq calendar

The Calendar widget's default source (IB's Wall Street Horizon needs a paid subscription) is
nasdaq.com's own calendar pages' JSON, `https://api.nasdaq.com/api/calendar/<calendar>?date=yyyy-MM-dd`
(`INasdaqApiClient` → `LT.Nasdaq/NasdaqApiClient`, read by `NasdaqCalendarParser`, used by
`NasdaqCalendarSource`). **Unofficial and undocumented:** no key or account, no published limits
or terms for API use, and it may change or disappear without notice — then the widget names
Nasdaq's failure in its warning line and the other source still shows. Checked on 2026-10-27,
all four answer:

| Calendar | Rows | Date semantics |
|---|---|---|
| `earnings` | `data.rows`: `symbol`, `name`, `time` (`time-pre-market` / `time-after-hours` / `time-not-supplied`), `epsForecast`, `fiscalQuarterEnding`, `marketCap` … | the day asked; ~170–620 rows on a busy day |
| `dividends` | `data.calendar.rows`: `symbol`, `companyName`, `dividend_Ex_Date`, `payment_Date`, `record_Date`, `dividend_Rate` (a number) … | ex-date = the day asked |
| `splits` | `data.rows`: `symbol`, `name`, `ratio` ("3 : 1"), `executionDate` | **ignores the day:** every split known from it on, each on its own effective date |
| `economicevents` | `data.rows`: `gmt` (a clock time, US Eastern by the look of it despite the name, or "All Day"), `country`, `eventName`, `actual`, `consensus`, `previous` (`&nbsp;` for none), `description` | **a day behind:** `date=D` lists D−1's releases, so D+1 is asked for (`NasdaqConstants.EconomicDateOffsetDays`) |

Every answer is `{"data":…,"message":…,"status":{"rCode":200,"bCodeMessage":[…]}}`: a day without
rows has `rows: null` (dividends also a `message`), a day Nasdaq has no record of `data: null` and
business code 1002 — both are an empty day; another code (1011 "Bad or No parameter") is shown as
Nasdaq's refusal, and an answer in another shape as "can't read", never as an empty list. Nasdaq
doesn't answer at all without a browser `User-Agent` (the request hangs), so the client sends one
with a browser `Accept` (`NasdaqConstants.UserAgent`).

Requests (`CalendarHelpers.GetNasdaqRequests`): splits once per load, from its first day; then
day by day earnings and dividends (weekdays only — no rows on the weekend days tried) and economic
releases (every day; there are weekend releases and holidays) — only the calendars the widget's
filter shows. A week of everything is 18 requests, a month at most ~80. `NasdaqApiClient` sends
one at a time, 250 ms apart; `NasdaqCalendarSource` keeps each answer for 30 minutes
(`CalendarConstants.EventsTtl`, Refresh asks again) and stops at the first failed request.

### Data cache

One cache for everything fetched from IB that is worth keeping, with a section in Settings
(Cache): a master switch, one switch per `CacheType`, a maximum size (MB,
`CacheConstants.MaxSizeMbMin`–`MaxSizeMbMax`, default 500) and Clear cache.

- `ICacheRepository` (`FileCacheRepository`, singleton) keeps one JSON entry per type and key,
  `{ Key, FetchedAt, Value }`. A file's write time is its `FetchedAt`; `Trim` deletes the least
  recently written files until the total fits. Unreadable files are misses; write failures are
  ignored (it is a cache).
- `ICacheManager` is what managers use. It applies the switches (a type that is off is neither
  read nor written, checked on every call, so switching off mid-request is harmless), trims
  after every write, and `GetOrFetchAsync` serves fresh entries, fetches expired ones and falls
  back to the expired copy with `IsStale` set when IB fails (never on cancellation).
  `CacheMaintenance` (hosted service) deletes the old `bar-cache` folder and trims on start.
- What is cached (TTLs in `CacheConstants` / `ChartTimeframeHelpers.GetCacheTtl`):

  | Type | What | TTL | When IB fails |
  |---|---|---|---|
  | `ChartBars` | historical bars | 1 min intraday, 30 min daily+ | stale copy, "Cached HH:mm" |
  | `Contracts` | `SearchAsync`, `SearchSymbolAsync` (so `ResolveTickerAsync`); non-empty answers only | 3 days | expired copy |
  | `OptionChains` | expirations, strike lists (contracts only, never prices or greeks) | 1 day | expired copy |
  | `Executions` | fills per account | 10 s | stale copy, marked in the widget |
  | `Snapshots` | last-known positions, open orders, account summary, per account | — | shown only while disconnected or failing |

- **Snapshots are for looking at only.** The Positions, Orders and Account widgets show them while
  disconnected (or when the live request fails before anything live arrived) with a warning
  "Cached HH:mm · account · not live", and drop them as soon as live data arrives. They are keyed
  per account (`CacheHelpers.GetAccountKey`), so another account's data never shows; before the
  first login `AccountStore.SnapshotAccount` falls back to the account selected last run.
  Snapshot orders are never cancellable, snapshot positions don't drive the link group, and no
  order path (`ValidateOrder`, `PlaceOrderAsync`) reads the cache.
