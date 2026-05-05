# Portfolio Positions Widget

Add a widget type that displays the current open positions on the connected IBKR account. Shows each position with symbol, quantity, average cost, current price, P&L, and market value — resolved live from TWS/IB Gateway.

## Widget type: `portfolio`

```js
{
  id: "uuid",
  type: "portfolio",
  width: 12,
  height: 300,
  x: 0,
  y: 0,
  showPnl: true,          // show unrealized P&L column
  showAvgCost: true,      // show average cost column
  showMarketValue: true,  // show market value column
  refreshInterval: 5,     // seconds
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1,
    textColor: "#E2E8F0",
    fontSize: 0.85,
    positiveColor: "#38A169",   // positive P&L row tint
    negativeColor: "#E53E3E"    // negative P&L row tint
  }
}
```

## Layout

Flat table — one row per open position:

```
Symbol   Qty   Avg Cost   Last Price   Unrealized P&L   Mkt Value
AAPL     100   182.40     185.30       +$290.00         $18,530
TSLA     -50   240.10     238.60       +$75.00          $11,930
```

Rows with positive unrealized P&L are tinted green, negative red. A totals row at the bottom sums unrealized P&L and market value across all positions.

## Backend — `GET /api/account/positions`

```
GET /api/account/positions
```

Fetches open positions from IBKR via `reqPositions` / `updatePortfolio`. Returns an array of position objects.

Response shape:

```json
[
  {
    "symbol": "AAPL",
    "secType": "STK",
    "exchange": "NASDAQ",
    "quantity": 100,
    "avgCost": 182.40,
    "marketPrice": 185.30,
    "marketValue": 18530.00,
    "unrealizedPnl": 290.00,
    "realizedPnl": 0.00
  }
]
```

Cache TTL: 5 s (positions update on every fill; short TTL keeps display fresh without hammering TWS).

### IBKR API notes

- Use `ib.reqPositions()` — fires `position` events with contract + qty + avg cost
- Market price / value / P&L come from `ib.reqPortfolioUpdates()` (`updatePortfolio` event on the account)
- Both calls require an active TWS/IB Gateway connection through the existing `ibkrClient`
- Flat positions only (no sub-account breakdown needed for MVP)

## Settings dialog

- **Show unrealized P&L** — toggle
- **Show average cost** — toggle
- **Show market value** — toggle
- **Refresh every** — seconds (default 5)
- Standard style fields: background, border, text color, font size, positive/negative tint colors

## Registration

- Add `portfolio` to `WIDGET_REGISTRY` in `widgetRenderer.jsx`
- Add render case to `TemplatePage.jsx` render switch and Add Widget modal
- Register in `WidgetTreePanel.jsx`: icon (briefcase or table icon), color (blue suggested), snippet (position count or "Portfolio")
- If AI widget creation is implemented, add `portfolio` to the `create_widget` tool schema

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePagePortfolio.jsx` | New portfolio positions widget component |
| `src/components/widgetRenderer.jsx` | Register `portfolio` type in `WIDGET_REGISTRY` |
| `src/pages/TemplatePage.jsx` | Add to render switch and Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon, color, snippet for new type |
| `server/routes/account.js` | Add `GET /positions` route alongside existing `/summary` |
| `server/ibkrClient.js` | Add `getPositions()` using `reqPositions` / `updatePortfolio` |
| `docs/template-widgets.md` | Document new widget type and data structure |
