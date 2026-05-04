import { getSetting, setSetting } from '../db/database'

// Fallback FOMC decision dates for 2026 (2:00 PM ET on the second day of each meeting)
const FALLBACK_FOMC_DATES = [
  '2026-01-28T14:00:00-05:00', // EST
  '2026-03-18T14:00:00-04:00', // EDT
  '2026-04-29T14:00:00-04:00',
  '2026-06-17T14:00:00-04:00',
  '2026-07-29T14:00:00-04:00',
  '2026-09-16T14:00:00-04:00',
  '2026-10-28T14:00:00-04:00',
  '2026-12-09T14:00:00-05:00', // EST
]

const CACHE_DURATION_MS = 24 * 60 * 60 * 1000 // 24 hours
const CACHE_VERSION = 2 // Increment to invalidate old cache

/**
 * Fetch FOMC dates from backend proxy
 */
async function fetchFomcDates() {
  try {
    const response = await fetch('/api/fomc/dates')
    if (!response.ok) {
      throw new Error('Failed to fetch FOMC dates')
    }
    const data = await response.json()
    return data.dates
  } catch {
    return null
  }
}

/**
 * Get FOMC dates, checking cache first
 * @returns {Promise<string[]>} Array of ISO date strings
 */
export async function getFomcDates() {
  const cacheVersion = await getSetting('fomcCacheVersion')
  const lastFetch = await getSetting('fomcLastFetch')
  const cachedDates = await getSetting('fomcDates')

  // Check if cache is valid (correct version and less than 24 hours old)
  const now = Date.now()
  if (cacheVersion === CACHE_VERSION && lastFetch && cachedDates && (now - lastFetch) < CACHE_DURATION_MS) {
    return cachedDates
  }

  // Try to fetch fresh dates
  const freshDates = await fetchFomcDates()

  if (freshDates && freshDates.length > 0) {
    await setSetting('fomcDates', freshDates)
    await setSetting('fomcLastFetch', now)
    await setSetting('fomcCacheVersion', CACHE_VERSION)
    return freshDates
  }

  // Use cached dates if available (and valid version), otherwise fallback
  if (cacheVersion === CACHE_VERSION && cachedDates && cachedDates.length > 0) {
    return cachedDates
  }

  return FALLBACK_FOMC_DATES
}

/**
 * Get the next FOMC decision date
 * @returns {Promise<Date|null>} Next decision Date object, or null if none found
 */
export async function getNextFomcDecision() {
  const dates = await getFomcDates()
  const now = new Date()

  for (const dateStr of dates) {
    const date = new Date(dateStr)
    if (date > now) {
      return date
    }
  }

  return null
}

/**
 * Format a date as "FOMC Mon DD" for display
 * @param {Date} date
 * @returns {string}
 */
export function formatFomcLabel(date) {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  return `FOMC ${months[date.getMonth()]} ${date.getDate()}`
}

/**
 * Format a date as "Mon DD YYYY" for placeholder display
 * @param {Date} date
 * @returns {string}
 */
export function formatFomcDatetime(date) {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  return `${months[date.getMonth()]} ${date.getDate()} ${date.getFullYear()}`
}

/**
 * Format countdown to target date as "Xd Yh Zm" or "Yh Zm Ws" when under a day
 * @param {Date} target
 * @returns {string}
 */
export function formatFomcCountdownText(target) {
  const { days, hours, minutes, seconds, totalMs } = calculateCountdown(target)
  if (totalMs <= 0) return 'Now'
  if (days > 0) return `${days}d ${hours}h ${minutes}m`
  if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`
  return `${minutes}m ${seconds}s`
}

/**
 * Calculate countdown from now to target date
 * @param {Date} target
 * @returns {{ days: number, hours: number, minutes: number, seconds: number, totalMs: number }}
 */
export function calculateCountdown(target) {
  const now = new Date()
  const diff = target.getTime() - now.getTime()

  if (diff <= 0) {
    return { days: 0, hours: 0, minutes: 0, seconds: 0, totalMs: 0 }
  }

  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))
  const seconds = Math.floor((diff % (1000 * 60)) / 1000)

  return { days, hours, minutes, seconds, totalMs: diff }
}
