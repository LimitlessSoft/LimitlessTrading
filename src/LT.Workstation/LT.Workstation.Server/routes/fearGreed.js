// OBSOLETE — superseded by /api/stats-nd which provides raw historical stats
// (high-N, low-N, avg-N) for use in template formula placeholders.
// To be deleted.

import { Router } from 'express'
import { getHistoricalData, getCurrentPrices, getConnectionStatus } from '../ibkrClient.js'

const router = Router()

// Cache for historical data (refresh every 5 minutes)
let dataCache = {
  timestamp: 0,
  components: null
}
const CACHE_TTL = 5 * 60 * 1000 // 5 minutes

// Calculate a score from 0-100 based on where value falls in range
function normalizeToScore(value, fearValue, greedValue) {
  // fearValue = extreme fear (score 0), greedValue = extreme greed (score 100)
  if (fearValue > greedValue) {
    // Inverted scale (like VIX where high = fear)
    if (value >= fearValue) return 0
    if (value <= greedValue) return 100
    return Math.round(100 * (fearValue - value) / (fearValue - greedValue))
  } else {
    // Normal scale (low = fear, high = greed)
    if (value <= fearValue) return 0
    if (value >= greedValue) return 100
    return Math.round(100 * (value - fearValue) / (greedValue - fearValue))
  }
}

// Calculate 125-day simple moving average
function calculateSMA(prices, period = 125) {
  if (prices.length < period) return null
  const slice = prices.slice(-period)
  const sum = slice.reduce((acc, p) => acc + p.close, 0)
  return sum / period
}

// Calculate return over N days
function calculateReturn(prices, days = 20) {
  if (prices.length < days + 1) return null
  const current = prices[prices.length - 1].close
  const past = prices[prices.length - days - 1].close
  return ((current - past) / past) * 100
}

// Fetch historical data for a symbol (last 150 days for MA calculation)
async function fetchHistoricalPrices(symbol) {
  try {
    const data = await getHistoricalData(symbol, '1 day', '6 M', '')
    return data
  } catch (error) {
    console.error(`Failed to fetch ${symbol} historical:`, error.message)
    return null
  }
}

// Calculate all fear/greed components
async function calculateComponents() {
  const status = getConnectionStatus()
  if (!status.connected) {
    return { error: 'Not connected to IBKR' }
  }

  // Fetch current prices for VIX
  const currentPrices = await getCurrentPrices(['VIX'])
  const vixPrice = currentPrices?.VIX

  // Fetch historical data for calculations
  const [spyData, tltData, hygData, lqdData] = await Promise.all([
    fetchHistoricalPrices('SPY'),
    fetchHistoricalPrices('TLT'),
    fetchHistoricalPrices('HYG'),
    fetchHistoricalPrices('LQD')
  ])

  const components = []

  // 1. VIX Component (Market Volatility)
  // VIX < 12 = extreme greed (100), VIX > 35 = extreme fear (0)
  if (vixPrice !== undefined && vixPrice !== null) {
    const vixScore = normalizeToScore(vixPrice, 35, 12)
    components.push({
      name: 'Market Volatility',
      indicator: 'VIX',
      value: vixPrice.toFixed(2),
      score: vixScore,
      description: vixPrice < 15 ? 'Low volatility (greed)' :
                   vixPrice > 25 ? 'High volatility (fear)' : 'Normal volatility'
    })
  }

  // 2. Stock Price Momentum (SPY vs 125-day MA)
  // > 5% above MA = extreme greed, > 5% below = extreme fear
  if (spyData && spyData.length >= 125) {
    const sma125 = calculateSMA(spyData, 125)
    const currentSpy = spyData[spyData.length - 1].close
    const pctFromMA = ((currentSpy - sma125) / sma125) * 100
    const momentumScore = normalizeToScore(pctFromMA, -8, 8)
    components.push({
      name: 'Stock Momentum',
      indicator: 'SPY vs 125-day MA',
      value: `${pctFromMA >= 0 ? '+' : ''}${pctFromMA.toFixed(2)}%`,
      score: momentumScore,
      description: pctFromMA > 3 ? 'Above average (greed)' :
                   pctFromMA < -3 ? 'Below average (fear)' : 'Near average'
    })
  }

  // 3. Safe Haven Demand (SPY vs TLT 20-day return comparison)
  // If stocks outperform bonds significantly = greed, bonds outperform = fear
  if (spyData && tltData && spyData.length >= 21 && tltData.length >= 21) {
    const spyReturn = calculateReturn(spyData, 20)
    const tltReturn = calculateReturn(tltData, 20)
    if (spyReturn !== null && tltReturn !== null) {
      const returnDiff = spyReturn - tltReturn
      // If SPY outperforms TLT by 5%+ = extreme greed, TLT outperforms by 5%+ = extreme fear
      const safeHavenScore = normalizeToScore(returnDiff, -8, 8)
      components.push({
        name: 'Safe Haven Demand',
        indicator: 'Stocks vs Bonds (20d)',
        value: `${returnDiff >= 0 ? '+' : ''}${returnDiff.toFixed(2)}%`,
        score: safeHavenScore,
        description: returnDiff > 3 ? 'Stocks favored (greed)' :
                     returnDiff < -3 ? 'Bonds favored (fear)' : 'Balanced'
      })
    }
  }

  // 4. Junk Bond Demand (HYG vs LQD spread - 20-day return comparison)
  // High yield outperforming = greed (risk-on), investment grade outperforming = fear (risk-off)
  if (hygData && lqdData && hygData.length >= 21 && lqdData.length >= 21) {
    const hygReturn = calculateReturn(hygData, 20)
    const lqdReturn = calculateReturn(lqdData, 20)
    if (hygReturn !== null && lqdReturn !== null) {
      const spreadDiff = hygReturn - lqdReturn
      // HYG outperforms LQD by 2%+ = extreme greed, LQD outperforms by 2%+ = extreme fear
      const junkBondScore = normalizeToScore(spreadDiff, -4, 4)
      components.push({
        name: 'Junk Bond Demand',
        indicator: 'HYG vs LQD (20d)',
        value: `${spreadDiff >= 0 ? '+' : ''}${spreadDiff.toFixed(2)}%`,
        score: junkBondScore,
        description: spreadDiff > 1 ? 'Risk-on (greed)' :
                     spreadDiff < -1 ? 'Risk-off (fear)' : 'Neutral'
      })
    }
  }

  // Calculate composite score (average of all components)
  if (components.length === 0) {
    return { error: 'No data available' }
  }

  const compositeScore = Math.round(
    components.reduce((sum, c) => sum + c.score, 0) / components.length
  )

  // Determine sentiment label
  let sentiment, sentimentColor
  if (compositeScore <= 24) {
    sentiment = 'Extreme Fear'
    sentimentColor = 'red'
  } else if (compositeScore <= 44) {
    sentiment = 'Fear'
    sentimentColor = 'orange'
  } else if (compositeScore <= 55) {
    sentiment = 'Neutral'
    sentimentColor = 'gray'
  } else if (compositeScore <= 74) {
    sentiment = 'Greed'
    sentimentColor = 'green'
  } else {
    sentiment = 'Extreme Greed'
    sentimentColor = 'teal'
  }

  return {
    score: compositeScore,
    sentiment,
    sentimentColor,
    components,
    timestamp: new Date().toISOString()
  }
}

// Get fear/greed index
router.get('/', async (req, res) => {
  try {
    // Check cache
    const now = Date.now()
    if (dataCache.components && (now - dataCache.timestamp) < CACHE_TTL) {
      return res.json(dataCache.components)
    }

    const result = await calculateComponents()

    if (!result.error) {
      dataCache = {
        timestamp: now,
        components: result
      }
    }

    res.json(result)
  } catch (error) {
    console.error('Fear/Greed calculation error:', error)
    res.status(500).json({ error: error.message })
  }
})

// Force refresh (bypass cache)
router.get('/refresh', async (req, res) => {
  try {
    const result = await calculateComponents()

    if (!result.error) {
      dataCache = {
        timestamp: Date.now(),
        components: result
      }
    }

    res.json(result)
  } catch (error) {
    console.error('Fear/Greed refresh error:', error)
    res.status(500).json({ error: error.message })
  }
})

export default router
