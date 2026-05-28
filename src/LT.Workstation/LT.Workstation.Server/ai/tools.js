import { getAccountSummary, getDetailedQuote, getHistoricalData, getConnectionStatus } from '../ibkrClient.js'


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
  },
  {
    name: 'get_historical_bars',
    description:
      'Fetch recent historical OHLCV price bars for a ticker symbol. Use this to assess trend, ' +
      'momentum, support/resistance, or any question requiring price history. ' +
      'Returns an array of bars with timestamp, open, high, low, close, volume.',
    parameters: {
      type: 'object',
      properties: {
        symbol: {
          type: 'string',
          description: 'Ticker symbol, e.g. AAPL, SPY, QQQ'
        },
        bar_size: {
          type: 'string',
          enum: ['1 min', '5 mins', '15 mins', '30 mins', '1 hour', '1 day'],
          description: 'Bar size. Default: 1 day'
        },
        duration: {
          type: 'string',
          description: 'How far back to fetch, e.g. "10 D", "1 M", "3 M". Default: "20 D"'
        }
      },
      required: ['symbol']
    }
  },
  {
    name: 'delete_alert',
    description:
      'Delete an existing alert by its ID or label. The alerts list is provided in the system context. ' +
      'Prefer matching by ID when available.',
    parameters: {
      type: 'object',
      properties: {
        id: {
          type: 'string',
          description: 'The alert ID to delete, e.g. "alert-1"'
        },
        label: {
          type: 'string',
          description: 'The alert label to delete when the ID is unknown'
        }
      },
      required: []
    }
  },
  {
    name: 'create_alert',
    description:
      'Create a price or data alert for a ticker symbol. The alert fires when the specified ' +
      'condition is met (e.g. SPY last price > 500). Use get_live_quote first to confirm the ' +
      'current price before setting the threshold.',
    parameters: {
      type: 'object',
      properties: {
        label: {
          type: 'string',
          description: 'Short human-readable name for the alert, e.g. "SPY above 500"'
        },
        symbol: {
          type: 'string',
          description: 'Ticker symbol, e.g. SPY, AAPL, QQQ'
        },
        property: {
          type: 'string',
          enum: ['last', 'bid', 'ask', 'change', 'changePercent', 'volume'],
          description: 'Quote field to watch. Default: last (price)'
        },
        operator: {
          type: 'string',
          enum: ['>', '<', '>=', '<=', '==', '!='],
          description: 'Comparison operator'
        },
        value: {
          type: 'number',
          description: 'Threshold to compare against'
        },
        refresh_seconds: {
          type: 'number',
          description: 'How often to re-check in seconds. Default: 30'
        },
        actions: {
          type: 'array',
          items: { type: 'string', enum: ['sound', 'message'] },
          description: 'What to do when the alert fires. Default: ["sound", "message"]'
        },
        message: {
          type: 'string',
          description: 'Notification message to display when the alert fires'
        }
      },
      required: ['label', 'symbol', 'operator', 'value']
    }
  },
  {
    name: 'create_widget',
    description:
      'Add a new widget to the current template page. Only call this when the user explicitly ' +
      'asks to add, create, or place a widget. The widget appears immediately on the page. ' +
      'Use get_live_quote first to verify a ticker exists before using it in a widget.',
    parameters: {
      type: 'object',
      properties: {
        type: {
          type: 'string',
          description: 'Widget type'
        },
        text: {
          type: 'string',
          description: 'Label text with optional placeholders. Required for type=label. Example: "@{SPY:last:5}"'
        },
        symbol: {
          type: 'string',
          description: 'Ticker symbol. Required for type=chart. Example: "AAPL"'
        },
        interval: {
          type: 'string',
          enum: ['1m', '2m', '3m', '5m', '15m', '30m', '1h', '1d'],
          description: 'Bar interval for type=chart. Defaults to "5m" if omitted.'
        },
        placeholder: {
          type: 'string',
          description: 'Placeholder expression. Required for type=badge and type=gauge. Example: "@{VIX:last:5}"'
        },
        title: {
          type: 'string',
          description: 'Optional label shown below a gauge arc'
        },
        min: { type: 'number', description: 'Gauge minimum value. Required for type=gauge' },
        max: { type: 'number', description: 'Gauge maximum value. Required for type=gauge' },
        segments: {
          type: 'array',
          description: 'Gauge colored arc bands. Required for type=gauge',
          items: {
            type: 'object',
            properties: {
              from: { type: 'number' },
              to: { type: 'number' },
              color: { type: 'string', description: 'Hex color e.g. "#48BB78"' }
            },
            required: ['from', 'to', 'color']
          }
        },
        series: {
          type: 'array',
          description: 'Data series. Required for type=graph',
          items: {
            type: 'object',
            properties: {
              name: { type: 'string' },
              expression: { type: 'string', description: 'Placeholder expression, e.g. "@{SPY:last:5}"' },
              color: { type: 'string', description: 'Hex color' },
              viewType: { type: 'string', enum: ['line', 'bars'] }
            },
            required: ['name', 'expression', 'color']
          }
        },
        maxHistory: { type: 'number', description: 'Max data points for type=graph. Default 200' },
        snapshotInterval: { type: 'number', description: 'Fetch interval in seconds for snapshot series. Default 300' },
        actionExpression: {
          type: 'string',
          description: 'Action expression for type=button. Syntax: ![order:BUY|SELL:SYMBOL:qty:orderType:limitPrice?:stopPrice?]. Example: "![order:BUY:AAPL:100:MKT]"'
        },
        confirmBeforeExecute: {
          type: 'boolean',
          description: 'For type=button. Show a confirmation dialog before firing the action. Default true.'
        },
        width: { type: 'number', description: 'Width in grid segments (1-20). Default varies by type' },
        height: { type: 'number', description: 'Height in pixels. Omit when using dock=fill-height.' },
        dock: { type: 'string', enum: ['fill', 'fill-height'], description: 'fill = full width+height, fill-height = custom width + full viewport height (omit height when using this)' },
        heightRows: { type: 'number', description: 'Used with dock=fill-height. Divides viewport height by this number. E.g. heightRows=2 for a 2-row layout where each row is half the viewport.' },
        style: {
          type: 'object',
          description: 'Visual style overrides',
          properties: {
            bgColor: { type: 'string' },
            borderColor: { type: 'string' },
            borderWidth: { type: 'number' },
            textColor: { type: 'string' },
            fontWeight: { type: 'string', enum: ['normal', 'bold', 'lighter'] },
            fontStyle: { type: 'string', enum: ['normal', 'italic'] },
            fontSize: { type: 'number', description: 'Font size in rem' },
            textAlign: { type: 'string', enum: ['left', 'center', 'right'] },
            verticalAlign: { type: 'string', enum: ['top', 'center', 'bottom'] }
          }
        }
      },
      required: ['type']
    }
  },
  {
    name: 'update_widget',
    description: 'Update properties of an existing widget on the current template page. Use to resize, reposition, or change settings of a widget. widgetId comes from the existing widgets list in the page context.',
    parameters: {
      type: 'object',
      properties: {
        widgetId: { type: 'string', description: 'ID of the widget to update' },
        changes: {
          type: 'object',
          description: 'Fields to update. Any subset of the widget\'s properties.',
          properties: {
            width: { type: 'number', description: 'Width in grid segments (1-20)' },
            height: { type: 'number', description: 'Height in pixels' },
            symbol: { type: 'string' },
            interval: { type: 'string', enum: ['1m', '2m', '3m', '5m', '15m', '30m', '1h', '1d'] },
            text: { type: 'string' },
            placeholder: { type: 'string' },
            min: { type: 'number' },
            max: { type: 'number' },
            title: { type: 'string' },
            dock: { type: 'string', enum: ['fill', 'fill-height'], description: 'fill = full width+height, fill-height = custom width + viewport height' },
            actionExpression: { type: 'string', description: 'Action expression for type=button. Syntax: ![order:BUY|SELL:SYMBOL:qty:orderType:limitPrice?:stopPrice?]' },
            confirmBeforeExecute: { type: 'boolean', description: 'For type=button. Show confirmation dialog before firing. Default true.' }
          }
        }
      },
      required: ['widgetId', 'changes']
    }
  },
  {
    name: 'delete_widget',
    description: 'Remove a widget from the current template page. widgetId comes from the existing widgets list in the page context.',
    parameters: {
      type: 'object',
      properties: {
        widgetId: { type: 'string', description: 'ID of the widget to delete' }
      },
      required: ['widgetId']
    }
  }
]

export function isConnected() {
  return getConnectionStatus().connected === true
}

export function buildAlertConfig({ label, symbol, property = 'last', operator, value, refresh_seconds = 30, actions = ['sound', 'message'], message }) {
  const sym = symbol.toUpperCase()
  return {
    label: label || `${sym} ${operator} ${value}`,
    schedule: {
      type: 'expression',
      expression: `@{${sym}:${property}:${refresh_seconds}}`,
      operator,
      value: String(value)
    },
    actions,
    message: message || `${sym} ${property} is ${operator} ${value}`
  }
}

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
    case 'get_historical_bars': {
      const { symbol, bar_size = '1 day', duration = '20 D' } = input
      const bars = await getHistoricalData(symbol.toUpperCase(), bar_size, duration)
      if (!bars || bars.length === 0) return { error: `No historical data available for ${symbol}` }
      return bars.map(b => ({ t: b.timestamp, o: b.open, h: b.high, l: b.low, c: b.close, v: b.volume }))
    }
    default:
      throw new Error(`Unknown tool: ${name}`)
  }
}
