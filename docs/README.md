# Architecture

Limitless Trading is a trading analysis toolkit. It consists of two independent backends, one local frontend, and one public website:

| Component | Stack | Location | Purpose |
|-----------|-------|----------|---------|
| **LT Workstation app** | Electron | `src/LT.Workstation/LT.Workstation.App/` | Desktop shell — spawns server, hosts frontend window |
| **LT Workstation frontend** | React + Vite | `src/LT.Workstation/LT.Workstation.Fe/` | Trading analysis UI |
| **LT Workstation server** | Node.js / Express | `src/LT.Workstation/LT.Workstation.Server/` | IBKR data proxy, user data persistence |
| **LT Public API** | .NET 9 Web API | `src/LimitlessTrading/LimitlessTrading.Public/LT.Public.Api/` | Publicly deployed API, hosted on Kubernetes |
| **LT Web** | Astro + Tailwind | `src/LimitlessTrading/LimitlessTrading.Web/` | Public marketing/info website, hosted on Kubernetes |

**LT Workstation** (both frontend and server) runs entirely on the user's local machine. All market data fetched from IBKR, user preferences, watchlists, and route configurations are stored locally and never leave the machine. The Express server is a local process — there is no remote Workstation server.

**LT Public API** and **LT Web** are deployed to remote infrastructure, hosted on Kubernetes. The API is at `api.limitlessworkstation.com`; the website is at `limitlessworkstation.com`. Both are independent services with no access to local Workstation data.

## Data Source Support

**Currently supported:** Interactive Brokers only, via TWS or IB Gateway (`@stoqey/ib`). All market data, account summary, and real-time quotes flow through an active TWS/Gateway connection.

> **Planned:** The data source layer is intended to be extended to support additional brokers and market data providers in the future. The current IBKR integration is the only supported source.

See [ar-analysis.md](ar-analysis.md) for the Limitless Trading app file structure and key patterns.  
See [public-api.md](public-api.md) for the LT Public API endpoints, Docker, Kubernetes, and CI/CD.  
See [web.md](web.md) for the LT Web public website, Docker, Kubernetes, and CI/CD.
