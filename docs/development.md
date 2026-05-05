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
2. Creates an `LimitlessTrading/` subdirectory inside the vault
3. Creates symlinks from that subdirectory into the repo:

| Vault path | Repo path | Contents |
|---|---|---|
| `LimitlessTrading/docs` | `docs/` | Technical documentation |
| `LimitlessTrading/planning` | `planning/` | Planning and marketing docs |
| `LimitlessTrading/todo` | `todo/` | Tasks and to-do notes |

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

### Run all apps (recommended)

```bash
bash scripts/dev.sh
```

Starts the Public API (background, logs to `/tmp/lt-api.log`) and the Workstation Electron app (foreground). If either is already running it is killed first. Press `Ctrl+C` to stop the Workstation; the API process will need to be killed separately if needed.

### Electron app only

```bash
cd src/LT.Workstation/LT.Workstation.App
npm install
npm run dev      # Starts Vite + Express + Electron together
npm run build    # Builds frontend then packages the Electron app
```

### Without Electron (browser)

```bash
cd src/LT.Workstation/LT.Workstation.Fe
npm run dev      # Vite dev server on port 8743

cd src/LT.Workstation/LT.Workstation.Server
npm start        # Express on port 8744
```

## Versioning

Workstation (App + Fe + Server) and Public API are versioned independently.

### Workstation

Single source of truth: `src/LT.Workstation/version.json`. All three `package.json` files mirror this value. The server reads it directly to expose `GET /api/version`.

```bash
node scripts/set-workstation-version.js           # bump patch  →  0.1.0 → 0.1.1
node scripts/set-workstation-version.js --minor   # bump minor  →  0.1.0 → 0.2.0
node scripts/set-workstation-version.js --major   # bump major  →  0.1.0 → 1.0.0
```

### Public API

Single source of truth: `<Version>` tag in `LT.Public.Api.csproj`.

```bash
node scripts/set-api-version.js           # bump patch
node scripts/set-api-version.js --minor   # bump minor
node scripts/set-api-version.js --major   # bump major
```

CI bumps the patch version automatically on every release. Minor and major bumps are done manually before pushing.
