# IBKR Connection Info in About Window

Include the active IBKR connection mode in the About window — both in the displayed info and in the clipboard copy.

## What to show

Add a new "Connection" section to the About modal (between License and Device Info), showing:

| Field | Example value |
|-------|---------------|
| app | TWS |
| mode | Paper |
| port | 7497 |
| status | Connected / Disconnected |

When disconnected, display `—` for app and mode, and `Disconnected` for status.

## Where to get the data

`connectedPort` is already fetched into state in `PageNavMenu` when the Connection modal opens. For the About modal, fetch it on open as well using the existing `getConnectionStatus()` and `getLastPort()` calls from `dataService.js`.

Derive app and mode from the port using the `CONNECTION_PRESETS` array already defined at the top of `PageNavMenu.jsx`:

```js
const preset = CONNECTION_PRESETS.find(p => p.port === connectedPort)
// preset.app  → "TWS" | "Gateway"
// preset.mode → "Live" | "Paper"
```

## Clipboard copy

Extend the `getDeviceInfo()` copy block (the copy button in the About modal header) to prepend the connection info:

```
connection app:  TWS
connection mode: Paper
connection port: 7497
connection status: Connected
```

## Files

| File | Change |
|------|--------|
| `src/components/PageNavMenu.jsx` | Fetch connection status on About modal open; add Connection section to modal body; include connection fields in copy text |
