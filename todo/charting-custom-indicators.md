# Charting: Custom Indicators & Full-Session Data

Extend the existing chart widget with a scripting environment for custom indicators, full 24-hour session data (including overnight 8pm–4am), options combo charting, and crash-safe persistence of drawings and indicator configs per symbol.

## Problem

IBKR TWS has ~155 built-in indicators with no way to create custom ones. Charts freeze for 10–45 seconds on symbol switches. Drawings are lost on crash. No overnight chart data is shown despite overnight execution being supported. Multi-leg combo positions cannot be charted as a single series.

## Implementation notes

### Custom indicator scripting

Add an expression-based indicator layer — no full programming language needed. Users define an indicator as a formula over existing series:

```
// Example: custom momentum
(close - close[14]) / close[14] * 100
```

- Supported built-ins: `open`, `close`, `high`, `low`, `volume`, `ema(n)`, `sma(n)`, `rsi(n)`, `atr(n)`, `bbands(n, std)`
- `[n]` lookback operator: `close[1]` = previous bar's close
- Arithmetic operators: `+ - * / ( )`
- Indicator renders as an overlay (same scale) or sub-pane (auto-scaled)
- Compile server-side to a safe sandboxed evaluator (no arbitrary code execution)

### Drawing persistence

- Auto-save every drawing action to the server keyed by `userId + symbol + timeframe`
- On chart load, fetch and replay saved drawings before rendering candles
- Undo/redo stack for drawings stored in memory per session

### Extended session data

- Add a `session` field to chart data requests: `regular` (default) | `extended` | `overnight` | `all`
- Backend fetches pre-market (4am–9:30am ET), regular (9:30am–4pm ET), after-hours (4pm–8pm ET), and overnight (8pm–4am ET) bars and stitches them into one continuous series
- Visual separator lines mark session boundaries on the chart

### Options combo charting

- Accept a `legs` array instead of a single `symbol`: `[{ symbol: "AAPL 250620C00190000", ratio: 1 }, { symbol: "AAPL 250620C00200000", ratio: -1 }]`
- Backend calculates mid-price of the spread each bar: `Σ (leg.bid + leg.ask) / 2 * leg.ratio`
- Chart renders the combo mid as a standard OHLC or line series

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageChart.jsx` | Add indicator scripting UI, drawing persistence, session toggle, combo legs input |
| `server/routes/chart.js` | Extended session stitching, combo mid calculation |
| `server/lib/indicatorEngine.js` | New — formula parser and evaluator for custom indicators |
| `server/lib/drawingStore.js` | New — per-symbol drawing persistence (DB or JSON store) |
| `docs/chart-widget.md` | Document new indicator scripting syntax and combo charting |
