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
│   ├── main.jsx           # App entry, routing setup (BrowserRouter)
│   ├── App.jsx            # Main dashboard, manages state and data loading
│   ├── theme.js           # Chakra UI theme configuration
│   ├── components/        # UI components
│   │   ├── PageNavMenu.jsx        # Global navigation header (sticky bar + hamburger menu)
│   │   ├── FedCountdown.jsx       # FOMC countdown bar with indicators
│   │   ├── SecuritySpreadsheet.jsx # Main data table with OHLCV data
│   │   ├── ThresholdReport.jsx    # Analysis report across securities
│   │   ├── CandlestickChart.jsx   # TradingView lightweight-charts
│   │   ├── VixDisplay.jsx         # VIX indicator display
│   │   ├── TreasuryYields.jsx     # Treasury yield display
│   │   ├── FearGreedGauge.jsx     # Fear & Greed Index speedometer
│   │   ├── LiveWatchlist.jsx      # Real-time streaming watchlist (SSE)
│   │   └── MarketClocks.jsx       # NYSE/LSE market hours display
│   ├── pages/
│   │   └── TemplatePage.jsx       # Dynamic page template for custom routes
│   ├── modules/
│   │   ├── dataService.js         # Frontend data fetching, caching logic
│   │   ├── routesService.js       # Routes API client (CRUD for custom pages)
│   │   ├── fomcService.js         # FOMC date fetching and countdown
│   │   └── DataManager.jsx        # Connection management UI
│   └── db/
│       └── database.js            # Dexie (IndexedDB) for client-side caching
└── server/                 # Express backend
    ├── index.js           # Server entry, routes setup
    ├── ibkrClient.js      # IBKR API wrapper using @stoqey/ib
    ├── data/
    │   └── routes-config.json  # Persistent storage for custom routes (gitignored)
    └── routes/
        ├── market.js      # Historical data endpoints
        ├── fomc.js        # FOMC schedule endpoints
        ├── treasury.js    # Treasury yield endpoints
        ├── fearGreed.js   # Fear & Greed Index endpoints
        └── routes.js      # Custom routes CRUD API
```

## Routing & Navigation System

### Overview
The app uses React Router with a catch-all route pattern. The main page (`/`) renders `App.jsx`, while all other paths render `TemplatePage.jsx`.

### main.jsx - Routing Setup
```jsx
<BrowserRouter>
  <Routes>
    <Route path="/" element={<App />} />
    <Route path="*" element={<TemplatePage />} />
  </Routes>
</BrowserRouter>
```

### PageNavMenu Component
A reusable sticky header bar with navigation menu. Used by both `FedCountdown` (main page) and `TemplatePage` (custom pages).

**Props:**
- `children` - Content to render after the hamburger menu
- `bg` - Background color (default: `'gray.800'`)
- `borderColor` - Bottom border color (default: `'gray.700'`)

**Features:**
- Sticky positioning (`top: 0`, `zIndex: 1000`)
- Hamburger menu with:
  - "Main Page (/)" link
  - List of custom routes from backend
  - "Add New Page" modal
- Renders children in a Flex container after the menu

**Usage in FedCountdown:**
```jsx
<PageNavMenu bg={style.bg} borderColor={style.borderColor}>
  {/* FOMC countdown content */}
  <Spacer />
  {/* Right-side indicators */}
</PageNavMenu>
```

**Usage in TemplatePage:**
```jsx
<PageNavMenu />  // Uses default gray colors, no children
```

### TemplatePage Component
**Location:** `src/pages/TemplatePage.jsx`

The template for all custom/dynamic pages. This is a key component for extensibility.

**Current behavior:**
1. Reads current path via `useLocation()`
2. Fetches all routes from backend via `fetchRoutes()`
3. Finds matching route by path
4. Displays page name as heading if route exists, otherwise shows raw path

**State:**
- `pageInfo` - The matching route object or `null`

**Route object shape:**
```js
{
  id: "uuid",
  path: "/custom-page",
  name: "Custom Page",
  createdAt: 1706000000000,
  updatedAt: 1706000000000,
  config: {}  // Extensible config object for future use
}
```

**Current structure:**
```jsx
<Box minH="100vh" bg="gray.900">
  <PageNavMenu />
  <Box p={8}>
    {pageInfo ? (
      <>
        <Heading>{pageInfo.name}</Heading>
        <Text>{pageInfo.path}</Text>
      </>
    ) : (
      <Text>{location.pathname}</Text>
    )}
  </Box>
</Box>
```

**Extension points:**
- The `config` object in routes can store page-specific settings
- TemplatePage can be enhanced to render different layouts based on config
- Can add widgets, charts, or other components based on route config

### Routes API

**Backend:** `server/routes/routes.js`
**Frontend client:** `src/modules/routesService.js`
**Storage:** `server/data/routes-config.json` (auto-created, gitignored)

**Endpoints:**
- `GET /api/routes` - List all routes
- `POST /api/routes` - Create route `{ name, path, config? }`
- `PUT /api/routes/:id` - Update route
- `DELETE /api/routes/:id` - Delete route

**Frontend functions:**
- `fetchRoutes()` - Get all routes
- `createRoute(name, path)` - Create new route
- `updateRoute(id, updates)` - Update route
- `deleteRoute(id)` - Delete route

## Data Flow

### Historical Market Data
1. Frontend requests data via `/api/historical/:symbol/chunk`
2. Backend connects to IBKR via `@stoqey/ib` library
3. Data is fetched in chunks (parallel requests to handle IBKR rate limits)
4. Frontend caches data in IndexedDB (Dexie) for offline access
5. Subsequent requests check cache completeness before hitting IBKR

### Real-time Data
- LiveWatchlist uses Server-Sent Events (SSE) for streaming updates
- VIX, Treasury yields fetched periodically from backend

## Key Patterns

- **Chunked parallel fetching**: Historical data is fetched in parallel batches (20 requests) with progress callbacks
- **Client-side caching**: All price data cached in IndexedDB, checked for completeness before re-fetching
- **Index symbol handling**: VIX, SPX, NDX, DJX use `SecType.IND` with specific exchanges (see `INDEX_SYMBOLS` in `ibkrClient.js`)
- **Composable headers**: PageNavMenu accepts children for flexible header content
- **Dynamic routing**: Catch-all route with backend-stored page configs

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

## UI Framework
- **Chakra UI** for components and styling
- Dark theme (`gray.900` background, `gray.800` for cards/headers)
- Color scheme follows urgency patterns (red/orange/yellow for alerts, blue for normal)
