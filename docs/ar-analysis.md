# Limitless Trading App

LT Workstation is the main trading analysis app — a React frontend (Vite) with a Node.js/Express server that connects to Interactive Brokers (IBKR) for market data. Both run locally on the user's machine. All data (market data, user preferences, watchlists, route configurations) is stored locally and never sent to a remote server.

## File Structure

```
src/LT.Workstation/
├── LT.Workstation.Fe/              # React frontend (Vite)
│   ├── src/
│   │   ├── main.jsx                # App entry, routing setup (BrowserRouter)
│   │   ├── theme.js                # Chakra UI theme configuration
│   │   ├── api.js                  # API base client
│   │   ├── api/
│   │   │   └── userDataService.js
│   │   ├── components/
│   │   │   ├── PageNavMenu.jsx         # Global navigation header (sticky bar + hamburger menu)
│   │   │   ├── CandlestickChart.jsx    # TradingView lightweight-charts
│   │   │   ├── TemplatePageLabel.jsx   # Resizable/styleable label for template pages
│   │   │   ├── TemplatePagePanel.jsx   # Container widget for nested layouts
│   │   │   ├── TemplatePageBadge.jsx   # Colored indicator widget
│   │   │   ├── TemplatePageChart.jsx   # Candlestick chart widget with link groups
│   │   │   ├── TemplatePageGauge.jsx   # Semicircle gauge widget with colored segments
│   │   │   ├── TemplatePageGraph.jsx   # Multi-series time-series graph widget
│   │   │   ├── AlertExpressionWatcher.jsx  # Evaluates alert expressions on a timer
│   │   │   ├── AlertOverlay.jsx        # On-screen overlay for triggered alerts
│   │   │   ├── AlertsPanel.jsx         # Alert management panel
│   │   │   ├── WidgetTreePanel.jsx     # Hierarchical widget tree sidebar
│   │   │   ├── WidgetsGalleryModal.jsx # Modal for browsing and adding widget types
│   │   │   ├── WidgetTooltip.jsx       # Tooltip display for widgets
│   │   │   ├── WidgetTooltipEditor.jsx # Editor for widget tooltip configuration
│   │   │   ├── AIChatPanel.jsx         # AI assistant chat UI
│   │   │   └── widgetRenderer.jsx      # Shared widget rendering dispatch
│   │   ├── pages/
│   │   │   └── TemplatePage.jsx        # Dynamic page template for custom routes
│   │   ├── modules/
│   │   │   ├── dataService.js          # Frontend data fetching and caching logic
│   │   │   ├── routesService.js        # Routes API client (CRUD for custom pages)
│   │   │   ├── fomcService.js          # FOMC date fetching and countdown
│   │   │   ├── AutoConnect.jsx         # Auto-connects to IBKR on app load
│   │   │   ├── accountService.js       # IBKR account summary polling + #{field} placeholder resolution
│   │   │   ├── alertService.js         # Alert state management with cron-based expression watching
│   │   │   ├── conditionalStyles.js    # Shared conditional style evaluation utilities
│   │   │   ├── exchangeService.js      # Exchange local time and market status calculator
│   │   │   ├── globalChartSettings.js  # Shared chart appearance settings (RTH/outside-RTH colors)
│   │   │   ├── symbolQuoteService.js   # SSE/polling quote subscriptions for @{...} placeholders
│   │   │   └── widgetLinkService.js    # Link group definitions for chart ticker sync
│   │   ├── hooks/
│   │   └── db/
│   │       └── database.js             # Dexie (IndexedDB) for client-side caching
│   ├── index.html
│   ├── vite.config.js
│   └── package.json
└── LT.Workstation.Server/          # Node.js/Express server
    ├── index.js                    # Server entry, routes setup
    ├── ibkrClient.js               # IBKR API wrapper using @stoqey/ib
    ├── ai/
    │   ├── tools.js                # AI tool definitions and execution
    │   ├── whisper.js              # Local Whisper transcription
    │   └── providers/
    │       ├── anthropic.js        # Anthropic SDK provider
    │       └── claude-cli.js       # Claude CLI provider
    ├── routes/
    │   ├── market.js               # Historical data endpoints
    │   ├── fomc.js                 # FOMC schedule endpoints
    │   ├── treasury.js             # Treasury yield endpoints
    │   ├── fearGreed.js            # Fear & Greed Index endpoints
    │   ├── routes.js               # Custom routes CRUD API
    │   ├── account.js              # IBKR account summary with 10s server-side cache
    │   ├── userData.js             # User preferences CRUD (watchlist, favorites, settings, etc.)
    │   ├── ai.js                   # AI chat endpoints
    │   ├── transcribe.js           # Audio transcription endpoint
    │   └── license.js              # License validation endpoint
    ├── data/
    │   ├── routes-config.json      # Persistent storage for custom routes (gitignored)
    │   └── user-data.json          # Persistent user preferences (gitignored)
    └── package.json
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
