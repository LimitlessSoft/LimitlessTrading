# Architecture

Limitless Trading is a trading analysis toolkit. It consists of two independent backends and one frontend:

| Component | Stack | Purpose |
|-----------|-------|---------|
| **Limitless Trading frontend** | React + Vite | Trading analysis UI |
| **Limitless Trading backend** | Node.js / Express | IBKR data proxy, user data persistence |
| **LT Public API** | .NET 9 Web API | Publicly deployed API, hosted on Kubernetes |

**Limitless Trading** (both frontend and backend) runs entirely on the user's local machine. All market data fetched from IBKR, user preferences, watchlists, and route configurations are stored locally and never leave the machine. The Express backend is a local process — there is no remote Limitless Trading server.

**LT Public API** is the only component deployed to remote infrastructure, hosted on Kubernetes at `api.<env>.aleksaristic.com`. It is an independent service with no access to the local Limitless Trading data.

## Data Source Support

**Currently supported:** Interactive Brokers only, via TWS or IB Gateway (`@stoqey/ib`). All market data, account summary, and real-time quotes flow through an active TWS/Gateway connection.

> **Planned:** The data source layer is intended to be extended to support additional brokers and market data providers in the future. The current IBKR integration is the only supported source.

See [ar-analysis.md](ar-analysis.md) for the Limitless Trading app file structure and key patterns.  
See [public-api.md](public-api.md) for the LT Public API endpoints, Docker, Kubernetes, and CI/CD.
