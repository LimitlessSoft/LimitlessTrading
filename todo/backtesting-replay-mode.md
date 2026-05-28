# Backtesting & Chart Replay Mode

Add a strategy replay mode to the chart widget: the user pins the chart to a past date and steps forward bar by bar, placing simulated orders and tracking cumulative P&L and trade statistics — without leaving the workspace.

## Problem

TWS has no built-in backtesting engine at all. ThinkorSwim's "OnDemand" replay mode is the most-cited reason traders keep a TOS account alongside IBKR. IBKR's answer is to point users to third-party tools. Even a basic walk-forward chart replay is a significant differentiator.

## How it works

The chart widget gains a **Replay** mode toggle in the toolbar. When activated:

1. The chart freezes at a user-selected start date (date picker in the toolbar)
2. All real-time data feeds for the chart are paused
3. A replay control bar appears below the chart:

```
◀◀  ◀  ▶  ▶▶   Speed: [1×  5×  10×  Max]   2024-01-15  09:35 ET
                                             [Exit Replay]
```

4. Pressing ▶ steps the chart forward one bar at a time (or plays automatically at the selected speed)
5. Candles and indicator values appear as they would have in real time — future bars are hidden
6. The user can place simulated orders using the order-ticket widget normally; orders go to the paper trading engine (not live IBKR)

## Simulated order engine (replay mode)

When replay mode is active, all order submissions are intercepted and routed to a local simulation engine instead of IBKR:

- **Market orders**: fill at the open price of the next bar
- **Limit orders**: fill when the bar's high (for buys) or low (for sells) crosses the limit price
- **Stop orders**: fill when the bar's low (for stops on longs) crosses the stop price

Track all replay trades in a session store. Show a **Replay P&L panel** alongside the chart:

```
Trades: 12   Win rate: 58%   Net P&L: +$1,240   Max drawdown: -$380
```

Each trade row: entry time, exit time, symbol, side, entry price, exit price, P&L.

## Data requirements

- Historical bar data for the replay period must be available (fetched from IBKR historical data API or cached)
- Cache historical data locally so replaying the same period doesn't re-fetch
- Indicator values are recomputed from the historical series as bars are revealed

## Integration with options analytics

When the chart is in replay mode and an options-analytics widget is open for the same underlying:
- The analytics widget uses the historical IV surface (if available) rather than live IV
- The P&L surface projects from the replayed date forward to expiry

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageChart.jsx` | Add Replay mode toggle, date picker, replay control bar |
| `src/lib/replayEngine.js` | New — replay state machine, bar stepping, speed control |
| `src/lib/simulatedOrderEngine.js` | New — intercept orders in replay mode; fill simulation logic |
| `src/components/ReplayPnlPanel.jsx` | New — live trade log and P&L summary during replay |
| `server/routes/history.js` | Add caching layer for historical bar data used in replay |
| `docs/chart-widget.md` | Document replay mode controls and simulated order behavior |
