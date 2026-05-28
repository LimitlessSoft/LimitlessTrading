# Options Analytics Widget

Add an `options-analytics` widget that renders an interactive P&L surface across underlying price and time, shows per-leg Greeks contributions, auto-centers the options chain on the ATM strike, and overlays a probability cone on the chart widget.

## Problem

IBKR has no equivalent to ThinkorSwim's "Analyze" tab. The Performance Profile tool can only project P&L to the nearest expiry for multi-leg strategies. The options chain does not auto-center on ATM. There is no probability cone overlay. "I manage all my positions on TOS but execute on IB" is a universal sentiment — this is the single biggest feature gap vs. TOS for options traders.

## Widget type: `options-analytics`

```js
{
  id: "uuid",
  type: "options-analytics",
  width: 20,
  height: 500,
  legs: [
    { symbol: "AAPL 250620C00190000", ratio: 1, action: "BUY" },
    { symbol: "AAPL 250620C00200000", ratio: -1, action: "SELL" }
  ],
  underlyingSymbol: "AAPL",
  priceRange: 0.20,          // ± 20% from current price
  targetDate: null,          // null = expiration of nearest leg
  showGreeks: true,
  showProbabilityCone: true,
  style: { bgColor: "#1a202c", textColor: "#E2E8F0" }
}
```

## Layout

Three panels:

**1. P&L surface (main panel)**
- X axis: underlying price (current price ± `priceRange`)
- Y axis: P&L in dollars
- Two lines: P&L at `targetDate` (colored) and P&L at expiry (white/grey)
- Hover tooltip: price, P&L at hover date, P&L at expiry, breakeven markers
- Draggable date slider to move the target date forward in time; lines update live

**2. Greeks summary (side panel)**
- Per-leg and net: delta, gamma, theta, vega, rho
- Net P&L, max profit, max loss, breakeven price(s)

**3. ATM-centered options chain (bottom panel)**
- Same as the options-chain widget but auto-centers on ATM on load
- Strike row nearest current price is highlighted and scrolled into view automatically

## Backend — P&L calculation

`POST /api/options/analyze`

```json
{
  "legs": [
    { "symbol": "AAPL 250620C00190000", "ratio": 1, "action": "BUY", "entryPrice": 4.20 },
    { "symbol": "AAPL 250620C00200000", "ratio": -1, "action": "SELL", "entryPrice": 1.80 }
  ],
  "underlyingPrices": [150, 155, 160, ..., 220],
  "targetDate": "2025-06-01",
  "expiryDate": "2025-06-20"
}
```

Server computes theoretical option prices at each `underlyingPrice` × `targetDate` combination using Black-Scholes with the current implied vol surface. Returns a grid of P&L values.

## Probability cone overlay

Expose a `showProbabilityCone` option on the **chart widget** (not just analytics widget):
- Fetch current ATM IV for the selected symbol and expiry
- Draw ±1σ and ±2σ price bands projected forward in time from today to expiry
- Shade the cone region with low-opacity fill

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageOptionsAnalytics.jsx` | New — full analytics widget with P&L surface, Greeks panel, ATM chain |
| `src/components/TemplatePageChart.jsx` | Add probability cone overlay option |
| `server/routes/options.js` | Add `POST /api/options/analyze` endpoint (Black-Scholes P&L grid) |
| `server/lib/blackScholes.js` | New — Black-Scholes pricing and Greeks library |
| `src/pages/TemplatePage.jsx` | Register `options-analytics` in render switch and Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon and snippet for new type |
| `docs/template-widgets.md` | Document `options-analytics` widget type |
