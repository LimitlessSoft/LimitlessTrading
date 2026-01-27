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
  subscribeQuote,
  resolveSymbolText,
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
  const lastTextRef = useRef(null)

  useEffect(() => {
    // Clean up previous subscriptions
    unsubscribesRef.current.forEach(unsub => unsub())
    unsubscribesRef.current = []

    if (!text) {
      setQuotes(new Map())
      return
    }

    // Parse placeholders
    const placeholders = parseSymbolPlaceholders(text)

    if (placeholders.length === 0) {
      setQuotes(new Map())
      return
    }

    // Group by symbol, taking the smallest interval for each
    const symbolIntervals = new Map()
    for (const { symbol, interval } of placeholders) {
      const existing = symbolIntervals.get(symbol)
      if (!existing || interval < existing) {
        symbolIntervals.set(symbol, interval)
      }
    }

    // Subscribe to each unique symbol
    const newUnsubscribes = []
    for (const [symbol, interval] of symbolIntervals.entries()) {
      const unsubscribe = subscribeQuote(symbol, interval, (sym, quote) => {
        setQuotes(prev => {
          const next = new Map(prev)
          next.set(sym, quote)
          return next
        })
      })
      newUnsubscribes.push(unsubscribe)
    }

    unsubscribesRef.current = newUnsubscribes
    lastTextRef.current = text

    // Cleanup on unmount or text change
    return () => {
      unsubscribesRef.current.forEach(unsub => unsub())
      unsubscribesRef.current = []
    }
  }, [text])

  /**
   * Resolve symbol placeholders in text with current quote values
   * @param {string} inputText - Text to resolve
   * @returns {string} Text with placeholders replaced
   */
  const resolveText = useCallback((inputText) => {
    return resolveSymbolText(inputText, quotes)
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
  return /@\{[A-Za-z0-9.^]+:\w+(?::\d+)?\}/.test(text)
}
