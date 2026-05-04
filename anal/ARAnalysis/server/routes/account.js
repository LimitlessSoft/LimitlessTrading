import express from 'express'
import { getAccountSummary } from '../ibkrClient.js'

const router = express.Router()

// Server-side cache — IBKR throttles reqAccountSummary; 10s TTL is safe
const CACHE_TTL = 10_000
let cachedSummary = null
let cacheTimestamp = 0
let pendingRequest = null

async function fetchSummary() {
  if (pendingRequest) return pendingRequest

  pendingRequest = getAccountSummary()
    .then(summary => {
      cachedSummary = summary
      cacheTimestamp = Date.now()
      return summary
    })
    .finally(() => {
      pendingRequest = null
    })

  return pendingRequest
}

router.get('/summary', async (req, res) => {
  try {
    if (cachedSummary && Date.now() - cacheTimestamp < CACHE_TTL) {
      return res.json(cachedSummary)
    }
    const summary = await fetchSummary()
    res.json(summary)
  } catch (err) {
    res.status(503).json({ error: err.message })
  }
})

export default router
