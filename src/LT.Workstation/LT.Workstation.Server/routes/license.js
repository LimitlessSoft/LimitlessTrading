import express from 'express'
import { readFileSync, writeFileSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const DATA_PATH = join(__dirname, '../data/user-data.json')

const PUBLIC_API_URL = process.env.AR_PUBLIC_API_URL || 'http://localhost:5096'
const CACHE_TTL_MS = 24 * 60 * 60 * 1000 // 24 hours

// In-memory cache so a brief Polar outage never locks out a paying user
let cache = null // { valid, customerEmail, cachedAt }

function readData() {
  try {
    return JSON.parse(readFileSync(DATA_PATH, 'utf-8'))
  } catch {
    return {}
  }
}

function writeData(data) {
  writeFileSync(DATA_PATH, JSON.stringify(data, null, 2))
}

function getLicenseKey() {
  const data = readData()
  return data.settings?.['license.key'] ?? null
}

async function validateKeyWithPublicApi(key) {
  const response = await fetch(`${PUBLIC_API_URL}/license/validate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ key })
  })
  if (!response.ok) throw new Error(`Public API returned ${response.status}`)
  return response.json() // { valid, customerEmail }
}

// GET /api/license/status
router.get('/status', async (req, res) => {
  if (cache && Date.now() - cache.cachedAt < CACHE_TTL_MS) {
    return res.json({ valid: cache.valid, customerEmail: cache.customerEmail })
  }

  const key = getLicenseKey()
  if (!key) return res.json({ valid: false, customerEmail: null })

  try {
    const result = await validateKeyWithPublicApi(key)
    cache = { ...result, cachedAt: Date.now() }
    res.json({ valid: result.valid, customerEmail: result.customerEmail })
  } catch {
    if (cache) return res.json({ valid: cache.valid, customerEmail: cache.customerEmail })
    res.json({ valid: false, customerEmail: null })
  }
})

// POST /api/license/activate
router.post('/activate', async (req, res) => {
  const { key } = req.body
  if (!key || typeof key !== 'string' || !key.trim()) {
    return res.status(400).json({ error: 'key is required' })
  }

  let result
  try {
    result = await validateKeyWithPublicApi(key.trim())
  } catch {
    return res.status(502).json({ error: 'Could not reach license server. Check your connection and try again.' })
  }

  if (result.valid) {
    const data = readData()
    data.settings = data.settings ?? {}
    data.settings['license.key'] = key.trim()
    writeData(data)
    cache = { ...result, cachedAt: Date.now() }
  }

  res.json({ valid: result.valid, customerEmail: result.customerEmail })
})

// POST /api/license/deactivate
router.post('/deactivate', (req, res) => {
  const data = readData()
  data.settings = data.settings ?? {}
  delete data.settings['license.key']
  writeData(data)
  cache = null
  res.json({ valid: false, customerEmail: null })
})

export function getCachedLicenseStatus() {
  return cache ? { valid: cache.valid } : { valid: false }
}

export function getCachedCustomerEmail() {
  return cache?.customerEmail ?? null
}

export default router
