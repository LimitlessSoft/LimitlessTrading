# Options Chain Widget

Add a widget type that displays the options chain for a given underlying symbol. Shows calls and puts side-by-side across strikes for a selected expiration date.

## Widget type: `options-chain`

```js
{
  id: "uuid",
  type: "options-chain",
  width: 16,
  height: 400,
  x: 0,
  y: 0,
  symbol: "AAPL",
  expiration: null,        // null = nearest expiry; ISO date string to pin
  strikeRange: 10,         // number of strikes above and below ATM to show
  showGreeks: true,        // show delta, gamma, theta, vega columns
  highlightITM: true,      // shade in-the-money rows
  refreshInterval: 5,      // seconds
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1,
    textColor: "#E2E8F0",
    fontSize: 0.85,
    callColor: "#38A169",   // ITM call row tint
    putColor: "#E53E3E"     // ITM put row tint
  }
}
```

## Layout

Two-panel table with strikes in the center column:

```
[ CALLS ]          Strike    [ PUTS ]
Bid  Ask  Vol  OI  Price  OI  Vol  Ask  Bid
```

Optional Greek columns (delta, gamma, theta, vega) shown on each side when `showGreeks: true`.

Header row: expiration date selector (dropdown populated from available expiries). Current underlying price shown above the table so the ATM strike is visually obvious.

## Backend — `GET /api/options`

`GET /api/options?symbol=AAPL&expiration=2025-06-20`

Returns calls and puts for one expiration. The `expiration` param is optional; omitting it returns the nearest available expiry.

Response shape:

```json
{
  "symbol": "AAPL",
  "expiration": "2025-06-20",
  "underlyingPrice": 182.50,
  "expirations": ["2025-05-17", "2025-06-20", "2025-09-19"],
  "calls": [
    {
      "strike": 180,
      "bid": 4.10,
      "ask": 4.20,
      "last": 4.15,
      "volume": 1200,
      "openInterest": 8400,
      "iv": 0.28,
      "delta": 0.54,
      "gamma": 0.04,
      "theta": -0.08,
      "vega": 0.12
    }
  ],
  "puts": [ /* same shape */ ]
}
```

### Data source options

| Source | Notes |
|--------|-------|
| **IBKR TWS** | `reqContractDetails` + `reqMktData` for options; most accurate for live trading use; requires active session |
| **Tradier** | Free developer tier; `GET /markets/options/chains`; clean REST API |
| **Polygon.io** | Options snapshot endpoint; free tier rate-limited |

Recommended: **IBKR** if the existing TWS connection is already in use; otherwise **Tradier** for simplicity.

## Settings dialog

- **Symbol** — ticker input
- **Strike range** — number of strikes above/below ATM (default 10)
- **Show Greeks** — toggle
- **Highlight ITM** — toggle
- **Refresh every** — seconds (default 5)
- Standard style fields: background, border, text color, font size, call/put tint colors

## Registration

- Add `options-chain` to the `TemplatePage.jsx` render switch and Add Widget modal
- Register in `WidgetTreePanel.jsx`: icon (table or chain icon), color (purple suggested), snippet (symbol name)
- If AI widget creation is implemented, add `options-chain` to the `create_widget` tool schema

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageOptionsChain.jsx` | New options chain widget component |
| `src/pages/TemplatePage.jsx` | Register new type in render switch + Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon, color, snippet for new type |
| `server/routes/options.js` | `GET /api/options` with caching |
| `server/index.js` | Mount options router |
| `docs/template-widgets.md` | Document new widget type and data structure |
