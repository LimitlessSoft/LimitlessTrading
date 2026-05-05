import { getPositions } from '../ibkrClient.js'

const POSITIONS_ALL_RE = /&\{positions(?::(\d+))?\}/

export async function resolveExpression(expr) {
  if (POSITIONS_ALL_RE.test(expr)) {
    const positions = await getPositions()
    const rows = Object.values(positions)
      .filter(({ position }) => position != null && position !== 0)
      .sort((a, b) => (a.localSymbol || a.symbol).localeCompare(b.localSymbol || b.symbol))
    return JSON.stringify(rows)
  }
  return null
}
