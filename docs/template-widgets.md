# Template Widgets

Template pages support editable widgets that persist to the backend via `route.config.labels`.

## Edit Mode

Toggle via the pencil icon button in the page header.

- **On**: resize handles, delete buttons, and settings dialogs are visible
- **Off**: widgets display with their configured styles only

## Widget Types

| Type | Component | Description |
|------|-----------|-------------|
| `label` | `TemplatePageLabel` | Text display with styling |
| `panel` | `TemplatePagePanel` | Container holding child widgets |
| `badge` | `TemplatePageBadge` | Colored circle indicator |
| `chart` | `TemplatePageChart` | Interactive candlestick chart fed from IBKR — see [chart-widget.md](chart-widget.md) |
| `gauge` | `TemplatePageGauge` | Semicircle speedometer with configurable colored segments |
| `graph` | `TemplatePageGraph` | Multi-series rolling time-series chart (line or bar) |

## Positioning

Widgets use absolute positioning on a 20-segment horizontal grid with a flow layout that wraps to the next row.

- **y**: sort order hint — lower values appear earlier in the flow
- **width**: `calc(segments/20 * 100% - 8px)` — the 8px gap keeps widgets from touching
- **Flow wrapping**: widgets fill left-to-right; when the next widget doesn't fit in the current row it wraps to the next row with an 8px vertical gap

The stored `x` value on a widget is ignored at the root level — horizontal position is entirely determined by the flow. Use the Widget Tree panel or drag vertically to reorder.

### Layout Algorithm

Used in `TemplatePage` for top-level widgets:

```js
const calculateLayout = (widgets, gap = 8) => {
  const sorted = [...widgets].sort((a, b) => (a.y || 0) - (b.y || 0))
  const positions = new Map()
  let curX = 0, curY = 0, rowH = 0

  for (const widget of sorted) {
    const w = widget.width
    const h = widget.height
    if (curX > 0 && curX + w > 20) { curY += rowH + gap; curX = 0; rowH = 0 }
    positions.set(widget.id, { x: curX, y: curY })
    curX += w
    rowH = Math.max(rowH, h)
  }
  return positions
}
```

`TemplatePagePanel` children use the same algorithm when `style.layoutMode === 'flow'`.

## Placeholders

Labels and badges support dynamic values via two syntaxes. The full reference is available in the app at `/docs/placeholders`.

### System placeholders — `${name:seconds}`

Resolve fixed system values. The `:seconds` part sets the refresh interval; when a label has multiple placeholders the shortest interval wins.

| Placeholder | Description |
|-------------|-------------|
| `${tws_connection_status:5}` | TWS connection state — `"Connected (port)"` or `"Disconnected"` |
| `${fomc_next:3600}` | Next FOMC decision date — e.g. `"Jun 17 2026"` |
| `${fomc_countdown:60}` | Countdown to next FOMC decision — e.g. `"49d 3h 22m"` or `"2h 15m 30s"` |

### Exchange placeholders — `${EXCHANGE:property:seconds}`

Resolve exchange local time or market status. Computed client-side from `Intl` — no backend call needed.

| Property | Description | Example output |
|----------|-------------|----------------|
| `time`   | Local time at the exchange | `09:32 ET` |
| `status` | Current session | `Open` / `Pre-Market` / `After-Hours` / `Closed` |

Supported exchange keys: `NYSE`, `NASDAQ`, `LSE`, `XETRA`, `TSX`, `JPX`, `HKEX`, `ASX`, `SGX`.  
Full details (hours, timezone per exchange) live at `/docs/placeholders` in the app.

### Symbol quote placeholders — `@{SYMBOL:property-arg1-arg2:seconds}`

Resolve real-time market data from IBKR. Intervals ≤5s use SSE streaming; >5s use HTTP polling.

Properties are either plain names (`last`, `changePercent`) or a base name with dash-separated arguments (`close-20`, `close-5`). The `:seconds` interval is optional and defaults to 5.

| Example | Description |
|---------|-------------|
| `@{SPY:last:5}` | SPY last price, every 5s |
| `@{SPY:changePercent:3}` | SPY % change, every 3s |
| `@{HYG:close-20:3600}` | HYG close from 20 trading days ago, refreshed hourly |

**Real-time properties** (refreshed at the given interval via SSE or polling):

`last`, `bid`, `ask`, `open`, `high`, `low`, `close`, `prevClose`, `vwap`, `change`, `changePercent`, `volume`, `avgVolume`, `bidSize`, `askSize`, `marketCap`, `week52High`, `week52Low`

**Historical close — `close-N`** (N = any number of trading days):

The closing price from exactly N trading days ago. Fetched from IBKR historical data, cached server-side for 1 hour. Use a slow refresh interval since the value only changes once per trading day.

```
@{HYG:close-20:3600}   → HYG close from 20 trading days ago
@{SPY:close-5:3600}    → SPY close from 5 trading days ago
@{VIX:close-17:3600}   → VIX level from 17 trading days ago
```

### Formula placeholders — `%[expression:seconds]`

Evaluate an arithmetic expression over live quote values. Ticker refs inside the expression use `@{SYMBOL:property}` with no interval — the formula's `:seconds` applies to all of them. Use standard JS arithmetic operators and parentheses freely within the expression.

| Example | Description |
|---------|-------------|
| `%[@{HYG:last} - @{LQD:last}:30]` | Price spread: HYG minus LQD |
| `%[(@{HYG:last} / @{LQD:last} - 1) * 100:30]` | HYG vs LQD relative performance (%) |
| `%[@{HYG:changePercent} - @{LQD:changePercent}:10]` | Day % change spread |
| `%[(@{AAPL:ask} + @{AAPL:bid}) / 2:3]` | Bid-ask midpoint |
| `%[(@{SPY:last} / @{SPY:vwap} - 1) * 100:5]` | SPY deviation from VWAP |
| `%[(@{HYG:last} / @{HYG:close-20} - 1) * 100:3600]` | HYG 20-day return (%) |
| `%[(@{HYG:last} / @{HYG:close-20} - 1) * 100 - (@{LQD:last} / @{LQD:close-20} - 1) * 100:3600]` | HYG vs LQD 20-day relative performance |

`close-N` properties can be mixed freely with real-time properties. Use a slow interval (e.g. `:3600`) when the formula includes historical closes since they only change once per day.

Result is formatted to 2 decimal places. Displays `--` if any referenced value is unavailable.

Formula placeholders are resolved before standalone symbol quotes, so their internal `@{...}` refs are not double-processed.

All placeholder types can be mixed in the same label text: `${tws_connection_status:5} | @{SPY:last:3} | #{NetLiquidation:30} | &{AAPL:position:30} | Spread: %[@{HYG:last} - @{LQD:last}:30]`.

### Position placeholders — `&{SYMBOL:property:seconds}`

Resolve portfolio position data from IBKR. Polled from `/api/account/positions` (10s server-side cache). Default refresh interval is 30s.

| Property | Description | Format |
|----------|-------------|--------|
| `position` | Shares/contracts held (negative = short) | Integer |
| `avgCost` | Average cost per share/contract | 2 decimals |

Example: `&{AAPL:position:30}` — shares of AAPL held, refreshed every 30s.

Position values are substituted as plain numbers before formula evaluation, so they compose freely with `%[...]` formulas:

```
%[&{AAPL:position} * @{AAPL:last}:10]              → live market value of AAPL position
%[(@{AAPL:last} - &{AAPL:avgCost}) * &{AAPL:position}:10]   → unrealized P&L for AAPL
%[(&{AAPL:position} * @{AAPL:last}) / #{NetLiquidation} * 100:10]  → AAPL as % of portfolio
```

Returns `--` for symbols not held or when IBKR is disconnected.

### Account placeholders — `#{field:seconds}`

Resolve IBKR account summary fields. Polled from `/api/account/summary` (10s server-side cache).

Syntax: `#{field}` or `#{field:seconds}`. Default refresh interval is 30s.

Supported fields:

| Field | Description |
|-------|-------------|
| `NetLiquidation` | Total net liquidation value |
| `TotalCashValue` | Total cash balance |
| `GrossPositionValue` | Total gross position value |
| `BuyingPower` | Available buying power |
| `AvailableFunds` | Available funds |
| `ExcessLiquidity` | Excess liquidity |
| `RealizedPnL` | Realized P&L |
| `UnrealizedPnL` | Unrealized P&L |
| `DailyPnL` | Day P&L |
| `MaintMarginReq` | Maintenance margin requirement |
| `InitMarginReq` | Initial margin requirement |

Example: `#{NetLiquidation:30}` — refreshes every 30s.

Unknown placeholders are displayed as-is.

### Adding a new resolver

When implementing a new placeholder resolver, also add it to the in-app documentation in `src/pages/PlaceholderDocsPage.jsx` so it appears at `/docs/placeholders`.

## Data Structures

### Label

```js
{
  id: "uuid",
  type: "label",        // optional — defaults to label
  text: "Label content",
  width: 4,             // 1–20 segments
  height: 60,           // pixels (min 40)
  x: 0,                 // horizontal position (segments 0–19)
  y: 0,                 // sort order hint
  createdAt: timestamp,
  style: {
    bgColor: "#4A5568",
    borderColor: "#718096",
    borderWidth: 0,
    textColor: "#FFFFFF",
    fontWeight: "normal",   // normal | bold | lighter
    fontStyle: "normal",    // normal | italic
    fontSize: 1,            // rem
    textAlign: "left",      // left | center | right
    verticalAlign: "center", // top | center | bottom
    conditionalStyles: [    // optional — first matching rule overrides colors
      {
        id: "uuid",
        placeholder: "${LSE:status:10}", // placeholder expression to evaluate
        operator: "contains",            // contains | equals | > | < | >= | <= | !=
        value: "Open",                   // string (contains/equals) or number (comparisons)
        bgColor: "#276749",              // null = no override
        textColor: null,
        borderColor: null,
        borderWidth: null
      }
    ]
  }
}
```

### Panel

```js
{
  id: "uuid",
  type: "panel",
  width: 8,             // default 8 segments
  height: 120,          // pixels (min 58)
  x: 0,
  y: 0,
  createdAt: timestamp,
  children: [],         // nested widgets (labels or panels)
  style: {
    bgColor: "#2D3748",
    borderColor: "#4A5568",
    borderWidth: 1
  }
}
```

### Badge

```js
{
  id: "uuid",
  type: "badge",
  width: 2,             // default 2 segments
  height: 40,           // pixels (min 20)
  x: 0,
  y: 0,
  placeholder: "${tws_connection_status:5}",  // value provider
  showValueAsText: false,
  displayMode: "circle",                      // circle | full
  text: "",                                   // optional label text
  style: {
    bgColor: "transparent",
    borderColor: "#718096",
    borderWidth: 0,
    badgeColor: "#718096",        // default badge indicator color
    conditionalStyles: [          // first matching rule overrides style fields
      {
        id: "uuid",
        placeholder: "${tws_connection_status:5}",
        operator: "contains",     // contains | equals | > | < | >= | <= | !=
        value: "Connected",
        badgeColor: "#48BB78",    // null = no override
        bgColor: null,
        borderColor: null,
        borderWidth: null
      }
    ]
  }
}
```

### Gauge

```js
{
  id: "uuid",
  type: "gauge",
  width: 4,             // segments
  height: 120,          // pixels
  x: 0,
  y: 0,
  placeholder: "@{VIX:last:5}",  // value provider (any placeholder expression)
  min: 0,               // gauge scale minimum (default 0)
  max: 100,             // gauge scale maximum (default 100)
  title: "VIX",         // optional label below the needle
  segments: [           // colored arc bands (default: green/yellow/orange/red quartiles)
    { from: 0, to: 25, color: "#48BB78" },
    { from: 25, to: 50, color: "#ECC94B" },
    { from: 50, to: 75, color: "#ED8936" },
    { from: 75, to: 100, color: "#F56565" }
  ],
  style: {
    bgColor: "transparent",
    borderColor: "#718096",
    borderWidth: 0
  }
}
```

### Graph

```js
{
  id: "uuid",
  type: "graph",
  width: 8,             // segments
  height: 160,          // pixels
  x: 0,
  y: 0,
  title: "",            // optional title shown in the widget header
  maxHistory: 200,      // max data points retained per series (default 200)
  snapshotInterval: 300, // fetch interval in seconds for snapshot series (default 300)
  series: [             // one entry per data series
    {
      id: "uuid",
      name: "SPY",                      // legend label
      mode: "live",                     // "live" (SSE/polling) or "snapshot" (periodic fetch)
      expression: "@{SPY:last:5}",      // placeholder for live; @{SYMBOL:property} for snapshot
      color: "#4299E1",
      viewType: "line"                  // "line" or "bars"
    }
  ],
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1
  }
}
```

**Live series** use any `@{SYMBOL:property:seconds}` expression; the value is sampled into rolling history on each update.

**Snapshot series** use `@{SYMBOL:property}` (no interval) and fetch from `/api/bars/:symbol/:property` on the shared `snapshotInterval`. Use `viewType: "bars"` to show periodic values as a bar chart aligned by index.

## Component Reference

All widget components share the same props interface:

| Prop | Type | Description |
|------|------|-------------|
| `label` | object | Widget data object |
| `layoutY` | number | Calculated pixel y position from parent layout |
| `onDelete(id)` | fn | Called when delete button is clicked |
| `onUpdate(id, changes)` | fn | Called for any property change |
| `isEditing` | bool | Whether edit mode is active |
| `containerRef` | ref | Parent container ref for width calculations |

### TemplatePageLabel

Resizable, draggable text label.

- Three resize handles in edit mode: right edge (horizontal), bottom edge (vertical), corner (both)
- Style dialog: colors, border, font
- Drag preview state held locally until backend update completes to prevent flicker

### TemplatePagePanel

Container that holds nested child widgets.

- Header bar in edit mode: drag to move, Add / Settings / Delete buttons
- Children auto-stack using the same layout algorithm
- Panels can be nested arbitrarily deep
- Content area scrolls when children exceed panel height

### TemplatePageBadge

Colored circle that reflects a placeholder value.

- Badge color driven by `style.badgeColor` (default) overridden by conditional styles rules
- Tooltip shows the current resolved value on hover

### TemplatePageGauge

Semicircle speedometer rendering a single numeric placeholder value.

- Arc divided into colored segments defined by `min`/`max`-relative `from`/`to` ranges
- Needle position tracks the live resolved value; shows `--` when unavailable
- Title and current value displayed below the arc

### TemplatePageGraph

Multi-series rolling chart that accumulates live or snapshot data points over time.

- **Live series**: samples a placeholder expression on each resolved update into a rolling buffer (`maxHistory` points)
- **Snapshot series**: periodically fetches `/api/bars/:symbol/:property` and appends bar values
- Each series can render as `line` or `bars`; multiple series share the same time axis
- Hover tooltip shows values from all series at the hovered timestamp

## Adding a New Widget Type

1. Create `src/components/TemplatePage[Name].jsx`
2. Accept the standard props: `label`, `layoutY`, `onDelete`, `onUpdate`, `isEditing`, `containerRef`
3. Position with:
   ```jsx
   position="absolute"
   left={`${(label.x / 20) * 100}%`}
   top={`${layoutY}px`}
   width={`calc(${(label.width / 20) * 100}% - 8px)`}
   ```
4. Gate all edit UI behind `isEditing`
5. Keep local preview state during drag; clear it once `label` prop catches up
6. Register the new type in `TemplatePage.jsx` render switch and in the Add Widget modal

## Widget Tree Panel

The widget tree panel is a 260 px sidebar that slides in from the right edge of the page. It gives a structured, hierarchical view of every widget — useful for navigating dense layouts without hunting on the canvas.

### Opening the panel

- Click the **Widget tree** button in the edit-mode toolbar (top-right when editing), or
- Use the **Widget tree** item in the page action menu.

The button appears highlighted (solid blue) while the panel is open. Click **Hide tree** / **Hide widget tree**, or the × in the panel header, to close it.

### Layout of the tree

Widgets are shown in two labeled sections:

| Section | Contents | Sort order |
|---------|----------|------------|
| **Page** | Top-level page widgets (`config.labels`) | `y` ascending |
| **Header** | Header widgets (`config.headerControls`) | `x` ascending |

Panel children appear as indented nodes beneath their parent, connected by a vertical guide line. Panels with children show a **chevron** that collapses or expands that branch.

Each row shows:
- A **type icon** in a color unique to that type (blue = label, purple = panel, green = badge, amber = chart, cyan = gauge, pink = graph)
- The widget **type name**
- A **snippet** — a short preview of the widget's content:
  - `label` → up to 30 chars of label text
  - `panel` → child count (e.g. `3 children`)
  - `badge` / `gauge` → up to 30 chars of the placeholder expression
  - `chart` → ticker symbol
  - `graph` → series count (e.g. `2 series`)
- A **settings icon** that appears on hover to open the widget's settings dialog
- A **grip icon** that appears on hover to indicate the row is draggable

Clicking a row selects it (highlighted background); clicking again deselects it. Selection is visual only and does not open a settings dialog.

### Reordering via drag-and-drop

Rows within the same section or panel can be reordered by dragging. A blue drop-line marks the insertion point.

Constraints:
- Only siblings can be reordered — dragging across sections or into/out of a panel is not supported.

When a drop completes, `handleReorderWidgets` in `TemplatePage.jsx` assigns updated `y` values (page widgets and panel children) or `x` values (header widgets) and persists the change immediately via `updateRoute`.

### Adding a type to the tree

When adding a new widget type, register it in `WidgetTreePanel.jsx`:
1. Add an SVG entry to the `ICONS` map.
2. Add a hex color to `TYPE_COLORS`.
3. Add a case to the `snippet` function to return a meaningful preview string.

## Shared behavior via hooks

When behavior needs to be added to all widget components, use the widget hook pattern rather than duplicating code in each file. See [widget-hook-pattern.md](widget-hook-pattern.md) for the full pattern and examples.
