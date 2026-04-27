# Template Controls

Template pages support editable controls that persist to the backend via `route.config.labels`.

## Edit Mode

Toggle via the pencil icon button in the page header.

- **On**: resize handles, delete buttons, and settings dialogs are visible
- **Off**: controls display with their configured styles only

## Control Types

| Type | Component | Description |
|------|-----------|-------------|
| `label` | `TemplatePageLabel` | Text display with styling |
| `panel` | `TemplatePagePanel` | Container holding child controls |
| `badge` | `TemplatePageBadge` | Colored circle indicator |
| `chart` | `TemplatePageChart` | Interactive candlestick chart fed from IBKR — see [chart-control.md](chart-control.md) |

## Positioning

Controls use absolute positioning with automatic vertical stacking on a 20-segment horizontal grid.

- **x**: horizontal position (0–19 segments), snaps to grid
- **y**: sort order hint — lower values appear higher; actual pixel position is calculated
- **width**: `calc(segments/20 * 100% - 8px)` — the 8px gap keeps controls from touching
- **Vertical stacking**: controls that overlap horizontally are stacked with an 8px gap; no overlapping

In edit mode, drag to move:
- Horizontal drag changes `x` (snaps to segment boundaries)
- Vertical drag updates the `y` sort hint; layout recalculates

### Layout Algorithm

Used in `TemplatePage` for top-level controls and in `TemplatePagePanel` for children:

```js
const calculateLayout = (controls, gap = 8) => {
  const sorted = [...controls].sort((a, b) => (a.y || 0) - (b.y || 0))
  const positions = new Map()
  const placed = []

  for (const control of sorted) {
    let y = 0
    for (const p of placed) {
      if (control.x < p.x + p.width && control.x + control.width > p.x) {
        y = Math.max(y, p.y + p.height + gap)
      }
    }
    positions.set(control.id, y)
    placed.push({ ...control, y })
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

### Exchange placeholders — `${EXCHANGE:property:seconds}`

Resolve exchange local time or market status. Computed client-side from `Intl` — no backend call needed.

| Property | Description | Example output |
|----------|-------------|----------------|
| `time`   | Local time at the exchange | `09:32 ET` |
| `status` | Current session | `Open` / `Pre-Market` / `After-Hours` / `Closed` |

Supported exchange keys: `NYSE`, `NASDAQ`, `LSE`, `XETRA`, `TSX`, `JPX`, `HKEX`, `ASX`, `SGX`.  
Full details (hours, timezone per exchange) live at `/docs/placeholders` in the app.

### Symbol quote placeholders — `@{SYMBOL:property:seconds}`

Resolve real-time market data from IBKR. Intervals ≤5s use SSE streaming; >5s use HTTP polling.

| Example | Description |
|---------|-------------|
| `@{SPY:last:5}` | SPY last price, every 5s |
| `@{SPY:changePercent:3}` | SPY % change, every 3s |

Available properties: `last`, `bid`, `ask`, `open`, `high`, `low`, `close`, `prevClose`, `vwap`, `change`, `changePercent`, `volume`, `avgVolume`, `bidSize`, `askSize`, `marketCap`, `week52High`, `week52Low`.

Both types can be mixed in the same label text: `${tws_connection_status:5} | @{SPY:last:3}`.

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
    verticalAlign: "center" // top | center | bottom
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
  children: [],         // nested controls (labels or panels)
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
  placeholder: "${tws_connection_status:5}",
  colorMappings: [
    { value: "Connected", color: "#48BB78", contains: true },
    { value: "Disconnected", color: "#F56565", contains: false }
  ],
  defaultColor: "#718096",
  style: {
    bgColor: "transparent",
    borderColor: "#718096",
    borderWidth: 0
  }
}
```

`contains: true` checks if the resolved value *contains* the string; `false` requires an exact match.

## Component Reference

All control components share the same props interface:

| Prop | Type | Description |
|------|------|-------------|
| `label` | object | Control data object |
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

Container that holds nested child controls.

- Header bar in edit mode: drag to move, Add / Settings / Delete buttons
- Children auto-stack using the same layout algorithm
- Panels can be nested arbitrarily deep
- Content area scrolls when children exceed panel height

### TemplatePageBadge

Colored circle that reflects a placeholder value.

- Color resolved by testing `colorMappings` in order; falls back to `defaultColor`
- Tooltip shows the current raw value on hover

## Adding a New Control Type

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
6. Register the new type in `TemplatePage.jsx` render switch and in the Add Control modal
