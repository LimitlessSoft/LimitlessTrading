#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Kill existing instances
echo "Checking for running instances..."

# Public API (dotnet LT.Public.Api)
API_PIDS=$(pgrep -f "LT.Public.Api" 2>/dev/null || true)
if [ -n "$API_PIDS" ]; then
  echo "Stopping Public API (PIDs: $API_PIDS)..."
  kill $API_PIDS
  sleep 1
fi

# Workstation Electron / Vite / Express stack (by process name and ports)
WORKSTATION_PIDS=$(pgrep -f "LT.Workstation" 2>/dev/null || true)
PORT_PIDS=$(fuser 8743/tcp 8744/tcp 2>/dev/null || true)
ALL_PIDS=$(echo "$WORKSTATION_PIDS $PORT_PIDS" | tr ' ' '\n' | sort -u | tr '\n' ' ' | xargs)
if [ -n "$ALL_PIDS" ]; then
  echo "Stopping Workstation (PIDs: $ALL_PIDS)..."
  kill $ALL_PIDS 2>/dev/null || true
  sleep 1
fi

# Start Public API in background, log to /tmp/lt-api.log
echo "Starting Public API..."
(
  cd "$REPO_ROOT/src/LimitlessTrading/LimitlessTrading.Public/LT.Public.Api/LT.Public.Api"
  dotnet run &>/tmp/lt-api.log
) &
echo "Public API started (log: /tmp/lt-api.log)"

# Start Workstation (Electron + Vite + Express) — foreground so Ctrl+C stops everything
echo "Starting Workstation..."
cd "$REPO_ROOT/src/LT.Workstation/LT.Workstation.App"
npm run dev
