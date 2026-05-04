import { spawn } from 'child_process'
import { executeTool, buildAlertConfig } from '../tools.js'

const SYSTEM_PROMPT_BASE =
  'You are a trading assistant built into this application. You have access to live account data ' +
  'and market quotes provided below. Keep answers short and direct. ' +
  'Format currency values clearly (e.g. $1.23M, $450K).\n\n' +
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
  'After the <create_alert> block, confirm the alert was set up (label, condition, current price if available).'

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
  const matches = text.match(/\b[A-Z]{1,5}\b/g) || []
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

async function buildContext(messages) {
  const parts = []

  // Account summary
  try {
    const summary = await executeTool('get_account_summary', {})
    const lines = Object.entries(summary).map(([k, { value }]) => `  ${k}: ${formatValue(value)}`)
    parts.push(`Account Summary:\n${lines.join('\n')}`)
  } catch {
    parts.push('Account Summary: unavailable (IBKR not connected)')
  }

  // Detect tickers in the latest user message
  const lastContent = messages.at(-1)?.content || ''
  const tickers = extractTickers(lastContent).slice(0, 5)

  if (tickers.length > 0) {
    const quoteLines = []
    for (const ticker of tickers) {
      try {
        const q = await executeTool('get_live_quote', { symbol: ticker })
        if (!q.error) {
          const sign = (q.change ?? 0) >= 0 ? '+' : ''
          quoteLines.push(`  ${ticker}: $${q.last?.toFixed(2) ?? 'N/A'}, change ${sign}${q.change?.toFixed(2) ?? 'N/A'} (${sign}${q.changePercent?.toFixed(2) ?? 'N/A'}%)`)
        }
      } catch {}
    }
    if (quoteLines.length > 0) {
      parts.push(`Live Quotes:\n${quoteLines.join('\n')}`)
    }
  }

  return parts.join('\n\n')
}

function formatHistory(messages) {
  // All messages except the last (current user message)
  const history = messages.slice(0, -1)
  if (history.length === 0) return null
  return history.map(m => `${m.role === 'user' ? 'User' : 'Assistant'}: ${m.content}`).join('\n')
}

export async function chat({ model, messages }) {
  const [context, historyText] = await Promise.all([
    buildContext(messages),
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
  const text = raw.replace(/<create_alert>([\s\S]*?)<\/create_alert>/g, (_, json) => {
    try {
      const input = JSON.parse(json.trim())
      actions.push({ type: 'create_alert', config: buildAlertConfig(input) })
    } catch {}
    return ''
  }).trim()
  return { text, actions }
}
