# Unified Workspace: Feature Parity Across Desktop and Web

Ensure every feature available in the Limitless Trading desktop app is equally accessible via the web app, with no silent capability gaps and clear redirects when context-specific limitations exist.

## Problem

IBKR forces users across five separate apps (TWS, IBKR Desktop, Client Portal, IBKR Mobile, GlobalTrader), each with different feature sets and no guidance on which to use. Users describe needing "3–4 apps to do what one should do." Conditional orders, screeners, research tools, and account management are all locked to different surfaces.

## Implementation notes

### Feature availability matrix

Maintain a central `featureFlags` map in the frontend that declares which features are supported in each context (electron desktop vs. web browser vs. future surfaces):

```js
const featureFlags = {
  conditionalOrders: { desktop: true, web: true },
  advancedCharts:    { desktop: true, web: true },
  paperTrading:      { desktop: true, web: true },
  // ...
}
```

When a feature is unavailable in the current context, show a non-silent fallback:

```
⚠ Conditional orders are not available in this context.
  [Open in Desktop App]
```

Never hide the option silently — always show it greyed out with an explanation.

### Consistent widget catalog

All widget types registered in `TemplatePage.jsx` must render correctly in both Electron and browser environments. Any widget with a desktop-only dependency (e.g., direct socket access) should degrade gracefully by falling back to the REST API layer.

### Navigation audit

Run a full audit of every route and widget settings panel:
- Does it work identically in browser and Electron?
- Does it fail silently or visibly if a dependency is missing?
- Is the feature discoverable from the main navigation?

Produce a parity report and create follow-up tasks for any gaps.

### Single onboarding flow

At first launch, route the user through a setup wizard regardless of surface:
- What are you trading? (equities, options, futures, crypto)
- How do you connect? (IBKR live, IBKR paper, other broker)
- Surface them only the relevant widgets and defaults for their answers

## Files

| File | Change |
|------|--------|
| `src/lib/featureFlags.js` | New — feature availability map per context |
| `src/components/FeatureUnavailable.jsx` | New — standard greyed-out with redirect component |
| `src/pages/TemplatePage.jsx` | Wrap widget render switch with feature flag checks |
| `src/pages/Onboarding.jsx` | New — first-launch setup wizard |
| `docs/README.md` | Update architecture overview to document feature parity policy |
