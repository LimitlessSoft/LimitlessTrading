import pkg from '@stoqey/ib'
const { IBApi, EventName, SecType } = pkg

let ib = null
let connectionState = {
  connected: false,
  port: null,
  clientId: null,
  error: null
}

const pendingRequests = new Map()
let nextRequestId = 1

export function getConnectionStatus() {
  return { ...connectionState }
}

export async function connect(port = 7497) {
  if (ib && connectionState.connected) {
    await disconnect()
  }

  return new Promise((resolve, reject) => {
    const clientId = Math.floor(Math.random() * 10000)

    ib = new IBApi({
      clientId,
      host: '127.0.0.1',
      port
    })

    const timeout = setTimeout(() => {
      ib.disconnect()
      connectionState = { connected: false, port: null, error: 'Connection timeout' }
      reject(new Error('Connection timeout'))
    }, 10000)

    ib.on(EventName.connected, () => {
      clearTimeout(timeout)
      connectionState = { connected: true, port, clientId }
      console.log(`Connected to IBKR on port ${port}`)
      resolve(connectionState)
    })

    ib.on(EventName.disconnected, () => {
      connectionState = { connected: false, port: null, clientId: null }
      console.log('Disconnected from IBKR')
    })

    ib.on(EventName.error, (err, code, reqId) => {
      console.error('IBKR error:', err?.message || err, 'code:', code)

      // Handle pending request errors
      if (reqId && pendingRequests.has(reqId)) {
        const request = pendingRequests.get(reqId)
        request.reject(new Error(err?.message || `IBKR error code: ${code}`))
        pendingRequests.delete(reqId)
      }

      // Connection errors
      if (code === 502 || code === 504) {
        clearTimeout(timeout)
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

    const contract = {
      symbol,
      secType: SecType.STK,
      exchange: 'SMART',
      currency: 'USD'
    }

    const whatToShow = 'TRADES'
    const useRTH = 1 // Regular trading hours only
    const formatDate = 1 // 1 = yyyyMMdd HH:mm:ss format

    pendingRequests.set(reqId, { resolve, reject })

    const onHistoricalData = (rId, date, open, high, low, close, volume, count, wap, hasGaps) => {
      if (rId !== reqId) return

      // Check for end marker
      if (date.startsWith('finished')) {
        ib.off(EventName.historicalData, onHistoricalData)
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

    // Set timeout (60 seconds per request)
    setTimeout(() => {
      if (pendingRequests.has(reqId)) {
        ib.off(EventName.historicalData, onHistoricalData)
        pendingRequests.delete(reqId)
        if (bars.length > 0) {
          resolve(bars) // Return partial data if any
        } else {
          reject(new Error('Request timeout'))
        }
      }
    }, 60000)

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
      ib.off(EventName.historicalData, onHistoricalData)
      pendingRequests.delete(reqId)
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
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  const seconds = String(date.getSeconds()).padStart(2, '0')
  return `${year}${month}${day} ${hours}:${minutes}:${seconds}`
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
    // Intraday: yyyyMMdd HH:mm:ss or timestamp
    if (dateStr.includes(' ')) {
      const [datePart, timePart] = dateStr.split(' ')
      const year = datePart.substring(0, 4)
      const month = datePart.substring(4, 6)
      const day = datePart.substring(6, 8)
      return new Date(`${year}-${month}-${day}T${timePart}`).getTime()
    } else {
      // Unix timestamp
      return parseInt(dateStr) * 1000
    }
  }
}

export { ib }
