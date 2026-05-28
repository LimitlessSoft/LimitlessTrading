# IBKR User Pain Points & Limitless Trading Opportunities

Research compiled from Reddit, EliteTrader, NexusFi, TradingView reviews, Trustpilot,
ConsumerAffairs, App Store/Google Play reviews, NerdWallet, BrokerChooser, GitHub issues,
and specialist algo-trading communities (QuantConnect, groups.io twsapi, NinjaTrader forums).

---

## 1. Charting: Poor Quality, Freezes, No Custom Indicators

### What users complain about

- TWS charts are universally described as "horrible" and outdated. Users use IBKR only for
  execution and route charting through Sierra Chart, TradingView, or ThinkorSwim.
- ~155 built-in indicators vs. 294+ on TradeStation and 308+ on tastytrade.
- No scripting language — no equivalent to thinkScript or EasyLanguage for custom indicators.
- Charts go to a black screen for 10–45 seconds when switching symbols.
- Chart drawings (trend lines, annotations) do not save reliably across sessions or watchlist entries.
- No overnight chart data (8pm–4am) even though IBKR supports overnight execution.
- Multi-leg options combo charts are not supported.
- The Advanced Charts (TradingView-powered inside TWS) cannot be set as the default layout.

### How Limitless Trading can solve this

- Ship a **first-class charting widget** as a core feature: candlestick, OHLC, Heikin Ashi,
  all major chart styles with smooth symbol switching and instant render (no black-screen gap).
- Provide a **custom indicator scripting environment** — even a simple expression-based system
  where users can combine existing indicators beats IBKR's zero offering.
- Persist all drawings and indicator configs **per symbol** automatically — save on change, not
  on session close, so crashes never lose work.
- Show **full 24-hour sessions** including overnight and pre-/post-market data on every chart.
- Support **options combo charting** — plot the live mid-price of a spread or multi-leg
  position as a single chart series.

---

## 2. Platform Fragmentation: Five Apps, No Unified Experience

### What users complain about

- IBKR offers five separate interfaces (TWS, IBKR Desktop, Client Portal web, IBKR Mobile,
  IBKR GlobalTrader), each with different capabilities, different UX paradigms, and no guidance
  on which to use. This report focuses on the desktop and web experiences.
- Features are silently absent on some platforms with no redirect or explanation.
- Conditional orders only exist in TWS; the bond screener only in the web portal; account
  management only in Client Portal; research tools scattered everywhere.
- Users describe needing "3–4 apps to do what one should do."

### How Limitless Trading can solve this

- Build one **unified workspace** where every feature is reachable from a single app on
  desktop and web — no capability fragmentation by surface.
- When a feature is unavailable in a given context, show a **clear redirect** rather than
  silently hiding the option.
- Expose a **consistent feature matrix** — the same order types, screeners, alerts, and
  analytics available everywhere.

---

## 3. TWS Performance: Slow Startup, Memory Leaks, Java Bloat

### What users complain about

- TWS gets progressively slower the longer it runs; restarts every 2–3 hours are standard practice.
- Opening large option chains leaks heap memory — memory does not recover when the chain is closed.
- CPU spikes to 100% during data-intensive operations (big option chains, 15+ charts open).
- Extreme cases: 16–24 GB RAM consumption before crash.
- TWS is Java-based, which users see as the root cause; IBKR recommends manually tuning heap
  size via command-line arguments to function normally.
- The newer IBKR Desktop app crashes multiple times per day on Apple M2/macOS Sonoma.

### How Limitless Trading can solve this

- A native application (or lightweight Electron shell with a lean renderer process) that
  **does not grow in memory** over a trading session.
- Lazy-load option chains and large data grids — fetch only the visible strikes/rows, not the
  full chain into memory at once.
- Expose a **memory/performance indicator** so users can see resource usage and trigger a
  soft reload without closing the whole app.

---

## 4. Order Entry: Two-Stage Transmit, Confusing Confirmation Dialogs

### What users complain about

- TWS splits submission into two stages: creating the order row, then pressing a separate
  "Transmit" button. New users frequently create orders that never get sent.
- Order confirmation pop-ups only offer "Transmit" or "Cancel" with no way to override
  individual warnings selectively.
- Hot Button (one-click) still opens a confirmation screen, defeating its purpose unless
  confirmations are globally disabled.
- Order history is hard to find; nothing is visually intuitive.

### How Limitless Trading can solve this

- **Single-action order submission** — one button places the order. Warn before, not after the
  intent to trade is clear.
- Smart confirmations: show a compact **order preview card** that the user dismisses once,
  with a "don't ask again for this order type" checkbox — not a modal blocking every action.
- Surface **order history and fills prominently** as a live feed in the main workspace, not
  buried in a separate screen.

---

## 5. Options Analytics: No TOS-Equivalent Analyze Tab

### What users complain about

- IBKR has no equivalent to ThinkorSwim's "Analyze" tab — no interactive P&L surface,
  theta decay over time, or probability cone simulation.
- The Performance Profile tool only computes P&L to the nearest expiration for multi-expiry
  strategies (calendars, diagonals) — documented IBKR limitation.
- Options chain does not auto-center on the at-the-money strikes; users must scroll manually.
- The hover-to-trade mechanic (red border = sell, blue border = buy) is non-obvious to new users.
### How Limitless Trading can solve this

- Build an **options analysis widget** that renders a live P&L surface across price × time,
  updates as underlying price moves, and shows individual Greeks contributions per leg.
- Auto-center the options chain on the **current ATM strike** on load and track it as price moves.
- Ship a **probability cone overlay** on charts — display expected move ranges derived from
  implied volatility for any expiry.

---

## 6. Alerts: No Push Notifications, Alerts Disappear

### What users complain about

- No native push notifications for price alerts — only email or SMS, which are effectively
  useless for time-sensitive triggers.
- Price alerts stored server-side have been known to vanish entirely with no recovery path;
  users lose hours of configuration work.
- Alert management UI is confusing and non-intuitive.
- Alert notifications configured on desktop frequently fail to deliver.

### How Limitless Trading can solve this

- **Reliable push notifications** for every alert type (price, volume, % move, options Greeks
  threshold, earnings events) on all devices.
- Store alerts **redundantly** — server-side with a local export/import backup so they
  survive any backend event.
- Alert builder with **a visual condition builder**: "Notify me when TSLA crosses above the
  20-day EMA AND RSI > 70" with point-and-click condition composition, no scripting required.

---

## 7. Market Screener: 50-Result Cap, Unstable Results, Bond Scanner Gaps

### What users complain about

- Market scanner results are capped at 50 per scan code with no way to expand.
- Scan ranking is explicitly non-deterministic — the same scan returns different results on
  different runs, which is not disclosed in the UI.
- The bond scanner is effectively unusable — bonds that should appear don't, and many listed
  bonds show as untradeable.
- Screener configuration is described as "a real pain" to set up; saving configs was listed
  as "coming soon" in official docs.
- Maximum of 10 filter criteria per screen.

### How Limitless Trading can solve this

- A screener with **no arbitrary result cap** — paginate or stream results.
- **Deterministic, timestamped results** — show when the scan last ran and guarantee
  identical inputs yield identical outputs.
- Allow saving, naming, and scheduling screener configurations.
- Support screeners across **all asset classes** — equities, options, ETFs, bonds — with a
  consistent filter interface.
- Surface **"why this result"** explanations — which criteria each result triggered, so
  users understand the ranking, not just the output.

---

## 8. Backtesting: Completely Absent

### What users complain about

- TWS has no built-in backtesting engine at all. Users must use Python libraries (Backtrader,
  QuantConnect), TradeStation, or ThinkorSwim's ThinkOnDemand.
- ThinkorSwim's replay mode ("OnDemand") is directly cited as a reason traders keep a TOS
  account alongside IBKR.
- IBKR's answer is to point users at their Quant Blog and third-party tools.

### How Limitless Trading can solve this

- Build a **strategy replay / backtesting mode** where users can run a chart through historical
  data, place simulated orders, and see cumulative P&L and trade statistics.
- Integrate backtesting results directly with the **options analytics widget** — show how a
  specific options strategy would have performed historically given the same entry conditions.
- Even a basic "mark this chart at a past date and walk forward" replay beats IBKR's zero offering.

---

## 9. Paper Trading: Platform Gaps, Confusing Account Switching

### What users complain about

- The paper trading web portal is nearly empty — no accessible menu for trades, portfolio, or
  watchlist in desktop browser paper mode.
- Paper trading does not support VWAP, Auction, RFQ, Pegged to Market order types.
- Orders often don't fill at all in paper mode (limit and market alike).
- Mutual fund trading is not supported in paper accounts.
- Market data in paper accounts is delayed.
- Switching between paper and live requires logging out and back in — paper/live toggle is
  only on the login screen, not anywhere inside the app.

### How Limitless Trading can solve this

- **In-app paper trading toggle** — switch between paper and live from within the workspace,
  no re-login required, with a clear visual indicator of which mode is active.
- Paper trading with **realistic fill simulation** — model slippage, partial fills, and spread
  crossing rather than filling at the displayed price, so paper results translate to live.
- Full feature parity between paper and live — same order types, same analytics, same alerts.

---

## 10. API: Requires TWS Running, Pacing Violations, No OAuth

### What users complain about

- The TWS API requires a running instance of TWS or IB Gateway — there is no standalone
  API server. Deploying automated strategies on a server means running a full Java GUI
  application with periodic manual re-authentication.
- Pacing violations are easy to trigger: no identical historical data requests within 15s,
  no more than 6 requests for the same contract within 2s, no more than 60/10 min.
  Violations cause throttling or full disconnection.
- No OAuth or API key authentication — every deployment requires browser-based GUI login on
  the same machine as the gateway.
- Only one active brokerage session per account — API connection and desktop sessions conflict.
- The Client Portal REST API has a hard 10 requests/second global rate limit.
- Account value updates via API push only every 3 minutes — not real time.
- Historical data access is limited to 1 year by default (more requires paid subscriptions).
- Fractional shares are not accessible via the API.
- Bar data from the API has a documented 5-second lag for bar-close data since Gateway 10.30+.
- Documentation is described as "the worst software documentation I ever saw" by 15-year veterans.

### How Limitless Trading can solve this

- Provide a **true headless API** — token-based (OAuth or API key), no GUI required, runs as
  a standalone service with zero manual re-authentication for normal operation.
- Generous, **clearly documented rate limits** with HTTP 429 responses that include a
  `Retry-After` header and a real-time rate limit dashboard in the developer portal.
- **Real-time account value and position pushes** via WebSocket — no polling, no 3-minute lag.
- Support **fractional shares and all order types** through the API, with parity to the UI.
- Provide a **developer sandbox** (paper trading environment) that is fully accessible via API
  without any interaction with a live account.
- Ship **SDKs with clear examples** for Python, TypeScript, and C# covering the most common
  workflows (place order, stream quotes, pull history, manage positions).

---

## 11. Authentication & Session Management: 2FA Friction, Single Session, Forced Logouts

### What users complain about

- Mandatory 2FA on every login with no "remember this device" option for extended periods.
- IB Key push notifications for 2FA frequently fail to arrive.
- Only one active brokerage session allowed per account — desktop and API sessions conflict.
- Nightly forced disconnection around midnight ET for server maintenance.
- Weekly reset after weekend market close requires re-authentication.
- The Client Portal logs users out after 1–2 minutes of inactivity.

### How Limitless Trading can solve this

- **Trusted device recognition** with configurable session lifetime (e.g., 7-day persistent
  sessions on trusted devices).
- Support **multiple concurrent sessions** — desktop and API simultaneously, each with
  independent session state.
- For API/algo use: **session tokens with long TTLs** and background silent refresh — no
  nightly disconnects for running strategies.

---

## 12. P&L Display: Inconsistent Values, No Portfolio Percentage View

### What users complain about

- P&L values in the TWS Account Window and Portfolio Window often differ for the same
  positions because they pull from different data sources with different update schedules.
- IBKR customer service has told users "TWS P&L is for informational purposes only" — trust
  your statement instead.
- No simple total portfolio P&L percentage displayed prominently. One EliteTrader thread is
  titled "Can't believe it — total P/L % missing?"

### How Limitless Trading can solve this

- **Single authoritative P&L** — one source of truth, consistent everywhere, updated in
  real time as prices move.
- Portfolio header shows **total P&L in both dollar and percentage** at all times: today's
  P&L, open P&L, and all-time P&L, clearly labelled.
- Per-position: show cost basis, current value, unrealized P&L ($), unrealized P&L (%),
  day's P&L — all in the same row with no need to cross-reference multiple windows.

---

## 13. Market Data Subscriptions: Paywalled, Misclassified, Confusing

### What users complain about

- IBKR defaults users to the "professional" market data classification (10× more expensive)
  without clear disclosure. Most retail users do not discover they are overpaying until they
  find a buried setting in Client Portal.
- Real-time data requires paid subscriptions — competitors (Robinhood, Webull, Fidelity,
  Schwab) provide free real-time quotes.
- Only 100 free snapshot quotes per month by default (non-streaming).
- 30+ separate exchange bundles with per-exchange fees — "one of the most confusing
  subscription systems in retail brokerage."
- Data is charged for the full month even on mid-month subscription/cancellation.

### How Limitless Trading can solve this

- **Inclusive real-time data** bundled with the platform subscription — no separate market data
  billing, no professional vs. non-professional classification surprises.
- If tiered data access is necessary, a **simple wizard** at onboarding: "What do you trade?
  Here is exactly what you need. Estimated cost: $X/month."
- Show data subscription status and what each active subscription covers in a
  **single clear settings page**.

---

## 14. Tax Reporting: Non-Standard 1099-B, Confusing Forms

### What users complain about

- IBKR's 1099-B reports short sales closed on trade date rather than settlement date —
  different from most brokers and inconsistent with IRS instructions, causing discrepancies
  when importing into TurboTax.
- Consolidated 1099 is split into four sub-forms with unclear field usage.
- IBKR recommends consulting a tax advisor for all 1099 amounts — an admission the
  documents are not self-explanatory.

### How Limitless Trading can solve this

- Generate **clear, annotated tax documents** with plain-English explanations of each section.
- Provide a **tax summary dashboard** in the app: realized gains/losses by asset class and
  holding period, wash sale flags, and estimated tax impact — updated throughout the year,
  not just at January statements.
- Offer **direct export** to TurboTax and TaxAct formats with standard-compliant data.

---

## 15. Customer Support: Unreachable, Tickets Ignored, Chat Timeouts

### What users complain about

- Support tickets unanswered for 10+ days despite promised 48-hour response times.
- Phone numbers are hard to find; bots block access to humans.
- Support chat in the web portal crashes mid-session, requiring full re-authentication to restart.
- "A joke of the industry" — users filing 3+ tickets over 10 days with zero response.
- IBKR rated 3.3/5 on Trustpilot largely due to support failures.

### How Limitless Trading can solve this

- **In-app live chat** that does not require re-authentication if the session is active.
- For critical issues (position discrepancy, unexpected account freeze): a **priority support
  path** that guarantees a human response within 1 hour.
- Proactive **status notifications** inside the app when known platform issues are detected,
  so users aren't left wondering if the problem is on their end.

---

## 16. Platform Reliability: 141 Outages in 5 Months, Nightly Disconnects

### What users complain about

- StatusGator tracked 141+ outages affecting IBKR users over a 5-month period.
- Nightly disconnection at ~midnight ET and weekly reset are by design but disruptive.
- "Market Data Connection Lost" messages cycling every 15–30 seconds during US trading hours
  (abnormal behavior, not just the nightly reset).
- A June 2024 technical glitch caused BRK.A to be halted from trading by NYSE.
- On high-volatility days, IBKR has publicly stated "no system-wide outages" while users
  reported issues.

### How Limitless Trading can solve this

- A **public status page** with real-time component health (data feed, order routing, API
  gateway, authentication) — updated automatically, not manually.
- **In-app incident banner** that appears the moment a component degrades, with an ETA
  and a direct link to the status page.
- Architecture that **degrades gracefully** — if the data feed is slow, orders still work;
  if an exchange feed is down, the rest of the app remains functional.

---

## Priority Matrix

| Pain Point | User Impact | Opportunity Size | Effort |
|---|---|---|---|
| Charting quality & custom indicators | Critical | Very Large | High |
| Platform fragmentation (5 apps) | Critical | Very Large | High |
| Options analytics (no TOS Analyze equivalent) | High | Large | High |
| Unified, reliable alerts with push notifications | High | Large | Medium |
| Single session / authentication friction | High | Large | Medium |
| Order entry clarity (two-stage transmit) | High | Large | Low |
| P&L consistency and portfolio % view | High | Large | Low |
| Paper trading feature parity & in-app toggle | Medium | Medium | Medium |
| Screener: no result cap, deterministic, saveable | Medium | Medium | Medium |
| API: headless auth, real-time pushes, SDKs | High | Large | High |
| Backtesting / replay mode | Medium | Medium | High |
| Market data: inclusive, no subscription confusion | Medium | Medium | Low |
| Tax reporting: annotated docs, year-round dashboard | Low | Small | Medium |
| Customer support: in-app chat, status page | Medium | Medium | Low |
