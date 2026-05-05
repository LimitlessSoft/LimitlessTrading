// Placeholder reference — single source of truth for both the AI system prompt and the /docs/placeholders frontend page.
// When adding or changing placeholder types, update this file and the React page together.

export const PLACEHOLDER_DOCS = `Placeholder Syntax Reference
=============================

Use EXACTLY the right prefix for each type. Wrong prefix = broken widget.

── SYSTEM / APP VALUES  \${name:seconds} ──
System-level status values. Use for exchange times, FOMC dates, connection status, etc.
NEVER use @{} for these — @{} is for stock quotes only.

  \${tws_connection_status:5}   — Connection status (Connected / Disconnected)
  \${fomc_next:3600}            — Next FOMC decision date
  \${fomc_countdown:60}         — Countdown to next FOMC decision

Exchange time and market status (syntax: \${EXCHANGE:property:seconds}):
  \${NYSE:time:1}               — NYSE local time (updates every second)
  \${NYSE:status:10}            — NYSE market status (Open / Pre-Market / After-Hours / Closed)
  \${LSE:time:30}               — LSE local time
  \${LSE:status:30}             — LSE market status
  :seconds is optional (defaults to 5 s)

Supported exchanges: NYSE, NASDAQ, LSE, XETRA, TSX, JPX, HKEX, ASX, SGX

── STOCK / ETF QUOTES  @{SYMBOL:property:seconds} ──
Live market data for stocks and ETFs ONLY. Never use for exchanges or system values.

  @{AAPL:last:5}               — AAPL last price, every 5 s
  @{SPY:change:3}              — SPY price change (+/- format)
  @{TSLA:changePercent:5}      — TSLA % change (+X.XX%)
  @{MSFT:volume:10}            — MSFT volume (K/M/B suffix)
  @{VIX:last}                  — VIX value (default 5 s)
  @{SPY:close-20:3600}         — SPY closing price 20 trading days ago

Properties: last, bid, ask, open, high, low, close, prevClose, vwap, change, changePercent,
            volume, avgVolume, bidSize, askSize, marketCap, week52High, week52Low,
            callVol, putVol, pcRatio, close-N (historical close N trading days ago)

── ACCOUNT VALUES  #{field:seconds} ──
Live account data from the connected broker.

  #{NetLiquidation:60}         — Net liquidation value
  #{BuyingPower:60}            — Available buying power
  #{UnrealizedPnL:30}          — Unrealized P&L

── POSITION DATA  &{SYMBOL:property:seconds} ──
Per-symbol position data from the connected broker. Default refresh 30 s.

  &{AAPL:position:30}          — Shares/contracts of AAPL held (negative = short)
  &{AAPL:avgCost:30}           — Average cost per share for AAPL
  &{AAPL:position}             — Same with default 30 s interval

Properties: position (integer), avgCost (2 decimals)
Returns -- for symbols not held or when disconnected.
Composes with formulas:
  %[&{AAPL:position} * @{AAPL:last}:10]                        — live market value of AAPL
  %[(@{AAPL:last} - &{AAPL:avgCost}) * &{AAPL:position}:10]   — unrealized P&L for AAPL

── ALL POSITIONS  &{positions:seconds} ──
Returns ALL open positions as a JSON array, sorted by symbol. Default refresh 30 s.
Use as the source expression for a table widget.

  &{positions:30}              — full position array, e.g. [{"localSymbol":"SPY 260522P00722000","symbol":"SPY","position":2,"avgCost":104.15,"secType":"OPT","expiry":"20260522","strike":722,"right":"P","multiplier":100}, ...]
  &{positions}                 — Same with default 30 s interval

Available row fields: description (human-readable e.g. "SPY May05'26 722 PUT"), symbol, localSymbol, position, avgCost, secType, expiry, strike, right, multiplier
Table widget source: set source=&{positions:30}, use {description} for the instrument column, {position} for quantity, {avgCost} for cost.

── FORMULAS  %[expression:seconds] ──
Arithmetic over live quote data. Reference values as @{SYMBOL:property} inside the expression.
Result formatted to 2 decimal places. Shows -- if any value is unavailable.
Operators: + - * / ** and grouping with ()

  %[(@{AAPL:ask} + @{AAPL:bid}) / 2:3]                — AAPL bid-ask midpoint
  %[(@{SPY:last} / @{SPY:vwap} - 1) * 100:5]          — SPY deviation from VWAP (%)
  %[@{HYG:last} - @{LQD:last}:30]                      — Spread: HYG minus LQD
  %[(@{HYG:last} / @{HYG:close-20} - 1) * 100:3600]   — HYG 20-day return (%)
  %[(@{SPY:putVol} / (@{SPY:callVol} + @{SPY:putVol})) * 100:300] — SPY put % of total volume

General notes:
  • Placeholder types can be mixed in the same widget text, e.g. \${NYSE:status:10} | @{SPY:last:3}
  • Symbols are automatically normalized to uppercase
  • Values ≤5 s stream via SSE; >5 s use HTTP polling`
