import express from 'express'
import { readFileSync, writeFileSync, mkdirSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
import { homedir } from 'os'
import { chat as anthropicChat } from '../ai/providers/anthropic.js'
import { chat as cliChat } from '../ai/providers/claude-cli.js'
import { getCachedLicenseStatus } from './license.js'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const DATA_PATH = join(__dirname, '../data/user-data.json')
const CLAUDE_CREDENTIALS_PATH = join(homedir(), '.claude', '.credentials.json')

const AI_DAILY_LIMIT = 10

function readData() {
  try {
    return JSON.parse(readFileSync(DATA_PATH, 'utf-8'))
  } catch {
    return {}
  }
}

function writeData(data) {
  mkdirSync(dirname(DATA_PATH), { recursive: true })
  writeFileSync(DATA_PATH, JSON.stringify(data, null, 2))
}

function readSettings() {
  return readData().settings || {}
}

function todayString() {
  return new Date().toISOString().slice(0, 10)
}

function getDailyUsage() {
  const data = readData()
  const usage = data.settings?.['ai.dailyUsage']
  if (usage?.date === todayString()) return { data, count: usage.count }
  return { data, count: 0 }
}

function incrementDailyUsage(data) {
  const today = todayString()
  const existing = data.settings?.['ai.dailyUsage']
  const count = existing?.date === today ? existing.count + 1 : 1
  data.settings = data.settings ?? {}
  data.settings['ai.dailyUsage'] = { date: today, count }
  writeData(data)
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
  const { message, history = [], alerts = [], pageContext = null } = req.body
  if (!message?.trim()) return res.status(400).json({ error: 'message required' })

  const { valid: isPro } = getCachedLicenseStatus()

  if (!isPro) {
    const { data, count } = getDailyUsage()
    if (count >= AI_DAILY_LIMIT) {
      return res.status(429).json({
        error: 'daily_limit_reached',
        message: `You've used all ${AI_DAILY_LIMIT} free messages for today. Upgrade to Pro for unlimited access.`,
        used: count,
        limit: AI_DAILY_LIMIT
      })
    }
    incrementDailyUsage(data)
  }

  const settings = readSettings()
  const provider = settings['ai.provider'] || 'claude_subscription'
  const model = settings['ai.model']

  if (!model) return res.status(400).json({ error: 'No model selected. Open AI settings to choose one.' })

  const messages = [...history, { role: 'user', content: message }]

  try {
    let result

    if (provider === 'claude_subscription') {
      result = await cliChat({ model, messages, alerts, pageContext })
    } else if (provider === 'anthropic') {
      const apiKey = settings['ai.anthropic.apiKey']
      if (!apiKey) return res.status(400).json({ error: 'No API key configured. Open AI settings to add one.' })
      result = await anthropicChat({ apiKey, model, messages, alerts, pageContext })
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
