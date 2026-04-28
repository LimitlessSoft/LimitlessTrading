/**
 * useSymbolQuotes React Hook
 *
 * Hook for subscribing to symbol quote updates from template text.
 * Parses @{SYMBOL:property:seconds} placeholders and manages subscriptions.
 *
 * Usage:
 *   const { quotes, resolveText } = useSymbolQuotes(label.text)
 *   const displayText = resolveText(label.text)
 */

import { useState, useEffect, useCallback, useRef } from 'react'
import {
  parseSymbolPlaceholders,
  parseFormulaPlaceholders,
  subscribeQuote,
  subscribeHistoricalClose,
  isHistoricalCloseProperty,
  resolveSymbolText,
  resolveFormulaText,
  getQuoteValue
} from '../modules/symbolQuoteService'

/**
 * Hook for subscribing to symbol quotes and resolving placeholder text
 * @param {string} text - Text that may contain @{symbol:property:seconds} placeholders
 * @returns {{ quotes: Map<string, Object>, resolveText: (text: string) => string, getValue: (symbol: string, property: string) => any }}
 */
export function useSymbolQuotes(text) {
  const [quotes, setQuotes] = useState(new Map())
  const unsubscribesRef = useRef([])

  useEffect(() => {
    // Clean up previous subscriptions
    unsubscribesRef.current.forEach(unsub => unsub())
    unsubscribesRef.current = []

    if (!text) {
      setQuotes(new Map())
      return
    }

    // Parse both standalone and formula placeholders
    const placeholders = [
      ...parseSymbolPlaceholders(text),
      ...parseFormulaPlaceholders(text),
    ]

    if (placeholders.length === 0) {
      setQuotes(new Map())
      return
    }

    const quoteCallback = (sym, quote) => {
      setQuotes(prev => {
        const next = new Map(prev)
        next.set(sym, quote)
        return next
      })
    }

    const newUnsubscribes = []

    // Subscribe to real-time/polling quotes for non-historical properties
    const liveSymbolIntervals = new Map()
    for (const { symbol, property, interval } of placeholders) {
      if (isHistoricalCloseProperty(property)) continue
      const existing = liveSymbolIntervals.get(symbol)
      if (!existing || interval < existing) {
        liveSymbolIntervals.set(symbol, interval)
      }
    }
    for (const [symbol, interval] of liveSymbolIntervals.entries()) {
      newUnsubscribes.push(subscribeQuote(symbol, interval, quoteCallback))
    }

    // Subscribe to historical close fetches (close5d, close20d etc.)
    for (const { symbol, property, interval } of placeholders) {
      if (!isHistoricalCloseProperty(property)) continue
      const days = parseInt(property.match(/^close-(\d+)$/)[1])
      // Ensure symbol also has a quote subscription so the callback fires on update
      if (!liveSymbolIntervals.has(symbol)) {
        newUnsubscribes.push(subscribeQuote(symbol, interval, quoteCallback))
        liveSymbolIntervals.set(symbol, interval)
      }
      newUnsubscribes.push(subscribeHistoricalClose(symbol, days, interval))
    }

    unsubscribesRef.current = newUnsubscribes

    // Cleanup on unmount or text change
    return () => {
      unsubscribesRef.current.forEach(unsub => unsub())
      unsubscribesRef.current = []
    }
  }, [text])

  /**
   * Resolve all placeholders: formulas first, then standalone symbol quotes.
   * Formulas run first so their internal @{...} refs aren't consumed by symbol resolution.
   * @param {string} inputText - Text to resolve
   * @returns {string} Text with placeholders replaced
   */
  const resolveText = useCallback((inputText) => {
    const afterFormulas = resolveFormulaText(inputText, quotes)
    return resolveSymbolText(afterFormulas, quotes)
  }, [quotes])

  /**
   * Get raw value for a symbol property (useful for color mapping)
   * @param {string} symbol - Stock symbol
   * @param {string} property - Property name
   * @returns {number|string|null}
   */
  const getValue = useCallback((symbol, property) => {
    return getQuoteValue(symbol, property, quotes)
  }, [quotes])

  return { quotes, resolveText, getValue }
}

/**
 * Check if text contains symbol quote placeholders
 * @param {string} text - Text to check
 * @returns {boolean}
 */
export function hasSymbolPlaceholders(text) {
  if (!text) return false
  return /@\{[A-Za-z0-9.^]+:\w+(?::\d+)?\}/.test(text) || text.includes('%[')
}
