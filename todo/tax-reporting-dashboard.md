# Tax Reporting Dashboard: Year-Round P&L Tracking & Export

Add a tax reporting section to the app that shows realized gains/losses by asset class and holding period, wash sale flags, and estimated tax impact — updated throughout the year, not just at January statement time. Offer export to standard formats compatible with TurboTax and TaxAct.

## Problem

IBKR's 1099-B reports short sales on trade date rather than settlement date, inconsistent with IRS instructions and other brokers, causing discrepancies when importing into TurboTax. The consolidated 1099 is split across four sub-forms with unclear field usage. IBKR recommends consulting a tax advisor for all 1099 amounts — an admission the documents are not self-explanatory.

## Tax summary widget: `tax-summary`

```js
{
  id: "uuid",
  type: "tax-summary",
  width: 20,
  height: 400,
  taxYear: 2025,
  style: { bgColor: "#1a202c", textColor: "#E2E8F0" }
}
```

## Layout

```
Tax Summary — 2025

  Short-term gains:    +$4,200   (held < 1 year — taxed as ordinary income)
  Short-term losses:   -$1,100
  Net short-term:      +$3,100

  Long-term gains:     +$12,400  (held > 1 year — lower tax rate)
  Long-term losses:    -$800
  Net long-term:       +$11,600

  Wash sale adjustments:  -$340   (3 trades affected)  [View details]

  Estimated federal tax:  ~$3,900  (based on 24% bracket — adjust in settings)

  [Export CSV]  [Export TurboTax .txf]  [Export TaxAct .csv]
```

## Data pipeline

Track every closed trade in a `trade_history` table:

```
symbol, qty, action, entryDate, entryPrice, exitDate, exitPrice, realizedPnl, holdingDays, assetClass
```

Data sources:
1. IBKR execution history (`reqExecutions`) — fetched on startup and on each new fill
2. LT's own order/fill log for paper and replay trades

### Holding period calculation

- Short-term: `exitDate - entryDate < 365 days`
- Long-term: `exitDate - entryDate >= 365 days`
- Options: short-term by default; long-term LEAPS if held > 365 days
- Settlement date used for both entry and exit (trade date + 1 business day for equities; same day for options)

### Wash sale detection

Flag a trade as a wash sale if the same security (or substantially identical security) was purchased within 30 days before or after the sale at a loss:
1. Identify loss trades
2. Scan ±30 calendar days for purchases of the same symbol
3. Mark the disallowed loss and add it to the cost basis of the replacement shares
4. Show affected trades with a ⚠ wash sale badge

## Export formats

**CSV**: standard column layout (symbol, qty, proceeds, cost basis, gain/loss, holding period, wash sale adjustment) suitable for manual import.

**TurboTax .txf**: Transfer Format file — TurboTax's import standard for capital gains. Each record maps to a 1099-B Box 1 entry.

**TaxAct CSV**: TaxAct's own import format (slightly different column names from TurboTax).

Settlement-date accounting is used for all exports to match IRS Schedule D and Form 8949 requirements.

## Settings

- Tax bracket (for estimated tax calculation): dropdown 10% / 12% / 22% / 24% / 32% / 35% / 37%
- State tax rate (optional, for total estimated tax)
- FIFO / LIFO / specific identification cost basis method

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageTaxSummary.jsx` | New — tax summary widget |
| `server/lib/tradeHistoryStore.js` | New — persistent trade history with P&L, holding period, wash sale detection |
| `server/lib/taxExporter.js` | New — CSV, .txf, and TaxAct export generators |
| `server/routes/tax.js` | New — `GET /api/tax/summary`, `GET /api/tax/export` |
| `src/pages/TemplatePage.jsx` | Register `tax-summary` in render switch and Add Widget modal |
| `docs/template-widgets.md` | Document `tax-summary` widget |
