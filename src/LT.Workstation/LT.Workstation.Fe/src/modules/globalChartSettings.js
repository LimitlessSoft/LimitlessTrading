const KEY = 'ar_global_chart_settings'

export const DEFAULT_GLOBAL_CHART_SETTINGS = {
  rthBgColor: '#1a2520',
  outsideRthBgColor: '#1a1a2e',
  fontSize: 9,
  invertScroll: false,
  invertDrag: false,
  volumeMultiplier: 1,
  ohlcPosition: 'mouse',
  showMarketIndicator: true,
  shiftScrollZoom: true,
  graphTooltipPosition: 'cursor',
  outsideRTH: true
}

export function getGlobalChartSettings() {
  try {
    const stored = localStorage.getItem(KEY)
    return stored
      ? { ...DEFAULT_GLOBAL_CHART_SETTINGS, ...JSON.parse(stored) }
      : { ...DEFAULT_GLOBAL_CHART_SETTINGS }
  } catch {
    return { ...DEFAULT_GLOBAL_CHART_SETTINGS }
  }
}

export function saveGlobalChartSettings(settings) {
  localStorage.setItem(KEY, JSON.stringify(settings))
  window.dispatchEvent(new CustomEvent('globalChartSettingsChanged', { detail: settings }))
}

const _weekdayFmt = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  weekday: 'short'
})
const _timeFmt = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  hour: 'numeric',
  minute: 'numeric',
  hour12: false
})
const _rthCache = new Map()

// Returns true if the market is currently open (NYSE RTH, weekdays only)
export function isMarketOpen() {
  const now = Date.now()
  const weekday = _weekdayFmt.formatToParts(now).find(p => p.type === 'weekday').value
  if (weekday === 'Sat' || weekday === 'Sun') return false
  return isRTHTimestamp(now)
}

// Returns true if timestamp falls within NYSE regular session (9:30–16:00 ET)
export function isRTHTimestamp(timestamp) {
  const minute = Math.floor(timestamp / 60000)
  if (_rthCache.has(minute)) return _rthCache.get(minute)
  const parts = _timeFmt.formatToParts(new Date(timestamp))
  const hour  = parseInt(parts.find(p => p.type === 'hour').value)
  const min   = parseInt(parts.find(p => p.type === 'minute').value)
  const total = hour * 60 + min
  const result = total >= 9 * 60 + 30 && total < 16 * 60
  if (_rthCache.size > 2000) _rthCache.clear()
  _rthCache.set(minute, result)
  return result
}
