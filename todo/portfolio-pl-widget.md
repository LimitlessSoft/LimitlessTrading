# Portfolio P&L Widget: Single Source of Truth with Percentage View

Add a `portfolio-summary` widget that displays a single, authoritative, real-time P&L across the entire portfolio in both dollar and percentage terms — today's P&L, open P&L, and all-time P&L — with per-position detail rows that include cost basis, unrealized P&L ($), unrealized P&L (%), and day's P&L in one view.

## Problem

IBKR's TWS Account Window and Portfolio Window show different P&L values for the same positions because they pull from different data sources with different update schedules. IBKR customer service has told users "TWS P&L is for informational purposes only." There is no total portfolio P&L percentage view anywhere — an EliteTrader thread is titled "Can't believe it — total P/L % missing?"

## Widget type: `portfolio-summary`

```js
{
  id: "uuid",
  type: "portfolio-summary",
  width: 20,
  height: 400,
  showDayPnl: true,
  showOpenPnl: true,
  showAllTimePnl: true,
  groupBy: "none",         // none | assetClass | sector
  style: {
    bgColor: "#1a202c",
    textColor: "#E2E8F0",
    positiveColor: "#38A169",
    negativeColor: "#E53E3E"
  }
}
```

## Layout

```
╔══════════════════════════════════════════════════════╗
║  Portfolio                                           ║
║  Today's P&L:   +$1,240  (+0.82%)                   ║
║  Open P&L:     +$18,450  (+12.3%)                   ║
║  All-time P&L:  +$34,200  (+34.2%)                  ║
╠══════════════════════════════════════════════════════╣
║ Symbol    Qty   Cost    Price   Unrl $   Unrl %  Day % ║
║ AAPL      100   180.00  185.50  +$550   +3.1%   +1.2% ║
║ TSLA       50   210.00  195.30  -$735   -7.0%   -2.1% ║
║ SPY calls   2     4.50    6.20  +$340  +37.8%   +5.4% ║
╚══════════════════════════════════════════════════════╝
```

- Summary row values are computed by the LT server from a single authoritative data source — not two separate polling endpoints
- All values update in real time as quotes change (WebSocket push)
- Color coding: positive values green, negative red
- Clicking a position row opens the position detail panel (Greeks for options, cost basis lots for equities)

## Data source (single source of truth)

The server maintains one position store updated from two streams:
1. **IBKR position callbacks** (`position`, `positionEnd`) — authoritative on quantity and average cost
2. **Live quotes** (`reqMktData`) — current price for P&L calculation

P&L formula: `unrealizedPnl = (currentPrice - avgCost) * qty * multiplier`

The server computes P&L server-side and pushes the result. The frontend never computes P&L independently — it only renders the server-pushed value. This eliminates the TWS-style divergence between windows using different formulas.

Day's P&L: `currentPrice - previousClose`. Previous close is fetched once at session start from the IBKR historical data API.

All-time P&L: requires trade history. Fetch from IBKR execution history on startup and maintain a running total updated with each new fill event.

## Backend

```
GET /v1/portfolio                — current positions with pre-computed P&L
WebSocket channel: "portfolio"  — real-time P&L updates as quotes move
```

```json
// Pushed on each quote update for a held position
{
  "channel": "portfolio",
  "symbol": "AAPL",
  "qty": 100,
  "avgCost": 180.00,
  "currentPrice": 185.50,
  "unrealizedPnl": 550.00,
  "unrealizedPnlPct": 0.031,
  "dayPnl": 120.00,
  "dayPnlPct": 0.012
}
```

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePagePortfolioSummary.jsx` | New — portfolio summary widget |
| `server/lib/portfolioStore.js` | New — single authoritative position and P&L store |
| `server/routes/v1/portfolio.js` | `GET /v1/portfolio` and WebSocket push on price change |
| `src/pages/TemplatePage.jsx` | Register `portfolio-summary` in render switch and Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon and snippet for `portfolio-summary` |
| `docs/template-widgets.md` | Document `portfolio-summary` widget |
