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

## Server-Side User Data

User preferences are persisted in `server/data/user-data.json` (gitignored) and served through the `/api/user-data` endpoints in `server/routes/userData.js`. This covers data that needs to survive browser cache clears or be shared across devices.

| Resource | Endpoints | Description |
|----------|-----------|-------------|
| Watchlist | `GET/POST /api/user-data/watchlist`, `DELETE /api/user-data/watchlist/:symbol`, `PUT /api/user-data/watchlist/reorder` | Live watchlist symbols with ordering |
| Favorites | `GET/POST /api/user-data/favorites`, `DELETE /api/user-data/favorites/:symbol` | Favorited securities |
| Settings | `GET /api/user-data/settings`, `GET/PUT /api/user-data/settings/:key` | Key-value app settings |
| Conditional formats | `GET/POST /api/user-data/conditional-formats`, `PUT/DELETE /api/user-data/conditional-formats/:id` | Formatting rules |
| Format groups | `GET/POST /api/user-data/format-groups`, `PUT/DELETE /api/user-data/format-groups/:id` | Formatting rule groups |
| Analysis presets | `GET/POST /api/user-data/analysis-presets`, `PUT/DELETE /api/user-data/analysis-presets/:id` | Saved analysis configurations |
| Watchgroups | `GET/POST /api/user-data/watchgroups`, `PUT/DELETE /api/user-data/watchgroups/:id` | Named security groups |
| Watchgroup symbols | `GET/POST /api/user-data/watchgroups/:id/symbols`, `DELETE /api/user-data/watchgroups/:id/symbols/:symbol`, `GET /api/user-data/watchgroups/by-symbol/:symbol` | Symbols within a watchgroup |

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
