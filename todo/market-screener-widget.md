# Market Screener Widget

Add a `screener` widget type with no result cap, deterministic and timestamped results, saveable filter configurations, multi-asset-class support (equities, ETFs, options, bonds), and per-result explanations showing which criteria each match triggered.

## Problem

IBKR's scanner caps results at 50 with no way to expand. Results are explicitly non-deterministic — the same scan returns different results on different runs, undisclosed in the UI. The bond scanner is effectively unusable. Screener configs cannot be saved. Maximum of 10 filter criteria per screen.

## Widget type: `screener`

```js
{
  id: "uuid",
  type: "screener",
  width: 24,
  height: 500,
  savedConfigId: "my-momentum-scan",   // null = unsaved
  assetClass: "equities",             // equities | etfs | options | bonds
  filters: [
    { field: "marketCap", operator: "greaterThan", value: 1000000000 },
    { field: "volume", operator: "greaterThan", value: 500000 },
    { field: "rsi14", operator: "lessThan", value: 30 },
    { field: "priceChange1d", operator: "lessThan", value: -0.05 }
  ],
  sortBy: "volume",
  sortDir: "desc",
  columns: ["symbol", "price", "priceChange1d", "volume", "marketCap", "rsi14"],
  style: { bgColor: "#1a202c", textColor: "#E2E8F0" }
}
```

## Layout

```
[ Asset class: Equities ▾ ]  [ Saved scans: My momentum ▾ ]  [ Save ]  [ Run ]

Filters:
  Market cap  >  $1B          [×]
  Volume      >  500,000      [×]
  RSI(14)     <  30           [×]
  1d change   <  -5%          [×]
  [ + Add filter ]

Last run: 2025-06-01 14:32:05 ET  —  248 results

| Symbol | Price | 1d %  | Volume   | Mkt Cap | RSI14 | Why matched         |
|--------|-------|-------|----------|---------|-------|---------------------|
| EXAMPLE| 42.10 | -6.2% | 8.2M     | $4.1B   | 28.4  | Vol ✓  RSI ✓  Chg ✓ |
```

- No result cap — paginate in batches of 100
- Timestamp on every run; same filters always produce same results at a given timestamp
- "Why matched" column shows which filter conditions each result satisfied

## Filter fields

**Equities / ETFs:** `price`, `priceChange1d`, `priceChange5d`, `volume`, `avgVolume20d`, `marketCap`, `pe`, `eps`, `dividendYield`, `beta`, `rsi14`, `ema20`, `sma50`, `sma200`, `atr14`, `52wHigh`, `52wLow`, `sector`, `exchange`

**Options:** `underlyingSymbol`, `strike`, `expiry`, `daysToExpiry`, `impliedVol`, `delta`, `openInterest`, `volume`, `bid`, `ask`, `spread`

**Bonds:** `issuer`, `maturityDate`, `couponRate`, `yieldToMaturity`, `creditRating`, `duration`, `faceValue`

## Saved configurations

```js
// GET /api/screener/configs
// POST /api/screener/configs  { name, assetClass, filters, sortBy, columns }
// DELETE /api/screener/configs/:id
```

Configs stored server-side per user; also exportable as JSON.

## Backend

`POST /api/screener/run`

```json
{
  "assetClass": "equities",
  "filters": [...],
  "sortBy": "volume",
  "sortDir": "desc",
  "page": 0,
  "pageSize": 100
}
```

Data sources:
- Equities/ETFs: pull from IBKR scanner API or a supplementary data provider (Polygon.io, Tradier) for fundamental fields IBKR doesn't expose
- Options: IBKR `reqContractDetails` + snapshot data
- Bonds: supplementary provider (FINRA TRACE, Tradier)

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageScreener.jsx` | New — screener widget with filter builder, results table, pagination |
| `src/components/ScreenerFilterBuilder.jsx` | New — reusable filter row builder component |
| `server/routes/screener.js` | New — `POST /api/screener/run` and config CRUD |
| `server/lib/screenerEngine.js` | New — filter evaluation, data aggregation, result ranking |
| `src/pages/TemplatePage.jsx` | Register `screener` in render switch and Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon and snippet for `screener` type |
| `docs/template-widgets.md` | Document `screener` widget type |
