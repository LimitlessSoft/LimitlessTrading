import axios from 'axios'
import {
  addPriceData,
  getPriceData,
  addSecurity,
  getSecurities,
  getSetting,
  setSetting,
  clearPriceData,
  clearAllData,
  setLastFetch,
  getLastFetch
} from '../db/database'

const api = axios.create({
  baseURL: '/api',
  timeout: 60000
})

// Connection state
let connectionStatus = { connected: false, port: null }

export async function getConnectionStatus() {
  try {
    const response = await api.get('/status')
    connectionStatus = response.data
    return connectionStatus
  } catch (error) {
    connectionStatus = { connected: false, port: null, error: error.message }
    return connectionStatus
  }
}

export async function connectToIBKR(port) {
  try {
    const response = await api.post('/connect', { port })
    connectionStatus = response.data
    if (connectionStatus.connected) {
      await setSetting('lastPort', port)
    }
    return connectionStatus
  } catch (error) {
    throw new Error(error.response?.data?.error || error.message)
  }
}

export async function disconnectFromIBKR() {
  try {
    const response = await api.post('/disconnect')
    connectionStatus = response.data
    return connectionStatus
  } catch (error) {
    throw new Error(error.response?.data?.error || error.message)
  }
}

// Fetch data from cache only
export async function fetchFromCache(symbol, interval) {
  const cached = await getPriceData(symbol, interval)
  return cached || []
}

// Fetch a single chunk from IBKR
async function fetchChunk(symbol, interval, endDate = null) {
  const params = { interval }
  if (endDate) {
    params.endDate = endDate.toISOString()
  }

  const response = await api.get(`/historical/${symbol}/chunk`, { params })
  return response.data
}

// Interval configuration
const INTERVAL_CONFIG = {
  '1m':  { daysPerChunk: 1,   years: 2 },
  '2m':  { daysPerChunk: 2,   years: 2 },
  '3m':  { daysPerChunk: 7,   years: 5 },
  '5m':  { daysPerChunk: 7,   years: 10 },
  '15m': { daysPerChunk: 14,  years: 10 },
  '30m': { daysPerChunk: 30,  years: 10 },
  '1h':  { daysPerChunk: 30,  years: 10 },
  '1d':  { daysPerChunk: 3650, years: 10 }
}

// Number of parallel requests (IBKR allows 50/sec, use 20 to be safe)
const PARALLEL_REQUESTS = 20

// Check if cached data is complete (covers full time range)
// Data is sorted descending (newest first)
function isCacheComplete(data, interval) {
  if (!data || data.length === 0) return false

  const config = INTERVAL_CONFIG[interval]
  if (!config) return false

  const now = Date.now()
  const newestBar = data[0]
  const oldestBar = data[data.length - 1]

  // Check if oldest bar is old enough (within 5% tolerance of target years)
  const targetOldestTime = now - (config.years * 365 * 24 * 60 * 60 * 1000)
  const tolerance = config.years * 365 * 24 * 60 * 60 * 1000 * 0.05 // 5% tolerance
  const isOldEnough = oldestBar.timestamp <= targetOldestTime + tolerance

  // Check if newest bar is recent (within last 3 days for weekends/holidays)
  const threeDaysAgo = now - (3 * 24 * 60 * 60 * 1000)
  const isRecentEnough = newestBar.timestamp >= threeDaysAgo

  // Allow tolerance for missing bars (holidays, market closures won't be filled by IBKR)
  // If missing < 10% of total bars, consider it complete
  const { missingCount } = checkMissingBars(data, interval)
  const missingRatio = missingCount / data.length
  if (missingRatio > 0.1) {
    console.log(`Cache has ${missingCount} missing bars (${(missingRatio * 100).toFixed(1)}%) for ${interval}, will re-fetch`)
    return false
  }

  return isOldEnough && isRecentEnough
}

// Load historical data with progress updates using parallel requests
// onProgress(data, progress) called after each batch
// Returns { cancel: function }
export function loadHistoricalData(symbol, interval, onProgress, onComplete, onError) {
  const config = INTERVAL_CONFIG[interval]
  if (!config) {
    onError(new Error(`Unknown interval: ${interval}`))
    return { cancel: () => {} }
  }

  let cancelled = false
  let allBars = []

  const run = async () => {
    try {
      // Check if cached data is already complete
      const cachedData = await getPriceData(symbol, interval)
      if (isCacheComplete(cachedData, interval)) {
        console.log(`Cache complete for ${symbol} ${interval}, skipping IBKR fetch`)
        onProgress(cachedData, 100)
        onComplete(cachedData)
        return
      }

      const totalDays = config.years * 365
      const numChunks = Math.ceil(totalDays / config.daysPerChunk)

      // Generate all end dates upfront
      const endDates = []
      let endDate = new Date()
      for (let i = 0; i < numChunks; i++) {
        endDates.push(i === 0 ? null : new Date(endDate))
        endDate.setDate(endDate.getDate() - config.daysPerChunk)
      }

      // Process in batches, updating UI as each chunk completes
      let completedChunks = 0
      let noMoreData = false

      for (let batchStart = 0; batchStart < numChunks && !noMoreData; batchStart += PARALLEL_REQUESTS) {
        if (cancelled) break

        const batchEnd = Math.min(batchStart + PARALLEL_REQUESTS, numChunks)
        const batchEndDates = endDates.slice(batchStart, batchEnd)

        // Track batch results
        let batchHasData = false
        let batchCompleted = 0

        // Fetch batch in parallel, updating UI as each completes
        const promises = batchEndDates.map(date =>
          fetchChunk(symbol, interval, date)
            .then(result => {
              if (result.data && result.data.length > 0) {
                allBars = [...allBars, ...result.data]
                batchHasData = true
                addPriceData(symbol, interval, result.data)
              }
              batchCompleted++
              completedChunks = batchStart + batchCompleted
              const progress = Math.round((completedChunks / numChunks) * 100)

              // Deduplicate, sort, and update UI after each chunk
              const uniqueBars = deduplicateAndSort(allBars)
              allBars = uniqueBars
              onProgress(uniqueBars, progress)

              return result
            })
            .catch(err => {
              console.error('Chunk failed:', err.message)
              batchCompleted++
              return { data: [] }
            })
        )

        await Promise.all(promises)

        // Only stop if entire batch returned no data (reached end of available history)
        if (!batchHasData) {
          noMoreData = true
        }
      }

      if (!cancelled) {
        await addSecurity(symbol)
        await setLastFetch(symbol, interval)

        // Get final data from cache
        const finalData = await getPriceData(symbol, interval)
        onComplete(finalData)
      }
    } catch (err) {
      if (!cancelled) {
        onError(err)
      }
    }
  }

  run()

  return {
    cancel: () => {
      cancelled = true
    }
  }
}

function deduplicateAndSort(bars) {
  const seen = new Map()
  for (const bar of bars) {
    seen.set(bar.timestamp, bar)
  }
  // Sort descending (newest first)
  return Array.from(seen.values()).sort((a, b) => b.timestamp - a.timestamp)
}

export async function getCachedSecurities() {
  return getSecurities()
}

export async function getLastPort() {
  return getSetting('lastPort')
}

export async function clearCache(symbol = null, interval = null) {
  await clearPriceData(symbol, interval)
}

export async function clearAllCache() {
  await clearAllData()
}

export async function getLastFetchTime(symbol, interval) {
  return getLastFetch(symbol, interval)
}

// Fetch data for screener - uses cache if available and recent, otherwise fetches from IBKR
// Returns at least 30 bars for metrics calculation
export async function fetchForScreener(symbol) {
  const interval = '1d'

  // Check cache first
  const cached = await getPriceData(symbol, interval)

  // If we have at least 30 recent bars, use cache
  if (cached && cached.length >= 30) {
    const newestBar = cached[0]
    const threeDaysAgo = Date.now() - (3 * 24 * 60 * 60 * 1000)
    if (newestBar.timestamp >= threeDaysAgo) {
      return cached
    }
  }

  // Otherwise fetch from IBKR (single chunk = ~1 year of daily data)
  try {
    const response = await axios.get(`/api/historical/${symbol}/chunk`, {
      params: { interval },
      timeout: 30000
    })

    if (response.data?.data && response.data.data.length > 0) {
      await addPriceData(symbol, interval, response.data.data)
      await addSecurity(symbol)
      // Return fresh data from cache (deduplicated)
      return getPriceData(symbol, interval)
    }
  } catch (err) {
    console.error(`Failed to fetch ${symbol}:`, err.message)
  }

  // Return whatever we have in cache
  return cached || []
}

// Get the start of day in market time (US Eastern)
function getMarketDayStart(timestamp) {
  const date = new Date(timestamp)
  // Get the calendar date components
  const year = date.getFullYear()
  const month = date.getMonth()
  const day = date.getDate()
  // Return start of that calendar day (midnight)
  return new Date(year, month, day, 0, 0, 0, 0).getTime()
}

function getMarketDayEnd(timestamp) {
  const date = new Date(timestamp)
  const year = date.getFullYear()
  const month = date.getMonth()
  const day = date.getDate()
  return new Date(year, month, day, 23, 59, 59, 999).getTime()
}

// Get intraday data for a date range from cache
// startTimestamp and endTimestamp define the range (inclusive of full days)
export async function getIntradayForDay(symbol, startTimestamp, interval = '5m', endTimestamp = null) {
  const cached = await getPriceData(symbol, interval)
  if (!cached || cached.length === 0) return []

  // If no end timestamp, use start (single day)
  if (!endTimestamp) endTimestamp = startTimestamp

  // Ensure start <= end
  if (startTimestamp > endTimestamp) {
    [startTimestamp, endTimestamp] = [endTimestamp, startTimestamp]
  }

  const rangeStart = getMarketDayStart(startTimestamp)
  const rangeEnd = getMarketDayEnd(endTimestamp)

  // Filter to only bars within this range
  return cached.filter(bar =>
    bar.timestamp >= rangeStart && bar.timestamp <= rangeEnd
  ).sort((a, b) => a.timestamp - b.timestamp) // Sort ascending for chart
}

// Fetch intraday data for a date range from IBKR
// startTimestamp and endTimestamp define the range (inclusive of full days)
// onProgress callback is called after each chunk is fetched and saved
export async function fetchIntradayForDay(symbol, startTimestamp, interval = '5m', endTimestamp = null, onProgress = null) {
  // If no end timestamp, use start (single day)
  if (!endTimestamp) endTimestamp = startTimestamp

  // Ensure start <= end
  if (startTimestamp > endTimestamp) {
    [startTimestamp, endTimestamp] = [endTimestamp, startTimestamp]
  }

  const rangeStart = getMarketDayStart(startTimestamp)
  const rangeEnd = getMarketDayEnd(endTimestamp)

  // Calculate approximate days for chunking
  const totalDays = Math.ceil((rangeEnd - rangeStart) / (24 * 60 * 60 * 1000)) + 1
  const numChunks = Math.ceil(totalDays / 7)

  // Generate all end dates for parallel fetching
  const endDates = []
  let currentEnd = new Date(rangeEnd)
  for (let i = 0; i < numChunks; i++) {
    if (currentEnd.getTime() < rangeStart) break
    endDates.push(new Date(currentEnd))
    currentEnd.setDate(currentEnd.getDate() - 7)
  }

  const allData = []
  const PARALLEL_REQUESTS = 5 // Number of parallel requests

  try {
    // Process in batches of parallel requests
    for (let batchStart = 0; batchStart < endDates.length; batchStart += PARALLEL_REQUESTS) {
      const batch = endDates.slice(batchStart, batchStart + PARALLEL_REQUESTS)

      // Fetch batch in parallel
      const promises = batch.map(endDate =>
        api.get(`/historical/${symbol}/chunk`, {
          params: {
            interval,
            endDate: endDate.toISOString()
          }
        }).then(async response => {
          const data = response.data?.data || []
          if (data.length > 0) {
            allData.push(...data)
            await addPriceData(symbol, interval, data)
            // Call progress callback after each chunk is saved
            if (onProgress) {
              onProgress()
            }
          }
          return data
        }).catch(err => {
          console.error('Chunk fetch failed:', err.message)
          return []
        })
      )

      await Promise.all(promises)
    }

    // Filter and return only data within the range
    return allData.filter(bar =>
      bar.timestamp >= rangeStart && bar.timestamp <= rangeEnd
    ).sort((a, b) => a.timestamp - b.timestamp)
  } catch (error) {
    console.error('Failed to fetch intraday data:', error)
    throw error
  }
}

// Interval definitions for UI
export const INTERVALS = [
  { value: '1m', label: '1 Minute (2Y)' },
  { value: '2m', label: '2 Minutes (2Y)' },
  { value: '3m', label: '3 Minutes (5Y)' },
  { value: '5m', label: '5 Minutes (10Y)' },
  { value: '15m', label: '15 Minutes (10Y)' },
  { value: '30m', label: '30 Minutes (10Y)' },
  { value: '1h', label: '1 Hour (10Y)' },
  { value: '1d', label: '1 Day (10Y)' }
]

// Interval durations in milliseconds for missing bar detection
const INTERVAL_MS = {
  '1m': 60 * 1000,
  '2m': 2 * 60 * 1000,
  '3m': 3 * 60 * 1000,
  '5m': 5 * 60 * 1000,
  '15m': 15 * 60 * 1000,
  '30m': 30 * 60 * 1000,
  '1h': 60 * 60 * 1000,
  '1d': 24 * 60 * 60 * 1000
}

// Check for missing bars in the data
// Data is sorted descending (newest first)
export function checkMissingBars(data, interval) {
  if (!data || data.length < 2) {
    return { hasMissing: false, missingCount: 0, latestBarTime: data?.[0]?.timestamp || null }
  }

  const intervalMs = INTERVAL_MS[interval]
  if (!intervalMs) {
    return { hasMissing: false, missingCount: 0, latestBarTime: data[0].timestamp }
  }

  const isDaily = interval === '1d'
  let missingCount = 0
  const latestBarTime = data[0].timestamp

  for (let i = 1; i < data.length; i++) {
    // Data is descending, so data[i-1] is newer than data[i]
    const gap = data[i - 1].timestamp - data[i].timestamp

    if (isDaily) {
      const maxGap = 4 * intervalMs
      if (gap > maxGap) {
        missingCount += Math.max(0, Math.floor(gap / intervalMs) - 4)
      }
    } else {
      const maxWeekendGap = 66 * 60 * 60 * 1000
      if (gap > maxWeekendGap) {
        missingCount += Math.floor((gap - maxWeekendGap) / intervalMs)
      }
    }
  }

  return {
    hasMissing: missingCount > 0,
    missingCount,
    latestBarTime
  }
}
