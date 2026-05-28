# Headless Public API: Token Auth, WebSocket Pushes & SDKs

Extend the LT Public API to support token-based (API key) authentication with no GUI requirement, real-time account and position pushes via WebSocket, full order-type parity with the UI, and a developer sandbox environment. Ship SDKs for Python and TypeScript.

## Problem

IBKR's API requires a running instance of TWS or IB Gateway with manual GUI login and 2FA on the same machine — there is no headless deployment path. Pacing violations disconnect the entire API client. Account value updates push only every 3 minutes. Fractional shares and several order types are inaccessible via API. Documentation is described as "the worst I ever saw" by 15-year veterans.

## Authentication

Replace any session-cookie auth with API key authentication:

```
Authorization: Bearer lt_live_abc123...
```

Keys are scoped:
- `read` — quotes, positions, account data
- `trade` — place and cancel orders
- `full` — all of the above

Keys are generated in the LT web dashboard, never expire by default (revocable), and can be rotated. No GUI login required for any API operation.

Separate key prefixes for sandbox vs. live: `lt_sandbox_...` vs. `lt_live_...`.

## REST API endpoints

Core endpoints (see `docs/public-api.md` for existing spec; extend it):

```
GET  /v1/account                   — balances, buying power, margin
GET  /v1/positions                 — all open positions
GET  /v1/orders                    — open and recent orders
POST /v1/orders                    — place order (all types)
DELETE /v1/orders/:id              — cancel order
GET  /v1/quotes/:symbol            — snapshot quote
GET  /v1/history/:symbol           — OHLC bars
GET  /v1/options/chain             — options chain for symbol + expiry
POST /v1/orders/bracket            — bracket order (entry + stop + target in one call)
POST /v1/orders/oca                — OCA group
```

Rate limits: 120 requests/minute per key, with `X-RateLimit-Remaining` and `Retry-After` headers on 429 responses. No pacing violations — the server manages IBKR's pacing internally and queues requests transparently.

## WebSocket streaming

```
wss://localhost:{port}/v1/stream?token=lt_live_abc123
```

Subscribe / unsubscribe pattern:

```json
// Client → server
{ "action": "subscribe", "channel": "quotes", "symbols": ["AAPL", "TSLA"] }
{ "action": "subscribe", "channel": "account" }
{ "action": "subscribe", "channel": "orders" }

// Server → client (pushed as events)
{ "channel": "quotes",   "symbol": "AAPL", "bid": 185.10, "ask": 185.12, "last": 185.11 }
{ "channel": "account",  "netLiq": 102450.00, "cashBalance": 18200.00, "buyingPower": 36400.00 }
{ "channel": "orders",   "orderId": "abc", "status": "Filled", "fillPrice": 185.11, "fillQty": 100 }
```

Account and position pushes are real-time (not every 3 minutes). Order status updates push immediately on IBKR callback.

## Developer sandbox

Requests using a `lt_sandbox_...` key route to the paper trading engine (IBKR paper or local simulator). Sandbox keys are free and do not require a funded IBKR account. Quotes in sandbox are real-time (delayed if no data subscription) but orders never reach a live exchange.

## SDKs

### Python

```python
from limitless import LimitlessClient

lt = LimitlessClient(api_key="lt_live_abc123")
quote = lt.quotes.get("AAPL")
order = lt.orders.place(symbol="AAPL", action="BUY", qty=100, order_type="LMT", limit_price=185.50)

# WebSocket streaming
async for event in lt.stream(channels=["quotes"], symbols=["AAPL"]):
    print(event)
```

### TypeScript

```typescript
import { LimitlessClient } from "@limitless/sdk";

const lt = new LimitlessClient({ apiKey: "lt_live_abc123" });
const quote = await lt.quotes.get("AAPL");
const stream = lt.stream({ channels: ["quotes"], symbols: ["AAPL"] });
stream.on("quote", (event) => console.log(event));
```

## Files

| File | Change |
|------|--------|
| `server/middleware/apiKeyAuth.js` | New — API key extraction, validation, scope enforcement |
| `server/routes/v1/` | New directory — all versioned public API routes |
| `server/lib/rateLimiter.js` | New — per-key rate limiting with header injection |
| `server/lib/streamManager.js` | New — WebSocket subscription management and push dispatch |
| `server/lib/ibkrPacingQueue.js` | New — internal queue that absorbs IBKR pacing constraints transparently |
| `docs/public-api.md` | Extend with new endpoints, auth scheme, WebSocket protocol, SDK examples |
| `sdk/python/` | New — Python SDK package |
| `sdk/typescript/` | New — TypeScript SDK package |
