/**
 * Position Service
 *
 * Singleton service for polling IBKR portfolio positions and resolving
 * &{SYMBOL:property:seconds} placeholders in expressions and widget text.
 *
 * Placeholder syntax: &{SYMBOL:property} or &{SYMBOL:property:seconds}
 * Example: &{AAPL:position:30} — shares held, refreshed every 30 s
 *
 * Supported properties:
 *   position  — number of shares/contracts held (plain integer)
 *   avgCost   — average cost per share (2 decimal places)
 *
 * Values are substituted as plain numbers so they compose with formula
 * placeholders: %[&{AAPL:position} * @{AAPL:last}:30]
 */

import axios from 'axios'

const api = axios.create({ baseURL: '/api', timeout: 15000 })

// &{SYMBOL:property} or &{SYMBOL:property:seconds}
const POSITION_PLACEHOLDER_RE = /&\{([A-Za-z0-9.^]+):(\w+)(?::(\d+))?\}/g

// &{positions} or &{positions:seconds} — returns all positions as JSON
const POSITIONS_ALL_RE = /&\{positions(?::(\d+))?\}/g

const DEFAULT_INTERVAL = 30

// Cache: symbol (uppercase) → { position, avgCost }
const positionCache = new Map()

// subscribers: interval (seconds) → { timer, callbacks: Set<fn> }
const pollingGroups = new Map()

/**
 * Parse position placeholders from text.
 * @param {string} text
 * @returns {Array<{symbol: string, property: string, interval: number}>}
 */
export function parsePositionPlaceholders(text) {
  if (!text || !text.includes('&{')) return []
  const results = []
  POSITION_PLACEHOLDER_RE.lastIndex = 0
  for (const m of text.matchAll(POSITION_PLACEHOLDER_RE)) {
    results.push({
      symbol: m[1].toUpperCase(),
      property: m[2].toLowerCase(),
      interval: m[3] ? parseInt(m[3], 10) : DEFAULT_INTERVAL
    })
  }
  POSITIONS_ALL_RE.lastIndex = 0
  for (const m of text.matchAll(POSITIONS_ALL_RE)) {
    results.push({
      symbol: '*',
      property: 'positions',
      interval: m[1] ? parseInt(m[1], 10) : DEFAULT_INTERVAL
    })
  }
  return results
}

async function fetchAndCache() {
  try {
    const res = await api.get('/account/positions')
    positionCache.clear()
    for (const [key, data] of Object.entries(res.data)) {
      positionCache.set(key.toUpperCase(), data)
    }
    return true
  } catch {
    return false
  }
}

function notifyAll(interval) {
  const group = pollingGroups.get(interval)
  if (!group) return
  for (const cb of group.callbacks) {
    try { cb(new Map(positionCache)) } catch {}
  }
}

async function pollGroup(interval) {
  await fetchAndCache()
  notifyAll(interval)
}

/**
 * Subscribe to position updates.
 * @param {number} interval - Polling interval in seconds
 * @param {Function} callback - Called with Map<symbol, {position, avgCost}> on each update
 * @returns {Function} Unsubscribe function
 */
export function subscribePositions(interval, callback) {
  let group = pollingGroups.get(interval)
  if (!group) {
    const timer = setInterval(() => pollGroup(interval), interval * 1000)
    group = { timer, callbacks: new Set() }
    pollingGroups.set(interval, group)
    pollGroup(interval)
  } else {
    if (positionCache.size > 0) {
      try { callback(new Map(positionCache)) } catch {}
    }
  }

  group.callbacks.add(callback)

  return () => {
    const g = pollingGroups.get(interval)
    if (!g) return
    g.callbacks.delete(callback)
    if (g.callbacks.size === 0) {
      clearInterval(g.timer)
      pollingGroups.delete(interval)
    }
  }
}

function formatPositionValue(property, data) {
  if (!data) return '--'
  if (property === 'position') {
    return data.position === null || data.position === undefined ? '--' : String(data.position)
  }
  if (property === 'avgcost') {
    return data.avgCost === null || data.avgCost === undefined ? '--' : data.avgCost.toFixed(2)
  }
  return '--'
}

/**
 * Resolve &{SYMBOL:property:seconds} placeholders using the provided cache snapshot.
 * @param {string} text
 * @param {Map<string, {position: number, avgCost: number}>} cache
 * @returns {string}
 */
export function resolvePositionText(text, cache) {
  if (!text || !text.includes('&{')) return text
  let resolved = text.replace(POSITIONS_ALL_RE, () => {
    const rows = []
    for (const [key, data] of cache.entries()) {
      if (data.position != null && data.position !== 0) {
        rows.push({ localSymbol: key, symbol: key, ...data })
      }
    }
    rows.sort((a, b) => (a.localSymbol || a.symbol).localeCompare(b.localSymbol || b.symbol))
    return JSON.stringify(rows)
  })
  resolved = resolved.replace(POSITION_PLACEHOLDER_RE, (match, symbol, property) => {
    const key = symbol.toUpperCase()
    const data = cache.get(key) || [...cache.values()].find(d => d.symbol === key)
    return formatPositionValue(property.toLowerCase(), data)
  })
  return resolved
}
