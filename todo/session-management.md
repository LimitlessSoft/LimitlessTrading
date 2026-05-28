# Session Management: Trusted Devices, Concurrent Sessions & Long-Lived API Tokens

Implement trusted device recognition with configurable session lifetime, support concurrent desktop and API sessions independently, and issue long-TTL tokens for API/algo use with background silent refresh so nightly disconnects never interrupt running strategies.

## Problem

IBKR requires full 2FA on every login with no "remember this device" option. Only one brokerage session is allowed per account — an API connection and a desktop session conflict with each other. Sessions are forcibly dropped at midnight ET nightly and after weekend market close. The Client Portal logs users out after 1–2 minutes of inactivity.

## Trusted device recognition

On successful login + 2FA, offer "Trust this device for 30 days":
- Issue a long-lived device token stored in a secure httpOnly cookie (web) or Electron secure storage (desktop)
- On subsequent logins from the same device, skip the 2FA step and issue a new session token silently
- Device tokens are listed in account settings with device name, last used date, and a revoke button
- Device trust is separate from the session — revoking trust forces 2FA on next login without immediately kicking the current session

## Concurrent sessions

Decouple desktop and API sessions into independent session slots:

```
Account: alex@example.com
  ├─ Desktop session  (expires in 30 days, trusted device)
  ├─ API key: lt_live_abc  (non-expiring, revocable)
  └─ API key: lt_live_def  (non-expiring, revocable)
```

- Desktop sessions and API key sessions never conflict with each other
- Multiple API keys can be active simultaneously (for separate algo strategies)
- An IBKR-level single-session constraint is handled by the LT server, which maintains a single IBKR connection and multiplexes all LT sessions through it

## Long-lived API tokens (no nightly disconnect)

API keys issued via the developer dashboard do not expire automatically. The LT server:
1. Maintains its own persistent connection to IBKR (via IB Gateway + IBC for auto-reconnect)
2. Handles nightly IBKR reconnects transparently — API clients never see a disconnect
3. If the IBKR connection drops, the server queues incoming API requests and replays them once reconnected, returning a `503 Reconnecting` response for requests that cannot be safely queued

## Session settings UI

Add a **Sessions & Devices** page in account settings:

```
Trusted devices
  MacBook Pro (Chrome)   Last used: today         [Revoke]
  Home desktop (LT app)  Last used: 2 days ago    [Revoke]

Active API keys
  lt_live_abc...  Momentum strategy   Created: 2025-05-01  [Revoke]
  lt_live_def...  Options scanner     Created: 2025-04-10  [Revoke]

  [+ Generate new API key]
```

## Files

| File | Change |
|------|--------|
| `server/middleware/auth.js` | Add trusted device token issuance and validation |
| `server/routes/auth.js` | Add `/auth/devices` (list, revoke) and `/auth/apikeys` (create, list, revoke) |
| `server/lib/ibkrConnectionManager.js` | New — persistent IBKR connection with auto-reconnect and request queuing |
| `src/pages/Settings.jsx` | Add Sessions & Devices tab |
| `src/components/DeviceList.jsx` | New — trusted device list with revoke buttons |
| `src/components/ApiKeyManager.jsx` | New — API key list and generation UI |
