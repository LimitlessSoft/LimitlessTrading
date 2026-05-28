# Button Widget

Add a `button` widget type that fires a single action expression when clicked. Depends on the action expressions infrastructure (`todo/action-expressions.md`).

## Widget type: `button`

```js
{
  id: "uuid",
  type: "button",
  width: 3,
  height: 50,
  x: 0,
  y: 0,
  text: "Buy 100 AAPL",
  actionExpression: "![order:BUY:AAPL:100:MKT]",
  confirmBeforeExecute: true,
  style: {
    bgColor: "#276749",
    textColor: "#FFFFFF",
    borderColor: "#38A169",
    borderWidth: 1,
    fontSize: 0.9,
    fontWeight: "bold"          // normal | bold | lighter
  }
}
```

## Behavior

- Renders as a full-width, full-height button with the configured `text`
- On click: if `confirmBeforeExecute` is true, show a confirmation modal displaying the action expression before firing; otherwise fire immediately
- Fires `actionExpression` via `executeActionExpression` from `src/utils/actionExpressions.js`
- Shows a brief success/error toast after execution (green tick or red cross with the IBKR error message)
- In edit mode: clicking opens the settings dialog instead of firing the action

## Settings dialog

- **Text** — button label
- **Action expression** — free-text input with syntax hint (`![order:...]`)
- **Confirm before execute** — toggle (default on)
- Style fields: background color, text color, border color/width, font size, font weight

## Registration

- Add `button` to the `TemplatePage.jsx` render switch and Add Widget modal
- Register in `WidgetTreePanel.jsx`: cursor/click icon, orange color, snippet = button text

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageButton.jsx` | New button widget component |
| `src/pages/TemplatePage.jsx` | Register `button` in render switch + Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon, color, snippet for `button` type |
| `docs/template-widgets.md` | Document `button` widget type and data structure |
