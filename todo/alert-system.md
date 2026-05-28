# Alert System: Visual Condition Builder & Reliable Delivery

Build a price and condition alert system with a visual condition builder (no scripting required), redundant server-side + local storage, and delivery via desktop notification and optionally email/webhook. Ensure alerts survive server restarts and are never silently lost.

## Problem

IBKR offers only email and SMS alerts — no native push/desktop notifications. Server-side alerts have been known to vanish entirely with no recovery path, losing hours of user configuration. The alert UI is confusing and non-intuitive. Alert notifications configured on desktop frequently fail to deliver.

## Data model

```js
{
  id: "uuid",
  name: "TSLA breakout",
  enabled: true,
  conditions: [
    { field: "price", operator: "crossesAbove", value: 250 },
    { field: "rsi14", operator: "greaterThan", value: 70 }
  ],
  conditionLogic: "AND",   // AND | OR
  symbol: "TSLA",
  delivery: ["desktop", "email"],   // desktop | email | webhook
  webhookUrl: null,
  repeatAfterMinutes: 60,           // 0 = fire once and disable
  createdAt: "2025-06-01T10:00:00Z",
  lastFiredAt: null
}
```

## Visual condition builder

A point-and-click UI — no scripting:

```
When [ TSLA ] [ price ] [ crosses above ] [ 250 ]
AND  [ TSLA ] [ RSI(14) ] [ is greater than ] [ 70 ]

[ + Add condition ]   Logic: ○ ALL must match  ○ ANY must match
```

Supported fields: `price`, `volume`, `change%`, `ema(n)`, `sma(n)`, `rsi(n)`, `atr(n)`, `bid`, `ask`, `openInterest` (options), `impliedVol` (options).

Supported operators: `crossesAbove`, `crossesBelow`, `isAbove`, `isBelow`, `equals`.

## Storage and redundancy

- Primary: server-side DB (alerts survive browser/app close)
- Secondary: export alerts to a local JSON file (`alerts-backup.json`) on every change; user can import this file to restore after any data loss
- On startup, compare server-side alerts with local backup and prompt the user if they differ

## Evaluation engine

Server-side loop (configurable interval, default 5 seconds):
1. For each enabled alert, fetch the latest quote / computed indicator value for its symbol
2. Evaluate all conditions using the previous tick value to detect "crosses" correctly
3. If conditions pass: mark `lastFiredAt`, dispatch delivery, and disable if `repeatAfterMinutes === 0`

## Delivery

- **Desktop**: use Electron's `Notification` API (or browser `Notification` API on web)
- **Email**: send via configured SMTP or a transactional email provider
- **Webhook**: POST `{ alert, symbol, triggeredAt, triggeredValue }` to `webhookUrl`

## Alert widget

Add an `alert-list` widget type for displaying and managing alerts inline in a template:

```js
{ type: "alert-list", width: 10, height: 300, filterSymbol: null }
```

Shows all alerts with status (active / fired / disabled), last fired time, and quick-enable/disable toggle.

## Files

| File | Change |
|------|--------|
| `src/components/AlertBuilder.jsx` | New — visual condition builder UI |
| `src/components/TemplatePageAlertList.jsx` | New — alert list widget |
| `server/routes/alerts.js` | New — CRUD endpoints for alerts |
| `server/lib/alertEngine.js` | New — evaluation loop and delivery dispatcher |
| `server/db/alerts.js` | New — alert persistence (DB or JSON file) |
| `src/pages/TemplatePage.jsx` | Register `alert-list` in render switch and Add Widget modal |
| `docs/template-widgets.md` | Document alert system and `alert-list` widget |
