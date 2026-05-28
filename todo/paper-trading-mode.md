# Paper Trading Mode: In-App Toggle & Realistic Fill Simulation

Add an in-app paper/live toggle that switches execution context without requiring a logout. Paper mode should support all the same order types, analytics, and alerts as live mode, and use realistic fill simulation (slippage, partial fills, spread crossing) so paper results translate meaningfully to live.

## Problem

IBKR's paper trading web portal is nearly empty (no trades, portfolio, or watchlist in browser paper mode). Paper mode does not support several order types (VWAP, Auction, Pegged to Market). Orders frequently don't fill at all. Switching between paper and live requires a full logout and re-login — the toggle exists only on the login screen.

## In-app toggle

Add a **Paper / Live** toggle to the main header/nav bar:

```
[ 🟢 LIVE ]  ←→  [ 📄 PAPER ]
```

- Toggling switches the active execution context without reloading the app or requiring re-authentication
- Layout, widgets, and settings remain unchanged — only the order routing target and portfolio data source change
- A persistent **orange banner** is shown at the top of the workspace when paper mode is active: "Paper Trading — orders are simulated and will not be executed live"
- Paper mode uses the IBKR paper trading account (via a separate TWS API connection on port 7497) or the built-in local simulation engine if IBKR paper is unavailable

## Realistic fill simulation (local engine)

When using the built-in simulation engine (not connected to IBKR paper):

| Order type | Fill logic |
|---|---|
| Market | Fill at `(bid + ask) / 2` of the current quote, with a configurable slippage multiplier |
| Limit buy | Fill when `ask ≤ limitPrice`; partial fill if available size < order quantity |
| Limit sell | Fill when `bid ≥ limitPrice`; partial fill if available size < order quantity |
| Stop | Trigger and fill as market when stop price is crossed |
| Stop-limit | Trigger at stop, fill as limit |

Slippage model: add `slippageTicks × tickSize` to the fill price for market orders (configurable, default 1 tick).

Partial fills: randomly simulate partial fills on illiquid symbols (configurable probability).

## Paper portfolio widget

Add a `paper-portfolio` widget (or extend the existing portfolio widget to switch data source based on active mode):

- Shows paper account positions, unrealized P&L, cash balance, and buying power
- Includes a "Reset paper account" button that clears all positions and restores the starting cash balance (configurable, default $1,000,000)
- Trade history tab shows all paper trades with entry/exit prices and P&L

## Feature parity requirements

Paper mode must support every feature live mode does:
- All order types (MKT, LMT, STP, STP_LMT, bracket, OCA)
- All alert types (alerts fire against real-time quotes even in paper mode)
- Options analytics with live IV data
- Full charting with all indicators

## Files

| File | Change |
|------|--------|
| `src/components/TradingModeToggle.jsx` | New — paper/live toggle button and banner |
| `src/lib/executionContext.js` | New — tracks active mode; routes orders to correct engine |
| `src/lib/simulatedOrderEngine.js` | New (shared with replay mode) — fill simulation with slippage and partials |
| `src/components/TemplatePagePortfolio.jsx` | Switch data source based on active execution context |
| `server/routes/orders.js` | Route to IBKR paper (port 7497) or local engine based on mode |
| `docs/template-widgets.md` | Document paper trading mode and toggle behavior |
