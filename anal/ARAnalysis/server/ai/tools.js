import { getAccountSummary, getDetailedQuote } from '../ibkrClient.js'

export const TOOL_DEFINITIONS = [
  {
    name: 'get_account_summary',
    description:
      'Fetch the IBKR account summary. Returns fields like NetLiquidation, TotalCashValue, ' +
      'GrossPositionValue, BuyingPower, AvailableFunds, ExcessLiquidity, RealizedPnL, ' +
      'UnrealizedPnL, DailyPnL, MaintMarginReq, InitMarginReq with their values and currencies.',
    parameters: {
      type: 'object',
      properties: {},
      required: []
    }
  },
  {
    name: 'get_live_quote',
    description:
      'Fetch the current live market quote for a stock or index symbol. ' +
      'Returns last price, bid, ask, open, high, low, prevClose, change, changePercent, volume, and more.',
    parameters: {
      type: 'object',
      properties: {
        symbol: {
          type: 'string',
          description: 'Ticker symbol, e.g. AAPL, TSLA, SPY, VIX'
        }
      },
      required: ['symbol']
    }
  }
]

export async function executeTool(name, input) {
  switch (name) {
    case 'get_account_summary': {
      const summary = await getAccountSummary()
      const result = {}
      for (const [field, { value, currency }] of Object.entries(summary)) {
        result[field] = { value, currency }
      }
      return result
    }
    case 'get_live_quote': {
      const { symbol } = input
      const quote = await getDetailedQuote(symbol.toUpperCase(), 2, false)
      if (!quote) return { error: `No quote available for ${symbol}` }
      return quote
    }
    default:
      throw new Error(`Unknown tool: ${name}`)
  }
}
