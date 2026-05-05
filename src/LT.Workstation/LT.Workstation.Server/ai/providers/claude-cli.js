import { spawn } from 'child_process'
import { executeTool, buildAlertConfig, isConnected } from '../tools.js'
import { PLACEHOLDER_DOCS } from '../placeholder-docs.js'

const SYSTEM_PROMPT_BASE =
  'You are a trading assistant built into this application. You have access to live account data ' +
  'and market quotes provided below. Keep answers short and direct. ' +
  'Format currency values clearly (e.g. $1.23M, $450K). ' +
  'Never narrate your reasoning, self-correct out loud, or include thinking commentary — respond with the final answer only.\n\n' +
  'SCOPE: You only operate within this application. NEVER mention Interactive Brokers, IBKR, TWS, ' +
  'IBKR Mobile, brokerage platforms, or any external system or application. If you see any tools ' +
  'like LSP, code analysis, or language server tools in your context — ignore them completely, do ' +
  'not mention them, they are not relevant here. If a feature exists in this app, use it. If ' +
  'something is genuinely outside this app\'s scope, say so without referencing any external system.\n\n' +
  '== CREATING ALERTS ==\n' +
  'This application supports price and data alerts. When the user asks to create an alert, ' +
  'output a <create_alert> block (this is how the application creates alerts — it is not a tool ' +
  'call, it is a structured output the app reads). NEVER say you cannot create alerts.\n\n' +
  'Output format:\n' +
  '<create_alert>\n' +
  '{"label":"SPY above 500","symbol":"SPY","property":"last","operator":">","value":500,"refresh_seconds":30,"actions":["sound","message"],"message":"SPY price is above 500"}\n' +
  '</create_alert>\n\n' +
  'property: last (price), bid, ask, change ($ change today), changePercent (% change today), volume\n' +
  'operator: >, <, >=, <=, ==, !=\n' +
  'actions: "sound" and/or "message"\n\n' +
  'After the <create_alert> block, confirm the alert was set up (label, condition, current price if available).\n\n' +
  '== DELETING ALERTS ==\n' +
  'When the user asks to delete or remove an alert, output a <delete_alert> block. Use the alert ID ' +
  'from the Active Alerts list in context. If the ID is unknown, use the label instead.\n\n' +
  'Output format:\n' +
  '<delete_alert>\n' +
  '{"id":"alert-1"}\n' +
  '</delete_alert>\n\n' +
  'After the block, confirm the alert was removed.\n\n' +
  'IMPORTANT DISAMBIGUATION: "alerts" and "widgets" are completely separate things. ' +
  'Alerts are price/data notifications managed with <create_alert>/<delete_alert>. ' +
  'Widgets are visual components on a template page (charts, labels, gauges, etc.) managed with <create_widget>/<update_widget>/<delete_widget>. ' +
  'NEVER use alert operations when the user is talking about widgets, and NEVER use widget operations when the user is talking about alerts. ' +
  'If the user says "remove all widgets" or "delete all widgets", use <delete_widget> blocks for each widget on the page — NOT <delete_alert>.'

// Common English words that look like tickers — skip them
const WORD_SKIP = new Set([
  'A', 'I', 'AM', 'AN', 'AS', 'AT', 'BE', 'BY', 'DO', 'GO', 'IF', 'IN', 'IS', 'IT',
  'ME', 'MY', 'NO', 'OF', 'OK', 'ON', 'OR', 'SO', 'TO', 'UP', 'US', 'WE',
  'AND', 'ARE', 'CAN', 'DID', 'FOR', 'GET', 'GOT', 'HAD', 'HAS', 'HIM', 'HIS',
  'HOW', 'ITS', 'LET', 'MAY', 'NOT', 'NOW', 'OLD', 'OUR', 'OUT', 'OWN', 'PUT',
  'SAY', 'SHE', 'THE', 'TOO', 'TWO', 'USE', 'WAS', 'WAY', 'WHO', 'WHY', 'YET',
  'ALSO', 'BACK', 'BEEN', 'CALL', 'COME', 'EVEN', 'FROM', 'GIVE', 'GOOD', 'HAVE',
  'HERE', 'HIGH', 'INTO', 'JUST', 'KEEP', 'LAST', 'LIKE', 'LONG', 'LOOK', 'MADE',
  'MAKE', 'MORE', 'MOST', 'MOVE', 'MUCH', 'NEED', 'NEXT', 'ONLY', 'OPEN', 'OVER',
  'PAST', 'SAME', 'SHOW', 'SIDE', 'SOME', 'SUCH', 'SURE', 'TAKE', 'THAN', 'THAT',
  'THEM', 'THEN', 'THEY', 'THIS', 'THUS', 'TIME', 'TOLD', 'VERY', 'WANT', 'WELL',
  'WENT', 'WERE', 'WHAT', 'WHEN', 'WITH', 'WILL', 'WOULD', 'YOUR', 'COULD',
  'DOES', 'DOWN', 'EACH', 'FIND', 'FIRST', 'GOING', 'LARGE', 'PRICE', 'SHARE',
  'STOCK', 'TRADE', 'WHERE', 'WHICH', 'WHILE', 'SHOULD', 'ABOUT', 'AFTER'
])

function extractTickers(text) {
  const matches = text.toUpperCase().match(/\b[A-Z]{1,5}\b/g) || []
  return [...new Set(matches.filter(t => t.length >= 2 && !WORD_SKIP.has(t)))]
}

function formatValue(v) {
  if (v === null || v === undefined) return 'N/A'
  if (typeof v !== 'number') return String(v)
  if (Math.abs(v) >= 1e9) return `$${(v / 1e9).toFixed(2)}B`
  if (Math.abs(v) >= 1e6) return `$${(v / 1e6).toFixed(2)}M`
  if (Math.abs(v) >= 1e3) return `$${(v / 1e3).toFixed(1)}K`
  return `$${v.toFixed(2)}`
}

function formatAlertCondition(schedule) {
  if (!schedule) return ''
  if (schedule.type === 'expression') {
    const m = schedule.expression?.match(/@\{(\w+):(\w+):(\d+)\}/)
    if (m) return `${m[1]} ${m[2]} ${schedule.operator} ${schedule.value}`
  }
  return schedule.expression || ''
}

async function buildContext(messages, alerts = [], pageContext = null) {
  const parts = []

  // Connection status
  if (!isConnected()) {
    parts.push('Market Data Connection: DISCONNECTED — live quotes and historical data are unavailable. Tell the user the app is not connected to the market data feed and they should reconnect.')
    return parts.join('\n\n')
  }

  // Account summary
  try {
    const summary = await executeTool('get_account_summary', {})
    const lines = Object.entries(summary).map(([k, { value }]) => `  ${k}: ${formatValue(value)}`)
    parts.push(`Account Summary:\n${lines.join('\n')}`)
  } catch {
    parts.push('Account Summary: unavailable')
  }

  // Active alerts
  if (alerts.length > 0) {
    const alertLines = alerts.map(a => {
      const cond = formatAlertCondition(a.schedule)
      const status = a.enabled === false ? 'disabled' : 'enabled'
      return `  ${a.id} | ${a.label}${cond ? ` | ${cond}` : ''} | ${status}`
    })
    parts.push(`Active Alerts (${alerts.length}):\n${alertLines.join('\n')}`)
  } else {
    parts.push('Active Alerts: none')
  }

  // Detect tickers in the latest user message
  const lastContent = messages.at(-1)?.content || ''
  const tickers = extractTickers(lastContent).slice(0, 5)

  if (tickers.length > 0) {
    const quoteLines = []
    const historyBlocks = []
    await Promise.all(tickers.map(async ticker => {
      const [quoteResult, barsResult] = await Promise.allSettled([
        executeTool('get_live_quote', { symbol: ticker }),
        executeTool('get_historical_bars', { symbol: ticker, bar_size: '1 day', duration: '20 D' })
      ])

      if (quoteResult.status === 'fulfilled' && !quoteResult.value.error) {
        const q = quoteResult.value
        const sign = (q.change ?? 0) >= 0 ? '+' : ''
        quoteLines.push(`  ${ticker}: $${q.last?.toFixed(2) ?? 'N/A'}, change ${sign}${q.change?.toFixed(2) ?? 'N/A'} (${sign}${q.changePercent?.toFixed(2) ?? 'N/A'}%)`)
      }

      if (barsResult.status === 'fulfilled' && !barsResult.value.error && Array.isArray(barsResult.value)) {
        const bars = barsResult.value
        const closes = bars.map(b => `${new Date(b.t).toISOString().slice(0, 10)} $${b.c.toFixed(2)}`).join(', ')
        historyBlocks.push(`  ${ticker} daily closes (oldest→newest): ${closes}`)
      }
    }))

    if (quoteLines.length > 0) {
      parts.push(`Live Quotes:\n${quoteLines.join('\n')}`)
    }
    if (historyBlocks.length > 0) {
      parts.push(`Recent Daily Price History:\n${historyBlocks.join('\n')}`)
    }
  }

  // Widget creation context
  if (pageContext?.type === 'template') {
    const widgetSummary = (pageContext.widgets || [])
      .map(w => `  [${w.id}] ${w.type} w:${w.width} h:${w.height}${w.dock ? ` dock:${w.dock}` : ''}${w.heightRows ? ` heightRows:${w.heightRows}` : ''}${w.text ? ` "${w.text}"` : w.symbol ? ` ${w.symbol}` : w.placeholder ? ` ${w.placeholder}` : ''}`)
      .join('\n')

    parts.push(
      `== WIDGETS ==\n` +
      `Current template page: "${pageContext.name}"\n` +
      `Existing widgets (id, type, width, height, content):\n${widgetSummary || '  (none)'}\n\n` +
      `IMPORTANT: Act immediately — never ask the user for confirmation or propose what you are about to do. Just do it.\n\n` +
      `These are structured output blocks the app reads — NOT tool calls. Strip blocks from your response text.\n\n` +
      `Add a widget:\n` +
      `<create_widget>\n` +
      `{"type":"label","text":"@{SPY:last:5}","width":4,"height":60,"style":{"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}}\n` +
      `</create_widget>\n\n` +
      `Update an existing widget (use the id from the list above):\n` +
      `<update_widget>\n` +
      `{"id":"<widgetId>","width":7,"height":400}\n` +
      `</update_widget>\n\n` +
      `Delete a widget:\n` +
      `<delete_widget>\n` +
      `{"id":"<widgetId>"}\n` +
      `</delete_widget>\n\n` +
      `Layout: width is in grid segments (1–20, where 20 = full page width). height is in pixels.\n` +
      `Typical widths: label=4, badge=2, gauge=6, chart/graph=10, panel=20. Typical heights: label=60, badge=40, gauge=160, chart/graph=220.\n` +
      `To make a row of widgets fill ALL remaining viewport height (after any fixed-height rows), set "dock":"fill-height" on those widgets and omit height — the layout engine automatically subtracts fixed rows.\n` +
      `For layouts with MULTIPLE fill-height rows (all equal), also set "heightRows":<N> where N is the number of fill-height rows (e.g. 2 equal fill rows → heightRows:2). For a single fill-height row, omit heightRows or set it to 1. To reset: set "heightRows":null.\n` +
      `When placing N widgets side by side on one row, their widths MUST sum to exactly 20. For 2 equal columns use 10/10. For 3 equal columns use 7/7/6. For 4 equal columns use 5/5/5/5.\n\n` +
      `Widget types and required fields:\n` +
      `  label   → text (string with placeholders), width (1–20), height (px), style\n` +
      `  badge   → placeholder, width (1–20), height (px), style\n` +
      `  gauge   → placeholder, min, max, segments ([{from,to,color}]), title, width (1–20), height (px)\n` +
      `  graph   → series ([{name,expression,color,viewType}]), maxHistory, snapshotInterval, width (1–20), height (px)\n` +
      `  chart   → symbol (ticker), interval (1m|2m|3m|5m|15m|30m|1h|1d — default "5m"), width (1–20), height (px)\n` +
      `  panel   → width (1–20), height (px), style, children:[]\n\n` +
      `Panel layout modes (set in style.layoutMode):\n` +
      `  "grid" — BEST for placing N widgets side by side. Set style.gridAxis:"columns" and style.gridCount:N. Children fill the grid cells automatically — their width/height are ignored.\n` +
      `           Example: panel with 2 charts side by side: {"type":"panel","width":20,"height":400,"style":{"layoutMode":"grid","gridAxis":"columns","gridCount":2},"children":[{"type":"chart","symbol":"AAPL"},{"type":"chart","symbol":"GOOGL"}]}\n` +
      `  "flow" — children fill left-to-right by width, wrapping to next row. Children need explicit width (1–20) and height (px).\n` +
      `  "absolute" (default) — children need explicit x (0–19) to sit side by side; without x they stack vertically.\n` +
      `IMPORTANT: dock:"fill-height" does NOT work inside panels. Always use a fixed pixel height for panel children.\n\n` +
      `Default style: {"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}\n\n` +
      `Conditional styles (label and badge): add "conditionalStyles" to style to change colors when a placeholder matches a condition.\n` +
      `First matching rule wins. Null fields are not overridden.\n` +
      `Example — green background when LSE is open, red when closed:\n` +
      `  "conditionalStyles": [\n` +
      `    {"id":"r1","placeholder":"\${LSE:status:10}","operator":"equals","value":"Open","bgColor":"#276749","textColor":null,"borderColor":null,"borderWidth":null},\n` +
      `    {"id":"r2","placeholder":"\${LSE:status:10}","operator":"equals","value":"Closed","bgColor":"#742A2A","textColor":null,"borderColor":null,"borderWidth":null}\n` +
      `  ]\n` +
      `Operators: equals, contains, >, <, >=, <=, !=\n` +
      `value is a string for equals/contains, a number for comparisons.\n\n` +
      PLACEHOLDER_DOCS
    )
  } else {
    parts.push(
      `== WIDGETS ==\n` +
      `The user is not currently on a template page. If they ask to add or modify a widget, ` +
      `tell them to navigate to a template page first. Do NOT output any widget blocks.`
    )
  }

  return parts.join('\n\n')
}

function formatHistory(messages) {
  // All messages except the last (current user message)
  const history = messages.slice(0, -1)
  if (history.length === 0) return null
  return history.map(m => `${m.role === 'user' ? 'User' : 'Assistant'}: ${m.content}`).join('\n')
}

export async function chat({ model, messages, alerts = [], pageContext = null }) {
  const [context, historyText] = await Promise.all([
    buildContext(messages, alerts, pageContext),
    Promise.resolve(formatHistory(messages))
  ])

  const systemPrompt = `${SYSTEM_PROMPT_BASE}\n\n${context}`

  const lastMessage = messages.at(-1)?.content || ''
  const prompt = historyText
    ? `Previous conversation:\n${historyText}\n\nCurrent question: ${lastMessage}`
    : lastMessage

  return new Promise((resolve, reject) => {
    const args = [
      '--print',
      '--output-format', 'json',
      '--no-session-persistence',
      '--tools', '',
      '--mcp-config', '{"mcpServers":{}}',
      '--strict-mcp-config',
      '--model', model,
      '--system-prompt', systemPrompt,
      prompt
    ]

    const env = Object.fromEntries(
      Object.entries(process.env).filter(([k]) => !k.startsWith('CLAUDE_CODE') && k !== 'CLAUDECODE')
    )

    const proc = spawn('claude', args, {
      env,
      timeout: 60000
    })

    let stdout = ''
    let stderr = ''

    proc.stdout.on('data', d => { stdout += d })
    proc.stderr.on('data', d => { stderr += d })

    proc.on('close', code => {
      if (code !== 0) {
        reject(new Error(`Claude CLI error (exit ${code}): ${stderr.trim() || 'unknown error'}`))
        return
      }
      try {
        const result = JSON.parse(stdout)
        if (result.is_error) {
          reject(new Error(result.result || 'Claude CLI returned an error'))
        } else {
          resolve(parseCliResponse(result.result ?? ''))
        }
      } catch {
        resolve(parseCliResponse(stdout.trim()))
      }
    })

    proc.on('error', err => reject(new Error(`Failed to start Claude CLI: ${err.message}`)))
  })
}

function parseCliResponse(raw) {
  const actions = []
  let text = raw.replace(/<create_widget>([\s\S]*?)<\/create_widget>/g, (_, json) => {
    try {
      const widgetDef = JSON.parse(json.trim())
      actions.push({ type: 'create_widget', widgetDef })
    } catch {}
    return ''
  })
  text = text.replace(/<create_alert>([\s\S]*?)<\/create_alert>/g, (_, json) => {
    try {
      const input = JSON.parse(json.trim())
      actions.push({ type: 'create_alert', config: buildAlertConfig(input) })
    } catch {}
    return ''
  })
  text = text.replace(/<delete_alert>([\s\S]*?)<\/delete_alert>/g, (_, json) => {
    try {
      const input = JSON.parse(json.trim())
      if (input.id || input.label) {
        actions.push({ type: 'delete_alert', id: input.id, label: input.label })
      }
    } catch {}
    return ''
  })
  text = text.replace(/<update_widget>([\s\S]*?)<\/update_widget>/g, (_, json) => {
    try {
      const input = JSON.parse(json.trim())
      const { id, ...changes } = input
      if (id) actions.push({ type: 'update_widget', widgetId: id, changes })
    } catch {}
    return ''
  })
  text = text.replace(/<delete_widget>([\s\S]*?)<\/delete_widget>/g, (_, json) => {
    try {
      const input = JSON.parse(json.trim())
      if (input.id) actions.push({ type: 'delete_widget', widgetId: input.id })
    } catch {}
    return ''
  }).trim()
  return { text, actions }
}
