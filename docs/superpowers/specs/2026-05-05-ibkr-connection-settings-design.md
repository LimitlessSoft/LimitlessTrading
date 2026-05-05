# IBKR Connection Settings

**Date:** 2026-05-05

## Problem

The app auto-connects to port 7496 (TWS Live) on startup. IB Gateway uses different ports (4001 live, 4002 paper). There is no UI to change the port, so users switching from TWS to Gateway cannot connect.

## Goal

Add a connection settings entry to the hamburger menu that lets the user switch between the four standard IBKR connection presets and immediately reconnects.

## Presets

| Label         | App     | Mode  | Port |
|---------------|---------|-------|------|
| TWS · Live    | TWS     | Live  | 7496 |
| TWS · Paper   | TWS     | Paper | 7497 |
| Gateway · Live  | Gateway | Live  | 4001 |
| Gateway · Paper | Gateway | Paper | 4002 |

## UI

### Menu entry

A "Connection" item is added to the hamburger menu above "About", using a plug/link icon (`ExternalLinkIcon` or similar from `@chakra-ui/icons`).

### Modal

- Title: "IBKR Connection"
- Body: 2×2 grid of preset cards (TWS column left, Gateway column right; Live row top, Paper row bottom)
- Each card shows the app name, mode label, and port number
- The card matching the currently connected port is highlighted in blue (`blue.600` border + subtle blue background)
- A disconnected state shows no card highlighted

### Interaction

1. User clicks a card
2. That card immediately shows a "Connecting…" spinner (disabled state on all cards)
3. The server calls `POST /api/disconnect` then `POST /api/connect` with the new port
4. On success: the card turns green briefly, then settles into the blue "active" highlight; modal stays open
5. On error: all cards re-enable, the selected card shows a red border; error message appears below the grid
6. The new port is persisted via `setSetting('lastPort', port)` (already done inside `connectToIBKR`)

## Architecture

### Files changed

**`PageNavMenu.jsx`** — only file that needs changes:
- Add `useDisclosure` for the connection modal
- Add connection state: `connStatus` (fetched on modal open via `getConnectionStatus()`), `connecting` flag, `connectError` string
- Add "Connection" `MenuItem` above "About"
- Add the connection `Modal` with the 2×2 preset grid
- On card click: call `disconnectFromIBKR()` then `connectToIBKR(port)` from `dataService`

**`AutoConnect.jsx`** — no change. Already reads `lastPort` from settings; fallback of 7496 is correct.

**`dataService.js`** — no change. `connectToIBKR` already saves `lastPort`.

## Error handling

- If the initial `getConnectionStatus()` call on modal open fails, treat as disconnected (no card highlighted)
- If reconnect fails, show the error message returned by the API below the grid; all cards re-enable for retry
- Timeout / network errors surface as the error string from the caught exception

## Out of scope

- Custom port input field
- Connecting to a remote host (non-localhost)
