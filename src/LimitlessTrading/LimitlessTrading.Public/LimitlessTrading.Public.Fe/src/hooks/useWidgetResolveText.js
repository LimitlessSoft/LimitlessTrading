import { useState, useEffect, useCallback } from 'react'
import { getConnectionStatus } from '../modules/dataService'
import { getExchangeTime, getExchangeStatus } from '../modules/exchangeService'
import { getNextFomcDecision, formatFomcDatetime, formatFomcCountdownText } from '../modules/fomcService'
import { useSymbolQuotes } from './useSymbolQuotes'
import { parseAccountPlaceholders, subscribeAccount, resolveAccountText } from '../modules/accountService'

// Simple system placeholder: ${key} or ${key:seconds}
// Does NOT match exchange patterns like ${NYSE:status:10} (those have non-numeric after the colon)
const SYSTEM_PLACEHOLDER_RE = /\$\{(\w+)(?::(\d+))?\}/g
const EXCHANGE_PLACEHOLDER_RE = /\$\{([A-Za-z0-9]+):(time|status)(?::(\d+))?\}/g

function getMinInterval(text) {
  let min = Infinity
  for (const m of text.matchAll(/\$\{[^}]+:(\d+)\}/g)) {
    const n = parseInt(m[1], 10)
    if (n < min) min = n
  }
  return min === Infinity ? null : min
}

/**
 * Shared placeholder resolution for template widgets.
 *
 * @param {string|string[]} textSources - All texts to scan for placeholder subscriptions.
 *   Include the widget's display text AND any conditional-style placeholder expressions
 *   so the correct polling intervals and symbol subscriptions are set up.
 * @returns {{ resolveText: (text: string) => string }}
 */
export function useWidgetResolveText(textSources) {
  const combined = (Array.isArray(textSources) ? textSources : [textSources])
    .filter(Boolean)
    .join(' ')

  const [systemValues, setSystemValues] = useState({})
  const [accountCache, setAccountCache] = useState(new Map())
  const hasSystem = combined.includes('${')

  const fetchValues = useCallback(async () => {
    if (!combined) return
    const values = {}

    if (combined.includes('${tws_connection_status')) {
      try {
        const status = await getConnectionStatus()
        values.tws_connection_status = status.connected
          ? `Connected (${status.port})`
          : 'Disconnected'
      } catch {
        values.tws_connection_status = 'Unknown'
      }
    }

    if (combined.includes('${fomc_next') || combined.includes('${fomc_countdown')) {
      try {
        const nextDate = await getNextFomcDecision()
        if (nextDate) {
          values.fomc_next = formatFomcDatetime(nextDate)
          values.fomc_countdown = formatFomcCountdownText(nextDate)
        } else {
          values.fomc_next = 'N/A'
          values.fomc_countdown = 'N/A'
        }
      } catch {
        values.fomc_next = 'Unknown'
        values.fomc_countdown = 'Unknown'
      }
    }

    for (const m of combined.matchAll(/\$\{([A-Za-z0-9]+):(time|status)(?::(\d+))?\}/g)) {
      const exchange = m[1].toUpperCase()
      const property = m[2]
      values[`${exchange}_${property}`] = property === 'time'
        ? getExchangeTime(exchange)
        : getExchangeStatus(exchange)
    }

    setSystemValues(values)
  }, [combined])

  useEffect(() => {
    if (!hasSystem) return
    const interval = getMinInterval(combined)
    if (!interval) return
    fetchValues()
    const timer = setInterval(fetchValues, interval * 1000)
    return () => clearInterval(timer)
  }, [hasSystem, combined, fetchValues])

  // Account placeholders: #{field:seconds}
  useEffect(() => {
    if (!combined.includes('#{')) return
    const placeholders = parseAccountPlaceholders(combined)
    if (placeholders.length === 0) return

    // Group by the minimum interval across all fields in this text
    const minInterval = placeholders.reduce((min, { interval }) => Math.min(min, interval), Infinity)
    const unsub = subscribeAccount(minInterval, (cache) => setAccountCache(new Map(cache)))
    return unsub
  }, [combined])

  const { resolveText: resolveSymbolText } = useSymbolQuotes(
    combined.includes('@{') || combined.includes('%[') ? combined : null
  )

  const resolveText = useCallback((text) => {
    if (!text) return ''
    let resolved = text
      .replace(SYSTEM_PLACEHOLDER_RE, (match, key) =>
        systemValues[key] !== undefined ? systemValues[key] : match
      )
      .replace(EXCHANGE_PLACEHOLDER_RE, (match, exchange, property) => {
        const key = `${exchange.toUpperCase()}_${property}`
        return systemValues[key] !== undefined ? systemValues[key] : match
      })
    resolved = resolveAccountText(resolved, accountCache)
    return resolveSymbolText(resolved)
  }, [systemValues, accountCache, resolveSymbolText])

  const resolvedValue = useCallback((text) => {
    const str = resolveText(text).trim()
    // Strip leading $, + for numeric parsing; trailing % is ignored
    const clean = str.replace(/^[$+]+/, '')
    // Handle K/M/B/T suffixes produced by formatQuoteValue (e.g. "450.3K", "1.2M", "$1.2B")
    const last = clean[clean.length - 1]
    const suffixMult = { K: 1e3, M: 1e6, B: 1e9, T: 1e12 }[last]
    if (suffixMult) {
      const n = parseFloat(clean)
      return isNaN(n) ? null : n * suffixMult
    }
    const n = parseFloat(clean)
    return isNaN(n) ? null : n
  }, [resolveText])

  return { resolveText, resolvedValue }
}
