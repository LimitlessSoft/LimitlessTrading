/**
 * Symbol Quote Service
 *
 * Singleton service for managing symbol quote subscriptions from template labels/badges.
 * Supports two modes based on refresh interval:
 * - SSE streaming (interval <= 5 seconds): Real-time updates via Server-Sent Events
 * - HTTP polling (interval > 5 seconds): Periodic fetches via /api/live-quotes
 *
 * Placeholder syntax: @{SYMBOL:property:seconds}
 * Example: @{AAPL:last:5} - AAPL's last price, refreshing every 5 seconds
 */

import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 30000
})

// Regex to match symbol quote placeholders: @{SYMBOL:property-arg1-arg2:seconds}
// Symbol allows letters, numbers, dots, and ^ (for index symbols like ^VIX)
// Property allows word chars plus dash-separated args: last, close-20, someProperty-arg1-arg2
const SYMBOL_QUOTE_REGEX = /@\{([A-Za-z0-9.^]+):([A-Za-z]\w*(?:-[A-Za-z0-9]+)*)(?::(\d+))?\}/g

// Valid quote properties (static)
const VALID_PROPERTIES = new Set([
  'bid', 'ask', 'last', 'high', 'low', 'open', 'prevClose', 'close',
  'vwap', 'volume', 'avgVolume', 'bidSize', 'askSize',
  'marketCap', 'week52High', 'week52Low', 'change', 'changePercent'
])

// close-20, close-5, close-17 etc. — closing price N trading days ago
const CLOSE_ND_REGEX = /^close-(\d+)$/

export function isValidProperty(property) {
  return VALID_PROPERTIES.has(property) || CLOSE_ND_REGEX.test(property)
}

export function isHistoricalCloseProperty(property) {
  return CLOSE_ND_REGEX.test(property)
}

// Threshold for SSE vs polling (seconds)
const SSE_THRESHOLD = 5

// Service state
let eventSource = null
let isSSEConnected = false
const quoteCache = new Map() // symbol -> quote data
const subscribers = new Map() // `${symbol}` -> Map<subscriberId, {callback, interval}>
const pollingTimers = new Map() // interval (seconds) -> { timer, symbols: Set<string> }
let nextSubscriberId = 1

// Symbols that need SSE (interval <= 5s)
const sseSymbols = new Set()

/**
 * Parse symbol placeholders from text
 * @param {string} text - Text containing @{symbol:property:seconds} placeholders
 * @returns {Array<{symbol: string, property: string, interval: number, match: string}>}
 */
export function parseSymbolPlaceholders(text) {
  if (!text) return []

  const placeholders = []
  let match

  // Reset regex state
  SYMBOL_QUOTE_REGEX.lastIndex = 0

  while ((match = SYMBOL_QUOTE_REGEX.exec(text)) !== null) {
    const symbol = match[1].toUpperCase()
    const property = match[2].toLowerCase()
    const interval = match[3] ? parseInt(match[3], 10) : 5 // default 5 seconds

    if (isValidProperty(property)) {
      placeholders.push({
        symbol,
        property,
        interval,
        match: match[0]
      })
    }
  }

  return placeholders
}

/**
 * Format a quote value for display
 * @param {string} property - The property name
 * @param {number|null} value - The raw value
 * @returns {string} Formatted value
 */
export function formatQuoteValue(property, value) {
  if (value === null || value === undefined) {
    return '--'
  }

  // Volume and size formatting (K, M, B)
  if (['volume', 'avgVolume', 'bidSize', 'askSize'].includes(property)) {
    if (value >= 1_000_000_000) {
      return `${(value / 1_000_000_000).toFixed(2)}B`
    }
    if (value >= 1_000_000) {
      return `${(value / 1_000_000).toFixed(2)}M`
    }
    if (value >= 1_000) {
      return `${(value / 1_000).toFixed(1)}K`
    }
    return value.toLocaleString()
  }

  // Market cap formatting (with $ prefix)
  if (property === 'marketCap') {
    if (value >= 1_000_000_000_000) {
      return `$${(value / 1_000_000_000_000).toFixed(2)}T`
    }
    if (value >= 1_000_000_000) {
      return `$${(value / 1_000_000_000).toFixed(2)}B`
    }
    if (value >= 1_000_000) {
      return `$${(value / 1_000_000).toFixed(1)}M`
    }
    return `$${value.toLocaleString()}`
  }

  // Percentage formatting
  if (property === 'changePercent') {
    const sign = value >= 0 ? '+' : ''
    return `${sign}${value.toFixed(2)}%`
  }

  // Change formatting (with sign)
  if (property === 'change') {
    const sign = value >= 0 ? '+' : ''
    return `${sign}${value.toFixed(2)}`
  }

  // Default: 2 decimal places for prices
  if (typeof value === 'number') {
    return value.toFixed(2)
  }

  return String(value)
}

/**
 * Get a specific property from a quote object
 * @param {Object} quote - Quote data from API
 * @param {string} property - Property name
 * @returns {number|null}
 */
function getQuoteProperty(quote, property) {
  if (!quote) return null

  // Map our property names to API response fields
  const propertyMap = {
    bid: 'bid',
    ask: 'ask',
    last: 'last',
    high: 'high',
    low: 'low',
    open: 'open',
    prevClose: 'prevClose',
    close: 'close',
    vwap: 'vwap',
    volume: 'volume',
    avgVolume: 'avgVolume',
    bidSize: 'bidSize',
    askSize: 'askSize',
    marketCap: 'marketCap',
    week52High: 'week52High',
    week52Low: 'week52Low',
    change: 'change',
    changePercent: 'changePercent'
  }

  const apiField = propertyMap[property]
  if (apiField !== undefined) return quote[apiField] ?? null

  // Dynamic historical close: close-5, close-17, close-20 etc.
  if (CLOSE_ND_REGEX.test(property)) {
    return quote[property] ?? null
  }

  return null
}

/**
 * Subscribe to quote updates for a symbol
 * @param {string} symbol - Stock symbol
 * @param {number} interval - Refresh interval in seconds
 * @param {Function} callback - Called with (symbol, quote) on updates
 * @returns {Function} Unsubscribe function
 */
export function subscribeQuote(symbol, interval, callback) {
  const normalizedSymbol = symbol.toUpperCase()
  const subscriberId = nextSubscriberId++

  // Initialize symbol's subscriber map if needed
  if (!subscribers.has(normalizedSymbol)) {
    subscribers.set(normalizedSymbol, new Map())
  }

  const symbolSubscribers = subscribers.get(normalizedSymbol)
  symbolSubscribers.set(subscriberId, { callback, interval })

  // Determine if this symbol needs SSE or polling
  const needsSSE = interval <= SSE_THRESHOLD

  if (needsSSE) {
    sseSymbols.add(normalizedSymbol)
    ensureSSEConnection()
    subscribeSymbolToBackend(normalizedSymbol)
  } else {
    ensurePollingTimer(interval, normalizedSymbol)
  }

  // Immediately send cached data if available
  if (quoteCache.has(normalizedSymbol)) {
    callback(normalizedSymbol, quoteCache.get(normalizedSymbol))
  }

  // Return unsubscribe function
  return () => {
    const subs = subscribers.get(normalizedSymbol)
    if (subs) {
      subs.delete(subscriberId)

      // If no more subscribers for this symbol, clean up
      if (subs.size === 0) {
        subscribers.delete(normalizedSymbol)
        cleanupSymbol(normalizedSymbol)
      } else {
        // Check if we still need SSE for this symbol
        updateSymbolMode(normalizedSymbol)
      }
    }
  }
}

/**
 * Update whether a symbol needs SSE or polling based on remaining subscribers
 */
function updateSymbolMode(symbol) {
  const subs = subscribers.get(symbol)
  if (!subs || subs.size === 0) return

  // Find minimum interval among subscribers
  let minInterval = Infinity
  for (const { interval } of subs.values()) {
    if (interval < minInterval) minInterval = interval
  }

  const needsSSE = minInterval <= SSE_THRESHOLD
  const hadSSE = sseSymbols.has(symbol)

  if (needsSSE && !hadSSE) {
    // Switch to SSE
    sseSymbols.add(symbol)
    removeFromPollingTimers(symbol)
    ensureSSEConnection()
    subscribeSymbolToBackend(symbol)
  } else if (!needsSSE && hadSSE) {
    // Switch to polling
    sseSymbols.delete(symbol)
    unsubscribeSymbolFromBackend(symbol)
    ensurePollingTimer(minInterval, symbol)
    maybeCloseSSE()
  }
}

/**
 * Clean up a symbol that has no more subscribers
 */
function cleanupSymbol(symbol) {
  if (sseSymbols.has(symbol)) {
    sseSymbols.delete(symbol)
    unsubscribeSymbolFromBackend(symbol)
    maybeCloseSSE()
  } else {
    removeFromPollingTimers(symbol)
  }
}

/**
 * Ensure SSE connection is open
 */
function ensureSSEConnection() {
  if (eventSource) return

  eventSource = new EventSource('/api/stream-quotes')

  eventSource.onopen = () => {
    console.log('[symbolQuoteService] SSE connection opened')
    isSSEConnected = true
  }

  eventSource.onmessage = (event) => {
    try {
      const data = JSON.parse(event.data)

      if (data.type === 'snapshot') {
        // Initial snapshot of all subscribed quotes
        for (const [symbol, quote] of Object.entries(data.quotes)) {
          handleQuoteUpdate(symbol, quote)
        }
      } else if (data.type === 'update') {
        // Real-time update for a single symbol
        handleQuoteUpdate(data.symbol, data.quote)
      }
    } catch (err) {
      console.error('[symbolQuoteService] Failed to parse SSE message:', err)
    }
  }

  eventSource.onerror = (err) => {
    console.error('[symbolQuoteService] SSE error:', err)
    isSSEConnected = false
    // EventSource will automatically reconnect
  }
}

/**
 * Close SSE connection if no symbols need it
 */
function maybeCloseSSE() {
  if (sseSymbols.size === 0 && eventSource) {
    console.log('[symbolQuoteService] Closing SSE connection (no SSE symbols)')
    eventSource.close()
    eventSource = null
    isSSEConnected = false
  }
}

/**
 * Subscribe a symbol to the backend for SSE updates
 */
async function subscribeSymbolToBackend(symbol) {
  try {
    await api.post('/subscribe', { symbols: [symbol] })
  } catch (err) {
    console.error(`[symbolQuoteService] Failed to subscribe ${symbol}:`, err)
  }
}

/**
 * Unsubscribe a symbol from the backend
 */
async function unsubscribeSymbolFromBackend(symbol) {
  try {
    await api.post('/unsubscribe', { symbols: [symbol] })
  } catch (err) {
    console.error(`[symbolQuoteService] Failed to unsubscribe ${symbol}:`, err)
  }
}

/**
 * Handle a quote update (from SSE or polling)
 */
function handleQuoteUpdate(symbol, quote) {
  const normalizedSymbol = symbol.toUpperCase()
  const existing = quoteCache.get(normalizedSymbol) || {}
  const merged = { ...existing, ...quote }
  quoteCache.set(normalizedSymbol, merged)

  // Notify all subscribers for this symbol
  const subs = subscribers.get(normalizedSymbol)
  if (subs) {
    for (const { callback } of subs.values()) {
      try {
        callback(normalizedSymbol, quote)
      } catch (err) {
        console.error(`[symbolQuoteService] Subscriber callback error:`, err)
      }
    }
  }
}

/**
 * Ensure polling timer exists for an interval
 */
function ensurePollingTimer(interval, symbol) {
  if (!pollingTimers.has(interval)) {
    const symbols = new Set([symbol])

    const timer = setInterval(() => {
      fetchPollingQuotes(interval)
    }, interval * 1000)

    pollingTimers.set(interval, { timer, symbols })

    // Fetch immediately
    fetchPollingQuotes(interval)
  } else {
    pollingTimers.get(interval).symbols.add(symbol)
  }
}

/**
 * Remove a symbol from all polling timers
 */
function removeFromPollingTimers(symbol) {
  for (const [interval, { timer, symbols }] of pollingTimers.entries()) {
    symbols.delete(symbol)

    // If no symbols left for this interval, clear the timer
    if (symbols.size === 0) {
      clearInterval(timer)
      pollingTimers.delete(interval)
    }
  }
}

/**
 * Fetch quotes for all symbols in a polling interval
 */
async function fetchPollingQuotes(interval) {
  const timerData = pollingTimers.get(interval)
  if (!timerData || timerData.symbols.size === 0) return

  const symbolsArray = Array.from(timerData.symbols)

  try {
    const response = await api.post('/live-quotes', { symbols: symbolsArray })

    // Response is { SYMBOL: quote, ... }
    for (const [symbol, quote] of Object.entries(response.data)) {
      handleQuoteUpdate(symbol, quote)
    }
  } catch (err) {
    console.error(`[symbolQuoteService] Polling fetch error:`, err)
  }
}

/**
 * Get cached quote for a symbol
 * @param {string} symbol - Stock symbol
 * @returns {Object|null} Quote data or null
 */
export function getCachedQuote(symbol) {
  return quoteCache.get(symbol.toUpperCase()) || null
}

/**
 * Resolve symbol placeholders in text with current values
 * @param {string} text - Text with @{symbol:property:seconds} placeholders
 * @param {Map<string, Object>} quotes - Map of symbol -> quote data
 * @returns {string} Text with placeholders replaced
 */
export function resolveSymbolText(text, quotes) {
  if (!text) return ''

  return text.replace(SYMBOL_QUOTE_REGEX, (match, symbol, property) => {
    const normalizedSymbol = symbol.toUpperCase()
    const quote = quotes.get(normalizedSymbol)
    if (!quote) return match

    const value = getQuoteProperty(quote, property.toLowerCase())
    return formatQuoteValue(property.toLowerCase(), value)
  })
}

/**
 * Parse formula placeholders from text, returning all ticker refs with their interval.
 * Formula syntax: %[(expression):seconds]
 * Ticker refs inside expression: @{SYMBOL:property} (no interval — inherited from formula)
 * @param {string} text
 * @returns {Array<{symbol: string, property: string, interval: number}>}
 */
export function parseFormulaPlaceholders(text) {
  if (!text || !text.includes('%[')) return []

  const placeholders = []

  for (const formulaMatch of text.matchAll(/%\[(.+?):(\d+)\]/g)) {
    const expr = formulaMatch[1]
    const interval = parseInt(formulaMatch[2], 10)

    for (const refMatch of expr.matchAll(/@\{([A-Za-z0-9.^]+):([A-Za-z]\w*(?:-[A-Za-z0-9]+)*)\}/g)) {
      const symbol = refMatch[1].toUpperCase()
      const property = refMatch[2].toLowerCase()

      const valid = isValidProperty(property)
      console.log(`[formula:parse] symbol=${symbol} property=${property} valid=${valid} interval=${interval}`)
      if (valid) {
        placeholders.push({ symbol, property, interval })
      }
    }
  }

  return placeholders
}

/**
 * Evaluate formula placeholders in text, replacing each %[(expr):seconds] with its result.
 * @param {string} text
 * @param {Map<string, Object>} quotes
 * @returns {string}
 */
export function resolveFormulaText(text, quotes) {
  if (!text || !text.includes('%[')) return text

  return text.replace(/%\[(.+?):(\d+)\]/g, (match, expr) => {
    let hasUnavailable = false

    const substituted = expr.replace(/@\{([A-Za-z0-9.^]+):([A-Za-z]\w*(?:-[A-Za-z0-9]+)*)\}/g, (_, symbol, property) => {
      const quote = quotes.get(symbol.toUpperCase())
      const value = getQuoteProperty(quote, property.toLowerCase())
      console.log(`[formula:resolve] symbol=${symbol} property=${property} value=${value} quoteKeys=${quote ? Object.keys(quote).join(',') : 'NO_QUOTE'}`)
      if (value === null || value === undefined) {
        hasUnavailable = true
        return '0'
      }
      return String(value)
    })

    if (hasUnavailable) return '--'

    try {
      // eslint-disable-next-line no-new-func
      const result = new Function('return (' + substituted + ')')()
      if (typeof result !== 'number' || !isFinite(result)) return '--'
      return result.toFixed(2)
    } catch {
      return '--'
    }
  })
}

/**
 * Get raw value from quote for a specific property
 * Useful for badges that need the actual value for color mapping
 * @param {string} symbol - Stock symbol
 * @param {string} property - Property name
 * @param {Map<string, Object>} quotes - Quote cache
 * @returns {number|string|null}
 */
export function getQuoteValue(symbol, property, quotes) {
  const quote = quotes.get(symbol.toUpperCase())
  return getQuoteProperty(quote, property.toLowerCase())
}

// Historical close subscriptions: `${symbol}:${days}` → { refCount, timerId }
const historicalCloseSubs = new Map()

/**
 * Subscribe to periodic fetches of the closing price from N trading days ago.
 * Results are merged into the quote cache as `close${days}d` and broadcast to all
 * existing quote subscribers for that symbol.
 * @param {string} symbol
 * @param {number} days - Number of trading days to look back
 * @param {number} interval - Refresh interval in seconds
 * @returns {Function} Unsubscribe function
 */
export function subscribeHistoricalClose(symbol, days, interval) {
  const upperSymbol = symbol.toUpperCase()
  const key = `${upperSymbol}:${days}`
  const propertyKey = `close-${days}`

  const fetchClose = async () => {
    console.log(`[historicalClose] fetching ${propertyKey} for ${upperSymbol}`)
    try {
      const res = await api.get(`/close-nd/${upperSymbol}?days=${days}`)
      console.log(`[historicalClose] got ${propertyKey} for ${upperSymbol}:`, res.data)
      const existing = quoteCache.get(upperSymbol) || { symbol: upperSymbol }
      handleQuoteUpdate(upperSymbol, { ...existing, [propertyKey]: res.data.close })
    } catch (err) {
      console.error(`[historicalClose] fetch error for ${upperSymbol} ${propertyKey}:`, err)
    }
  }

  const existing = historicalCloseSubs.get(key)
  if (existing) {
    existing.refCount++
    console.log(`[historicalClose] reusing existing sub for ${key}, refCount=${existing.refCount}`)
    // Immediately deliver cached value if available
    const cached = quoteCache.get(upperSymbol)
    if (cached?.[propertyKey] !== undefined) {
      handleQuoteUpdate(upperSymbol, cached)
    }
  } else {
    console.log(`[historicalClose] creating new sub for ${key}, interval=${interval}s`)
    fetchClose()
    const timerId = setInterval(fetchClose, interval * 1000)
    historicalCloseSubs.set(key, { refCount: 1, timerId })
  }

  return () => {
    const sub = historicalCloseSubs.get(key)
    if (!sub) return
    sub.refCount--
    if (sub.refCount <= 0) {
      clearInterval(sub.timerId)
      historicalCloseSubs.delete(key)
    }
  }
}

// Export for testing/debugging
export function _getServiceState() {
  return {
    sseConnected: isSSEConnected,
    sseSymbols: Array.from(sseSymbols),
    pollingIntervals: Array.from(pollingTimers.keys()),
    cachedSymbols: Array.from(quoteCache.keys()),
    subscriberCount: Array.from(subscribers.entries()).map(([s, m]) => ({ symbol: s, count: m.size }))
  }
}
