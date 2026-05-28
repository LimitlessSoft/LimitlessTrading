import { Router } from 'express'
import { getHistoricalNews, getConnectionStatus } from '../ibkrClient.js'

const router = Router()

// Cache: `yahoo:SYMBOL` or `yahoo:_general` -> { items, fetchedAt }
const yahooCache = new Map()
const YAHOO_TTL = 10 * 60 * 1000

// Cache: `ibkr:SYMBOL` -> { items, fetchedAt }
const ibkrCache = new Map()
const IBKR_TTL = 5 * 60 * 1000

function parseRss(xml) {
  const items = []
  const itemRe = /<item>([\s\S]*?)<\/item>/g
  let match
  while ((match = itemRe.exec(xml)) !== null) {
    const block = match[1]
    const title = (/<title><!\[CDATA\[([\s\S]*?)\]\]><\/title>/.exec(block) ||
                   /<title>([\s\S]*?)<\/title>/.exec(block))?.[1]?.trim()
    const link  = (/<link>([\s\S]*?)<\/link>/.exec(block) ||
                   /<guid[^>]*>(https?:\/\/[^\s<]+)<\/guid>/.exec(block))?.[1]?.trim()
    const pubDate = /<pubDate>([\s\S]*?)<\/pubDate>/.exec(block)?.[1]?.trim()
    const source = (/<source[^>]*>([\s\S]*?)<\/source>/.exec(block))?.[1]?.trim() || 'Yahoo Finance'
    if (title && link) {
      items.push({
        title,
        url: link,
        source,
        publishedAt: pubDate ? new Date(pubDate).toISOString() : new Date().toISOString(),
      })
    }
  }
  return items
}

// GET /api/news/yahoo?symbol=AAPL&limit=15
router.get('/yahoo', async (req, res) => {
  const symbol = (req.query.symbol || '').toUpperCase().trim()
  const limit = Math.min(50, Math.max(1, parseInt(req.query.limit) || 15))
  const cacheKey = symbol ? `yahoo:${symbol}` : 'yahoo:_general'

  const cached = yahooCache.get(cacheKey)
  if (cached && Date.now() - cached.fetchedAt < YAHOO_TTL) {
    return res.json(cached.items.slice(0, limit))
  }

  try {
    const url = symbol
      ? `https://feeds.finance.yahoo.com/rss/2.0/headline?s=${encodeURIComponent(symbol)}&region=US&lang=en-US`
      : `https://feeds.finance.yahoo.com/rss/2.0/headline?region=US&lang=en-US`

    const response = await fetch(url, {
      headers: { 'User-Agent': 'Mozilla/5.0 (compatible; LimitlessTrading/1.0)' },
      signal: AbortSignal.timeout(8000),
    })

    if (!response.ok) {
      return res.status(502).json({ error: `Yahoo Finance returned ${response.status}` })
    }

    const xml = await response.text()
    const items = parseRss(xml)
    yahooCache.set(cacheKey, { items, fetchedAt: Date.now() })
    res.json(items.slice(0, limit))
  } catch (err) {
    console.error('[news/yahoo] fetch error:', err.message)
    res.status(502).json({ error: err.message })
  }
})

// GET /api/news/ibkr?symbol=AAPL&limit=15
router.get('/ibkr', async (req, res) => {
  const symbol = (req.query.symbol || '').toUpperCase().trim()
  const limit = Math.min(50, Math.max(1, parseInt(req.query.limit) || 15))

  if (!symbol) return res.status(400).json({ error: 'symbol is required for IBKR news' })

  const status = getConnectionStatus()
  if (!status.connected) return res.status(503).json({ error: 'Not connected to IBKR' })

  const cacheKey = `ibkr:${symbol}`
  const cached = ibkrCache.get(cacheKey)
  if (cached && Date.now() - cached.fetchedAt < IBKR_TTL) {
    return res.json(cached.items.slice(0, limit))
  }

  try {
    const raw = await getHistoricalNews(symbol, limit)
    const items = raw.map(a => ({
      title: a.headline,
      url: null,
      source: a.providerCode,
      publishedAt: a.time ? new Date(a.time).toISOString() : new Date().toISOString(),
      articleId: a.articleId,
      providerCode: a.providerCode,
    }))
    ibkrCache.set(cacheKey, { items, fetchedAt: Date.now() })
    res.json(items.slice(0, limit))
  } catch (err) {
    console.error('[news/ibkr] error:', err.message)
    if (err.message.includes('Not connected')) {
      return res.status(503).json({ error: err.message })
    }
    res.status(500).json({ error: err.message })
  }
})

export default router
