/**
 * Account Service
 *
 * Singleton service for polling IBKR account summary data and resolving
 * #{field:seconds} placeholders in expressions and widget text.
 *
 * Placeholder syntax: #{field} or #{field:seconds}
 * Example: #{NetLiquidation:30} — net liquidation value, refreshed every 30 s
 *
 * Supported fields (from IBKR reqAccountSummary):
 *   NetLiquidation, TotalCashValue, GrossPositionValue,
 *   BuyingPower, AvailableFunds, ExcessLiquidity,
 *   RealizedPnL, UnrealizedPnL, DailyPnL,
 *   MaintMarginReq, InitMarginReq
 */

import axios from 'axios'

const api = axios.create({ baseURL: '/api', timeout: 15000 })

// #{field} or #{field:seconds}
const ACCOUNT_PLACEHOLDER_RE = /\#\{(\w+)(?::(\d+))?\}/g

const DEFAULT_INTERVAL = 30

// Flat cache: field → numeric value (or null)
const valueCache = new Map()

// subscribers: interval (seconds) → { timer, fields: Set<string>, callbacks: Set<fn> }
const pollingGroups = new Map()

let nextSubId = 1

/**
 * Parse account placeholders from text.
 * @param {string} text
 * @returns {Array<{field: string, interval: number}>}
 */
export function parseAccountPlaceholders(text) {
  if (!text || !text.includes('#{')) return []
  const results = []
  ACCOUNT_PLACEHOLDER_RE.lastIndex = 0
  for (const m of text.matchAll(ACCOUNT_PLACEHOLDER_RE)) {
    results.push({
      field: m[1],
      interval: m[2] ? parseInt(m[2], 10) : DEFAULT_INTERVAL
    })
  }
  return results
}

function formatAccountValue(value) {
  if (value === null || value === undefined) return '--'
  if (typeof value === 'number') {
    if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)}B`
    if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(2)}M`
    if (Math.abs(value) >= 1_000) return `${(value / 1_000).toFixed(1)}K`
    return value.toFixed(2)
  }
  return String(value)
}

async function fetchAndCache() {
  try {
    const res = await api.get('/account/summary')
    for (const [field, { value }] of Object.entries(res.data)) {
      valueCache.set(field, value)
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
    try { cb(new Map(valueCache)) } catch {}
  }
}

async function pollGroup(interval) {
  await fetchAndCache()
  notifyAll(interval)
}

/**
 * Subscribe to account value updates.
 * @param {number} interval - Polling interval in seconds
 * @param {Function} callback - Called with Map<field, value> on each update
 * @returns {Function} Unsubscribe function
 */
export function subscribeAccount(interval, callback) {
  const subId = nextSubId++

  let group = pollingGroups.get(interval)
  if (!group) {
    const timer = setInterval(() => pollGroup(interval), interval * 1000)
    group = { timer, callbacks: new Set() }
    pollingGroups.set(interval, group)
    // Immediate first fetch
    pollGroup(interval)
  } else {
    // Deliver cached values immediately
    if (valueCache.size > 0) {
      try { callback(new Map(valueCache)) } catch {}
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

/**
 * Get the cached value for an account field.
 * @param {string} field
 * @returns {number|string|null}
 */
export function getCachedAccountValue(field) {
  return valueCache.has(field) ? valueCache.get(field) : null
}

/**
 * Resolve #{field:seconds} placeholders in text using the provided cache snapshot.
 * @param {string} text
 * @param {Map<string, any>} cache
 * @returns {string}
 */
export function resolveAccountText(text, cache) {
  if (!text || !text.includes('#{')) return text
  return text.replace(ACCOUNT_PLACEHOLDER_RE, (match, field) => {
    if (!cache.has(field)) return match
    return formatAccountValue(cache.get(field))
  })
}
