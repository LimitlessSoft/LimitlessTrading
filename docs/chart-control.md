# Chart Control

`TemplatePageChart` is a candlestick chart control that fetches historical OHLCV data from IBKR and renders it as an interactive SVG inside a template page.

## Adding to a Template Page

Select **Chart** from the Add Control menu in edit mode. The control requires a symbol and interval to be set before data is loaded.

---

## Configuration

Opened via the settings (⚙) icon in the top-right corner of the control. Settings are per-chart unless marked **Global**.

### Per-Chart Settings

| Setting | Description |
|---------|-------------|
| Symbol | Ticker symbol (e.g. `AAPL`, `SPY`) |
| Interval | Bar interval — see supported intervals below |

### Global Chart Settings

Global settings apply to **all** chart controls on all pages simultaneously. They are persisted to `localStorage` under the key `ar_global_chart_settings`.

| Setting | Default | Description |
|---------|---------|-------------|
| RTH Background | `#1a2520` | Background colour for Regular Trading Hours bars |
| Outside RTH Background | `#1a1a2e` | Background colour for pre/post-market bars |
| Font Size | `9` | Axis label and top-bar font size (px) |
| Volume Multiplier | `1` | Scale factor applied to volume bar heights. Values > 1 make bars taller; useful when volume is low relative to the chart area |
| Invert Scroll | `false` | Reverses the pan direction of Ctrl+scroll |
| Invert Drag | `false` | Reverses the pan direction of click-drag |
| Clear All Caches | — | Wipes all cached price data from IndexedDB for all symbols and intervals |

---

## Supported Intervals

| Value | Label | Initial Load | Max History |
|-------|-------|-------------|-------------|
| `1m`  | 1 Minute | 5 days | 2 years |
| `2m`  | 2 Minutes | 10 days | 2 years |
| `3m`  | 3 Minutes | 30 days | 5 years |
| `5m`  | 5 Minutes | 30 days | 10 years |
| `15m` | 15 Minutes | 60 days | 10 years |
| `30m` | 30 Minutes | 90 days | 10 years |
| `1h`  | 1 Hour | 90 days | 10 years |
| `1d`  | Daily | 1 year | 10 years |

The interval is changed by clicking the interval label (e.g. `· 1h`) in the top bar.

---

## Interactions

### Navigation

| Action | Effect |
|--------|--------|
| Scroll wheel | Zoom in/out (fewer or more bars visible) |
| Ctrl + scroll | Pan left/right along the time axis |
| Click + drag | Pan left/right along the time axis |
| Hover over candle | Show OHLC tooltip in top bar and crosshair lines |

Zoom range: 20–1000 visible bars (default 300). Each scroll tick scales by ~15%.

Drag and scroll directions can be inverted in Global Chart Settings.

### Symbol Editing

Click the **symbol label** (e.g. `SPY`) in the top-left of the chart to edit it inline. Press Enter to confirm or Escape to cancel.

---

## Toolbar Buttons

| Button | Action |
|--------|--------|
| ↺ (Reload) | Fetches the last 30 days from IBKR and merges into cache. Fast — does not re-fetch full history |
| ↺↺ (Full Reload) | Clears and re-fetches the full initial window from IBKR with a progress indicator |
| ⚙ (Settings) | Opens per-chart and global settings dialog |
| ✕ (Delete) | Removes the control (edit mode only) |

---

## Chart Visuals

### Layout

```
┌─────────────────────────────────────────────┐
│ SPY · 1h   O:701.00  H:703.12  L:700.65  C:702.50  │  ← top bar
├─────────────────────────────────────────────┤
│                                             │
│         [candlestick area  ~78%]            │
│                                             │
├─────────────────────────────────────────────┤
│         [volume area       ~16%]            │
├─────────────────────────────────────────────┤
│  04:00 PM    08:00 AM   12:00 PM   04:00 PM │  ← time axis
└─────────────────────────────────────────────┘
```

The right edge shows 5 horizontal price grid labels. Left padding is minimal (8px); right padding scales with font size.

### Candle Sizing

Candle body width = `spacing × 0.6` where `spacing = chartWidth / visibleBars`. This ensures candles fill their slot proportionally at any zoom level with no fixed cap.

### RTH Background Bands (intraday only)

Each bar within NYSE Regular Trading Hours (09:30–16:00 ET) gets a background rect drawn in the RTH colour. Bars outside those hours use the outside-RTH colour as a base layer. Both colours are configurable in Global Chart Settings.

### Day Separators

A vertical dashed line (`#4A5568`, 4px dash) is drawn at the left edge of the first bar of each calendar day, making session boundaries visible at a glance.

### Crosshair

Appears while hovering over the chart:

- **Vertical line** — snaps to the centre of the hovered candle
- **Horizontal line** — follows exact mouse Y position
- **Price label** — drawn over the right-axis at the crosshair Y, showing the price at that level

### Volume Bars

Volume bars are rendered at the bottom of the chart area. Height is proportional to the bar's volume relative to the maximum visible volume, scaled by the **Volume Multiplier** global setting. Bars are capped at the full volume area height even when the multiplier is > 1.

---

## Data Loading

### Initial Load

On first load (empty cache) the chart fetches `initialDays` worth of data (see table above). Each interval is divided into fixed-size chunks fetched in batches of 5 parallel IBKR requests.

### Cache

Data is stored in IndexedDB via `addPriceData` / `getPriceData`. On subsequent loads, if the newest cached bar is within the last 3 days, the cache is used as-is with no IBKR fetch.

### Lazy History Expansion

As the user pans backward, older data is fetched automatically:

- Fetch is triggered when the **oldest visible bar** is within 150 bars of the oldest cached bar
- Each lazy-load fetches 5 chunks (parallel) going further back
- A small spinner appears in the bottom-left while fetching
- When IBKR returns no data for a batch, `hasMoreHistory` is set to `false` and no further fetches are attempted for that symbol/interval session

### Reload Behaviour

- **Reload button (fast)**: calls `refreshRecentData` — fetches the last 30 days and merges, leaving older cache intact
- **Full Reload button**: calls `loadHistoricalData` — re-fetches the full `initialDays` window from scratch

### IBKR Request Parameters

- `useRTH = 0` — includes pre/post-market data
- Request timeout: 15 seconds per chunk (requests that exceed this are treated as empty and skipped)
- Max parallel requests: 5 (kept low to respect IBKR pacing limits)

---

## Data Flow

```
TemplatePageChart
  ├─ on mount / symbol+interval change
  │    └─ fetchFromCache → if empty → loadHistoricalData
  ├─ on viewOffset / maxBars change (near left edge)
  │    └─ fetchOlderData (5 parallel chunks) → merge into cache → setData
  ├─ Reload button
  │    └─ refreshRecentData (last 30 days) → merge → setData
  └─ Full Reload button
       └─ loadHistoricalData (initialDays) → setData with progress
```

---

## Data Structure

```js
{
  id: "uuid",
  type: "chart",
  symbol: "SPY",
  interval: "1h",
  width: 10,           // 1–20 segments
  height: 220,         // pixels (min 100)
  x: 0,
  y: 0,
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1
  }
}
```

---

## Module Reference

### `src/components/TemplatePageChart.jsx`

Contains two components:

- **`ChartSVG`** — pure SVG renderer. Receives pre-sliced `data`, dimensions, colours, and event callbacks. Owns tooltip and crosshair state locally.
- **`TemplatePageChart`** — stateful wrapper. Owns data fetching, zoom/pan state, settings modal, resize/drag handles.

Key state:

| State | Description |
|-------|-------------|
| `data` | Full sorted (ascending) array of cached bars for the current symbol+interval |
| `viewOffset` | Bars from the right end currently hidden (0 = most recent data at right edge) |
| `maxBars` | Number of bars in the visible window (zoom level) |
| `isFetchingOlder` | True while a lazy-load fetch is in-flight |
| `hasMoreHistory` | False once IBKR returns no data for an older batch |

Key refs:

| Ref | Description |
|-----|-------------|
| `loadGenRef` | Monotonic counter; incremented on symbol/interval change. Callbacks check this to discard stale results from cancelled loads |
| `globalSettingsRef` | Mirror of `globalSettings` state kept in sync for use inside non-reactive wheel/drag handlers |
| `cancelRef` | Cancel function for the active `loadHistoricalData` call |
| `olderCancelRef` | Cancel function for the active `fetchOlderData` call |

### `src/modules/globalChartSettings.js`

| Export | Description |
|--------|-------------|
| `DEFAULT_GLOBAL_CHART_SETTINGS` | Default values object |
| `getGlobalChartSettings()` | Reads from `localStorage`, merging with defaults |
| `saveGlobalChartSettings(settings)` | Writes to `localStorage` and dispatches `globalChartSettingsChanged` CustomEvent so all open charts update simultaneously |
| `isRTHTimestamp(timestamp)` | Returns `true` if the timestamp falls within NYSE RTH (09:30–16:00 ET) using `Intl.DateTimeFormat` |

### `src/modules/dataService.js` — chart-related exports

| Export | Description |
|--------|-------------|
| `loadHistoricalData(symbol, interval, onProgress, onComplete, onError)` | Fetches `initialDays` of data in parallel batches. Returns `{ cancel }`. `onProgress(data, 0–100)` fires after each chunk |
| `fetchOlderData(symbol, interval, beforeTimestamp, onProgress, onComplete, onError)` | Fetches 5 chunks (parallel) going back from `beforeTimestamp`. `onComplete(data, hasData)` — `hasData` is false if IBKR returned nothing |
| `refreshRecentData(symbol, interval, days?)` | Fetches last N days (default 30) and merges into cache. Returns updated cache array |
| `fetchFromCache(symbol, interval)` | Returns cached data from IndexedDB without any IBKR call |
| `clearAllCache()` | Deletes all price data from IndexedDB |
| `INTERVALS` | Array of `{ value, label }` for the interval picker |
