# Order Entry UX: Single-Action Submit & Prominent Fill Feed

Improve the order-ticket widget to use a single-action submission flow, replace the blocking confirmation modal with a compact dismissible preview card, and surface a live order and fill feed directly in the main workspace.

## Problem

TWS splits order submission into two stages (create row → press Transmit), causing new users to unknowingly leave orders unsubmitted. Confirmation dialogs are modal and offer only "Transmit" or "Cancel" with no per-warning override. Order history is buried, making it hard to verify a fill without navigating away.

## Implementation notes

### Single-action submission

The order-ticket widget has one "Place Order" button. Clicking it:
1. Resolves all `@{...}` placeholder expressions
2. Validates the resulting values (symbol exists, quantity > 0, limit price set if order type is LMT)
3. Shows the confirmation preview card (see below) if `confirmBeforeExecute` is true
4. On confirm (or immediately if confirmation is disabled), fires the order

There is no intermediate "staged" state — the order is either being confirmed or is live.

### Confirmation preview card

Replace the modal with a non-blocking toast-style card that slides in from the bottom of the order ticket:

```
╔══════════════════════════════╗
║  BUY 100 AAPL @ LMT $185.50 ║
║  [Confirm]  [Cancel]         ║
║  □ Don't ask again for LMT   ║
╚══════════════════════════════╝
```

- Auto-dismisses after 10 seconds (cancelled if not confirmed)
- "Don't ask again" persists per order type in user preferences
- Does not block interaction with other widgets while open

### Live order & fill feed widget

Add a new widget type `order-feed` — a lightweight live list of today's orders and fills:

```js
{
  type: "order-feed",
  width: 12,
  height: 200,
  showPending: true,
  showFilled: true,
  showCancelled: false
}
```

Each row shows: time, symbol, action, qty, order type, status (working / filled / cancelled), fill price. Rows flash briefly on status change. Clicking a row opens full order details.

Backend: subscribe to IBKR `openOrder` and `execDetails` callbacks; push updates to the frontend via WebSocket.

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageOrderTicket.jsx` | Replace two-stage flow with single-action + preview card |
| `src/components/OrderConfirmCard.jsx` | New — non-modal confirmation card component |
| `src/components/TemplatePageOrderFeed.jsx` | New — live order and fill feed widget |
| `src/pages/TemplatePage.jsx` | Register `order-feed` in render switch and Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon and snippet for `order-feed` type |
| `server/routes/orders.js` | Push `openOrder` and `execDetails` events over WebSocket |
