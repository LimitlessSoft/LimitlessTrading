import Anthropic from '@anthropic-ai/sdk'
import { TOOL_DEFINITIONS, executeTool, buildAlertConfig } from '../tools.js'
import { PLACEHOLDER_DOCS } from '../placeholder-docs.js'

const SYSTEM_PROMPT_BASE =
  'You are a trading assistant built into this application. ' +
  'Use your tools to fetch real-time account data and market quotes — never guess values. ' +
  'Format currency values clearly (e.g. $1.23M, $450K). Keep answers short and direct. ' +
  'SCOPE: Only operate within this application. NEVER mention Interactive Brokers, IBKR, TWS, ' +
  'IBKR Mobile, brokerage platforms, or any external system. ' +
  'When creating alerts, call create_alert directly — you have this capability. ' +
  'When deleting alerts, call delete_alert with the alert ID from the Active Alerts list. ' +
  'IMPORTANT DISAMBIGUATION: "alerts" and "widgets" are completely separate things. ' +
  'Alerts are price/data notifications. Widgets are visual components on a template page (charts, labels, gauges, etc.). ' +
  'NEVER use alert tools when the user is talking about widgets, and vice versa.'

function formatAlertCondition(schedule) {
  if (!schedule) return ''
  if (schedule.type === 'expression') {
    const m = schedule.expression?.match(/@\{(\w+):(\w+):(\d+)\}/)
    if (m) return `${m[1]} ${m[2]} ${schedule.operator} ${schedule.value}`
  }
  return schedule.expression || ''
}

function buildSystemPrompt(alerts = [], pageContext = null) {
  const alertSection = alerts.length > 0
    ? `\n\nActive Alerts (${alerts.length}):\n` + alerts.map(a => {
        const cond = formatAlertCondition(a.schedule)
        const status = a.enabled === false ? 'disabled' : 'enabled'
        return `  ${a.id} | ${a.label}${cond ? ` | ${cond}` : ''} | ${status}`
      }).join('\n')
    : '\n\nActive Alerts: none'

  let pageSection = ''
  if (pageContext?.type === 'template') {
    const widgetSummary = (pageContext.widgets || [])
      .map(w => `  [${w.id}] ${w.type} w:${w.width} h:${w.height}${w.dock ? ` dock:${w.dock}` : ''}${w.heightRows ? ` heightRows:${w.heightRows}` : ''}${w.text ? ` "${w.text}"` : w.symbol ? ` ${w.symbol}` : w.placeholder ? ` ${w.placeholder}` : ''}`)
      .join('\n')
    pageSection = `\n\nCurrent template page: "${pageContext.name}"\nExisting widgets (id, type, width, height, content):\n${widgetSummary || '  (none)'}` +
      `\n\nIMPORTANT: Act immediately — never ask the user for confirmation or propose what you are about to do. Just do it.` +
      `\n\nLayout: width is in grid segments (1–20, where 20 = full page width). height is in pixels. ` +
      `Typical widths: label=4, badge=2, gauge=6, chart/graph=10, panel=20. Typical heights: label=60, badge=40, gauge=160, chart/graph=220. ` +
      `To make a row of widgets fill ALL remaining viewport height (after any fixed-height rows), set dock="fill-height" on those widgets and omit height — the layout engine automatically subtracts fixed rows. ` +
      `For layouts with MULTIPLE fill-height rows (all equal), also set heightRows=N where N is the number of fill-height rows. For a single fill-height row, omit heightRows. To reset: set heightRows=null. ` +
      `When placing N widgets side by side on one row, their widths MUST sum to exactly 20. For 2 equal columns use 10/10. For 3 equal columns use 7/7/6. For 4 equal columns use 5/5/5/5.\n\n` +
      `Conditional styles (label and badge): add "conditionalStyles" to the style object to change colors when a placeholder matches a condition. First matching rule wins. Null fields are not overridden. ` +
      `Example — green when LSE is open, red when closed: "conditionalStyles":[{"id":"r1","placeholder":"\${LSE:status:10}","operator":"equals","value":"Open","bgColor":"#276749","textColor":null,"borderColor":null,"borderWidth":null},{"id":"r2","placeholder":"\${LSE:status:10}","operator":"equals","value":"Closed","bgColor":"#742A2A","textColor":null,"borderColor":null,"borderWidth":null}]. ` +
      `Operators: equals, contains, >, <, >=, <=, !=\n\n` +
      `Panel layout modes (set in style.layoutMode): ` +
      `"grid" — BEST for N widgets side by side. Set gridAxis:"columns", gridCount:N. Children fill grid cells automatically; their width/height are ignored. Example: {"type":"panel","width":20,"height":400,"style":{"layoutMode":"grid","gridAxis":"columns","gridCount":2},"children":[{"type":"chart","symbol":"AAPL"},{"type":"chart","symbol":"GOOGL"}]}. ` +
      `"flow" — children fill left-to-right, wrapping by width. Children need explicit width and height. ` +
      `"absolute" (default) — children need explicit x (segment) to sit side by side; without x they stack. ` +
      `IMPORTANT: dock:fill-height does NOT work inside panels. Use fixed pixel heights for panel children.\n\n` +
      PLACEHOLDER_DOCS
  } else {
    pageSection = '\n\nCurrent page: not a template page — the create_widget tool is unavailable here. If the user asks to add a widget, tell them to navigate to a template page first.'
  }

  return SYSTEM_PROMPT_BASE + alertSection + pageSection
}

function toAnthropicTools(defs) {
  return defs.map(d => ({
    name: d.name,
    description: d.description,
    input_schema: d.parameters
  }))
}

export async function chat({ apiKey, model, messages, alerts = [], pageContext = null }) {
  const client = new Anthropic({ apiKey })
  const tools = toAnthropicTools(TOOL_DEFINITIONS)

  const anthropicMessages = messages.map(m => ({ role: m.role, content: m.content }))
  const pendingActions = []

  while (true) {
    const response = await client.messages.create({
      model,
      max_tokens: 1024,
      system: buildSystemPrompt(alerts, pageContext),
      tools,
      messages: anthropicMessages
    })

    if (response.stop_reason === 'tool_use') {
      anthropicMessages.push({ role: 'assistant', content: response.content })

      const toolResults = []
      for (const block of response.content) {
        if (block.type !== 'tool_use') continue

        let result
        if (block.name === 'create_alert') {
          const config = buildAlertConfig(block.input)
          pendingActions.push({ type: 'create_alert', config })
          result = { success: true, message: `Alert "${config.label}" will be created.` }
        } else if (block.name === 'delete_alert') {
          const { id, label } = block.input
          pendingActions.push({ type: 'delete_alert', id, label })
          result = { success: true, message: `Alert will be deleted.` }
        } else if (block.name === 'create_widget') {
          pendingActions.push({ type: 'create_widget', widgetDef: block.input })
          result = { success: true, message: `Widget of type "${block.input.type}" will be added to the page.` }
        } else if (block.name === 'update_widget') {
          pendingActions.push({ type: 'update_widget', widgetId: block.input.widgetId, changes: block.input.changes })
          result = { success: true, message: `Widget will be updated.` }
        } else if (block.name === 'delete_widget') {
          pendingActions.push({ type: 'delete_widget', widgetId: block.input.widgetId })
          result = { success: true, message: `Widget will be deleted.` }
        } else {
          try {
            result = await executeTool(block.name, block.input)
          } catch (err) {
            result = { error: err.message }
          }
        }

        toolResults.push({
          type: 'tool_result',
          tool_use_id: block.id,
          content: JSON.stringify(result)
        })
      }
      anthropicMessages.push({ role: 'user', content: toolResults })
    } else {
      const textBlock = response.content.find(b => b.type === 'text')
      return { text: textBlock?.text ?? '', actions: pendingActions }
    }
  }
}

export const MODELS = [
  { id: 'claude-opus-4-7', label: 'Claude Opus 4' },
  { id: 'claude-sonnet-4-6', label: 'Claude Sonnet 4' },
  { id: 'claude-haiku-4-5-20251001', label: 'Claude Haiku 4.5' }
]
