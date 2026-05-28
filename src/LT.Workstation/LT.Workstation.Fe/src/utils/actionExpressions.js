import axios from 'axios'

const api = axios.create({ baseURL: '/api', timeout: 15000 })

// Matches the full ![...] expression
const ACTION_EXPR_REGEX = /^!\[(.+)\]$/

// Matches @{SYMBOL:property} refs inside an action expression (no interval — one-shot)
const PLACEHOLDER_REF_REGEX = /@\{([A-Za-z0-9.^]+):([A-Za-z]\w*(?:-[A-Za-z0-9]+)*)\}/g

const VALID_ACTIONS = new Set(['BUY', 'SELL'])
const VALID_ORDER_TYPES = new Set(['MKT', 'LMT', 'STP', 'STP_LMT'])

/**
 * Parse an action expression string.
 * @param {string} expr - e.g. "![order:BUY:AAPL:100:LMT:185.50]"
 * @returns {{ kind: 'order', action, symbol, quantity, orderType, limitPrice, stopPrice }}
 * @throws {Error} on invalid syntax or missing required params
 */
export function parseActionExpression(expr) {
  const outer = ACTION_EXPR_REGEX.exec(expr?.trim())
  if (!outer) throw new Error(`Invalid action expression syntax: ${expr}`)

  const parts = outer[1].split(':')
  const kind = parts[0]

  if (kind === 'order') {
    const [, action, symbol, quantity, orderType, limitPrice, stopPrice] = parts

    if (!VALID_ACTIONS.has(action)) {
      throw new Error(`Invalid order action "${action}" — must be BUY or SELL`)
    }
    if (!symbol) throw new Error('Order action expression missing symbol')
    if (!quantity) throw new Error('Order action expression missing quantity')
    if (!VALID_ORDER_TYPES.has(orderType)) {
      throw new Error(`Invalid orderType "${orderType}" — must be MKT, LMT, STP, or STP_LMT`)
    }
    if ((orderType === 'LMT' || orderType === 'STP_LMT') && !limitPrice) {
      throw new Error(`limitPrice is required for ${orderType} orders`)
    }
    if ((orderType === 'STP' || orderType === 'STP_LMT') && !stopPrice) {
      throw new Error(`stopPrice is required for ${orderType} orders`)
    }

    return { kind: 'order', action, symbol, quantity, orderType, limitPrice: limitPrice ?? null, stopPrice: stopPrice ?? null }
  }

  throw new Error(`Unknown action kind "${kind}"`)
}

/**
 * Resolve embedded @{SYMBOL:property} refs in a raw segment string.
 * resolveValue is called with the full "@{SYMBOL:property}" match and should return
 * the current numeric value (or null if unavailable).
 * @param {string} segment
 * @param {(placeholder: string) => number|null} resolveValue
 * @returns {string}
 */
function resolveSegment(segment, resolveValue) {
  if (!segment) return segment
  PLACEHOLDER_REF_REGEX.lastIndex = 0
  return segment.replace(PLACEHOLDER_REF_REGEX, (match) => {
    const val = resolveValue(match)
    return val != null ? String(val) : match
  })
}

/**
 * Execute an action expression.
 * @param {string} expr - Raw action expression string e.g. "![order:BUY:AAPL:100:LMT:@{AAPL:ask}]"
 * @param {(placeholder: string) => number|null} resolveValue - Resolves @{...} refs to their current numeric value
 * @returns {Promise<{ orderId: number, status: string }>}
 */
export async function executeActionExpression(expr, resolveValue = () => null) {
  const parsed = parseActionExpression(expr)

  if (parsed.kind === 'order') {
    const quantity = resolveSegment(parsed.quantity, resolveValue)
    const limitPrice = parsed.limitPrice ? resolveSegment(parsed.limitPrice, resolveValue) : null
    const stopPrice = parsed.stopPrice ? resolveSegment(parsed.stopPrice, resolveValue) : null
    const symbol = resolveSegment(parsed.symbol, resolveValue)

    const body = {
      action: parsed.action,
      symbol,
      quantity: Number(quantity),
      orderType: parsed.orderType,
      ...(limitPrice != null && { limitPrice: Number(limitPrice) }),
      ...(stopPrice != null && { stopPrice: Number(stopPrice) }),
    }

    const res = await api.post('/orders', body)
    return res.data
  }
}

/**
 * Build a human-readable summary of an action expression for confirmation dialogs.
 * Resolves any @{...} placeholders before building the string.
 * @param {string} expr
 * @param {(placeholder: string) => number|null} resolveValue
 * @returns {string} e.g. "BUY 100 AAPL @ LMT $185.50"
 */
export function describeActionExpression(expr, resolveValue = () => null) {
  try {
    const parsed = parseActionExpression(expr)
    if (parsed.kind === 'order') {
      const symbol = resolveSegment(parsed.symbol, resolveValue)
      const quantity = resolveSegment(parsed.quantity, resolveValue)
      const orderType = parsed.orderType
      let desc = `${parsed.action} ${quantity} ${symbol} @ ${orderType}`
      if (parsed.limitPrice) desc += ` $${resolveSegment(parsed.limitPrice, resolveValue)}`
      if (parsed.stopPrice) desc += ` stop $${resolveSegment(parsed.stopPrice, resolveValue)}`
      return desc
    }
  } catch {
    // fall through
  }
  return expr
}
