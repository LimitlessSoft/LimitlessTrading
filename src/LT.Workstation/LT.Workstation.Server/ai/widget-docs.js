export const WIDGET_DOCS = `Widget Reference
=================

All widgets live on template pages. Use <create_widget>, <update_widget>, <delete_widget> blocks to manage them.
widgetId for updates/deletes comes from the existing widgets list in the page context.

── LAYOUT ──

Width is in grid segments (1–20, where 20 = full page width). Height is in pixels.
When placing N widgets side by side, their widths MUST sum to exactly 20.
  2 equal columns → 10/10
  3 equal columns → 7/7/6
  4 equal columns → 5/5/5/5

dock:"fill-height" — widget fills all remaining viewport height after fixed-height rows. Omit height when using this.
  Multiple equal fill-height rows → also set heightRows:N (e.g. 2 rows → heightRows:2). Single row → omit heightRows.
  IMPORTANT: dock:fill-height does NOT work inside panels. Use fixed pixel heights for panel children.

── WIDGET TYPES ──

label
  Displays text with optional placeholder expressions. Supports conditional styles.
  Required: text (string with placeholders)
  Optional: width (default 4), height (default 60), style, title
  Typical: width=4, height=60
  Example: {"type":"label","text":"@{SPY:last:5}","width":4,"height":60}

badge
  Colored circle or full-width indicator. Color driven by conditional styles.
  Required: placeholder (expression to evaluate)
  Optional: width (default 2), height (default 40), displayMode ("circle"|"full"), showValueAsText, text, style
  Typical: width=2, height=40
  Example: {"type":"badge","placeholder":"\${tws_connection_status:5}","width":2,"height":40}

gauge
  Semicircle speedometer with colored arc bands and a needle.
  Required: placeholder, min, max, segments ([{from, to, color}])
  Optional: title, width (default 6), height (default 160), style
  Typical: width=6, height=160
  Example: {"type":"gauge","placeholder":"@{VIX:last:5}","min":0,"max":80,"title":"VIX","segments":[{"from":0,"to":20,"color":"#48BB78"},{"from":20,"to":40,"color":"#ECC94B"},{"from":40,"to":60,"color":"#ED8936"},{"from":60,"to":80,"color":"#F56565"}],"width":6,"height":160}

graph
  Multi-series rolling time-series chart (line or bars).
  Required: series ([{id, name, expression, color, viewType}])
  Optional: maxHistory (default 200), snapshotInterval (default 300), title, width (default 10), height (default 220), style
  series.viewType: "line" or "bars"
  series.expression: any placeholder expression e.g. "@{SPY:last:5}"
  Typical: width=10, height=220
  Example: {"type":"graph","series":[{"id":"s1","name":"SPY","expression":"@{SPY:last:5}","color":"#4299E1","viewType":"line"}],"width":10,"height":220}

chart
  Interactive candlestick chart with OHLCV data from IBKR.
  Required: symbol (ticker)
  Optional: interval ("1m"|"2m"|"3m"|"5m"|"15m"|"30m"|"1h"|"1d" — default "5m"), width (default 10), height (default 220), style
  Typical: width=10, height=220
  Example: {"type":"chart","symbol":"AAPL","interval":"5m","width":10,"height":220}

panel
  Container that holds child widgets. Children use the same layout algorithm.
  Required: children (array of widget definitions)
  Optional: width (default 20), height (default 200), style (including layoutMode)
  Layout modes (style.layoutMode):
    "grid"     — BEST for N side-by-side widgets. Set style.gridAxis:"columns", style.gridCount:N.
                 Children fill cells automatically — their width/height are ignored.
                 Example: {"type":"panel","width":20,"height":400,"style":{"layoutMode":"grid","gridAxis":"columns","gridCount":2},"children":[{"type":"chart","symbol":"AAPL"},{"type":"chart","symbol":"GOOGL"}]}
    "flow"     — children fill left-to-right by width, wrapping to next row. Children need explicit width and height.
    "absolute" — (default) children need explicit x (0–19) to sit side by side; without x they stack vertically.
  Typical: width=20, height=200

table
  Tabular data widget. Source expression resolves to a JSON array or object; each element becomes a row.
  If source resolves to an array → rows are array elements.
  If source resolves to an object → rows are {key, ...valueFields} per entry.
  Required: source (expression resolving to JSON array/object), columns ([{id, name, expression}])
  Optional: title, width (default 10), height (default 200), style
  Column expression uses {fieldName} tokens referencing row object fields.
  Fallback syntax: {field1 ?? field2 ?? field3} — resolves to the first non-empty value.
  Typical: width=10, height=200
  Example — positions table:
    {"type":"table","source":"&{positions:30}","columns":[{"id":"c1","name":"Instrument","expression":"{description ?? localSymbol ?? symbol}"},{"id":"c2","name":"Qty","expression":"{position}"},{"id":"c3","name":"Avg Cost","expression":"{avgCost}"}],"width":10,"height":200}
  Available fields from &{positions}: description (human-readable e.g. "SPY May05'26 722 PUT"), symbol, localSymbol, position, avgCost, secType, expiry, strike, right, multiplier

── STYLE ──

Default style (applies to label, badge, panel):
  {"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}

Conditional styles (label and badge only):
  Add "conditionalStyles" array to style. First matching rule overrides the specified colors. Null fields are not overridden.
  Each rule: {"id":"r1","placeholder":"expression","operator":"op","value":"val","bgColor":null,"textColor":null,"borderColor":null,"borderWidth":null}
  Operators: equals, contains, >, <, >=, <=, !=
  value is a string for equals/contains, a number for comparison operators.
  Example — green when NYSE open, red when closed:
    "conditionalStyles":[
      {"id":"r1","placeholder":"\${NYSE:status:10}","operator":"equals","value":"Open","bgColor":"#276749","textColor":null,"borderColor":null,"borderWidth":null},
      {"id":"r2","placeholder":"\${NYSE:status:10}","operator":"equals","value":"Closed","bgColor":"#742A2A","textColor":null,"borderColor":null,"borderWidth":null}
    ]`
