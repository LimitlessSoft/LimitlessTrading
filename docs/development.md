# Development

## Prerequisites

- Node.js
- Interactive Brokers TWS or IB Gateway running
  - Port 7497 for paper trading
  - Port 7496 for live trading

> **Note:** Interactive Brokers is currently the only supported data source. Support for additional brokers and data providers is planned for the future.

## Obsidian Integration

The repo can be linked into an Obsidian vault so that notes stay in sync with the source files automatically — no manual copying required.

Run the link script once:

```bash
bash scripts/link-obsidian.sh
```

The script:
1. Finds all Obsidian vaults under `$HOME` (prompts if more than one is found)
2. Creates an `ARTradingTools/` subdirectory inside the vault
3. Creates symlinks from that subdirectory into the repo:

| Vault path | Repo path | Contents |
|---|---|---|
| `ARTradingTools/docs` | `docs/` | Technical documentation |
| `ARTradingTools/planning` | `planning/` | Planning and marketing docs |
| `ARTradingTools/todo` | `todo/` | Tasks and to-do notes |

Because these are symlinks, any edits made in Obsidian write directly to the repo files, and any edits made in the repo are immediately visible in Obsidian. There is no sync step.

To add a new folder to the symlink set, add an entry to the `SYMLINKS` array at the top of `scripts/link-obsidian.sh`:

```bash
SYMLINKS=(
  "docs:$REPO_ROOT/docs"
  "planning:$REPO_ROOT/planning"
  "todo:$REPO_ROOT/todo"
  "your-folder:$REPO_ROOT/your-folder"   # add new entries here
)
```

Re-running the script is safe — existing up-to-date symlinks are skipped.

## Commands

```bash
cd anal/ARAnalysis

# Start both frontend and backend
npm start

# Run separately
npm run dev      # Frontend only — Vite dev server on port 8743
npm run server   # Backend only — Express on port 8744

# Production build
npm run build
```
