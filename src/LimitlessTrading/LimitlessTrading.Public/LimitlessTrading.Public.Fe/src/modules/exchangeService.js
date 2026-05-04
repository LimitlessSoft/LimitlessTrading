/**
 * Exchange time and status service
 *
 * Provides getExchangeTime / getExchangeStatus for use in template placeholders
 * and the MarketClocks component.
 *
 * Placeholder syntax:
 *   ${NYSE:time:30}   — current time at NYSE, refresh every 30 s
 *   ${NYSE:status:5}  — market status at NYSE, refresh every 5 s
 */

export const EXCHANGES = {
  NYSE: {
    name: 'NYSE',
    timezone: 'America/New_York',
    preMarketOpen:   { h: 4,  m: 0  },
    marketOpen:      { h: 9,  m: 30 },
    marketClose:     { h: 16, m: 0  },
    afterHoursClose: { h: 20, m: 0  },
  },
  NASDAQ: {
    name: 'NASDAQ',
    timezone: 'America/New_York',
    preMarketOpen:   { h: 4,  m: 0  },
    marketOpen:      { h: 9,  m: 30 },
    marketClose:     { h: 16, m: 0  },
    afterHoursClose: { h: 20, m: 0  },
  },
  LSE: {
    name: 'LSE',
    timezone: 'Europe/London',
    preMarketOpen:   { h: 5,  m: 5  },
    marketOpen:      { h: 8,  m: 0  },
    marketClose:     { h: 16, m: 30 },
    afterHoursClose: { h: 17, m: 15 },
  },
  XETRA: {
    name: 'XETRA',
    timezone: 'Europe/Berlin',
    marketOpen:  { h: 9,  m: 0  },
    marketClose: { h: 17, m: 30 },
  },
  TSX: {
    name: 'TSX',
    timezone: 'America/Toronto',
    preMarketOpen:   { h: 7,  m: 0  },
    marketOpen:      { h: 9,  m: 30 },
    marketClose:     { h: 16, m: 0  },
    afterHoursClose: { h: 17, m: 0  },
  },
  JPX: {
    name: 'JPX',
    timezone: 'Asia/Tokyo',
    marketOpen:  { h: 9,  m: 0  },
    marketClose: { h: 15, m: 30 },
    lunchStart:  { h: 11, m: 30 },
    lunchEnd:    { h: 12, m: 30 },
  },
  HKEX: {
    name: 'HKEX',
    timezone: 'Asia/Hong_Kong',
    marketOpen:  { h: 9,  m: 30 },
    marketClose: { h: 16, m: 0  },
    lunchStart:  { h: 12, m: 0  },
    lunchEnd:    { h: 13, m: 0  },
  },
  ASX: {
    name: 'ASX',
    timezone: 'Australia/Sydney',
    marketOpen:  { h: 10, m: 0  },
    marketClose: { h: 16, m: 0  },
  },
  SGX: {
    name: 'SGX',
    timezone: 'Asia/Singapore',
    marketOpen:  { h: 9,  m: 0  },
    marketClose: { h: 17, m: 0  },
  },
}

const DAY_MAP = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 }

function getLocalTimeParts(timezone) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: timezone,
    hour: 'numeric',
    minute: 'numeric',
    weekday: 'short',
    hour12: false,
  }).formatToParts(new Date())

  const hours   = parseInt(parts.find(p => p.type === 'hour').value, 10)
  const minutes = parseInt(parts.find(p => p.type === 'minute').value, 10)
  const weekday = parts.find(p => p.type === 'weekday').value
  const dayNum  = DAY_MAP[weekday] ?? -1

  return { hours, minutes, t: hours * 60 + minutes, isWeekend: dayNum === 0 || dayNum === 6 }
}

function toMin(hm) {
  return hm.h * 60 + hm.m
}

/**
 * @param {string} exchange — e.g. "NYSE", "LSE"
 * @returns {"Open"|"Pre-Market"|"After-Hours"|"Closed"|"Unknown"}
 */
export function getExchangeStatus(exchange) {
  const info = EXCHANGES[exchange.toUpperCase()]
  if (!info) return 'Unknown'

  const { t, isWeekend } = getLocalTimeParts(info.timezone)
  if (isWeekend) return 'Closed'

  const open  = toMin(info.marketOpen)
  const close = toMin(info.marketClose)

  if (t >= open && t < close) {
    if (info.lunchStart && t >= toMin(info.lunchStart) && t < toMin(info.lunchEnd)) {
      return 'Closed'
    }
    return 'Open'
  }

  if (info.preMarketOpen && t >= toMin(info.preMarketOpen) && t < open) {
    return 'Pre-Market'
  }

  if (info.afterHoursClose && t >= close && t < toMin(info.afterHoursClose)) {
    return 'After-Hours'
  }

  return 'Closed'
}

/**
 * @param {string} exchange — e.g. "NYSE", "LSE"
 * @returns {string} — e.g. "09:32 ET", "14:45 GMT"
 */
export function getExchangeTime(exchange) {
  const info = EXCHANGES[exchange.toUpperCase()]
  if (!info) return '--'

  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: info.timezone,
    hour:     '2-digit',
    minute:   '2-digit',
    second:   '2-digit',
    hour12:   false,
    timeZoneName: 'short',
  }).formatToParts(new Date())

  const hour   = parts.find(p => p.type === 'hour').value
  const minute = parts.find(p => p.type === 'minute').value
  const second = parts.find(p => p.type === 'second').value
  const tz     = parts.find(p => p.type === 'timeZoneName').value

  return `${hour}:${minute}:${second} ${tz}`
}

export function isKnownExchange(exchange) {
  return Object.prototype.hasOwnProperty.call(EXCHANGES, exchange.toUpperCase())
}
