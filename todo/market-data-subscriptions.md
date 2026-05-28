# Market Data Subscriptions: Inclusive Bundling & Transparent Settings

Replace the per-exchange, per-classification market data subscription complexity with a single bundled data plan included in the LT subscription. If tiered access is necessary, surface a guided wizard at onboarding and a single clear settings page showing exactly what is active and what it costs.

## Problem

IBKR defaults users to the "professional" market data classification (10× more expensive) without disclosure. Real-time streaming data requires paid subscriptions — competitors (Fidelity, Schwab) include it free. Only 100 free snapshot quotes per month are available by default. There are 30+ separate exchange bundles, each priced separately, and data is charged for the full month even on mid-month cancellation. "One of the most confusing subscription systems in retail brokerage."

## Approach: LT abstracts IBKR data billing

LT manages IBKR market data subscriptions on behalf of users and bundles the cost into the LT subscription tier. Users never need to understand IBKR's exchange bundle system.

### Subscription tiers

| Tier | Data included | IBKR subscriptions managed by LT |
|---|---|---|
| Free | 15-min delayed quotes, no streaming | IBKR delayed (no sub needed) |
| Standard | Real-time streaming for US equities + options | IBKR US Equities bundle (non-professional) |
| Pro | Real-time streaming for US + major international markets + futures | IBKR extended bundles |

LT handles:
- Activating/deactivating the correct IBKR subscriptions for the user's tier
- Ensuring the correct professional/non-professional classification (defaulting non-professional for all retail users unless the user explicitly self-classifies as professional)
- Absorbing or passing through IBKR's data costs transparently

### Non-professional classification by default

When activating IBKR market data on a user's behalf, LT always selects non-professional classification unless the user has explicitly selected professional in their LT account settings, with a clear disclosure of what the classification means and what it costs.

## Data subscription settings page

A single page in account settings (replacing any IBKR Client Portal navigation):

```
Market Data

Your plan: Standard
Includes: Real-time US equities, US options, major ETFs

Data status:
  ✓ US Equities (NYSE, NASDAQ)   Active
  ✓ US Options (OPRA)            Active
  ✗ Futures (CME)                Not included in Standard — [Upgrade]
  ✗ International (LSE, TSX)     Not included in Standard — [Upgrade]

Classification: Non-professional  [Change — see what this means]
```

## Onboarding wizard

At first login, after account connection:

```
What do you trade? (select all that apply)
  ☑ US stocks and ETFs
  ☑ US options
  ☐ Futures
  ☐ International stocks

Based on your selections, you need:
  Real-time US equities + options data
  Included in your Standard plan — no extra cost.

  [Continue]
```

## Graceful degradation for missing subscriptions

When a widget requests data for a symbol not covered by the user's subscriptions:
- Show the delayed quote with a clear label: "15-min delayed — [Upgrade to real-time]"
- Never show a confusing IBKR error code or a silent empty field

## Files

| File | Change |
|------|--------|
| `server/lib/dataSubscriptionManager.js` | New — activate/deactivate IBKR data subs per user tier; enforce non-professional default |
| `server/routes/subscriptions.js` | New — `GET /api/subscriptions/status`, `POST /api/subscriptions/classify` |
| `src/pages/Settings.jsx` | Add Market Data tab with subscription status and classification UI |
| `src/pages/Onboarding.jsx` | Add data subscription step to first-launch wizard |
| `src/components/DelayedDataBadge.jsx` | New — "15-min delayed" label shown when real-time is unavailable |
