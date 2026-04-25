# Development

## Prerequisites

- Node.js
- Interactive Brokers TWS or IB Gateway running
  - Port 7497 for paper trading
  - Port 7496 for live trading

## Commands

```bash
cd anal/ARAnalysis

# Start both frontend and backend
npm start

# Run separately
npm run dev      # Frontend only — Vite dev server on port 3000
npm run server   # Backend only — Express on port 3001

# Production build
npm run build
```
