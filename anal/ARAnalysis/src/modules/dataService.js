import axios from 'axios'
import {
  addPriceData,
  getPriceData,
  addSecurity,
  getRecentSecurities,
  updateSecurityAccess,
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

// Fetch only the most recent N days from IBKR and merge into cache
// Much faster than a full reload — use this for the chart reload button
export async function refreshRecentData(symbol, interval, days = 30) {
  const config = INTERVAL_CONFIG[interval]
  if (!config) throw new Error(`Unknown interval: ${interval}`)

  const numChunks = Math.ceil(days / config.daysPerChunk)
  let endDate = new Date()

  for (let i = 0; i < numChunks; i++) {
    const chunkEndDate = i === 0 ? null : new Date(endDate)
    try {
      const result = await fetchChunk(symbol, interval, chunkEndDate)
      if (result.data && result.data.length > 0) {
        await addPriceData(symbol, interval, result.data)
      }
    } catch (err) {
      console.warn(`[refreshRecentData] chunk ${i} failed:`, err.message)
    }
    endDate.setDate(endDate.getDate() - config.daysPerChunk)
  }

  return getPriceData(symbol, interval)
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
// initialDays: how much to load upfront; older data is fetched lazily as the user pans back
const INTERVAL_CONFIG = {
  '1m':  { daysPerChunk: 1,    years: 2,  initialDays: 5   },
  '2m':  { daysPerChunk: 2,    years: 2,  initialDays: 10  },
  '3m':  { daysPerChunk: 7,    years: 5,  initialDays: 30  },
  '5m':  { daysPerChunk: 7,    years: 10, initialDays: 30  },
  '15m': { daysPerChunk: 14,   years: 10, initialDays: 60  },
  '30m': { daysPerChunk: 30,   years: 10, initialDays: 90  },
  '1h':  { daysPerChunk: 30,   years: 10, initialDays: 90  },
  '1d':  { daysPerChunk: 3650, years: 10, initialDays: 365 }
}

// Number of parallel requests — keep low to avoid IBKR pacing rejections
const PARALLEL_REQUESTS = 5

// Returns true if cache has recent data — skip initial fetch, rely on lazy load for history
function hasFreshCache(data) {
  if (!data || data.length === 0) return false
  const newestBar = data[0] // descending, newest first
  const threeDaysAgo = Date.now() - (3 * 24 * 60 * 60 * 1000)
  return newestBar.timestamp >= threeDaysAgo
}

// Returns true if the cache is missing data from the most recent trading session.
// Used to trigger a silent background refresh on chart mount.
export function cacheNeedsRefresh(data) {
  if (!data || data.length === 0) return false
  const newestBar = data[0] // descending, newest first
  return !isSameDate(newestBar.timestamp, getMostRecentTradingDay())
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
      // If cache already has recent data, use it — lazy load will extend history as needed
      const cachedData = await getPriceData(symbol, interval)
      if (hasFreshCache(cachedData)) {
        console.log(`Cache fresh for ${symbol} ${interval} (${cachedData.length} bars), skipping initial fetch`)
        onProgress(cachedData, 100)
        onComplete(cachedData)
        return
      }

      const totalDays = config.initialDays
      const numChunks = Math.ceil(totalDays / config.daysPerChunk)

      // Generate all end dates upfront
      const endDates = []
      let endDate = new Date()
      for (let i = 0; i < numChunks; i++) {
        endDates.push(i === 0 ? null : new Date(endDate))
        endDate.setDate(endDate.getDate() - config.daysPerChunk)
      }

      const loadStart = Date.now()
      console.log(`[load] ${symbol} ${interval} — ${numChunks} chunks planned (${PARALLEL_REQUESTS} parallel)`)

      // Process in batches, updating UI as each chunk completes
      let completedChunks = 0
      let noMoreData = false
      let totalBars = 0

      for (let batchStart = 0; batchStart < numChunks && !noMoreData; batchStart += PARALLEL_REQUESTS) {
        if (cancelled) break

        const batchEnd = Math.min(batchStart + PARALLEL_REQUESTS, numChunks)
        const batchEndDates = endDates.slice(batchStart, batchEnd)
        const batchT0 = Date.now()

        // Track batch results
        let batchHasData = false
        let batchCompleted = 0
        let batchBars = 0

        // Fetch batch in parallel, updating UI as each completes
        const promises = batchEndDates.map(date =>
          fetchChunk(symbol, interval, date)
            .then(result => {
              if (result.data && result.data.length > 0) {
                allBars = [...allBars, ...result.data]
                batchHasData = true
                batchBars += result.data.length
                totalBars += result.data.length
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

        const batchMs = Date.now() - batchT0
        console.log(`[load] batch ${batchStart}–${batchEnd - 1} | ${batchMs}ms | ${batchBars} bars | hasData=${batchHasData}`)

        // Only stop if entire batch returned no data (reached end of available history)
        if (!batchHasData) {
          console.log(`[load] no data in batch — stopping early at chunk ${batchStart}`)
          noMoreData = true
        }
      }

      if (!cancelled) {
        const totalMs = ((Date.now() - loadStart) / 1000).toFixed(1)
        console.log(`[load] done — ${totalMs}s total | ${totalBars} bars fetched | ${allBars.length} unique`)
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

// Fetch a batch of older chunks before a given timestamp (lazy history expansion)
// onProgress(data) called after each chunk; onComplete(data, hasData) when batch is done
export function fetchOlderData(symbol, interval, beforeTimestamp, onProgress, onComplete, onError) {
  const config = INTERVAL_CONFIG[interval]
  if (!config) {
    onError(new Error(`Unknown interval: ${interval}`))
    return { cancel: () => {} }
  }

  const OLDER_BATCH = 5
  let cancelled = false

  const run = async () => {
    try {
      // Pre-compute all end dates so we can fetch in parallel
      const endDates = []
      const endDate = new Date(beforeTimestamp)
      for (let i = 0; i < OLDER_BATCH; i++) {
        endDates.push(new Date(endDate))
        endDate.setDate(endDate.getDate() - config.daysPerChunk)
      }

      let hasData = false

      await Promise.all(endDates.map(async (date) => {
        if (cancelled) return
        try {
          const result = await fetchChunk(symbol, interval, date)
          if (result.data && result.data.length > 0) {
            await addPriceData(symbol, interval, result.data)
            hasData = true
            const merged = await getPriceData(symbol, interval)
            onProgress([...merged].sort((a, b) => a.timestamp - b.timestamp))
          }
        } catch (err) {
          console.warn(`[fetchOlderData] chunk failed:`, err.message)
        }
      }))

      if (!cancelled) {
        const finalData = await getPriceData(symbol, interval)
        onComplete([...finalData].sort((a, b) => a.timestamp - b.timestamp), hasData)
      }
    } catch (err) {
      if (!cancelled) onError(err)
    }
  }

  run()
  return { cancel: () => { cancelled = true } }
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
  return getRecentSecurities()
}

export { updateSecurityAccess }

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

// Get the most recent trading day (Mon-Fri, before or on today)
function getMostRecentTradingDay() {
  const now = new Date()
  const day = now.getDay() // 0=Sun, 1=Mon, ..., 6=Sat
  const hour = now.getHours()

  // Start with today
  let tradingDay = new Date(now.getFullYear(), now.getMonth(), now.getDate())

  // If it's before market open (9:30 ET), use previous trading day
  // Simplified: if before 10am local, consider previous day
  if (hour < 10) {
    tradingDay.setDate(tradingDay.getDate() - 1)
  }

  // Adjust for weekends
  const tradingDayOfWeek = tradingDay.getDay()
  if (tradingDayOfWeek === 0) { // Sunday
    tradingDay.setDate(tradingDay.getDate() - 2) // Go to Friday
  } else if (tradingDayOfWeek === 6) { // Saturday
    tradingDay.setDate(tradingDay.getDate() - 1) // Go to Friday
  }

  return tradingDay
}

// Check if bar date matches a target date (ignoring time)
function isSameDate(timestamp, targetDate) {
  const barDate = new Date(timestamp)
  return barDate.getFullYear() === targetDate.getFullYear() &&
         barDate.getMonth() === targetDate.getMonth() &&
         barDate.getDate() === targetDate.getDate()
}

// Check if bar is from recent trading days (within maxTradingDays)
function isFromRecentTradingDay(barTimestamp, maxTradingDays) {
  const mostRecent = getMostRecentTradingDay()

  // Check if bar is from most recent trading day or within allowed range
  for (let i = 0; i < maxTradingDays; i++) {
    const checkDate = new Date(mostRecent)
    checkDate.setDate(checkDate.getDate() - i)

    // Skip weekends when counting back
    while (checkDate.getDay() === 0 || checkDate.getDay() === 6) {
      checkDate.setDate(checkDate.getDate() - 1)
    }

    if (isSameDate(barTimestamp, checkDate)) {
      return true
    }
  }
  return false
}

// Fetch data for screener - uses cache if bar is from recent trading day
// Returns { data, fromCache } - data array and whether it came from cache
// maxTradingDays: how many trading days old the data can be (default: 1 = must be from most recent trading day)
export async function fetchForScreener(symbol, maxTradingDays = 1) {
  // Validate symbol
  if (!symbol || typeof symbol !== 'string' || symbol.trim() === '') {
    console.warn('fetchForScreener called with empty symbol')
    return { data: [], fromCache: false }
  }

  const cleanSymbol = symbol.trim().toUpperCase()
  const interval = '1d'

  // Check cache first
  const cached = await getPriceData(cleanSymbol, interval)

  // If we have at least 30 bars and newest bar is from recent trading day, use cache
  if (cached && cached.length >= 30) {
    const newestBar = cached[0]
    const barDate = new Date(newestBar.timestamp).toDateString()
    const mostRecentTradingDay = getMostRecentTradingDay().toDateString()

    if (isFromRecentTradingDay(newestBar.timestamp, maxTradingDays)) {
      console.log(`${cleanSymbol}: CACHE HIT - bar from ${barDate}, recent trading day is ${mostRecentTradingDay}`)
      return { data: cached, fromCache: true }
    } else {
      console.log(`${cleanSymbol}: CACHE MISS - bar from ${barDate}, need within ${maxTradingDays} trading days of ${mostRecentTradingDay}`)
    }
  } else {
    console.log(`${cleanSymbol}: CACHE MISS - only ${cached?.length || 0} bars (need 30)`)
  }

  // Otherwise fetch from IBKR (single chunk = ~1 year of daily data)
  try {
    const response = await axios.get(`/api/historical/${encodeURIComponent(cleanSymbol)}/chunk`, {
      params: { interval },
      timeout: 30000
    })

    if (response.data?.data && response.data.data.length > 0) {
      await addPriceData(cleanSymbol, interval, response.data.data)
      await addSecurity(cleanSymbol)
      // Return fresh data from cache (deduplicated)
      const freshData = await getPriceData(cleanSymbol, interval)
      return { data: freshData, fromCache: false }
    }
  } catch (err) {
    console.error(`Failed to fetch ${cleanSymbol}:`, err.message)
  }

  // Return whatever we have in cache
  return { data: cached || [], fromCache: !!cached }
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

// Get median 5-minute volume from market open to current time of day over past N days
// Returns { median5mVol, sampleDays, barsPerDay } or null if insufficient data
export async function getHistoricalMedian5mVolume(symbol, numDays = 10, interval = '5m') {
  const cached = await getPriceData(symbol, interval)
  if (!cached || cached.length === 0) return null

  const now = new Date()
  // Get current time in Eastern timezone
  const easternNow = new Date(now.toLocaleString('en-US', { timeZone: 'America/New_York' }))
  const currentHours = easternNow.getHours()
  const currentMinutes = easternNow.getMinutes()
  const currentTimeInMinutes = currentHours * 60 + currentMinutes

  // Market open is 9:30 AM ET (570 min), close is 4:00 PM ET (960 min)
  const marketOpenMinutes = 570
  const marketCloseMinutes = 960

  // If before market open, use full day; if after close, cap at close
  const effectiveEndMinutes = Math.min(
    Math.max(currentTimeInMinutes, marketOpenMinutes),
    marketCloseMinutes
  )

  // Collect volumes from past N trading days, for bars from market open to current time
  const volumesByDay = []
  const today = new Date(easternNow)
  today.setHours(0, 0, 0, 0)

  // Go back up to numDays * 2 calendar days to find numDays trading days
  for (let daysBack = 1; daysBack <= numDays * 2 && volumesByDay.length < numDays; daysBack++) {
    const targetDate = new Date(today)
    targetDate.setDate(targetDate.getDate() - daysBack)

    // Skip weekends
    const dayOfWeek = targetDate.getDay()
    if (dayOfWeek === 0 || dayOfWeek === 6) continue

    // Get bars for this day from cache
    const dayStart = targetDate.getTime()
    const dayEnd = dayStart + 24 * 60 * 60 * 1000

    const dayBars = cached.filter(bar => {
      if (bar.timestamp < dayStart || bar.timestamp >= dayEnd) return false

      // Convert bar timestamp to Eastern time to check if it's within our time window
      const barDate = new Date(bar.timestamp)
      const barEastern = new Date(barDate.toLocaleString('en-US', { timeZone: 'America/New_York' }))
      const barHours = barEastern.getHours()
      const barMinutes = barEastern.getMinutes()
      const barTimeInMinutes = barHours * 60 + barMinutes

      // Include bars from market open to current time of day
      return barTimeInMinutes >= marketOpenMinutes && barTimeInMinutes < effectiveEndMinutes
    })

    if (dayBars.length > 0) {
      const dayVolumes = dayBars.map(bar => bar.volume).filter(v => v != null && v > 0)
      if (dayVolumes.length > 0) {
        volumesByDay.push({
          date: targetDate,
          volumes: dayVolumes,
          avgVolume: dayVolumes.reduce((a, b) => a + b, 0) / dayVolumes.length
        })
      }
    }
  }

  if (volumesByDay.length === 0) return null

  // Collect all volumes across all days
  const allVolumes = volumesByDay.flatMap(d => d.volumes).sort((a, b) => a - b)

  // Calculate median
  const mid = Math.floor(allVolumes.length / 2)
  const median5mVol = allVolumes.length % 2 === 0
    ? (allVolumes[mid - 1] + allVolumes[mid]) / 2
    : allVolumes[mid]

  return {
    median5mVol,
    sampleDays: volumesByDay.length,
    totalBars: allVolumes.length,
    avgBarsPerDay: Math.round(allVolumes.length / volumesByDay.length)
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

// Fetch options chain for a symbol
export async function fetchStockHistory(symbol, barSize, duration) {
  if (!symbol) return []
  try {
    const params = new URLSearchParams({
      ...(barSize && { barSize }),
      ...(duration && { duration })
    })
    const response = await api.get(`/stock-history/${encodeURIComponent(symbol.trim().toUpperCase())}?${params}`)
    return response.data?.data || []
  } catch (err) {
    console.error(`Failed to fetch stock history for ${symbol}:`, err.message)
    return []
  }
}

export async function fetchOptionsParams(symbol) {
  if (!symbol || typeof symbol !== 'string' || symbol.trim() === '') {
    return null
  }
  try {
    const response = await api.get(`/options-params/${encodeURIComponent(symbol.trim().toUpperCase())}`)
    return response.data
  } catch (err) {
    console.error(`Failed to fetch options params for ${symbol}:`, err.message)
    throw err
  }
}

export async function fetchOptionsChain(symbol, currentPrice) {
  if (!symbol || typeof symbol !== 'string' || symbol.trim() === '') {
    return null
  }

  try {
    const params = currentPrice ? `?price=${currentPrice}` : ''
    const response = await api.get(`/options-chain/${encodeURIComponent(symbol.trim().toUpperCase())}${params}`)
    return response.data
  } catch (err) {
    console.error(`Failed to fetch options chain for ${symbol}:`, err.message)
    throw err
  }
}

// Fetch current prices for multiple symbols
export async function fetchCurrentPrices(symbols) {
  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return {}
  }

  try {
    const response = await api.post('/current-prices', { symbols })
    return response.data
  } catch (err) {
    console.error('Failed to fetch current prices:', err.message)
    return {}
  }
}

// Fetch detailed live quotes for multiple symbols
// Returns { symbol: { last, open, high, low, prevClose, vwap, bid, ask, volume } }
export async function fetchLiveQuotes(symbols) {
  if (!symbols || !Array.isArray(symbols) || symbols.length === 0) {
    return {}
  }

  try {
    const response = await api.post('/live-quotes', { symbols })
    return response.data
  } catch (err) {
    console.error('Failed to fetch live quotes:', err.message)
    return {}
  }
}

// Fetch historical data for an option contract
export async function fetchOptionHistory(symbol, expiration, strike, right, barSize, duration, exchange, multiplier) {
  try {
    const params = new URLSearchParams({
      expiration,
      strike: strike.toString(),
      right,
      ...(barSize && { barSize }),
      ...(duration && { duration }),
      ...(exchange && { exchange }),
      ...(multiplier && { multiplier })
    })
    const response = await api.get(`/option-history/${encodeURIComponent(symbol)}?${params}`)
    return response.data?.data || []
  } catch (err) {
    console.error(`Failed to fetch option history for ${symbol} ${expiration} ${strike} ${right}:`, err.message)
    throw err
  }
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
