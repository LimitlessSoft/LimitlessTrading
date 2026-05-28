# App Performance: Lazy Loading & Memory Management

Ensure the Limitless Trading app does not grow in memory or degrade in performance over a trading session. Implement lazy loading for options chains and large data grids, and expose a performance indicator users can use to soft-reload without closing the app.

## Problem

TWS leaks memory every time an option chain is opened (heap never recovered on close), pegs CPU at 100% during data-intensive operations, and requires a full restart every 2–3 hours to remain usable. In extreme cases it consumes 16–24 GB of RAM. Users should never need to restart a trading platform to keep it functional.

## Implementation notes

### Virtualized options chain and data grids

Replace any table that renders all rows at once with a virtualized list — only the rows in the viewport (plus a small overscan buffer) are mounted in the DOM:

- Use `react-virtual` or `@tanstack/virtual` for the options chain strike table, watchlist, and order history grid
- On scroll, new rows mount and off-screen rows unmount, keeping the DOM node count constant
- Pair with on-demand data fetching: only request strike data for the visible strike range; expand the request as the user scrolls

### Market data subscription cleanup

Track active `reqMktData` / WebSocket subscriptions per widget. When a widget is closed or its symbol changes:
1. Cancel the previous subscription immediately
2. Remove the cached quote data from the store
3. Confirm the subscription count drops in the next heartbeat

This prevents the TWS-style accumulation where option chain subscriptions persist after the chain is closed.

### Memory/performance indicator

Add a subtle status bar element showing:
- Current JS heap usage (via `performance.memory.usedJSHeapSize` in Electron / Chrome)
- Number of active market data subscriptions
- A "Reload workspace" button that hot-reloads the renderer process without closing the app or losing layout config

```
Heap: 412 MB  |  Subscriptions: 47  |  [Reload]
```

### Subscription budget

Enforce a configurable cap on simultaneous market data subscriptions (default: 100). When the cap is reached, show a warning and queue new subscriptions, dropping the oldest idle one first.

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageOptionsChain.jsx` | Replace table with virtualized list; cleanup subscriptions on unmount |
| `src/lib/marketDataStore.js` | Track active subscriptions; enforce budget; cleanup on unsubscribe |
| `src/components/StatusBar.jsx` | New — heap/subscription indicator and soft-reload button |
| `src/pages/TemplatePage.jsx` | Mount `StatusBar` at layout level |
