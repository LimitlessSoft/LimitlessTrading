import { Router } from 'express'
import {
  connect,
  disconnect,
  getHistoricalData,
  getConnectionStatus,
  getOptionsChain,
  getOptionsFlow,
  getCurrentPrices,
  getOptionHistoricalData,
  getOptionParams,
  getDetailedQuotes,
  getDetailedQuote,
  subscribeMarketData,
  unsubscribeMarketData,
  subscribeToQuotes,
  getLatestQuotes,
  getSubscribedSymbols,
  unsubscribeAllMarketData,
  getRecentErrors,
  isDelayedMode,
  startBarsAccumulator,
  getBars,
  clearBars
} from '../ibkrClient.js'

const router = Router()

// Server-side cache for close-nd results: `${symbol}:${days}` → { close, date, fetchedAt }
const historicalCloseCache = new Map()
const CLOSE_ND_TTL = 60 * 60 * 1000 // 1 hour

// Server-side cache for stats-nd results: `${symbol}:${days}` → { high, low, avg, fetchedAt }
const historicalStatsCache = new Map()
const STATS_ND_TTL = 60 * 60 * 1000 // 1 hour

// Server-side cache for options-flow results: symbol → { data, fetchedAt }
const optionsFlowCache = new Map()
const OPTIONS_FLOW_TTL = 5 * 60 * 1000 // 5 minutes

// Interval to IBKR bar size/duration mapping
const INTERVAL_CONFIG = {
  '1m':  { barSize: '1 min',   duration: '1 D',   years: 2 },
  '2m':  { barSize: '2 mins',  duration: '2 D',   years: 2 },
  '3m':  { barSize: '3 mins',  duration: '1 W',   years: 5 },
  '5m':  { barSize: '5 mins',  duration: '1 W',   years: 10 },
  '15m': { barSize: '15 mins', duration: '2 W',   years: 10 },
  '30m': { barSize: '30 mins', duration: '1 M',   years: 10 },
  '1h':  { barSize: '1 hour',  duration: '1 M',   years: 10 },
  '1d':  { barSize: '1 day',   duration: '10 Y',  years: 10 }
}

// Parse duration string to days
function parseDurationToDays(duration) {
  const match = duration.match(/^(\d+)\s*([DWMY])$/)
  if (!match) return 1
  const num = parseInt(match[1])
  const unit = match[2]
  switch (unit) {
    case 'D': return num
    case 'W': return num * 7
    case 'M': return num * 30
    case 'Y': return num * 365
    default: return num
  }
}

// Connect to IBKR
router.post('/connect', async (req, res) => {
  const { port = 7496 } = req.body
  try {
    const status = await connect(port)
    res.json(status)
  } catch (error) {
    res.status(500).json({ error: error.message, connected: false })
  }
})

// Disconnect from IBKR
router.post('/disconnect', async (req, res) => {
  try {
    const status = await disconnect()
    res.json(status)
  } catch (error) {
    res.status(500).json({ error: error.message })
  }
})

// Get connection status
router.get('/status', (req, res) => {
  res.json(getConnectionStatus())
})

// Get interval configuration (for frontend to know how to chunk requests)
router.get('/intervals', (req, res) => {
  const intervals = Object.entries(INTERVAL_CONFIG).map(([key, config]) => ({
    value: key,
    barSize: config.barSize,
    duration: config.duration,
    daysPerChunk: parseDurationToDays(config.duration),
    years: config.years
  }))
  res.json(intervals)
})

// Fetch a single chunk of historical data
// Query params: interval, endDate (optional, ISO string or empty for "now")
router.get('/historical/:symbol/chunk', async (req, res) => {
  const { symbol } = req.params
  const { interval = '1d', endDate } = req.query

  // Validate symbol
  if (!symbol || symbol.trim() === '') {
    return res.status(400).json({ error: 'Symbol is required' })
  }

  const config = INTERVAL_CONFIG[interval]
  if (!config) {
    return res.status(400).json({ error: `Invalid interval: ${interval}` })
  }

  try {
    // Format endDate for IBKR: yyyymmdd-hh:mm:ss (dash between date and time means UTC)
    let endDateTime = ''
    if (endDate) {
      const d = new Date(endDate)
      const pad = (n) => String(n).padStart(2, '0')
      endDateTime = `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}-${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`
    }

    const data = await getHistoricalData(
      symbol.toUpperCase(),
      config.barSize,
      config.duration,
      endDateTime
    )

    res.json({
      data,
      daysPerChunk: parseDurationToDays(config.duration)
    })
  } catch (error) {
    res.status(500).json({ error: error.message })
  }
})

// Get current prices for multiple symbols
router.post('/current-prices', async (req, res) => {
  const { symbols } = req.body

  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return res.status(400).json({ error: 'Symbols array is required' })
  }

  try {
    const prices = await getCurrentPrices(symbols.map(s => s.toUpperCase()))
    res.json(prices)
  } catch (error) {
    console.error('Current prices error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Get detailed quotes for multiple symbols (prev close, open, last, vwap, etc.)
// This is the legacy polling endpoint - prefer /stream-quotes for real-time
router.post('/live-quotes', async (req, res) => {
  const { symbols } = req.body

  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return res.status(400).json({ error: 'Symbols array is required' })
  }

  try {
    const quotes = await getDetailedQuotes(symbols.map(s => s.toUpperCase()))
    res.json(quotes)
  } catch (error) {
    console.error('Live quotes error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Subscribe to real-time market data for symbols
router.post('/subscribe', async (req, res) => {
  const { symbols } = req.body

  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return res.status(400).json({ error: 'Symbols array is required' })
  }

  const results = {}
  for (const symbol of symbols) {
    const reqId = subscribeMarketData(symbol.toUpperCase())
    results[symbol.toUpperCase()] = reqId !== null
  }

  res.json({ subscribed: results, activeSymbols: getSubscribedSymbols() })
})

// Unsubscribe from market data for symbols
router.post('/unsubscribe', async (req, res) => {
  const { symbols } = req.body

  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return res.status(400).json({ error: 'Symbols array is required' })
  }

  const results = {}
  for (const symbol of symbols) {
    results[symbol.toUpperCase()] = unsubscribeMarketData(symbol.toUpperCase())
  }

  res.json({ unsubscribed: results, activeSymbols: getSubscribedSymbols() })
})

// Unsubscribe from all market data
router.post('/unsubscribe-all', async (req, res) => {
  unsubscribeAllMarketData()
  res.json({ success: true, activeSymbols: [] })
})

// Get currently subscribed symbols
router.get('/subscriptions', (req, res) => {
  res.json({ symbols: getSubscribedSymbols() })
})

// Debug: show recent IBKR errors and latest quote cache
router.get('/debug', (req, res) => {
  const quotes = getLatestQuotes(getSubscribedSymbols())
  res.json({
    subscribedSymbols: getSubscribedSymbols(),
    delayedMode: isDelayedMode(),
    latestQuotes: quotes,
    recentErrors: getRecentErrors()
  })
})

// SSE endpoint for streaming real-time quotes
router.get('/stream-quotes', (req, res) => {
  // Set up SSE headers
  res.setHeader('Content-Type', 'text/event-stream')
  res.setHeader('Cache-Control', 'no-cache')
  res.setHeader('Connection', 'keep-alive')
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.flushHeaders()

  console.log('SSE client connected for quote streaming')

  // Send initial data for all subscribed symbols
  const initialQuotes = getLatestQuotes(getSubscribedSymbols())
  if (Object.keys(initialQuotes).length > 0) {
    res.write(`data: ${JSON.stringify({ type: 'snapshot', quotes: initialQuotes, delayed: isDelayedMode() })}\n\n`)
  }

  // Subscribe to quote updates
  const unsubscribe = subscribeToQuotes((symbol, quote) => {
    try {
      res.write(`data: ${JSON.stringify({ type: 'update', symbol, quote })}\n\n`)
    } catch (e) {
      // Client disconnected
    }
  })

  // Send heartbeat every 30 seconds to keep connection alive
  const heartbeat = setInterval(() => {
    try {
      res.write(`: heartbeat\n\n`)
    } catch (e) {
      // Client disconnected
    }
  }, 30000)

  // Clean up on client disconnect
  req.on('close', () => {
    console.log('SSE client disconnected')
    unsubscribe()
    clearInterval(heartbeat)
  })
})

// Get the closing price from N trading days ago
router.get('/close-nd/:symbol', async (req, res) => {
  const { symbol } = req.params
  const days = parseInt(req.query.days)

  if (!symbol || !days || days < 1 || days > 252) {
    return res.status(400).json({ error: 'days must be between 1 and 252' })
  }

  const upperSymbol = symbol.toUpperCase()
  const cacheKey = `${upperSymbol}:${days}`
  const cached = historicalCloseCache.get(cacheKey)

  if (cached && Date.now() - cached.fetchedAt < CLOSE_ND_TTL) {
    return res.json({ symbol: upperSymbol, days, close: cached.close, date: cached.date })
  }

  try {
    const calendarDays = Math.ceil(days * 2) + 10
    const bars = await getHistoricalData(upperSymbol, '1 day', `${calendarDays} D`)

    if (bars.length <= days) {
      return res.status(404).json({ error: 'Not enough historical data' })
    }

    const targetBar = bars[bars.length - 1 - days]
    historicalCloseCache.set(cacheKey, { close: targetBar.close, date: targetBar.timestamp, fetchedAt: Date.now() })

    res.json({ symbol: upperSymbol, days, close: targetBar.close, date: targetBar.timestamp })
  } catch (error) {
    console.error(`[close-nd] error for ${upperSymbol}:`, error.message)
    res.status(500).json({ error: error.message })
  }
})

// Get high, low, avg close over last N trading days
router.get('/stats-nd/:symbol', async (req, res) => {
  const { symbol } = req.params
  const days = parseInt(req.query.days)

  if (!symbol || !days || days < 1 || days > 504) {
    return res.status(400).json({ error: 'days must be between 1 and 504' })
  }

  const upperSymbol = symbol.toUpperCase()
  const cacheKey = `${upperSymbol}:${days}`
  const cached = historicalStatsCache.get(cacheKey)

  if (cached && Date.now() - cached.fetchedAt < STATS_ND_TTL) {
    return res.json({ symbol: upperSymbol, days, high: cached.high, low: cached.low, avg: cached.avg })
  }

  try {
    const calendarDays = Math.ceil(days * 2) + 10
    const duration = calendarDays > 365
      ? `${Math.ceil(calendarDays / 365)} Y`
      : `${calendarDays} D`
    const bars = await getHistoricalData(upperSymbol, '1 day', duration)

    if (bars.length < days) {
      return res.status(404).json({ error: 'Not enough historical data' })
    }

    const closes = bars.slice(-days).map(b => b.close)
    const high = Math.max(...closes)
    const low = Math.min(...closes)
    const avg = closes.reduce((a, b) => a + b, 0) / closes.length

    historicalStatsCache.set(cacheKey, { high, low, avg, fetchedAt: Date.now() })
    res.json({ symbol: upperSymbol, days, high, low, avg })
  } catch (error) {
    console.error(`[stats-nd] error for ${upperSymbol}:`, error.message)
    res.status(500).json({ error: error.message })
  }
})

// Get VIX index price (uses detailed quote to capture both last and close ticks)
router.get('/vix', async (req, res) => {
  try {
    const quote = await getDetailedQuote('VIX', 2, false)
    if (!quote) {
      return res.status(503).json({ error: 'VIX data unavailable' })
    }
    const price = quote.last ?? quote.prevClose
    if (price === null || price === undefined) {
      return res.status(503).json({ error: 'VIX price unavailable' })
    }
    res.json({ price })
  } catch (error) {
    console.error('VIX error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Get options chain for a symbol
// Get historical price bars for a stock/index over a given period
router.get('/stock-history/:symbol', async (req, res) => {
  const { symbol } = req.params
  const { barSize, duration } = req.query
  if (!symbol || symbol.trim() === '') {
    return res.status(400).json({ error: 'Symbol is required' })
  }
  try {
    const data = await getHistoricalData(symbol.toUpperCase(), barSize || '1 hour', duration || '3 D')
    res.json({ data })
  } catch (error) {
    console.error('Stock history error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Get option expirations and strikes only (lightweight, no market data required)
router.get('/options-params/:symbol', async (req, res) => {
  const { symbol } = req.params
  if (!symbol || symbol.trim() === '') {
    return res.status(400).json({ error: 'Symbol is required' })
  }
  try {
    const params = await getOptionParams(symbol.toUpperCase())
    res.json(params)
  } catch (error) {
    console.error('Options params error:', error)
    res.status(500).json({ error: error.message })
  }
})

router.get('/options-chain/:symbol', async (req, res) => {
  const { symbol } = req.params
  const { price } = req.query

  if (!symbol || symbol.trim() === '') {
    return res.status(400).json({ error: 'Symbol is required' })
  }

  try {
    const currentPrice = price ? parseFloat(price) : null
    const chain = await getOptionsChain(symbol.toUpperCase(), currentPrice)
    res.json(chain)
  } catch (error) {
    console.error('Options chain error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Get historical data for an option contract
router.get('/option-history/:symbol', async (req, res) => {
  const { symbol } = req.params
  const { expiration, strike, right, barSize, duration, exchange, multiplier } = req.query

  if (!symbol || !expiration || !strike || !right) {
    return res.status(400).json({ error: 'Symbol, expiration, strike, and right (C/P) are required' })
  }

  try {
    const data = await getOptionHistoricalData(
      symbol.toUpperCase(),
      expiration,
      parseFloat(strike),
      right.toUpperCase(),
      barSize || '1 hour',
      duration || '3 D',
      exchange || 'SMART',
      multiplier || '100'
    )
    res.json({ data })
  } catch (error) {
    console.error('Option history error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Get aggregate call/put volume for a symbol's near-money options
// Query params: price (optional current spot price for ATM centering)
router.get('/options-flow/:symbol', async (req, res) => {
  const { symbol } = req.params
  const { price } = req.query

  if (!symbol || symbol.trim() === '') {
    return res.status(400).json({ error: 'Symbol is required' })
  }

  const upperSymbol = symbol.toUpperCase()
  const cached = optionsFlowCache.get(upperSymbol)

  if (cached && Date.now() - cached.fetchedAt < OPTIONS_FLOW_TTL) {
    return res.json(cached.data)
  }

  try {
    const flow = await getOptionsFlow(upperSymbol)
    optionsFlowCache.set(upperSymbol, { data: flow, fetchedAt: Date.now() })
    res.json(flow)
  } catch (error) {
    console.error(`[options-flow] error for ${upperSymbol}:`, error.message)
    res.status(500).json({ error: error.message })
  }
})

// Generic bars endpoint — starts accumulator on first call, returns [{timestamp, value}].
// Supports any property registered in PROPERTY_FETCHERS (callVol, putVol, pcRatio, last, etc.)
// Cumulative properties (callVol, putVol, volume) return per-interval deltas; others return raw.
router.get('/bars/:symbol/:property', (req, res) => {
  const { symbol, property } = req.params
  const interval = Math.max(10, parseInt(req.query.interval) || 300)

  const { connected } = getConnectionStatus()
  if (!connected) {
    return res.status(503).json({ error: 'Not connected to IBKR' })
  }

  startBarsAccumulator(symbol.toUpperCase(), property, interval * 1000)
  res.json(getBars(symbol.toUpperCase(), property, interval * 1000))
})

router.delete('/bars/:symbol/:property', (req, res) => {
  const { symbol, property } = req.params
  const interval = Math.max(10, parseInt(req.query.interval) || 300)
  clearBars(symbol.toUpperCase(), property, interval * 1000)
  res.json({ ok: true })
})

export default router
