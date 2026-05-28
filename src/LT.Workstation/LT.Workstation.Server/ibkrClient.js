import pkg from '@stoqey/ib'
import { readFileSync, writeFileSync, mkdirSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
const { IBApi, EventName, SecType, OrderAction, OrderType, MarketOrder, LimitOrder, StopOrder, StopLimitOrder } = pkg

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)

// Index symbols that need special contract handling
const INDEX_SYMBOLS = {
  'VIX': { exchange: 'CBOE', whatToShow: 'TRADES' },
  'SPX': { exchange: 'CBOE', whatToShow: 'TRADES' },
  'NDX': { exchange: 'NASDAQ', whatToShow: 'TRADES' },
  'DJX': { exchange: 'CBOE', whatToShow: 'TRADES' }
}

let ib = null
let connectionState = {
  connected: false,
  port: null,
  clientId: null,
  error: null
}

const pendingRequests = new Map()
let nextRequestId = 1

// Subscription management for real-time streaming
const activeSubscriptions = new Map() // reqId -> { symbol, listeners: Set<callback> }
const symbolToReqId = new Map() // symbol -> reqId
const latestQuotes = new Map() // symbol -> quote data

// Event emitter for quote updates
const quoteListeners = new Set() // Set<(symbol, quote) => void>

// Recent IBKR errors for debugging
const recentErrors = []
const MAX_ERRORS = 50
export function getRecentErrors() { return [...recentErrors] }

let delayedModeActive = false
export function isDelayedMode() { return delayedModeActive }

export function subscribeToQuotes(callback) {
  quoteListeners.add(callback)
  return () => quoteListeners.delete(callback)
}

export function getLatestQuote(symbol) {
  return latestQuotes.get(symbol.toUpperCase()) || null
}

export function getLatestQuotes(symbols) {
  const result = {}
  for (const symbol of symbols) {
    const quote = latestQuotes.get(symbol.toUpperCase())
    if (quote) result[symbol] = quote
  }
  return result
}

function clearSubscriptionError(sub) {
  if (sub.subscriptionError) {
    delete sub.subscriptionError
    delete sub.retriedDelayed
    // If all subscriptions are now error-free, restore live mode
    const anyError = [...activeSubscriptions.values()].some(s => s.subscriptionError)
    if (!anyError && delayedModeActive) {
      delayedModeActive = false
      try { ib.reqMarketDataType(1) } catch (e) {}
    }
  }
}

function emitQuoteUpdate(symbol, quote) {
  latestQuotes.set(symbol.toUpperCase(), quote)
  for (const listener of quoteListeners) {
    try {
      listener(symbol, quote)
    } catch (e) {
      console.error('Quote listener error:', e)
    }
  }
}

export function getConnectionStatus() {
  return { ...connectionState }
}

export async function connect(port = 7496) {
  if (ib) {
    await disconnect()
  }

  delayedModeActive = false

  return new Promise((resolve, reject) => {
    const clientId = Math.floor(Math.random() * 10000)

    ib = new IBApi({
      clientId,
      host: '127.0.0.1',
      port
    })

    const timeout = setTimeout(() => {
      ib.disconnect()
      ib = null
      connectionState = { connected: false, port: null, error: 'Connection timeout' }
      reject(new Error('Connection timeout'))
    }, 10000)

    ib.on(EventName.connected, () => {
      clearTimeout(timeout)
      connectionState = { connected: true, port, clientId }
      console.log(`Connected to IBKR on port ${port}`)

      // Re-issue reqMktData for all active subscriptions after reconnect
      if (symbolToReqId.size > 0) {
        console.log(`Resubscribing ${symbolToReqId.size} symbols after reconnect`)
        for (const [symbol, reqId] of symbolToReqId) {
          const indexConfig = INDEX_SYMBOLS[symbol]
          const contract = indexConfig
            ? { symbol, secType: SecType.IND, exchange: indexConfig.exchange, currency: 'USD' }
            : { symbol, secType: SecType.STK, exchange: 'SMART', currency: 'USD' }
          try {
            ib.reqMktData(reqId, contract, '233,165', false, false, [])
          } catch (err) {
            console.error(`Failed to resubscribe ${symbol}:`, err)
          }
        }
      }

      resolve(connectionState)
    })

    ib.on(EventName.disconnected, () => {
      connectionState = { connected: false, port: null, clientId: null }
      console.log('Disconnected from IBKR')
    })

    ib.on(EventName.error, (err, code, reqId) => {
      const isPending = reqId && pendingRequests.has(reqId)
      const msg = `[reqId=${reqId ?? 'none'}${isPending ? ' PENDING' : ''}]: ${err?.message || err} code: ${code}`
      console.error(`IBKR error ${msg}`)
      recentErrors.push({ ts: Date.now(), msg, reqId, code })
      if (recentErrors.length > MAX_ERRORS) recentErrors.shift()

      // Market data subscription error — switch ALL subscriptions to 15-min delayed (free)
      if ((code === 10089 || code === 354) && !delayedModeActive) {
        delayedModeActive = true
        console.log(`Live market data unavailable (${code}), switching all subscriptions to 15-min delayed data`)
        setTimeout(() => {
          try {
            ib.reqMarketDataType(3)
          } catch (e) {
            console.error('reqMarketDataType(3) failed:', e.message)
          }
          setTimeout(() => {
            for (const [sym, rId] of symbolToReqId) {
              const sub = activeSubscriptions.get(rId)
              if (!sub) continue
              sub.retriedDelayed = true
              try {
                ib.reqMktData(rId, sub.contract, '', false, false, [])
              } catch (e) {
                console.error(`Failed to re-subscribe ${sym} in delayed mode:`, e.message)
              }
            }
          }, 500)
        }, 100)
      } else if ((code === 10089 || code === 354) && delayedModeActive) {
        // Only flag as error if this subscription has actually been retried in delayed mode
        const sub = activeSubscriptions.get(reqId)
        if (sub?.retriedDelayed && !sub.subscriptionError) {
          const errMsg = 'API market data blocked — fix in TWS: Edit → Global Configuration → API → Settings'
          sub.subscriptionError = errMsg
          emitQuoteUpdate(sub.symbol, { ...sub.quote, subscriptionError: errMsg })
          console.log(`Delayed data also failed for ${sub.symbol}`)
        }
      }

      // Handle pending request errors
      if (isPending) {
        const request = pendingRequests.get(reqId)
        if (request.reject) {
          request.reject(new Error(err?.message || `IBKR error code: ${code}`))
        }
        pendingRequests.delete(reqId)
      }

      // Connection errors
      if (code === 502 || code === 504) {
        clearTimeout(timeout)
        ib = null
        connectionState = { connected: false, port: null, error: err?.message }
        reject(new Error(err?.message || 'Connection failed'))
      }
    })

    try {
      ib.connect()
    } catch (err) {
      clearTimeout(timeout)
      reject(err)
    }
  })
}

export async function disconnect() {
  if (ib) {
    ib.disconnect()
    ib = null
  }
  connectionState = { connected: false, port: null, clientId: null }
  return connectionState
}

export async function getHistoricalData(symbol, barSize = '1 day', duration = '1 Y', endDateTime = '') {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++
    const bars = []

    // Check if symbol is an index
    const indexConfig = INDEX_SYMBOLS[symbol.toUpperCase()]
    const contract = indexConfig ? {
      symbol,
      secType: SecType.IND,
      exchange: indexConfig.exchange,
      currency: 'USD'
    } : {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    // Indices may need different data type
    const whatToShow = indexConfig?.whatToShow || 'TRADES'
    const useRTH = 0 // 0 = include pre-market and after-hours data
    const formatDate = 1 // 1 = yyyyMMdd HH:mm:ss format

    const t0 = Date.now()
    pendingRequests.set(reqId, { resolve, reject })

    const onHistoricalData = (rId, date, open, high, low, close, volume, count, wap, hasGaps) => {
      if (rId !== reqId) return

      // Check for end marker
      if (date.startsWith('finished')) {
        const elapsed = ((Date.now() - t0) / 1000).toFixed(2)
        const outsideRth = bars.filter(b => {
          const d = new Date(b.timestamp)
          const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/New_York', hour: 'numeric', minute: 'numeric', hour12: false }).formatToParts(d)
          const h = parseInt(parts.find(p => p.type === 'hour').value)
          const m = parseInt(parts.find(p => p.type === 'minute').value)
          const t = h * 60 + m
          return t < 570 || t >= 960
        }).length
        console.log(`[hist ${reqId}] ${elapsed}s | ${bars.length} bars | ${outsideRth} outside RTH | end=${endDateTime || 'now'} dur=${duration} bar=${barSize}`)
        ib?.off(EventName.historicalData, onHistoricalData)
        pendingRequests.delete(reqId)
        resolve(bars)
        return
      }

      bars.push({
        timestamp: parseIBKRDate(date),
        open,
        high,
        low,
        close,
        volume,
        count,
        wap
      })
    }

    ib.on(EventName.historicalData, onHistoricalData)

    // Set timeout (15 seconds per request)
    setTimeout(() => {
      if (pendingRequests.has(reqId)) {
        ib?.off(EventName.historicalData, onHistoricalData)
        pendingRequests.delete(reqId)
        if (bars.length > 0) {
          resolve(bars) // Return partial data if any
        } else {
          reject(new Error('Request timeout'))
        }
      }
    }, 15000)

    try {
      ib.reqHistoricalData(
        reqId,
        contract,
        endDateTime,
        duration,
        barSize,
        whatToShow,
        useRTH,
        formatDate,
        false // keepUpToDate
      )
    } catch (err) {
      ib?.off(EventName.historicalData, onHistoricalData)
      pendingRequests.delete(reqId)
      reject(err)
    }
  })
}

// Get historical data for an option contract
export async function getOptionHistoricalData(symbol, expiration, strike, right, barSize = '1 hour', duration = '3 D', exchange = 'SMART', multiplier = '100') {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  const whatToShowCandidates = ['TRADES', 'MIDPOINT']
  // Try SMART first — it routes to wherever data exists. Specific exchange as fallback.
  const exchangeCandidates = exchange && exchange !== 'SMART' ? ['SMART', exchange] : ['SMART']
  // If daily bars are requested, also try hourly as fallback (options rarely have daily data)
  const barSizeCandidates = barSize === '1 day'
    ? [{ bs: '1 day', dur: duration }, { bs: '1 hour', dur: duration }]
    : [{ bs: barSize, dur: duration }]

  for (const { bs, dur } of barSizeCandidates) {
    for (const exch of exchangeCandidates) {
      for (const whatToShow of whatToShowCandidates) {
        try {
          const bars = await _requestOptionHistory(symbol, expiration, strike, right, bs, dur, whatToShow, exch, multiplier)
          if (bars.length > 0) return bars
        } catch (err) {
          if (err.message.includes('Not connected')) throw err
          console.log(`  Option history ${exch}/${whatToShow}/${bs} failed: ${err.message.split('\n')[0]}`)
        }
      }
    }
  }

  return []
}

async function _requestOptionHistory(symbol, expiration, strike, right, barSize, duration, whatToShow, exchange, multiplier) {
  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++
    const bars = []

    const contract = {
      symbol,
      secType: 'OPT',
      exchange: exchange || 'SMART',
      currency: 'USD',
      lastTradeDateOrContractMonth: expiration,
      strike: parseFloat(strike),
      right,
      multiplier: multiplier || '100'
    }

    const useRTH = 0 // include extended hours for more data
    const formatDate = 1

    const cleanup = () => {
      ib?.off(EventName.historicalData, onHistoricalData)
      pendingRequests.delete(reqId)
    }

    const onHistoricalData = (rId, date, open, high, low, close, volume, count, wap, hasGaps) => {
      if (rId !== reqId) return

      if (date.startsWith('finished')) {
        console.log(`  [opt req ${reqId}] got ${bars.length} bars (${ts()})`)
        cleanup()
        resolve(bars)
        return
      }

      bars.push({
        timestamp: parseIBKRDate(date),
        open,
        high,
        low,
        close,
        volume
      })
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: (err) => { cleanup(); reject(err) }
    })

    ib.on(EventName.historicalData, onHistoricalData)

    const t0 = Date.now()
    const ts = () => `+${((Date.now() - t0) / 1000).toFixed(1)}s`
    console.log(`  [opt req ${reqId}] ${symbol} ${expiration} ${strike}${right} | ${exchange} ${whatToShow} ${barSize} ${duration} @ ${new Date().toISOString()}`)

    setTimeout(() => {
      if (pendingRequests.has(reqId)) {
        cleanup()
        if (bars.length > 0) {
          console.log(`  [opt req ${reqId}] timeout but got ${bars.length} bars — returning (${ts()})`)
          resolve(bars)
        } else {
          console.log(`  [opt req ${reqId}] timeout with no data (${ts()})`)
          reject(new Error('Request timeout'))
        }
      }
    }, 20000)

    try {
      ib.reqHistoricalData(reqId, contract, '', duration, barSize, whatToShow, useRTH, formatDate, false)
    } catch (err) {
      console.log(`  [opt req ${reqId}] threw: ${err.message} (${ts()})`)
      cleanup()
      reject(err)
    }
  })
}

// Parse IBKR duration string to days
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

// Fetch multiple years of intraday data by making sequential requests
export async function getHistoricalDataMultiYear(symbol, barSize, durationPerRequest, years = 10) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  const allBars = []
  let endDate = new Date()

  // Calculate how many requests needed to cover the desired years
  const daysPerRequest = parseDurationToDays(durationPerRequest)
  const totalDays = years * 365
  const numRequests = Math.ceil(totalDays / daysPerRequest)

  console.log(`Fetching ${years} years of ${barSize} data for ${symbol}...`)
  console.log(`  Duration per request: ${durationPerRequest} (${daysPerRequest} days)`)
  console.log(`  Total requests needed: ${numRequests}`)

  for (let i = 0; i < numRequests; i++) {
    const endDateTime = formatIBKRDateTime(endDate)

    if (i % 10 === 0 || i === numRequests - 1) {
      console.log(`  Request ${i + 1}/${numRequests}, ending ${endDateTime}...`)
    }

    try {
      const bars = await getHistoricalData(symbol, barSize, durationPerRequest, endDateTime)

      if (bars.length === 0) {
        console.log(`  No more data available after ${i + 1} requests`)
        break
      }

      // Add bars to beginning since we're going backwards
      allBars.unshift(...bars)

      // Move end date back by the duration amount
      endDate = new Date(endDate)
      endDate.setDate(endDate.getDate() - daysPerRequest)

      // Small delay to avoid IBKR rate limiting
      await sleep(500)

    } catch (err) {
      console.error(`  Error on request ${i + 1}:`, err.message)
      // If we have some data, return it; otherwise keep trying
      if (allBars.length > 0) {
        break
      }
    }
  }

  // Remove duplicates and sort by timestamp
  const uniqueBars = removeDuplicates(allBars)
  console.log(`Fetched ${uniqueBars.length} total bars for ${symbol}`)

  return uniqueBars
}

// Stream multiple years of data, calling onChunk for each batch
export async function streamHistoricalDataMultiYear(symbol, barSize, durationPerRequest, years, onChunk) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  let endDate = new Date()
  const daysPerRequest = parseDurationToDays(durationPerRequest)
  const totalDays = years * 365
  const numRequests = Math.ceil(totalDays / daysPerRequest)

  console.log(`Streaming ${years} years of ${barSize} data for ${symbol}...`)

  for (let i = 0; i < numRequests; i++) {
    const endDateTime = formatIBKRDateTime(endDate)
    const progress = Math.round(((i + 1) / numRequests) * 100)

    try {
      const bars = await getHistoricalData(symbol, barSize, durationPerRequest, endDateTime)

      if (bars.length === 0) {
        console.log(`  No more data available after ${i + 1} requests`)
        break
      }

      // Send chunk to client - returns false if client disconnected
      const shouldContinue = await onChunk(bars, progress)
      if (shouldContinue === false) {
        console.log(`  Client disconnected, stopping`)
        break
      }

      // Move end date back
      endDate = new Date(endDate)
      endDate.setDate(endDate.getDate() - daysPerRequest)

      await sleep(500)

    } catch (err) {
      console.error(`  Error on request ${i + 1}:`, err.message)
      break
    }
  }
}

// Fetch data from now until a specific timestamp (incremental update)
export async function getHistoricalDataSince(symbol, barSize, durationPerRequest, sinceTimestamp) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  const allBars = []
  let endDate = new Date()
  const sinceDate = new Date(sinceTimestamp)
  const daysPerRequest = parseDurationToDays(durationPerRequest)

  // Calculate how many days we need to fetch
  const daysSince = Math.ceil((endDate - sinceDate) / (1000 * 60 * 60 * 24))
  const numRequests = Math.ceil(daysSince / daysPerRequest) + 1 // +1 for safety overlap

  console.log(`Fetching data for ${symbol} since ${sinceDate.toISOString()}...`)
  console.log(`  Days to fetch: ${daysSince}, Requests needed: ${numRequests}`)

  for (let i = 0; i < numRequests; i++) {
    const endDateTime = formatIBKRDateTime(endDate)

    console.log(`  Request ${i + 1}/${numRequests}, ending ${endDateTime}...`)

    try {
      const bars = await getHistoricalData(symbol, barSize, durationPerRequest, endDateTime)

      if (bars.length === 0) {
        console.log(`  No more data available`)
        break
      }

      // Check if we've reached data older than sinceTimestamp
      const oldestBar = bars[0]
      if (oldestBar && oldestBar.timestamp < sinceTimestamp) {
        // Filter to only include bars newer than sinceTimestamp
        const newBars = bars.filter(bar => bar.timestamp > sinceTimestamp)
        allBars.unshift(...newBars)
        console.log(`  Reached existing data, stopping`)
        break
      }

      allBars.unshift(...bars)

      // Move end date back
      endDate = new Date(endDate)
      endDate.setDate(endDate.getDate() - daysPerRequest)

      // Stop if we've gone past the since date
      if (endDate < sinceDate) {
        break
      }

      await sleep(500)

    } catch (err) {
      console.error(`  Error on request ${i + 1}:`, err.message)
      if (allBars.length > 0) {
        break
      }
    }
  }

  const uniqueBars = removeDuplicates(allBars)
  console.log(`Fetched ${uniqueBars.length} new bars for ${symbol}`)

  return uniqueBars
}

function formatIBKRDateTime(date) {
  // Format: yyyymmdd-hh:mm:ss (dash between date and time means UTC)
  const year = date.getUTCFullYear()
  const month = String(date.getUTCMonth() + 1).padStart(2, '0')
  const day = String(date.getUTCDate()).padStart(2, '0')
  const hours = String(date.getUTCHours()).padStart(2, '0')
  const minutes = String(date.getUTCMinutes()).padStart(2, '0')
  const seconds = String(date.getUTCSeconds()).padStart(2, '0')
  return `${year}${month}${day}-${hours}:${minutes}:${seconds}`
}

function removeDuplicates(bars) {
  const seen = new Set()
  return bars.filter(bar => {
    if (seen.has(bar.timestamp)) {
      return false
    }
    seen.add(bar.timestamp)
    return true
  }).sort((a, b) => a.timestamp - b.timestamp)
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms))
}

function parseIBKRDate(dateStr) {
  // IBKR date format: "yyyyMMdd HH:mm:ss" or "yyyyMMdd"
  if (dateStr.length === 8) {
    // Daily bar: yyyyMMdd
    const year = dateStr.substring(0, 4)
    const month = dateStr.substring(4, 6)
    const day = dateStr.substring(6, 8)
    return new Date(`${year}-${month}-${day}T00:00:00`).getTime()
  } else {
    // Intraday: yyyyMMdd HH:mm:ss or unix timestamp
    if (dateStr.includes(' ')) {
      // IBKR intraday times are in US Eastern — convert to UTC using the Eastern offset
      // at noon on this date (using noon avoids DST transition edge cases)
      const [datePart, timePart] = dateStr.split(' ')
      const y = datePart.substring(0, 4)
      const mo = datePart.substring(4, 6)
      const d = datePart.substring(6, 8)
      const [hh, mm, ss] = timePart.split(':').map(Number)
      const noonUTC = new Date(`${y}-${mo}-${d}T12:00:00Z`)
      const noonParts = new Intl.DateTimeFormat('en-US', {
        timeZone: 'America/New_York', hour: 'numeric', minute: 'numeric', hour12: false
      }).formatToParts(noonUTC)
      const easternNoonH = parseInt(noonParts.find(p => p.type === 'hour').value)
      return Date.UTC(+y, +mo - 1, +d, hh + (12 - easternNoonH), mm, ss)
    } else {
      // Unix timestamp
      return parseInt(dateStr) * 1000
    }
  }
}

// Get current/last price for a symbol (single attempt)
async function getCurrentPriceOnce(symbol) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  return new Promise((resolve) => {
    const reqId = nextRequestId++

    // Check if symbol is an index
    const indexConfig = INDEX_SYMBOLS[symbol.toUpperCase()]
    const contract = indexConfig ? {
      symbol,
      secType: SecType.IND,
      exchange: indexConfig.exchange,
      currency: 'USD'
    } : {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    let lastPrice = null
    let resolved = false

    const onTickPrice = (rId, tickType, price) => {
      if (rId !== reqId) return
      // tickType 4 = last price, 9 = close price
      if (tickType === 4 && price > 0) {
        lastPrice = price
      } else if (tickType === 9 && price > 0 && lastPrice === null) {
        lastPrice = price // Use close if no last price yet
      }
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.tickPrice, onTickPrice) } catch (e) {}
      pendingRequests.delete(reqId)
      try { ib?.cancelMktData(reqId) } catch (e) {}
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: () => {
        cleanup()
        resolve(null) // Resolve with null on error instead of rejecting
      }
    })
    ib.on(EventName.tickPrice, onTickPrice)

    // Wait 1.5 seconds for price data then return
    setTimeout(() => {
      if (!resolved) {
        cleanup()
        resolve(lastPrice)
      }
    }, 1500)

    try {
      ib.reqMktData(reqId, contract, '', false, false, [])
    } catch (err) {
      cleanup()
      resolve(null)
    }
  })
}

// Get detailed quote data for a symbol (prev close, open, last, vwap, high, low)
// Tick types: 1=bid, 2=ask, 4=last, 5=bidSize, 6=high, 7=low, 8=volume, 9=close, 14=open
// Generic ticks: 81=vwap, 47=fundamentals (market cap via generic tick string "258")
async function getDetailedQuoteOnce(symbol) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  return new Promise((resolve) => {
    const reqId = nextRequestId++

    const indexConfig = INDEX_SYMBOLS[symbol.toUpperCase()]
    const contract = indexConfig ? {
      symbol,
      secType: SecType.IND,
      exchange: indexConfig.exchange,
      currency: 'USD'
    } : {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    const quote = {
      symbol,
      last: null,
      open: null,
      high: null,
      low: null,
      prevClose: null,
      vwap: null,
      bid: null,
      ask: null,
      bidSize: null,
      askSize: null,
      volume: null,
      avgVolume: null,
      marketCap: null,
      week52High: null,
      week52Low: null
    }
    let resolved = false
    let dataReceived = false

    const onTickPrice = (rId, tickType, price) => {
      if (rId !== reqId || price <= 0) return
      dataReceived = true
      switch (tickType) {
        case 1: quote.bid = price; break
        case 2: quote.ask = price; break
        case 4: quote.last = price; break
        case 6: quote.high = price; break
        case 7: quote.low = price; break
        case 9: quote.prevClose = price; break
        case 14: quote.open = price; break
        case 15: quote.week52Low = price; break
        case 16: quote.week52High = price; break
      }
    }

    const onTickSize = (rId, tickType, size) => {
      if (rId !== reqId) return
      dataReceived = true
      switch (tickType) {
        case 0: quote.bidSize = size; break
        case 3: quote.askSize = size; break
        case 5: quote.lastSize = size; break
        case 8: quote.volume = size; break
        case 21: quote.avgVolume = size; break
      }
    }

    const onTickGeneric = (rId, tickType, value) => {
      if (rId !== reqId) return
      dataReceived = true
      if (tickType === 81 && value > 0) quote.vwap = value
    }

    const onTickString = (rId, tickType, value) => {
      if (rId !== reqId) return
      // Log first time to debug
      if (tickType === 47) {
        console.log(`tickString(47) for ${symbol}: ${value?.substring(0, 300)}`)
      }
      // tickType 47 = fundamental ratios string
      // tickType 48 = RT volume string (sometimes contains data)
      if ((tickType === 47 || tickType === 48) && value) {
        dataReceived = true
        // Try multiple parsing strategies
        // Format 1: "FIELD1=VALUE1;FIELD2=VALUE2;..."
        // Format 2: Key:Value pairs
        const mktCapMatch = value.match(/MKTCAP[=:]\s*([\d.]+)/i)
        if (mktCapMatch) {
          const val = parseFloat(mktCapMatch[1])
          if (!isNaN(val) && val > 0) {
            quote.marketCap = val * 1000000 // MKTCAP is in millions
            console.log(`Found MKTCAP for ${symbol}: ${val}M -> ${quote.marketCap}`)
          }
        }
      }
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.tickPrice, onTickPrice) } catch (e) {}
      try { ib?.off(EventName.tickSize, onTickSize) } catch (e) {}
      try { ib?.off(EventName.tickGeneric, onTickGeneric) } catch (e) {}
      try { ib?.off(EventName.tickString, onTickString) } catch (e) {}
      pendingRequests.delete(reqId)
      try { ib?.cancelMktData(reqId) } catch (e) {}
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: () => {
        cleanup()
        resolve(null)
      }
    })

    ib.on(EventName.tickPrice, onTickPrice)
    ib.on(EventName.tickSize, onTickSize)
    ib.on(EventName.tickGeneric, onTickGeneric)
    ib.on(EventName.tickString, onTickString)

    // Wait 2 seconds for quote data
    setTimeout(() => {
      if (!resolved) {
        cleanup()
        if (dataReceived) {
          resolve(quote)
        } else {
          resolve(null)
        }
      }
    }, 2000)

    try {
      // Indices don't support generic ticks like VWAP/fundamentals — use empty string
      const genericTicks = indexConfig ? '' : '233,258,165'
      ib.reqMktData(reqId, contract, genericTicks, false, false, [])
    } catch (err) {
      cleanup()
      resolve(null)
    }
  })
}

// Get market cap via fundamental data request
async function getMarketCap(symbol) {
  if (!ib || !connectionState.connected) {
    return null
  }

  // Skip for indices
  const indexConfig = INDEX_SYMBOLS[symbol.toUpperCase()]
  if (indexConfig) return null

  return new Promise((resolve) => {
    const reqId = nextRequestId++
    let resolved = false

    const contract = {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    const onFundamentalData = (rId, data) => {
      if (rId !== reqId) return
      cleanup()
      // Parse XML data to find market cap
      // Format varies but typically includes <MKTCAP>value</MKTCAP> or similar
      try {
        const mktCapMatch = data.match(/<MKTCAP[^>]*>([^<]+)<\/MKTCAP>/i) ||
                           data.match(/MKTCAP[=:]?\s*([\d.]+)/i)
        if (mktCapMatch) {
          const val = parseFloat(mktCapMatch[1])
          if (!isNaN(val) && val > 0) {
            resolve(val * 1000000) // Usually in millions
            return
          }
        }
      } catch (e) {
        console.error('Error parsing fundamental data:', e)
      }
      resolve(null)
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.fundamentalData, onFundamentalData) } catch (e) {}
      pendingRequests.delete(reqId)
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: () => { cleanup(); resolve(null) }
    })

    ib.on(EventName.fundamentalData, onFundamentalData)

    setTimeout(() => {
      if (!resolved) {
        cleanup()
        resolve(null)
      }
    }, 3000)

    try {
      // Request ReportSnapshot which includes market cap
      ib.reqFundamentalData(reqId, contract, 'ReportSnapshot', [])
    } catch (err) {
      cleanup()
      resolve(null)
    }
  })
}

// Get detailed quote with retry
// requireLast: if false, return quote even if last is null (e.g. indices outside RTH that only send prevClose)
export async function getDetailedQuote(symbol, maxRetries = 2, requireLast = true) {
  let bestQuote = null
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    const quote = await getDetailedQuoteOnce(symbol)
    if (quote !== null) {
      if (!requireLast || quote.last !== null) {
        if (quote.marketCap === null) {
          const mktCap = await getMarketCap(symbol)
          if (mktCap) quote.marketCap = mktCap
        }
        return quote
      }
      // Keep the best partial quote (has prevClose at minimum) for fallback
      if (quote.prevClose !== null) bestQuote = quote
    }
    if (attempt < maxRetries) {
      await sleep(300)
    }
  }
  return bestQuote // non-null only when requireLast=true but we have prevClose
}

// Get detailed quotes for multiple symbols in parallel
export async function getDetailedQuotes(symbols) {
  const BATCH_SIZE = 10
  const results = {}

  for (let i = 0; i < symbols.length; i += BATCH_SIZE) {
    const batch = symbols.slice(i, i + BATCH_SIZE)
    const batchPromises = batch.map(async (symbol) => {
      const quote = await getDetailedQuote(symbol)
      return { symbol, quote }
    })

    const batchResults = await Promise.all(batchPromises)
    batchResults.forEach(({ symbol, quote }) => {
      results[symbol] = quote
    })

    if (i + BATCH_SIZE < symbols.length) {
      await sleep(200)
    }
  }

  return results
}

// Get current/last price for a symbol with retry
export async function getCurrentPrice(symbol, maxRetries = 2) {
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    const price = await getCurrentPriceOnce(symbol)
    if (price !== null) {
      return price
    }
    if (attempt < maxRetries) {
      await sleep(300) // Small delay before retry
    }
  }
  return null
}

// Get current prices for multiple symbols in parallel
export async function getCurrentPrices(symbols) {
  const BATCH_SIZE = 10
  const results = {}

  for (let i = 0; i < symbols.length; i += BATCH_SIZE) {
    const batch = symbols.slice(i, i + BATCH_SIZE)
    const batchPromises = batch.map(async (symbol) => {
      const price = await getCurrentPrice(symbol)
      return { symbol, price }
    })

    const batchResults = await Promise.all(batchPromises)
    batchResults.forEach(({ symbol, price }) => {
      results[symbol] = price
    })

    // Small delay between batches
    if (i + BATCH_SIZE < symbols.length) {
      await sleep(200)
    }
  }

  return results
}

// Fetch volume for a single option contract — waits up to 3s specifically for the volume tick
// Get today's cumulative volume for an option contract.
// Uses snapshot mode so IBKR sends a one-time complete data packet including tick type 8 (volume).
async function getOptionVolumeOnly(symbol, expiration, strike, right, exchange, multiplier) {
  return new Promise((resolve) => {
    const reqId = nextRequestId++
    const contract = {
      symbol, secType: 'OPT', exchange: exchange || 'SMART', currency: 'USD',
      lastTradeDateOrContractMonth: expiration,
      strike: parseFloat(strike), right,
      multiplier: multiplier || '100'
    }

    let volume = 0
    let resolved = false

    const cleanup = () => {
      if (resolved) return
      resolved = true
      ib?.off(EventName.tickSize, onTickSize)
      ib?.off(EventName.tickSnapshotEnd, onSnapshotEnd)
      pendingRequests.delete(reqId)
      try { ib?.cancelMktData(reqId) } catch (e) {}
    }

    const onTickSize = (rId, tickType, size) => {
      if (rId !== reqId) return
      // tick type 8 = daily volume
      if (tickType === 8 && size > 0) volume = size
    }

    const onSnapshotEnd = (rId) => {
      if (rId !== reqId) return
      cleanup()
      resolve(volume)
    }

    pendingRequests.set(reqId, { resolve, reject: () => { cleanup(); resolve(0) } })
    ib.on(EventName.tickSize, onTickSize)
    ib.on(EventName.tickSnapshotEnd, onSnapshotEnd)

    // Fallback timeout in case tickSnapshotEnd never fires
    setTimeout(() => {
      if (!resolved) { cleanup(); resolve(volume) }
    }, 5000)

    try {
      // snapshot=true: IBKR sends all current ticks once then stops
      ib.reqMktData(reqId, contract, '', true, false, [])
    } catch (err) {
      cleanup()
      resolve(0)
    }
  })
}

// Get aggregate options flow for a symbol using tick types 29/30 (OPTION_CALL_VOLUME /
// OPTION_PUT_VOLUME) on the underlying contract. IBKR sends these as part of the
// standard market data snapshot when genericTickList includes "100".
export async function getOptionsFlow(symbol) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  console.log(`Fetching options flow for ${symbol}...`)

  return new Promise((resolve) => {
    const reqId = nextRequestId++
    const indexConfig = INDEX_SYMBOLS[symbol.toUpperCase()]
    const contract = indexConfig
      ? { symbol, secType: 'IND', exchange: indexConfig.exchange, currency: 'USD' }
      : { symbol, secType: 'STK', exchange: 'SMART', currency: 'USD' }

    let callVol = 0
    let putVol = 0
    let resolved = false

    const cleanup = () => {
      if (resolved) return
      resolved = true
      ib?.off(EventName.tickSize, onTick)
      ib?.off(EventName.tickGeneric, onTick)
      pendingRequests.delete(reqId)
      try { ib?.cancelMktData(reqId) } catch (e) {}
    }

    const finish = () => {
      const pcRatio = callVol > 0 ? putVol / callVol : null
      console.log(`  Flow result: callVol=${callVol} putVol=${putVol} pcRatio=${pcRatio?.toFixed(2)}`)
      resolve({ symbol, totalCallVol: callVol, totalPutVol: putVol, pcRatio, byExpiration: [] })
    }

    // OPTION_CALL_VOLUME=29 and OPTION_PUT_VOLUME=30 may arrive via tickSize OR tickGeneric
    const onTick = (rId, tickType, value) => {
      if (rId !== reqId) return
      if (tickType === 29 && value > 0) callVol = Math.round(value)
      if (tickType === 30 && value > 0) putVol = Math.round(value)
    }

    pendingRequests.set(reqId, { resolve, reject: () => { cleanup(); resolve({ symbol, totalCallVol: 0, totalPutVol: 0, pcRatio: null, byExpiration: [] }) } })
    ib.on(EventName.tickSize, onTick)
    ib.on(EventName.tickGeneric, onTick)

    // snapshot=true is incompatible with genericTickList — use streaming + manual cancel
    setTimeout(() => {
      if (!resolved) {
        cleanup()
        console.log(`  Flow timeout: callVol=${callVol} putVol=${putVol}`)
        finish()
      }
    }, 5000)

    try {
      // genericTickList "100" requests option volume ticks (29=call vol, 30=put vol)
      ib.reqMktData(reqId, contract, '100', false, false, [])
    } catch (err) {
      cleanup()
      resolve({ symbol, totalCallVol: 0, totalPutVol: 0, pcRatio: null, byExpiration: [] })
    }
  })
}

// Get options chain for a symbol
export async function getOptionsChain(symbol, currentPrice) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  console.log(`Fetching options chain for ${symbol}...`)

  // Step 1: Get option parameters (expirations, strikes)
  const params = await getOptionParams(symbol)
  if (!params || params.expirations.length === 0) {
    return { symbol, options: [] }
  }

  console.log(`  Found ${params.expirations.length} expirations, ${params.strikes.length} strikes`)

  // Step 2: Filter to relevant strikes (near the money, within 20%)
  const nearStrikes = params.strikes.filter(strike => {
    if (!currentPrice) return true
    const diff = Math.abs(strike - currentPrice) / currentPrice
    return diff <= 0.2
  })

  // Step 3: Get first 4 expirations
  const nearExpirations = params.expirations.slice(0, 4)

  console.log(`  Filtering to ${nearExpirations.length} expirations, ${nearStrikes.length} strikes`)

  // Step 4: Build list of all contracts to fetch
  const contractsToFetch = []
  for (const expiration of nearExpirations) {
    for (const strike of nearStrikes) {
      for (const right of ['C', 'P']) {
        contractsToFetch.push({ expiration, strike, right })
      }
    }
  }

  console.log(`  Fetching ${contractsToFetch.length} option contracts...`)

  // Step 5: Fetch in batches of 8 to avoid overwhelming IBKR
  const BATCH_SIZE = 8
  const options = []

  for (let i = 0; i < contractsToFetch.length; i += BATCH_SIZE) {
    const batch = contractsToFetch.slice(i, i + BATCH_SIZE)
    const batchPromises = batch.map(({ expiration, strike, right }) =>
      getOptionData(symbol, expiration, strike, right, params.exchange, params.multiplier)
    )

    const batchResults = await Promise.all(batchPromises)
    batchResults.forEach(optData => {
      if (optData) options.push(optData)
    })

    // Small delay between batches
    if (i + BATCH_SIZE < contractsToFetch.length) {
      await sleep(200)
    }
  }

  console.log(`  Fetched ${options.length} option contracts with data`)
  return { symbol, options }
}

// Cache for option params: symbol → { data, ts }
const optionParamsCache = new Map()
const PARAMS_CACHE_TTL = 60 * 60 * 1000 // 1 hour

// Get contract details to find conId
async function getContractId(symbol) {
  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++

    const contract = {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    let conId = null
    let resolved = false

    const onContractDetails = (rId, contractDetails) => {
      if (rId !== reqId) return
      if (contractDetails?.contract?.conId) {
        conId = contractDetails.contract.conId
        console.log(`  Found conId for ${symbol}: ${conId}`)
      }
    }

    const onContractDetailsEnd = (rId) => {
      if (rId !== reqId) return
      cleanup()
      resolve(conId)
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.contractDetails, onContractDetails) } catch (e) {}
      try { ib?.off(EventName.contractDetailsEnd, onContractDetailsEnd) } catch (e) {}
      pendingRequests.delete(reqId)
    }

    pendingRequests.set(reqId, { resolve, reject })
    ib.on(EventName.contractDetails, onContractDetails)
    ib.on(EventName.contractDetailsEnd, onContractDetailsEnd)

    setTimeout(() => {
      if (!resolved) {
        cleanup()
        resolve(conId)
      }
    }, 5000)

    try {
      ib.reqContractDetails(reqId, contract)
    } catch (err) {
      cleanup()
      reject(err)
    }
  })
}

// Get option parameters (expirations and strikes)
export async function getOptionParams(symbol) {
  // Return cached result if fresh
  const cached = optionParamsCache.get(symbol)
  if (cached && Date.now() - cached.ts < PARAMS_CACHE_TTL) {
    console.log(`  Option params cache hit for ${symbol}`)
    return cached.data
  }

  // First get the contract ID
  const conId = await getContractId(symbol)
  if (!conId) {
    console.log(`  Could not find conId for ${symbol}`)
    return { expirations: [], strikes: [], exchange: '', multiplier: '100' }
  }

  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++

    // Track best exchange = the one with the most expirations (most complete data)
    let bestExpirations = []
    let bestStrikes = []
    let bestExchange = ''
    let bestMultiplier = '100'
    let resolved = false

    const onSecDefOptParams = (rId, exchg, underlyingConId, tradingClass, mult, exps, strks) => {
      if (rId !== reqId) return
      console.log(`  Option params: exchange=${exchg}, expirations=${exps?.length}, strikes=${strks?.length}`)
      if (exps && exps.length > bestExpirations.length) {
        bestExpirations = [...exps].sort()
        bestStrikes = [...strks].sort((a, b) => a - b)
        bestExchange = exchg
        bestMultiplier = mult || '100'
      }
    }

    const onSecDefOptParamsEnd = (rId) => {
      if (rId !== reqId) return
      cleanup()
      const result = { expirations: bestExpirations, strikes: bestStrikes, exchange: bestExchange, multiplier: bestMultiplier }
      optionParamsCache.set(symbol, { data: result, ts: Date.now() })
      resolve(result)
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.securityDefinitionOptionParameter, onSecDefOptParams) } catch (e) {}
      try { ib?.off(EventName.securityDefinitionOptionParameterEnd, onSecDefOptParamsEnd) } catch (e) {}
      pendingRequests.delete(reqId)
    }

    pendingRequests.set(reqId, { resolve, reject })
    ib.on(EventName.securityDefinitionOptionParameter, onSecDefOptParams)
    ib.on(EventName.securityDefinitionOptionParameterEnd, onSecDefOptParamsEnd)

    setTimeout(() => {
      if (!resolved) {
        cleanup()
        const result = { expirations: bestExpirations, strikes: bestStrikes, exchange: bestExchange, multiplier: bestMultiplier }
        if (result.expirations.length > 0) optionParamsCache.set(symbol, { data: result, ts: Date.now() })
        resolve(result)
      }
    }, 10000)

    try {
      console.log(`  Requesting option params for ${symbol}, conId=${conId}, reqId=${reqId}`)
      ib.reqSecDefOptParams(reqId, symbol, '', 'STK', conId)
    } catch (err) {
      console.error(`  reqSecDefOptParams error:`, err)
      cleanup()
      reject(err)
    }
  })
}

// Get data for a single option contract using streaming (not snapshot)
async function getOptionData(symbol, expiration, strike, right, exchange, multiplier) {
  return new Promise((resolve) => {
    const reqId = nextRequestId++

    const contract = {
      symbol,
      secType: 'OPT',
      exchange: 'SMART',  // Always use SMART for options
      currency: 'USD',
      lastTradeDateOrContractMonth: expiration,
      strike,
      right,
      multiplier: multiplier || '100'
    }

    let optionData = {
      symbol,
      expiration,
      strike,
      right,
      bid: null,
      ask: null,
      last: null,
      volume: null,
      openInterest: null
    }
    let resolved = false
    let dataReceived = false

    const onTickPrice = (rId, tickType, price) => {
      if (rId !== reqId) return
      dataReceived = true
      if (tickType === 1) optionData.bid = price  // bid
      if (tickType === 2) optionData.ask = price  // ask
      if (tickType === 4) optionData.last = price // last
    }

    const onTickSize = (rId, tickType, size) => {
      if (rId !== reqId) return
      dataReceived = true
      if (tickType === 8) optionData.volume = size       // volume
      if (tickType === 27) optionData.openInterest = size // open interest
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.tickPrice, onTickPrice) } catch (e) {}
      try { ib?.off(EventName.tickSize, onTickSize) } catch (e) {}
      pendingRequests.delete(reqId)
      try { ib?.cancelMktData(reqId) } catch (e) {}
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: () => {
        cleanup()
        resolve(null) // Resolve with null on error instead of rejecting
      }
    })
    ib.on(EventName.tickPrice, onTickPrice)
    ib.on(EventName.tickSize, onTickSize)

    // Use streaming mode with timeout - collect data for 1.5 seconds then return
    setTimeout(() => {
      if (!resolved) {
        cleanup()
        if (dataReceived && (optionData.bid !== null || optionData.ask !== null)) {
          resolve(optionData)
        } else {
          resolve(null)
        }
      }
    }, 1500)

    try {
      // Request streaming market data (false = not snapshot)
      ib.reqMktData(reqId, contract, '', false, false, [])
    } catch (err) {
      cleanup()
      resolve(null)
    }
  })
}

// Subscribe to real-time market data for a symbol (persistent subscription)
export function subscribeMarketData(symbol) {
  if (!ib || !connectionState.connected) {
    console.log(`Cannot subscribe to ${symbol}: not connected`)
    return null
  }

  const upperSymbol = symbol.toUpperCase()

  // Already subscribed?
  if (symbolToReqId.has(upperSymbol)) {
    console.log(`Already subscribed to ${upperSymbol}`)
    return symbolToReqId.get(upperSymbol)
  }

  const reqId = nextRequestId++

  const indexConfig = INDEX_SYMBOLS[upperSymbol]
  const contract = indexConfig ? {
    symbol: upperSymbol,
    secType: SecType.IND,
    exchange: indexConfig.exchange,
    currency: 'USD'
  } : {
    symbol: upperSymbol,
    secType: SecType.STK,
    exchange: 'SMART',
    currency: 'USD'
  }

  // Initialize quote data
  const quote = {
    symbol: upperSymbol,
    last: null,
    open: null,
    high: null,
    low: null,
    prevClose: null,
    vwap: null,
    bid: null,
    ask: null,
    bidSize: null,
    askSize: null,
    volume: null,
    avgVolume: null,
    marketCap: null,
    week52High: null,
    week52Low: null,
    lastUpdate: null
  }

  activeSubscriptions.set(reqId, { symbol: upperSymbol, quote, contract })
  symbolToReqId.set(upperSymbol, reqId)

  // Set up tick handlers (these are shared, filter by reqId)
  const onTickPrice = (rId, tickType, price) => {
    if (rId !== reqId || price <= 0) return
    const sub = activeSubscriptions.get(reqId)
    if (!sub) return

    let changed = false
    switch (tickType) {
      case 1: if (sub.quote.bid !== price) { sub.quote.bid = price; changed = true } break
      case 2: if (sub.quote.ask !== price) { sub.quote.ask = price; changed = true } break
      case 4: if (sub.quote.last !== price) { sub.quote.last = price; changed = true } break
      case 6: if (sub.quote.high !== price) { sub.quote.high = price; changed = true } break
      case 7: if (sub.quote.low !== price) { sub.quote.low = price; changed = true } break
      case 9: if (sub.quote.prevClose !== price) { sub.quote.prevClose = price; changed = true } break
      case 14: if (sub.quote.open !== price) { sub.quote.open = price; changed = true } break
      case 15: if (sub.quote.week52Low !== price) { sub.quote.week52Low = price; changed = true } break
      case 16: if (sub.quote.week52High !== price) { sub.quote.week52High = price; changed = true } break
    }
    if (changed) {
      sub.quote.lastUpdate = Date.now()
      clearSubscriptionError(sub)
      emitQuoteUpdate(upperSymbol, { ...sub.quote })
    }
  }

  const onTickSize = (rId, tickType, size) => {
    if (rId !== reqId) return
    const sub = activeSubscriptions.get(reqId)
    if (!sub) return

    let changed = false
    switch (tickType) {
      case 0: if (sub.quote.bidSize !== size) { sub.quote.bidSize = size; changed = true } break
      case 3: if (sub.quote.askSize !== size) { sub.quote.askSize = size; changed = true } break
      case 8: if (sub.quote.volume !== size) { sub.quote.volume = size; changed = true } break
      case 21: if (sub.quote.avgVolume !== size) { sub.quote.avgVolume = size; changed = true } break
    }
    if (changed) {
      sub.quote.lastUpdate = Date.now()
      clearSubscriptionError(sub)
      emitQuoteUpdate(upperSymbol, { ...sub.quote })
    }
  }

  const onTickGeneric = (rId, tickType, value) => {
    if (rId !== reqId) return
    const sub = activeSubscriptions.get(reqId)
    if (!sub) return

    if (tickType === 81 && value > 0 && sub.quote.vwap !== value) {
      sub.quote.vwap = value
      sub.quote.lastUpdate = Date.now()
      clearSubscriptionError(sub)
      emitQuoteUpdate(upperSymbol, { ...sub.quote })
    }
  }

  // Store handlers for cleanup
  activeSubscriptions.get(reqId).handlers = { onTickPrice, onTickSize, onTickGeneric }

  ib.on(EventName.tickPrice, onTickPrice)
  ib.on(EventName.tickSize, onTickSize)
  ib.on(EventName.tickGeneric, onTickGeneric)

  try {
    // Request streaming market data with generic ticks for VWAP, avg volume
    ib.reqMktData(reqId, contract, '233,165', false, false, [])
    console.log(`Subscribed to market data for ${upperSymbol} (reqId: ${reqId})`)
    return reqId
  } catch (err) {
    console.error(`Failed to subscribe to ${upperSymbol}:`, err)
    activeSubscriptions.delete(reqId)
    symbolToReqId.delete(upperSymbol)
    ib?.off(EventName.tickPrice, onTickPrice)
    ib?.off(EventName.tickSize, onTickSize)
    ib?.off(EventName.tickGeneric, onTickGeneric)
    return null
  }
}

// Unsubscribe from market data for a symbol
export function unsubscribeMarketData(symbol) {
  const upperSymbol = symbol.toUpperCase()
  const reqId = symbolToReqId.get(upperSymbol)

  if (!reqId) {
    console.log(`Not subscribed to ${upperSymbol}`)
    return false
  }

  const sub = activeSubscriptions.get(reqId)
  if (sub && sub.handlers) {
    try { ib?.off(EventName.tickPrice, sub.handlers.onTickPrice) } catch (e) {}
    try { ib?.off(EventName.tickSize, sub.handlers.onTickSize) } catch (e) {}
    try { ib?.off(EventName.tickGeneric, sub.handlers.onTickGeneric) } catch (e) {}
  }

  try {
    if (ib && connectionState.connected) {
      ib?.cancelMktData(reqId)
    }
  } catch (e) {
    console.error(`Error canceling market data for ${upperSymbol}:`, e)
  }

  activeSubscriptions.delete(reqId)
  symbolToReqId.delete(upperSymbol)
  latestQuotes.delete(upperSymbol)

  console.log(`Unsubscribed from market data for ${upperSymbol}`)
  return true
}

// Get list of actively subscribed symbols
export function getSubscribedSymbols() {
  return Array.from(symbolToReqId.keys())
}

// Unsubscribe from all market data
export function unsubscribeAllMarketData() {
  for (const symbol of symbolToReqId.keys()) {
    unsubscribeMarketData(symbol)
  }
}

// Generic bars accumulator — samples any property on an interval and stores bars
// Cumulative properties (callVol, putVol, volume) store deltas; all others store raw values.

const DELTA_PROPERTIES = new Set(['callVol', 'putVol', 'volume'])

// Fetchers return a numeric value (or null) for a given symbol
const PROPERTY_FETCHERS = {
  callVol:   async (sym) => { const f = await getOptionsFlow(sym); return f.totalCallVol },
  putVol:    async (sym) => { const f = await getOptionsFlow(sym); return f.totalPutVol },
  pcRatio:   async (sym) => { const f = await getOptionsFlow(sym); return f.pcRatio },
  last:      (sym) => getLatestQuote(sym)?.last ?? null,
  bid:       (sym) => getLatestQuote(sym)?.bid ?? null,
  ask:       (sym) => getLatestQuote(sym)?.ask ?? null,
  high:      (sym) => getLatestQuote(sym)?.high ?? null,
  low:       (sym) => getLatestQuote(sym)?.low ?? null,
  volume:    (sym) => getLatestQuote(sym)?.volume ?? null,
  vwap:      (sym) => getLatestQuote(sym)?.vwap ?? null,
  prevClose: (sym) => getLatestQuote(sym)?.prevClose ?? null,
}

const BARS_PATH = join(__dirname, 'data/bars.json')

function loadBarsData() {
  try { return JSON.parse(readFileSync(BARS_PATH, 'utf-8')) } catch { return {} }
}

function saveBarsData(data) {
  try {
    mkdirSync(join(__dirname, 'data'), { recursive: true })
    writeFileSync(BARS_PATH, JSON.stringify(data))
  } catch (e) {
    console.error('[bars] save error:', e.message)
  }
}

const barsState = new Map() // `${symbol}:${property}:${intervalMs}` -> { bars, lastValue, timer }

export function startBarsAccumulator(symbol, property, intervalMs) {
  const upper = symbol.toUpperCase()
  const key = `${upper}:${property}:${intervalMs}`
  if (barsState.has(key)) return

  const fetcher = PROPERTY_FETCHERS[property]
  if (!fetcher) {
    console.warn(`[bars] no fetcher for property "${property}"`)
    return
  }

  const isDelta = DELTA_PROPERTIES.has(property)
  const persisted = loadBarsData()
  const state = { bars: persisted[key] || [], lastValue: null, timer: null }
  barsState.set(key, state)

  const sample = async () => {
    if (!ib || !connectionState.connected) return
    try {
      const value = await fetcher(upper)
      if (value == null || !isFinite(value)) return
      const now = Date.now()
      if (isDelta) {
        if (state.lastValue !== null) {
          const delta = Math.max(0, value - state.lastValue)
          state.bars.push({ timestamp: now, value: delta })
          if (state.bars.length > 200) state.bars.shift()
          const all = {}; for (const [k, s] of barsState) all[k] = s.bars
          saveBarsData(all)
          console.log(`[bars] ${key} delta=${delta} (${state.bars.length} bars)`)
        }
      } else {
        state.bars.push({ timestamp: now, value })
        if (state.bars.length > 200) state.bars.shift()
        const all = {}; for (const [k, s] of barsState) all[k] = s.bars
        saveBarsData(all)
      }
      state.lastValue = value
    } catch (e) {
      console.error(`[bars] ${key} sample error:`, e.message)
    }
  }

  sample()
  state.timer = setInterval(sample, intervalMs)
  console.log(`[bars] started ${key} every ${intervalMs / 1000}s (${state.bars.length} bars loaded)`)
}

export function getBars(symbol, property, intervalMs) {
  const key = `${symbol.toUpperCase()}:${property}:${intervalMs}`
  const state = barsState.get(key)
  return state ? [...state.bars] : []
}

export function stopBarsAccumulator(symbol, property, intervalMs) {
  const key = `${symbol.toUpperCase()}:${property}:${intervalMs}`
  const state = barsState.get(key)
  if (state?.timer) clearInterval(state.timer)
  barsState.delete(key)
}

export function clearBars(symbol, property, intervalMs) {
  const key = `${symbol.toUpperCase()}:${property}:${intervalMs}`
  const state = barsState.get(key)
  if (state) {
    if (state.timer) clearInterval(state.timer)
    barsState.delete(key)
  }
  const persisted = loadBarsData()
  delete persisted[key]
  saveBarsData(persisted)
}

const ACCOUNT_SUMMARY_TAGS = [
  'NetLiquidation', 'TotalCashValue', 'GrossPositionValue',
  'BuyingPower', 'AvailableFunds', 'ExcessLiquidity',
  'RealizedPnL', 'UnrealizedPnL', 'DailyPnL',
  'MaintMarginReq', 'InitMarginReq'
].join(',')

export async function getAccountSummary(tags = ACCOUNT_SUMMARY_TAGS) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++
    const summary = {}
    let resolved = false

    const onAccountSummary = (rId, account, tag, value, currency) => {
      if (rId !== reqId) return
      const numeric = parseFloat(value)
      summary[tag] = { value: isNaN(numeric) ? value : numeric, currency }
    }

    const onAccountSummaryEnd = (rId) => {
      if (rId !== reqId) return
      cleanup()
      resolve(summary)
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.accountSummary, onAccountSummary) } catch (e) {}
      try { ib?.off(EventName.accountSummaryEnd, onAccountSummaryEnd) } catch (e) {}
      pendingRequests.delete(reqId)
      try { ib?.cancelAccountSummary(reqId) } catch (e) {}
    }

    pendingRequests.set(reqId, {
      resolve,
      reject: (err) => { cleanup(); reject(err) }
    })

    ib.on(EventName.accountSummary, onAccountSummary)
    ib.on(EventName.accountSummaryEnd, onAccountSummaryEnd)

    setTimeout(() => {
      if (!resolved) {
        cleanup()
        if (Object.keys(summary).length > 0) {
          resolve(summary)
        } else {
          reject(new Error('Account summary timeout'))
        }
      }
    }, 10000)

    try {
      ib.reqAccountSummary(reqId, 'All', tags)
    } catch (err) {
      cleanup()
      reject(err)
    }
  })
}

export async function getPositions() {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  return new Promise((resolve, reject) => {
    const positions = new Map()
    let resolved = false

    const onPosition = (account, contract, pos, avgCost) => {
      const sym = contract.symbol.toUpperCase()
      const secType = contract.secType || 'STK'
      const expiry = contract.lastTradeDateOrContractMonth || ''
      const strike = contract.strike || ''
      const right = contract.right || ''
      const local = contract.localSymbol?.trim()

      const key = local ||
        (secType !== 'STK' ? `${sym}_${secType}_${expiry}_${strike}_${right}` : sym)

      let description = sym
      if (secType === 'OPT' && expiry) {
        const y = expiry.slice(0, 4), m = expiry.slice(4, 6), d = expiry.slice(6, 8)
        const dateStr = new Date(`${y}-${m}-${d}`).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' }).replace(', ', "'")
        description = `${sym} ${dateStr} ${strike} ${right === 'P' ? 'PUT' : right === 'C' ? 'CALL' : right}`
      } else if (secType !== 'STK') {
        description = local || key
      }

      positions.set(key, {
        position: pos,
        avgCost,
        symbol: sym,
        localSymbol: key,
        description,
        secType,
        expiry: expiry || null,
        strike: strike || null,
        right: right || null,
        multiplier: contract.multiplier ? Number(contract.multiplier) : null,
      })
    }

    const onPositionEnd = () => {
      cleanup()
      resolve(Object.fromEntries(positions))
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.position, onPosition) } catch (e) {}
      try { ib?.off(EventName.positionEnd, onPositionEnd) } catch (e) {}
      try { ib?.cancelPositions() } catch (e) {}
    }

    ib.on(EventName.position, onPosition)
    ib.on(EventName.positionEnd, onPositionEnd)

    setTimeout(() => {
      if (!resolved) {
        cleanup()
        resolve(Object.fromEntries(positions))
      }
    }, 10000)

    try {
      ib.reqPositions()
    } catch (err) {
      cleanup()
      reject(err)
    }
  })
}

export async function placeOrder({ action, symbol, quantity, orderType, limitPrice, stopPrice }) {
  if (!ib || !connectionState.connected) {
    throw new Error('Not connected to IBKR')
  }

  const upperSymbol = symbol.toUpperCase()
  const ibAction = action === 'BUY' ? OrderAction.BUY : OrderAction.SELL
  const qty = Number(quantity)

  const contract = {
    symbol: upperSymbol,
    secType: SecType.STK,
    exchange: 'SMART',
    currency: 'USD'
  }

  let order
  switch (orderType) {
    case 'LMT':
      order = new LimitOrder(ibAction, Number(limitPrice), qty)
      break
    case 'STP':
      order = new StopOrder(ibAction, Number(stopPrice), qty)
      break
    case 'STP_LMT':
      order = new StopLimitOrder(ibAction, Number(limitPrice), Number(stopPrice), qty)
      break
    default: // MKT
      order = new MarketOrder(ibAction, qty)
  }

  return new Promise((resolve, reject) => {
    const onNextValidId = (orderId) => {
      ib.off(EventName.nextValidId, onNextValidId)

      const onOrderStatus = (id, status) => {
        if (id !== orderId) return
        ib.off(EventName.orderStatus, onOrderStatus)
        if (status === 'Submitted' || status === 'PreSubmitted' || status === 'Filled') {
          resolve({ orderId, status })
        } else if (status === 'Cancelled' || status === 'Inactive') {
          reject(new Error(`Order ${orderId} ${status}`))
        }
      }

      const timeout = setTimeout(() => {
        ib.off(EventName.orderStatus, onOrderStatus)
        resolve({ orderId, status: 'Submitted' })
      }, 5000)

      ib.on(EventName.orderStatus, onOrderStatus)

      try {
        ib.placeOrder(orderId, contract, order)
      } catch (err) {
        clearTimeout(timeout)
        ib.off(EventName.orderStatus, onOrderStatus)
        reject(err)
      }
    }

    ib.once(EventName.nextValidId, onNextValidId)
    ib.reqIds(1)
  })
}

export async function getNewsProviders() {
  if (!ib || !connectionState.connected) return []
  return new Promise((resolve) => {
    let resolved = false
    const onProviders = (providers) => {
      if (resolved) return
      resolved = true
      resolve(providers || [])
    }
    const timeout = setTimeout(() => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.newsProviders, onProviders) } catch {}
      resolve([])
    }, 5000)
    ib.on(EventName.newsProviders, (providers) => {
      clearTimeout(timeout)
      try { ib?.off(EventName.newsProviders, onProviders) } catch {}
      onProviders(providers)
    })
    try {
      ib.reqNewsProviders()
    } catch {
      clearTimeout(timeout)
      try { ib?.off(EventName.newsProviders, onProviders) } catch {}
      resolve([])
    }
  })
}

let newsProvidersCache = null
const NEWS_PROVIDERS_TTL = 10 * 60 * 1000

async function getCachedNewsProviders() {
  if (newsProvidersCache && Date.now() - newsProvidersCache.fetchedAt < NEWS_PROVIDERS_TTL) {
    return newsProvidersCache.providers
  }
  const providers = await getNewsProviders()
  newsProvidersCache = { providers, fetchedAt: Date.now() }
  return providers
}

export async function getHistoricalNews(symbol, limit = 10) {
  if (!ib || !connectionState.connected) throw new Error('Not connected to IBKR')

  const conId = await getContractId(symbol)
  if (!conId) throw new Error(`Could not resolve contract for ${symbol}`)

  const providers = await getCachedNewsProviders()
  if (!providers.length) throw new Error('No news providers available')
  const providerCodes = providers.map(p => p.code || p.providerCode).filter(Boolean).join('+')

  return new Promise((resolve, reject) => {
    const reqId = nextRequestId++
    const articles = []
    let resolved = false

    const endDateTime = ''
    const startDateTime = (() => {
      const d = new Date()
      d.setDate(d.getDate() - 30)
      return d.toISOString().replace('T', ' ').slice(0, 19)
    })()

    const onNews = (rId, time, providerCode, articleId, headline) => {
      if (rId !== reqId) return
      articles.push({ time, providerCode, articleId, headline })
    }

    const onNewsEnd = (rId) => {
      if (rId !== reqId) return
      cleanup()
      resolve(articles)
    }

    const cleanup = () => {
      if (resolved) return
      resolved = true
      try { ib?.off(EventName.historicalNews, onNews) } catch {}
      try { ib?.off(EventName.historicalNewsEnd, onNewsEnd) } catch {}
    }

    setTimeout(() => {
      if (!resolved) { cleanup(); resolve(articles) }
    }, 10000)

    ib.on(EventName.historicalNews, onNews)
    ib.on(EventName.historicalNewsEnd, onNewsEnd)

    try {
      ib.reqHistoricalNews(reqId, conId, providerCodes, startDateTime, endDateTime, limit, [])
    } catch (err) {
      cleanup()
      reject(err)
    }
  })
}

export { ib }
