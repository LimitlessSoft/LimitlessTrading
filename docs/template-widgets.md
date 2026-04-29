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

## Positioning

Widgets use absolute positioning with automatic vertical stacking on a 20-segment horizontal grid.

- **x**: horizontal position (0–19 segments), snaps to grid
- **y**: sort order hint — lower values appear higher; actual pixel position is calculated
- **width**: `calc(segments/20 * 100% - 8px)` — the 8px gap keeps widgets from touching
- **Vertical stacking**: widgets that overlap horizontally are stacked with an 8px gap; no overlapping

In edit mode, drag to move:
- Horizontal drag changes `x` (snaps to segment boundaries)
- Vertical drag updates the `y` sort hint; layout recalculates

### Layout Algorithm

Used in `TemplatePage` for top-level widgets and in `TemplatePagePanel` for children:

```js
const calculateLayout = (widgets, gap = 8) => {
  const sorted = [...widgets].sort((a, b) => (a.y || 0) - (b.y || 0))
  const positions = new Map()
  const placed = []

  for (const widget of sorted) {
    let y = 0
    for (const p of placed) {
      if (widget.x < p.x + p.width && widget.x + widget.width > p.x) {
        y = Math.max(y, p.y + p.height + gap)
      }
    }
    positions.set(widget.id, y)
    placed.push({ ...widget, y })
  }
  return positions
}
```

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

All placeholder types can be mixed in the same label text: `${tws_connection_status:5} | @{SPY:last:3} | Spread: %[@{HYG:last} - @{LQD:last}:30]`.

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
