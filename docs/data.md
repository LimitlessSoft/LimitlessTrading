# Data

## Historical Market Data

1. Frontend requests data via `GET /api/historical/:symbol/chunk`
2. Backend connects to IBKR via `@stoqey/ib`
3. Data is fetched in parallel batches of 20 requests (IBKR rate limit workaround)
4. Frontend caches all price data in IndexedDB (Dexie)
5. On subsequent requests the cache is checked for completeness before hitting IBKR

### Interval Configuration

Both `dataService.js` (frontend) and `market.js` (backend) share an `INTERVAL_CONFIG` that maps interval keys to IBKR parameters:

| Interval | History |
|----------|---------|
| `1m`, `2m` | 2 years |
| `3m` → `1d` | Up to 10 years |

## Real-time Data

- **LiveWatchlist**: Server-Sent Events (SSE) for streaming price updates
- **VIX, Treasury yields**: polled periodically from backend endpoints

## IndexedDB Schema (Dexie)

| Store | Fields |
|-------|--------|
| `securities` | symbol, name, exchange |
| `priceData` | symbol, interval, timestamp, OHLCV |
| `settings` | key-value (lastPort, lastFetch times) |
| `favorites` | symbol |
| `conditionalFormats` | formatting rules with groups |
| `analysisPresets` | saved analysis configurations |

## Index Symbols

VIX, SPX, NDX, and DJX use `SecType.IND` with exchange-specific routing. See `INDEX_SYMBOLS` in `server/ibkrClient.js`.
