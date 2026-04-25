# Architecture

ARTradingTools is a trading analysis toolkit. The main application is **ARAnalysis** — a React frontend (Vite) with an Express backend that connects to Interactive Brokers (IBKR) for market data.

## File Structure

```
anal/ARAnalysis/
├── src/                    # React frontend (Vite)
│   ├── main.jsx            # App entry, routing setup (BrowserRouter)
│   ├── App.jsx             # Main dashboard, manages state and data loading
│   ├── theme.js            # Chakra UI theme configuration
│   ├── components/
│   │   ├── PageNavMenu.jsx         # Global navigation header (sticky bar + hamburger menu)
│   │   ├── FedCountdown.jsx        # FOMC countdown bar with indicators
│   │   ├── SecuritySpreadsheet.jsx # Main data table with OHLCV data
│   │   ├── ThresholdReport.jsx     # Analysis report across securities
│   │   ├── CandlestickChart.jsx    # TradingView lightweight-charts
│   │   ├── VixDisplay.jsx          # VIX indicator display
│   │   ├── TreasuryYields.jsx      # Treasury yield display
│   │   ├── FearGreedGauge.jsx      # Fear & Greed Index speedometer
│   │   ├── LiveWatchlist.jsx       # Real-time streaming watchlist (SSE)
│   │   ├── MarketClocks.jsx        # NYSE/LSE market hours display
│   │   ├── TemplatePageLabel.jsx   # Resizable/styleable label for template pages
│   │   ├── TemplatePagePanel.jsx   # Container control for nested layouts
│   │   └── TemplatePageBadge.jsx   # Colored indicator control
│   ├── pages/
│   │   └── TemplatePage.jsx        # Dynamic page template for custom routes
│   ├── modules/
│   │   ├── dataService.js          # Frontend data fetching and caching logic
│   │   ├── routesService.js        # Routes API client (CRUD for custom pages)
│   │   ├── fomcService.js          # FOMC date fetching and countdown
│   │   └── DataManager.jsx         # Connection management UI
│   └── db/
│       └── database.js             # Dexie (IndexedDB) for client-side caching
└── server/
    ├── index.js            # Server entry, routes setup
    ├── ibkrClient.js       # IBKR API wrapper using @stoqey/ib
    ├── data/
    │   └── routes-config.json  # Persistent storage for custom routes (gitignored)
    └── routes/
        ├── market.js       # Historical data endpoints
        ├── fomc.js         # FOMC schedule endpoints
        ├── treasury.js     # Treasury yield endpoints
        ├── fearGreed.js    # Fear & Greed Index endpoints
        └── routes.js       # Custom routes CRUD API
```

## UI Framework

- **Chakra UI** for components and styling
- Dark theme: `gray.900` background, `gray.800` for cards/headers
- Color scheme follows urgency patterns: red/orange/yellow for alerts, blue for normal

## Key Patterns

- **Chunked parallel fetching**: Historical data fetched in parallel batches (20 requests) with progress callbacks
- **Client-side caching**: All price data cached in IndexedDB, checked for completeness before re-fetching
- **Index symbol handling**: VIX, SPX, NDX, DJX use `SecType.IND` with specific exchanges (see `INDEX_SYMBOLS` in `ibkrClient.js`)
- **Composable headers**: `PageNavMenu` accepts children for flexible header content
- **Dynamic routing**: Catch-all route with backend-stored page configs
