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
| `TD.X.Client` / `Domain/ApiClients` | Typed client for an external API | `LT.Ibkr` — the only project that references `TwsApi` |
| `TD.X.Api` | `Program.cs`, DI, controllers | `LT.App` — `Program.cs`, DI, ViewModels |
| `TD.X.Fe` (`pages/`, `widgets/`, `zStore/`, `helpers/`) | UI | `LT.App` (`Pages/`, `Widgets/`, `Helpers/`) |
| `TD.X.Tests` | Unit tests | `LT.Tests` |

## Layout

```
LimitlessTrading.slnx
src/
├── LT.Contracts/                  # no dependency on Avalonia or TwsApi
│   ├── Constants/                 # limits, defaults, paths (ConnectionConstants, AppConstants)
│   ├── Dtos/<Feature>/            # what managers return to the UI
│   ├── Entities/                  # what repositories persist
│   ├── Enums/                     # + Enums/ValidationCodes/
│   ├── Helpers/                   # static helper classes (no private helpers in managers)
│   ├── Interfaces/
│   │   ├── IApiClients/           # IIbkrApiClient — the broker boundary
│   │   ├── IManagers/
│   │   └── IRepositories/
│   └── Requests/<Feature>/
├── LT.Domain/
│   ├── Managers/                  # IConnectionManager → ConnectionManager, …
│   └── Validators/                # LSCoreValidatorBase<TRequest>
├── LT.Repository/
│   └── Repositories/              # SettingsRepository (JSON in the user's app-data folder)
├── LT.Ibkr/
│   ├── DtoMappings/               # IBApi types → LT.Contracts DTOs
│   └── IbkrApiClient.cs           # implements IIbkrApiClient over TwsApi
├── LT.App/                        # Avalonia (Fluent, dark)
│   ├── Program.cs                 # composition root (generic host + LSCore DI)
│   ├── App.axaml                  # themes, global styles
│   ├── Shell/                     # MainWindow: menu, connection bar, page host, status bar
│   ├── Pages/<Page>/              # a workspace composed of widgets (TradingPage)
│   ├── Widgets/<Widget>/          # <Widget>View.axaml + <Widget>ViewModel.cs
│   ├── ViewModels/                # ViewModelBase
│   └── Helpers/                   # UI-only helpers (error messages, …)
└── LT.Tests/                      # xUnit v3, never connects to IB
```

## Dependency direction

```
LT.App ──► LT.Domain ──► LT.Contracts ◄── LT.Repository
   │                          ▲
   └──► LT.Ibkr ──────────────┘   (LT.Ibkr ──► TwsApi)
```

- `LT.Contracts` references nothing of ours, and **never** `TwsApi`/`IBApi` or Avalonia.
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

- Stateful services (`IIbkrApiClient`, `IConnectionManager`, settings repository) are re-registered
  as **singletons after** the scan — the last registration wins.
- `host.UseLSCoreDependencyInjection()` must run right after `Build()` — `request.Validate()`
  resolves validators through it.
- `services.AddTwsApi(...)` registers `ITwsClientFactory`.
- ViewModels have no interfaces, so they're registered explicitly as transient.

> **Known issue — Windows publish.** The LSCore scan calls `Assembly.LoadFrom` on *every* `*.dll`
> in the base directory before filtering by name. A RID-specific / self-contained Windows publish
> puts native DLLs (`libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll`) there, and
> `LoadFrom` throws `BadImageFormatException` on them. `dotnet run` is unaffected (natives sit in
> `runtimes/`). Fix in LSCore (filter file names by the prefix before loading, or skip files that
> fail to load) before shipping a Windows build.

## Rules carried over from Termodom

1. **Update interfaces with implementations** — interface in `Contracts/Interfaces/...` first.
2. **No private helpers in managers** — reusable logic goes to `Contracts/Helpers/` as
   `public static class`, dependencies passed as parameters.
3. **Validation** — requests are validated in the manager with `request.Validate()`
   (LSCore.Validation). Messages live in `Enums/ValidationCodes/*ValidationCodes` with
   `[LSCoreValidationMessage]`; limits in `Constants/`. No inline user-facing strings in validators.
4. **Enums that get persisted: add values at the END only.**
5. **Mapping at boundaries** — entity ↔ DTO and IB type ↔ DTO conversions live in `DtoMappings/`.

## Persistence

`LT.Repository` stores settings as JSON under the OS app-data folder
(`Environment.SpecialFolder.ApplicationData`: `%APPDATA%\LimitlessTrading` on Windows,
`~/.config/LimitlessTrading` on Linux and macOS). When we need queryable local data (bar cache, layouts, journal) the
plan is SQLite + EF Core behind the same repository interfaces.
