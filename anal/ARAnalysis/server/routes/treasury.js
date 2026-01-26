import { Router } from 'express'

const router = Router()

// Cache for Treasury yields (refresh every 15 minutes)
let yieldsCache = null
let lastFetch = 0
const CACHE_DURATION = 15 * 60 * 1000 // 15 minutes

// Fetch Treasury yields from US Treasury XML feed
async function fetchTreasuryYields() {
  const now = Date.now()

  // Return cached data if fresh
  if (yieldsCache && (now - lastFetch) < CACHE_DURATION) {
    return yieldsCache
  }

  try {
    // Treasury's daily yield curve XML feed for current year
    const year = new Date().getFullYear()
    const url = `https://home.treasury.gov/resource-center/data-chart-center/interest-rates/pages/xml?data=daily_treasury_yield_curve&field_tdr_date_value=${year}`

    const response = await fetch(url, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (compatible; ARAnalysis/1.0)',
        'Accept': 'application/xml, text/xml, */*'
      }
    })

    if (!response.ok) {
      throw new Error(`Treasury API error: ${response.status}`)
    }

    const xml = await response.text()
    const yields = parseYieldsFromXml(xml)

    if (yields) {
      yieldsCache = yields
      lastFetch = now
    }

    return yields
  } catch (error) {
    console.error('Treasury yields fetch error:', error.message)
    // Return stale cache if available
    if (yieldsCache) {
      return yieldsCache
    }
    throw error
  }
}

function parseYieldsFromXml(xml) {
  // Find all entries and get the most recent ones
  const entries = xml.match(/<entry>([\s\S]*?)<\/entry>/g)

  if (!entries || entries.length === 0) {
    console.error('No entries found in Treasury XML')
    return null
  }

  // Helper to extract values from an entry
  const getEntryValues = (entry) => {
    const getValue = (tag) => {
      const match = entry.match(new RegExp(`<d:${tag}[^>]*>([^<]*)<\/d:${tag}>`, 'i'))
      if (match && match[1]) {
        const val = parseFloat(match[1])
        return isNaN(val) ? null : val
      }
      return null
    }

    const dateMatch = entry.match(/<d:NEW_DATE[^>]*>([^<]*)<\/d:NEW_DATE>/i)
    const date = dateMatch ? dateMatch[1] : null

    return {
      date,
      '2y': getValue('BC_2YEAR'),
      '5y': getValue('BC_5YEAR'),
      '10y': getValue('BC_10YEAR'),
      '20y': getValue('BC_20YEAR'),
      '30y': getValue('BC_30YEAR'),
    }
  }

  // Get the last two entries (current and previous day)
  const currentEntry = entries[entries.length - 1]
  const previousEntry = entries.length >= 2 ? entries[entries.length - 2] : null

  const current = getEntryValues(currentEntry)
  const previous = previousEntry ? getEntryValues(previousEntry) : null

  // Calculate changes
  const maturities = ['2y', '5y', '10y', '20y', '30y']
  const yields = {
    date: current.date,
  }

  for (const m of maturities) {
    yields[m] = {
      value: current[m],
      change: (previous && current[m] !== null && previous[m] !== null)
        ? +(current[m] - previous[m]).toFixed(3)
        : null
    }
  }

  // Validate we got at least some data
  const hasData = maturities.some(m => yields[m].value !== null)

  if (!hasData) {
    console.error('No valid yield data extracted from XML')
    return null
  }

  console.log('Treasury yields fetched:', yields)
  return yields
}

// GET /api/treasury/yields
router.get('/yields', async (req, res) => {
  try {
    const yields = await fetchTreasuryYields()
    if (!yields) {
      return res.status(503).json({ error: 'Unable to fetch Treasury yields' })
    }
    res.json(yields)
  } catch (error) {
    console.error('Treasury yields error:', error)
    res.status(500).json({ error: error.message })
  }
})

export default router
