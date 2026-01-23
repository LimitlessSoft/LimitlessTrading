# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ARTradingTools is a trading analysis toolkit. The main application is **ARAnalysis**, a React web app with an Express backend that connects to Interactive Brokers (IBKR) for historical market data.

## Commands

### Development
```bash
cd anal/ARAnalysis

# Start both frontend and backend concurrently
npm start

# Or run separately:
npm run dev      # Frontend only (Vite dev server on port 3000)
npm run server   # Backend only (Express on port 3001)

# Build for production
npm run build
```

### Prerequisites
- Node.js
- Interactive Brokers TWS or IB Gateway running (port 7497 for paper trading, 7496 for live)

## Architecture

```
anal/ARAnalysis/
├── src/                    # React frontend (Vite)
│   ├── App.jsx            # Main app, manages state and data loading
│   ├── components/        # UI components
│   │   ├── SecuritySpreadsheet.jsx    # Main data table with OHLCV data
│   │   ├── ThresholdReport.jsx        # Analysis report across securities
│   │   ├── ConditionalFormatManager.jsx  # Formatting rules editor
│   │   └── CandlestickChart.jsx       # TradingView lightweight-charts
│   ├── modules/
│   │   ├── dataService.js  # Frontend data fetching, caching logic
│   │   └── DataManager.jsx # Connection management UI
│   └── db/
│       └── database.js     # Dexie (IndexedDB) for client-side caching
└── server/                 # Express backend
    ├── index.js           # Server entry, routes setup
    ├── ibkrClient.js      # IBKR API wrapper using @stoqey/ib
    └── routes/
        └── market.js      # Historical data endpoints
```

### Data Flow
1. Frontend requests data via `/api/historical/:symbol/chunk`
2. Backend connects to IBKR via `@stoqey/ib` library
3. Data is fetched in chunks (parallel requests to handle IBKR rate limits)
4. Frontend caches data in IndexedDB (Dexie) for offline access
5. Subsequent requests check cache completeness before hitting IBKR

### Key Patterns
- **Chunked parallel fetching**: Historical data is fetched in parallel batches (20 requests) with progress callbacks
- **Client-side caching**: All price data cached in IndexedDB, checked for completeness before re-fetching
- **Index symbol handling**: VIX, SPX, NDX, DJX use `SecType.IND` with specific exchanges (see `INDEX_SYMBOLS` in `ibkrClient.js`)

### Interval Configuration
Both frontend (`dataService.js`) and backend (`market.js`) have `INTERVAL_CONFIG` that maps intervals to IBKR parameters:
- `1m`/`2m`: 2 years of data
- `3m` to `1d`: up to 10 years of data

## Database Schema (Dexie/IndexedDB)
- `securities`: symbol, name, exchange
- `priceData`: symbol, interval, timestamp, OHLCV
- `settings`: key-value store (lastPort, lastFetch times)
- `favorites`: symbol
- `conditionalFormats`: formatting rules with groups
- `analysisPresets`: saved analysis configurations
