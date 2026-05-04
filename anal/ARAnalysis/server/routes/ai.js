import express from 'express'
import { readFileSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
import { homedir } from 'os'
import { chat as anthropicChat } from '../ai/providers/anthropic.js'
import { chat as cliChat } from '../ai/providers/claude-cli.js'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const DATA_PATH = join(__dirname, '../data/user-data.json')
const CLAUDE_CREDENTIALS_PATH = join(homedir(), '.claude', '.credentials.json')

function readSettings() {
  try {
    const data = JSON.parse(readFileSync(DATA_PATH, 'utf-8'))
    return data.settings || {}
  } catch {
    return {}
  }
}

function readClaudeOAuth() {
  const creds = JSON.parse(readFileSync(CLAUDE_CREDENTIALS_PATH, 'utf-8'))
  const oauth = creds.claudeAiOauth
  if (!oauth) throw new Error('No Claude subscription credentials found. Authenticate via the Claude CLI.')
  if (oauth.expiresAt < Date.now()) throw new Error('Claude subscription token expired. Run the Claude CLI to re-authenticate.')
  return oauth
}

router.get('/subscription-status', (req, res) => {
  try {
    const oauth = readClaudeOAuth()
    res.json({ valid: true, subscriptionType: oauth.subscriptionType, rateLimitTier: oauth.rateLimitTier })
  } catch (err) {
    res.json({ valid: false, reason: err.message })
  }
})

router.post('/chat', async (req, res) => {
  const { message, history = [] } = req.body
  if (!message?.trim()) return res.status(400).json({ error: 'message required' })

  const settings = readSettings()
  const provider = settings['ai.provider'] || 'claude_subscription'
  const model = settings['ai.model']

  if (!model) return res.status(400).json({ error: 'No model selected. Open AI settings to choose one.' })

  const messages = [...history, { role: 'user', content: message }]

  try {
    let result

    if (provider === 'claude_subscription') {
      result = await cliChat({ model, messages })
    } else if (provider === 'anthropic') {
      const apiKey = settings['ai.anthropic.apiKey']
      if (!apiKey) return res.status(400).json({ error: 'No API key configured. Open AI settings to add one.' })
      result = await anthropicChat({ apiKey, model, messages })
    } else {
      return res.status(400).json({ error: `Provider "${provider}" is not yet supported.` })
    }

    const response = typeof result === 'string' ? result : result.text
    const actions = typeof result === 'string' ? [] : (result.actions || [])
    res.json({ response, actions })
  } catch (err) {
    res.status(500).json({ error: err.message })
  }
})

export default router
