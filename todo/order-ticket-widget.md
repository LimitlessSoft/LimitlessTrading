# Order Ticket Widget

Add an `order-ticket` widget type — a form with editable fields (symbol, action, quantity, order type, price) that submits an IBKR order. Each field can hold a static value or a `@{...}` placeholder expression resolved at submit time. Depends on the action expressions infrastructure (`todo/action-expressions.md`).

## Widget type: `order-ticket`

```js
{
  id: "uuid",
  type: "order-ticket",
  width: 8,
  height: 220,
  x: 0,
  y: 0,
  symbol: "AAPL",               // static or @{} placeholder
  action: "BUY",                // BUY | SELL
  quantity: "100",              // static or @{} placeholder
  orderType: "LMT",             // MKT | LMT | STP | STP_LMT
  limitPrice: "@{AAPL:ask:1}", // static, @{} placeholder, or empty
  stopPrice: "",
  confirmBeforeExecute: true,
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1,
    textColor: "#E2E8F0"
  }
}
```

## Layout

```
[ Symbol ] [ BUY / SELL ] [ Qty ]
[ Order type ] [ Limit $ ] [ Stop $ ]
[ ──────── Place Order ──────── ]
```

Fields that hold `@{...}` expressions display the live resolved value in a dimmed style to indicate they are driven by market data. The user can still override them by typing a static value directly into the field.

## Behavior

- Non-edit mode: quantity, limit price, and stop price are editable by the user at runtime; symbol, action, and order type can be locked via widget config or left editable
- On "Place Order": resolves all `@{...}` placeholders in field values, builds a `![order:...]` expression from the resolved values, then fires it via `executeActionExpression`
- If `confirmBeforeExecute` is true, show a summary modal before firing: "Buy 100 AAPL @ LMT $185.50 — confirm?"
- Success/error toast after execution
- In edit mode: fields are not interactive; clicking opens the settings dialog

## Settings dialog

- **Symbol** — ticker or `@{}` placeholder expression
- **Default action** — BUY / SELL toggle
- **Default quantity** — static number or `@{}` placeholder
- **Default order type** — dropdown: MKT / LMT / STP / STP_LMT
- **Default limit price** — static or `@{}` placeholder
- **Default stop price** — static or `@{}` placeholder
- **Confirm before execute** — toggle (default on)
- Standard style fields: background color, border color/width, text color

## Registration

- Add `order-ticket` to the `TemplatePage.jsx` render switch and Add Widget modal
- Register in `WidgetTreePanel.jsx`: receipt/order icon, teal color, snippet = `{action} {symbol}`

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageOrderTicket.jsx` | New order-ticket widget component |
| `src/pages/TemplatePage.jsx` | Register `order-ticket` in render switch + Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon, color, snippet for `order-ticket` type |
| `docs/template-widgets.md` | Document `order-ticket` widget type and data structure |
