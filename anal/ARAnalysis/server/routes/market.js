import { Router } from 'express'
import { connect, disconnect, getHistoricalData, getConnectionStatus, getOptionsChain, getCurrentPrices, getOptionHistoricalData } from '../ibkrClient.js'

const router = Router()

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
  const { port = 7497 } = req.body
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

// Get options chain for a symbol
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
  const { expiration, strike, right } = req.query

  if (!symbol || !expiration || !strike || !right) {
    return res.status(400).json({ error: 'Symbol, expiration, strike, and right (C/P) are required' })
  }

  try {
    const data = await getOptionHistoricalData(
      symbol.toUpperCase(),
      expiration,
      parseFloat(strike),
      right.toUpperCase()
    )
    res.json({ data })
  } catch (error) {
    console.error('Option history error:', error)
    res.status(500).json({ error: error.message })
  }
})

export default router
