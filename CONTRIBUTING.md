# Contributing to LimitlessTrading

Thanks for wanting to help make a better Interactive Brokers front-end. Bug reports, ideas, docs
fixes and code are all welcome.

## Before you start

- **Read [CLAUDE.md](CLAUDE.md) Part 1.** It is the short list of rules every change follows (for
  humans as much as for AI assistants). The `docs/` folder explains the why.
- **Open an issue first for anything bigger than a small fix**, so we can agree on the approach
  before you spend time on it.
- **This is a read-only build.** There is no code that places, modifies or cancels orders, and
  `IIbkrApiClient` has no order methods. Trading is a deliberate, separately discussed step — PRs
  that add order placement on the side will not be merged.

## Getting set up

You need the **.NET SDK 10.0.4xx** (pinned in `global.json`) and, to run against IB,
[IBC](https://github.com/IbcAlpha/IBC) plus the offline/stable **IB Gateway** installer.
A paper trading account is the safest way to try things out.

```bash
git clone git@github.com:LimitlessSoft/LimitlessTrading.git
cd LimitlessTrading
dotnet build LimitlessTrading.slnx
dotnet test
dotnet run --project src/LT.App
```

`IBKR.Net` is restored from the local feed in `packages/` (see `nuget.config`) until it is on
nuget.org; no extra steps.

## Making a change

1. **Branch** from `master`: `feature/<short-name>`, `bugfix/<short-name>` or `docs/<short-name>`.
2. **Follow the layering** ([docs/architecture.md](docs/architecture.md)):
   - `IBKR.Net` types stay inside `LT.Ibkr`; everything crossing `IIbkrApiClient` is an
     `LT.Contracts` DTO.
   - Change the interface in `LT.Contracts/Interfaces/` first, then the implementation.
   - ViewModels inject managers only; reusable logic goes to `LT.Contracts/Helpers/`.
   - Anything from IB reaches bound UI state through `Dispatcher.UIThread.Post`
     ([docs/ui-patterns.md](docs/ui-patterns.md)).
   - Respect IB's limits: requests go through `RequestThrottle`, quotes through
     `IMarketDataManager`. Found a new IB quirk? Write it down in
     [docs/ibkr-tws-api.md](docs/ibkr-tws-api.md).
   - New enum values go at the **end** (they are persisted as integers).
3. **Test it.** Add or update tests in `src/LT.Tests` (xUnit v3). Tests never connect to IB — use
   the fakes in `LT.Tests/Fakes`. Time-dependent code takes a `TimeProvider`.
4. **Format** with [CSharpier](https://csharpier.com): `csharpier format .` (tabs, width 100).
5. **Check** that `dotnet build` has no warnings and `dotnet test` passes.

## Commits and pull requests

- Write commit messages that say what changed and why; keep unrelated changes in separate PRs.
- In the PR description, explain the problem, the fix and how you tested it — against IB
  (paper or live, which widgets) or only unit tests. Screenshots help for UI changes.
- Update the docs in `docs/` when behaviour, limits or architecture change.

## Reporting bugs

Open an issue with: what you did, what you expected, what happened, your OS, and any IB error code
shown in the app (e.g. `[2188] …`). **Never paste account numbers, usernames, passwords or
`settings.json` contents.**

## Licensing of contributions

LimitlessTrading is licensed under **MIT with the Commons Clause** (see [LICENSE](LICENSE)): free
to use, modify and share, but not to sell. By submitting a contribution you agree that it is
licensed under the same terms and that you have the right to contribute it.
