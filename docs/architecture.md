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
│   │   ├── IApiClients/           # IIbkrApiClient — the broker boundary
│   │   ├── IManagers/
│   │   ├── IProcesses/            # IIbcGatewayProcess — child processes we own (IBC + Gateway)
│   │   └── IRepositories/
│   └── Requests/<Feature>/
├── LT.Domain/
│   ├── Managers/                  # IConnectionManager → ConnectionManager, …
│   └── Validators/                # LSCoreValidatorBase<TRequest>
├── LT.Repository/
│   └── Repositories/              # SettingsRepository (JSON in the user's app-data folder)
├── LT.Ibkr/
│   ├── DtoMappings/               # IBKR.Net types ↔ LT.Contracts DTOs
│   ├── Ibc/                       # IbcGatewayProcess — starts / stops IB Gateway via IBC
│   ├── RequestThrottle.cs         # IB pacing (messages/s, historical requests / 10 min)
│   └── IbkrApiClient.cs           # implements IIbkrApiClient over IBKR.Net (read-only)
├── LT.App/                        # Avalonia (Fluent, dark)
│   ├── Program.cs                 # composition root (generic host + LSCore DI)
│   ├── App.axaml                  # themes, global styles
│   ├── Shell/                     # MainWindow: icon toolbar (hamburger menu), page host, status bar
│   ├── Pages/<Page>/              # a page: its widgets placed in a workspace (TradingPage)
│   ├── Workspace/                 # widget wrappers: center split layout, side strips, floating windows
│   ├── Widgets/<Widget>/          # <Widget>View.axaml + <Widget>ViewModel.cs
│   ├── Stores/                    # app-wide UI state shared by widgets (≈ Termodom zStore)
│   ├── Services/                  # hosted services (ConnectionWatchdog)
│   ├── ViewModels/                # ViewModelBase, ConnectedViewModelBase
│   └── Helpers/                   # UI-only helpers (formatting, LatestValue, error messages)
└── LT.Tests/                      # xUnit v3, never connects to IB
```

## Dependency direction

```
LT.App ──► LT.Domain ──► LT.Contracts ◄── LT.Repository
   │                          ▲
   └──► LT.Ibkr ──────────────┘   (LT.Ibkr ──► IBKR.Net)
```

- `LT.Contracts` references nothing of ours, and **never** `IBKR.Net` or Avalonia.
- `LT.Domain` depends only on `LT.Contracts` interfaces — it doesn't know IBKR is behind
  `IIbkrApiClient`, so managers are unit-testable with fakes.
- `LT.Ibkr` maps every IB type to a DTO at the boundary. IB types never reach the Domain or UI.
- `LT.App` is the composition root: it references every layer only to wire DI. ViewModels inject
  **managers** (`IConnectionManager`), never repositories or `IIbkrApiClient` directly — same rule as
  Termodom controllers.

## Dependency injection

`Program.cs` builds a generic host (`Host.CreateApplicationBuilder`) and calls
`builder.AddLSCoreDependencyInjection("LT")`, which scans every `LT*.dll` and registers
`Foo : IFoo` as **transient** plus all `LSCoreValidatorBase<T>` validators. Then:

- Stateful services (`IIbkrApiClient`, `IConnectionManager`, `IMarketDataManager`,
  `IMessageLogManager`, `ISettingsManager`, both repositories) are re-registered as **singletons
  after** the scan — the last registration wins.
- `TimeProvider.System` is registered so managers that depend on time (reconnect backoff, bar
  cache TTL) are testable with `FakeTimeProvider`.
- The host is started (`host.Start()`) so `ConnectionWatchdog` (a `BackgroundService`) runs the
  heartbeat; it's stopped after the Avalonia lifetime ends.
- `UpdateScheduler` (also a `BackgroundService`) clears the previous version's folder left by an
  update, then has `IUpdateManager` check GitHub Releases ~10 s after start and every 6 h.
  `AppUpdater` (`LT.App/Services`) does the platform work: download, SHA-256 check against
  GitHub's asset digest, unpack next to the app folder as `.LimitlessTrading.update`, swap the
  folders (the old one becomes `.LimitlessTrading.old`) and start the new executable from a shell
  that waits for this process to exit. While the update manager is `Restarting`, shutdown keeps IB
  Gateway running so the new version reuses it. Builds from source have version `0.0.0-dev`
  (`Directory.Build.props`; the release workflow passes `-p:Version`): they never check by
  themselves and can't install.
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

`LT.App/Stores` are singleton `ObservableObject`s for UI state several widgets share — the role
Zustand stores play in Termodom FEs:

- `AccountStore` — managed accounts and the selected one (persisted); positions, orders,
  executions and the account panel follow it.
- `LinkGroupStore` — the contract of each link group (`LinkGroup`: ten colors). A
  linkable widget holds a `ContractLink` (its group, publish / receive); see
  [ui-patterns.md → Link groups](ui-patterns.md#link-groups).

## Persistence

`LT.Repository` stores settings (connection, general settings, layout, watchlist, selected
account) in `settings.json` and historical bars in `bar-cache/<conId>_<timeframe>_<rth>.json`,
both under the OS app-data folder
(`Environment.SpecialFolder.ApplicationData`: `%APPDATA%\LimitlessTrading` on Windows,
`~/.config/LimitlessTrading` on Linux and macOS). When we need queryable local data (bar cache, layouts, journal) the
plan is SQLite + EF Core behind the same repository interfaces.
