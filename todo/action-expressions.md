# Action Expressions

Add a new expression system for widgets that executes an operation on demand rather than resolving a display value. This is infrastructure used by the button widget and order-ticket widget.

## Syntax

Action expressions use `![ ]` and cannot appear in label text or any read field. They are stored in widget config and fired when the user triggers the widget.

```
![order:BUY:AAPL:100:MKT]
![order:SELL:AAPL:100:LMT:185.50]
![order:BUY:SPY:100:LMT:@{SPY:last}]    ← @{} placeholder resolves at fire time
```

Order action expression format:

```
![order:<action>:<symbol>:<quantity>:<orderType>:<limitPrice?>:<stopPrice?>]
```

| Segment | Values | Notes |
|---------|--------|-------|
| `action` | `BUY` \| `SELL` | |
| `symbol` | ticker string | |
| `quantity` | integer or `@{...}` placeholder | resolved at fire time |
| `orderType` | `MKT` \| `LMT` \| `STP` \| `STP_LMT` | |
| `limitPrice` | number or `@{...}` placeholder | required for LMT / STP_LMT |
| `stopPrice` | number or `@{...}` placeholder | required for STP / STP_LMT |

`@{SYMBOL:property}` placeholders inside an action expression are resolved at the moment the action fires — one-shot read from the last known live value, no interval.

## Implementation — `src/utils/actionExpressions.js`

Exports two functions:

**`parseActionExpression(expr)`** — validates the `![...]` syntax and returns a structured object of named params, or throws on invalid syntax.

**`executeActionExpression(expr, resolveValue)`** — resolves any embedded `@{...}` placeholders via `resolveValue(placeholder)`, then dispatches to the matching action handler. The order handler calls `POST /api/orders`.

## Backend — `POST /api/orders`

Places an order via IBKR TWS.

```json
POST /api/orders
{
  "action": "BUY",
  "symbol": "AAPL",
  "quantity": 100,
  "orderType": "LMT",
  "limitPrice": 185.50,
  "stopPrice": null
}
```

Response:

```json
{ "orderId": 42, "status": "Submitted" }
```

Error response:

```json
{ "error": "TWS not connected" }
```

Uses the existing IBKR TWS connection (`ib` client in `server/ibkr.js`). Calls `ib.placeOrder(...)` with a `Contract` (STK, SMART, USD) and `Order` built from the request body.

## Documentation

Add the `![...]` action expression syntax to `src/pages/PlaceholderDocsPage.jsx` so it appears at `/docs/placeholders`.

## Files

| File | Change |
|------|--------|
| `src/utils/actionExpressions.js` | New — parse, validate, and execute action expressions |
| `server/routes/orders.js` | New — `POST /api/orders` with IBKR TWS integration |
| `server/index.js` | Mount orders router |
| `src/pages/PlaceholderDocsPage.jsx` | Document `![...]` action expression syntax |
