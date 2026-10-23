# Vision — TWS, but better

LimitlessTrading is a cross-platform (**Windows, macOS, Linux**) desktop trading front-end for
Interactive Brokers, built with **Avalonia** on **.NET 10**. It connects to TWS / IB Gateway through
the socket API ([ibkr-tws-api.md](ibkr-tws-api.md)) and aims to replace the TWS UI for day-to-day use.

## Why

TWS is powerful but:

- Java/Swing UI — dated, dense, inconsistent; poor HiDPI and multi-monitor behaviour.
- Layouts are fragile and hard to share; settings are buried in a huge Global Configuration tree.
- Order entry is click-heavy and error-prone; confirmations are either too noisy or absent.
- Memory hungry, slow startup, daily restart disrupts the session.
- Little extensibility — no scripting, no custom panels.

## What "better" means (guiding principles)

1. **Fast and calm UI** — dark theme by default, readable typography, clear P/L colouring,
   keyboard-first workflows.
2. **Safe order entry** — explicit preview (what-if margin/commission), sane defaults, configurable
   hotkeys, one confirmation that actually tells you something.
3. **Resilient connection** — disconnects (TWS daily restart, network) are expected: visible state,
   automatic reconnect, resubscribe everything.
4. **Composable workspaces** — dockable widgets (watchlist, chart, order ticket, positions, orders,
   executions, account), saved as named layouts.
5. **Respect IB limits** — pacing, market-data lines and historical data limits are handled by the
   app, not discovered by the user through disconnects.
6. **Cross-platform parity** — the same app and the same layouts on all three OSes.

## Roadmap sketch

| Phase | Scope |
|---|---|
| 0 — Shell | Solution layout, DI, connection bar, positions widget, placeholder widgets — **done** |
| 1 — Connectivity | Heartbeat + reconnect, IB message log, account summary, managed account picker |
| 2 — Market data | Contract search, watchlist with L1 quotes (throttled subscriptions), delayed-data fallback |
| 3 — Charts | Historical bars + real-time bars, local bar cache |
| 4 — Trading | Order ticket, open orders, executions, cancel/modify, what-if preview |
| 5 — Workspaces | Docking, saved layouts, hotkeys, settings UI |
| 6 — Advanced | Options chain, L2 depth, scanners, news, alerts |

Phase 1+ items that `TwsApi` does not cover yet are listed in
[ibkr-tws-api.md → Not covered yet](ibkr-tws-api.md#not-covered-yet-we-will-need-these-for-tws-but-better).
