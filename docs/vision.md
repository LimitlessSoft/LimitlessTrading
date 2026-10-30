# Vision — TWS, but better

LimitlessTrading is a cross-platform (**Windows, macOS, Linux**) desktop trading front-end for
Interactive Brokers, built with **Avalonia** on **.NET 10**. It starts IB Gateway itself (the user logs in in Gateway's window) and talks to it through
the socket API ([ibkr-tws-api.md](ibkr-tws-api.md)), takes account history from IB's Flex Web
Service ([ibkr-flex.md](ibkr-flex.md)), and aims to replace the TWS UI for day-to-day use.

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

| Phase | Scope | Status |
|---|---|---|
| 0 — Shell | Solution layout, DI, connection bar, positions widget | **done** |
| 1 — Connectivity | Heartbeat + auto-reconnect with backoff, reuse of a Gateway still running on start, message log, account summary + P&L, account picker | **done** — the log holds app events only (IB notices are not routed to it yet) |
| 2 — Market data | Contract search, watchlist with L1 quotes, request pacing, line limit, delayed-data fallback | **done** — search is by exact symbol (no `reqMatchingSymbols`) |
| 3 — Charts | Candlestick chart, historical + 5 s real-time bars, local bar cache | **done** |
| 3 — Charts | Indicators: built-ins (SMA, EMA, VWAP, Bollinger, RSI, MACD, ATR, volume MA) in overlay / own panes with legends and settings, and user C# scripts edited in any editor and hot-reloaded ([indicators.md](indicators.md)) | **built** — scripts run in-process behind a compile-time ban list (not a sandbox); no in-app editor, colour picker or resizable panes yet |
| 4 — Trading | Live positions with P&L, open orders, executions | **done** — executions wait for IBKR.Net support (no fill recorded yet) |
| 4 — Trading | Order ticket: market / limit, quantity or cash amount, review before send, status, cancel | **built, in use** — first live order (stock limit, 2026-10-26) worked; nothing filled yet; modify and what-if not started |
| 4 — Trading | Day trade performance: local journal of fills and intraday P&L, curve with high / low / drawdown, FIFO round trips with win rate, profit factor, expectancy and a trades table per day | **built** — the P&L curve works; trades and stats come from the socket API's executions (today, while connected) and IB Flex (all history) |
| 5 — Workspaces | Settings dialog, workspace where every widget can be spawned (charts several times) and dragged into the center split layout (beside / above / below), a collapsible side strip or its own floating window, saved layout (window, open panels, widths, tab, timeframe), hotkeys | **done** |
| 5 — Workspaces | Multiple named layouts, dragging strip icons | not started |
| 6 — Advanced | Option chain (a tab per expiration, strikes around the money, L1 quotes) | **done** — no greeks yet |
| 6 — Advanced | News widget: the link group's symbol (history, then live) or the broad tapes of the selected providers, provider chips, text filter, plain-text reading pane | **done** — IB plus the app's own feeds (SEC, PR Newswire, Fed, BLS on by default; Business Wire, FDA, CNBC, MarketWatch, Yahoo, Seeking Alpha optional), works without IB, local archive with search, cross-source dedupe, keyword / watchlist alerts with a sound ([news-feeds.md](news-feeds.md)) |
| 6 — Advanced | Account performance: equity curve, returns (today / week / month / YTD, max drawdown), daily P&L calendar and bars | **built** — past days come from IB Flex (end-of-day NAV back to the account's start, deposits / withdrawals left out of P&L), today is recorded live; without Flex only what the app recorded |
| 6 — Advanced | Depth widget: the link group's order book as a ladder (asks above, bids below), SMART depth or the listing exchange's, price / size / running total / exchange, size bars, spread | **built** — IBKR.Net 0.6.0; IB allows 3 depth subscriptions per account; SMART depth shows only exchanges the account has depth for (IEX on the live account) |
| 6 — Advanced | Calendar widget: market-wide earnings, other corporate events and economic releases by day, this week / next week / this month, earnings / economic / other events, click to link; sources switchable in Settings | **built** — Nasdaq's public calendars (on by default, no setup or IB connection: earnings with time, EPS estimate and reported EPS and surprise — the same day from Yahoo Finance, with a link to the company's results 8-K on SEC EDGAR as soon as it is filed, re-checked while the widget is open, a row whose EPS comes in or changes then highlighted for 5 minutes — ex-dividend dates, splits, economic releases) merged with IB's Wall Street Horizon (off by default; IBKR.Net 0.8.0, needs a subscription the live account lacks, so its answers are still unseen); duplicates shown once, a failing source named while the others still show |
| 6 — Advanced | Overview widget: one contract in tabs, under a live quote header (price, change, bid / ask, open, ranges incl. 52-week, volume and average volume) | **started** — Basic info tab (identity, classification, market state and today's hours, min tick, routing, SEC shares outstanding and market cap for US stocks); Financials tab (income statement, balance sheet, cash flow, ratios and valuation per fiscal year or quarter, from SEC 10-K / 10-Q filings, US stocks); news, options tabs to come |
| 6 — Advanced | Scanners, alerts | not started — scanners need IBKR.Net support |

Orders exist since the order ticket (`IIbkrApiClient.PlaceOrderAsync`): every order is reviewed
in the ticket and sent only on "Send order". Never send one from code without the user's yes.

Items IBKR.Net does not cover yet are listed in
[ibkr-tws-api.md → Not covered yet](ibkr-tws-api.md#not-covered-yet).
