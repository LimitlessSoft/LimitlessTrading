import Anthropic from '@anthropic-ai/sdk'
import { TOOL_DEFINITIONS, executeTool, buildAlertConfig } from '../tools.js'

const SYSTEM_PROMPT =
  'You are a trading assistant built into this application. ' +
  'Use your tools to fetch real-time account data and market quotes — never guess values. ' +
  'Format currency values clearly (e.g. $1.23M, $450K). Keep answers short and direct. ' +
  'SCOPE: Only operate within this application. NEVER mention Interactive Brokers, IBKR, TWS, ' +
  'IBKR Mobile, brokerage platforms, or any external system. ' +
  'When creating alerts, call create_alert directly — you have this capability.'

function toAnthropicTools(defs) {
  return defs.map(d => ({
    name: d.name,
    description: d.description,
    input_schema: d.parameters
  }))
}

export async function chat({ apiKey, model, messages }) {
  const client = new Anthropic({ apiKey })
  const tools = toAnthropicTools(TOOL_DEFINITIONS)

  const anthropicMessages = messages.map(m => ({ role: m.role, content: m.content }))
  const pendingActions = []

  while (true) {
    const response = await client.messages.create({
      model,
      max_tokens: 1024,
      system: SYSTEM_PROMPT,
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
