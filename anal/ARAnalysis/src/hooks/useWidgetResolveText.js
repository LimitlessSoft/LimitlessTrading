import { useState, useEffect, useCallback } from 'react'
import { getConnectionStatus } from '../modules/dataService'
import { getExchangeTime, getExchangeStatus } from '../modules/exchangeService'
import { useSymbolQuotes } from './useSymbolQuotes'

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
    return resolveSymbolText(resolved)
  }, [systemValues, resolveSymbolText])

  const resolvedValue = useCallback((text) => {
    const n = parseFloat(resolveText(text))
    return isNaN(n) ? null : n
  }, [resolveText])

  return { resolveText, resolvedValue }
}
