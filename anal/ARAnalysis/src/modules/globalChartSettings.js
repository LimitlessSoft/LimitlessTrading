const KEY = 'ar_global_chart_settings'

export const DEFAULT_GLOBAL_CHART_SETTINGS = {
  rthBgColor: '#1a2520',
  outsideRthBgColor: '#1a1a2e',
  fontSize: 9,
  invertScroll: false,
  invertDrag: false,
  volumeMultiplier: 1,
  ohlcPosition: 'mouse',
  showMarketIndicator: true
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

// Returns true if the market is currently open (NYSE RTH, weekdays only)
export function isMarketOpen() {
  const now = Date.now()
  const date = new Date(now)
  const dayParts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York',
    weekday: 'short'
  }).formatToParts(date)
  const weekday = dayParts.find(p => p.type === 'weekday').value
  if (weekday === 'Sat' || weekday === 'Sun') return false
  return isRTHTimestamp(now)
}

// Returns true if timestamp falls within NYSE regular session (9:30–16:00 ET)
export function isRTHTimestamp(timestamp) {
  const date = new Date(timestamp)
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/New_York',
    hour: 'numeric',
    minute: 'numeric',
    hour12: false
  }).formatToParts(date)
  const hour   = parseInt(parts.find(p => p.type === 'hour').value)
  const minute = parseInt(parts.find(p => p.type === 'minute').value)
  const total  = hour * 60 + minute
  return total >= 9 * 60 + 30 && total < 16 * 60
}
