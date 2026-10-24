# CLAUDE.md

Guidance for Claude Code in this repository.

**Part 1 is rules — they always apply. Part 2 is reference — read it when you need it.**
Detailed documentation lives in `/docs/`; open the relevant file when working on that area.

---

# Part 1 — Rules

## Git

**Never commit changes automatically.** Commit only when the user asks; after finishing a task,
say the changes are ready.

## Code rules

0. **Read-only build** — there is no order placement/cancel/modify code. Don't add order
   methods to `IIbkrApiClient` unless the user explicitly asks for trading.
1. **`IBKR.Net` types live only in `LT.Ibkr`.** Everything crossing `IIbkrApiClient` is an
   `LT.Contracts` DTO; map IB types in `LT.Ibkr/DtoMappings/`. See
   [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md).
2. **Always update interfaces when modifying implementations** — the interface in
   `LT.Contracts/Interfaces/` first, then the implementation.
3. **No private helpers in Managers** — reusable logic goes to `LT.Contracts/Helpers/` as a
   `public static class`, dependencies passed as parameters.
4. **ViewModels inject managers only** — never repositories or `IIbkrApiClient`.
5. **UI thread** — anything coming from IB (events, `await foreach`) reaches bound state only via
   `Dispatcher.UIThread.Post`. See [docs/ui-patterns.md](docs/ui-patterns.md).
6. **Validation** — `request.Validate()` in the manager; messages in
   `Enums/ValidationCodes/*ValidationCodes` with `[LSCoreValidationMessage]`, limits in
   `Contracts/Constants/`. No inline user-facing strings in validators.
7. **Add enum values at the END only** — persisted enums are integer-backed.
8. **Stateful services are singletons registered after `AddLSCoreDependencyInjection`** in
   `Program.cs`; new ViewModels must be registered there too (the scan skips them).
9. **Respect IB limits** — every request goes through `RequestThrottle` in `IbkrApiClient`;
   quotes go through `IMarketDataManager` (line limit). Never fire unbounded batches.
12. **Shared UI state lives in `LT.App/Stores`**; widgets needing a connection derive from
    `ConnectedViewModelBase` and use its session token.
13. **Time-dependent managers take `TimeProvider`**, tests use `FakeTimeProvider`.
14. **Widgets are placed only through the workspace** (`LT.App/Workspace`) — never put a widget
    view straight into a page. Adding one is listed in [docs/ui-patterns.md](docs/ui-patterns.md#workspace-ltappworkspace).
10. **Tests never connect to IB** — fake `IIbkrApiClient` (`LT.Tests/Fakes`).
11. **Format with CSharpier** (`csharpier format .`) — tabs, width 100.

---

# Part 2 — Reference

## Overview

Avalonia 12 / .NET 10 desktop app for Interactive Brokers ("TWS but better"), Windows/macOS/Linux.
Layers follow the Termodom ecosystem and use LSCore (`DependencyInjection`, `Validation`,
`Mapper`):

| Project | Role |
|---|---|
| `LT.Contracts` | DTOs, Requests, Entities, Enums, Interfaces, Helpers, Constants |
| `LT.Domain` | Managers, Validators |
| `LT.Repository` | Local persistence (JSON settings) |
| `LT.Ibkr` | `IIbkrApiClient` over IBKR.Net (our own IB client, no IB code) |
| `LT.App` | Avalonia app — `Program.cs` composition root, Shell, Pages, Widgets |
| `LT.Tests` | xUnit v3 on Microsoft.Testing.Platform |

## Commands

```bash
dotnet run --project src/LT.App
dotnet build LimitlessTrading.slnx
dotnet test
csharpier format .
```

## Documentation index

| Doc | When to read |
|---|---|
| [docs/vision.md](docs/vision.md) | Product goals, roadmap phases |
| [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md) | Anything touching IB: library surface, gaps, ports, limits, error codes |
| [docs/architecture.md](docs/architecture.md) | Project layout, dependency direction, DI, Termodom mapping |
| [docs/ui-patterns.md](docs/ui-patterns.md) | Widgets/pages, MVVM, threading, styling |
