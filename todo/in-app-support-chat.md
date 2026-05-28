# In-App Support Chat & Proactive Incident Notifications

Add a persistent in-app support chat that does not require re-authentication, a priority support path for critical account issues, and proactive incident banners that appear automatically when a known platform issue is detected.

## Problem

IBKR support tickets go unanswered for 10+ days. Phone numbers are hard to find; bots block access to humans. The support chat in the web portal crashes mid-session and requires full re-authentication to restart — meaning if the platform is broken, the support tool is also broken. IBKR is rated 3.3/5 on Trustpilot largely due to support failures.

## In-app chat widget

Add a persistent chat button (bottom-right corner, all pages) that opens a slide-over chat panel:

```
╔══════════════════════╗
║  Support             ║
║  ─────────────────── ║
║  Hi, how can we      ║
║  help you today?     ║
║                      ║
║  [Type a message...] ║
╚══════════════════════╝
```

- Chat session is tied to the LT app session — no separate login or re-authentication required
- Chat history persists across page navigations and app restarts (stored server-side per user)
- If the user is disconnected mid-chat (network drop), messages queue locally and are sent on reconnect

### Priority support path

When the user opens a chat while a position discrepancy, unexpected account freeze, or order execution failure is detected:
- Automatically tag the conversation as `priority`
- Route to a human support agent queue with a guaranteed 1-hour response SLA (vs. standard async queue)

Priority triggers:
- Open position in portfolio with no matching order
- Order stuck in "Submitted" state for > 60 seconds
- Account balance change > $5,000 with no corresponding trade in the last 5 minutes

### Bot / FAQ layer

Before routing to a human, the chat bot checks the message against a knowledge base of common issues (connection errors, data subscription questions, order type docs). If a match is found, it shows the answer with a "Was this helpful? [Yes] [No — connect me to support]" prompt.

## Proactive incident notifications

Monitor the LT server's connection health and push an incident banner to all active clients when a degradation is detected:

```
⚠ We're experiencing delays in market data for NASDAQ-listed symbols.
  Orders are unaffected. Last updated: 14:32 ET  [Status page →]
```

Trigger conditions:
- Quote update rate for tracked symbols drops below 50% of normal for > 30 seconds
- Order acknowledgement latency exceeds 2 seconds average over 10 orders
- IBKR API returns error codes 1100/1101/1102 (connectivity lost) for > 1 connected client

Banner dismisses automatically when the issue resolves. Never requires the user to refresh or navigate away to see the update.

## Files

| File | Change |
|------|--------|
| `src/components/SupportChat.jsx` | New — persistent chat panel, session-bound, priority tagging |
| `src/components/IncidentBanner.jsx` | New — proactive incident notification banner |
| `server/routes/support.js` | New — chat message CRUD, session history, priority queue routing |
| `server/lib/healthMonitor.js` | New — detects data/order degradation and broadcasts incident events |
| `src/pages/TemplatePage.jsx` | Mount `SupportChat` and `IncidentBanner` at layout level |
