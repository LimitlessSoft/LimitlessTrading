# Platform Status Page & Graceful Degradation

Build a public status page showing real-time component health (data feed, order routing, API gateway, authentication), add an in-app incident banner that appears automatically on degradation, and architect the app to degrade gracefully — orders must still work if the data feed is slow, and data must still work if one exchange feed is down.

## Problem

StatusGator tracked 141+ outages affecting IBKR users over a 5-month period. When issues occur, IBKR has publicly stated "no system-wide outages" while users experienced problems, leaving traders unsure if the problem is on their end. When the platform degrades, there is no in-app notification — users must check external sites.

## Public status page

A lightweight public page (no login required) at `/status`:

```
Limitless Trading — System Status

✓ Market data feed          Operational
✓ Order routing             Operational
⚠ Options data (OPRA)       Degraded — slower than normal updates  (since 14:20 ET)
✓ API gateway               Operational
✓ Authentication            Operational
✓ Historical data           Operational

Recent incidents
  2025-06-01 14:20 ET  Options data slowdown  (ongoing)
  2025-05-28 09:31 ET  Brief login delay  (resolved in 4 min)
```

Each component has three states: `operational` (green), `degraded` (yellow), `outage` (red).

Status is computed automatically by the health monitor — never updated manually.

## Health monitoring

`server/lib/healthMonitor.js` runs on a 10-second interval and checks:

| Component | Health check |
|---|---|
| Market data feed | Quote update rate for a basket of liquid symbols (AAPL, SPY, QQQ) — degraded if < 50% of normal rate |
| Options data | Quote update rate for a basket of ATM options — degraded if < 50% |
| Order routing | Round-trip latency for a test order submission to IBKR paper — degraded if > 2s, outage if no response |
| API gateway | Self-ping of the `/v1/health` endpoint — degraded if > 500ms, outage if unreachable |
| Authentication | Token validation latency — degraded if > 300ms |
| Historical data | Latency of a small bar request — degraded if > 5s |

On status change, the monitor:
1. Updates the status store
2. Broadcasts the change to all connected WebSocket clients (triggers the in-app banner)
3. Logs the incident with start time

## In-app incident banner

See `todo/in-app-support-chat.md` — the `IncidentBanner` component subscribes to the health monitor WebSocket channel and displays automatically on degradation. This task covers the server-side health monitor and status page; the banner component is tracked there.

## Graceful degradation architecture

Design rules enforced across all server routes and frontend components:

**Data feed degradation:** If real-time quotes are slow or unavailable for a symbol, fall back to the last known quote with a "stale" timestamp label. Never show an empty field or an unhandled error — always show the last good value plus its age: `$185.10 (as of 14:31 ET)`.

**Single exchange outage:** If one exchange feed (e.g., OPRA for options) goes down, symbols from that exchange show stale data with a badge. Symbols from other exchanges continue to update normally. The app never becomes fully non-functional due to one exchange's issues.

**Order routing outage:** If the IBKR order routing connection is down, the order ticket shows a clear error: "Order routing is currently unavailable. Your order was not placed." Never silently accept an order and fail to submit it.

**API gateway degradation:** API endpoints respond with `503 Service Unavailable` and `Retry-After` header rather than timing out silently. WebSocket clients receive a `{ "type": "degraded", "component": "orderRouting" }` event.

## Files

| File | Change |
|------|--------|
| `server/lib/healthMonitor.js` | New — component health checks, status store, WebSocket broadcast |
| `server/routes/status.js` | New — `GET /status` (public page), `GET /api/status` (JSON for in-app use) |
| `src/pages/StatusPage.jsx` | New — public status page (no auth required) |
| `server/lib/gracefulFallback.js` | New — stale-data wrapper and error response helpers used across all routes |
| `server/routes/v1/health.js` | New — `GET /v1/health` for API gateway self-ping |
| `docs/README.md` | Document status page URL and graceful degradation policy |
