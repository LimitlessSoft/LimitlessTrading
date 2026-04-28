/**
 * Alert Service
 *
 * Manages alerts that can be created by widgets or built-in schedules.
 * Each alert has a schedule and one or more actions:
 *
 * Actions:
 *   'message' — display a notification on screen
 *   'sound'   — play a beep via Web Audio API
 *   'event'   — push a named event to subscribers (for widgets to react to)
 *
 * Alert config shape:
 * {
 *   id: string,                    // auto-generated if omitted
 *   label: string,                 // human-readable name
 *   schedule: {
 *     type: 'interval',            // 'interval' (more types may be added later)
 *     ms: number,                  // interval in ms (used when alignToHour is false)
 *     alignToHour: bool,           // fire at the top of each hour
 *   },
 *   actions: string[],             // ['message', 'sound', 'event']
 *   getMessage: () => string,      // function returning message text, or...
 *   message: string,               // ...a static string
 *   eventName: string,             // required for 'event' action
 *   sound: {                       // optional sound overrides
 *     frequency: number,           // Hz, default 520
 *     duration: number,            // seconds, default 0.4
 *     volume: number,              // 0–1, default 0.25
 *   }
 * }
 */

const alerts = new Map()
const eventSubscribers = new Map()  // eventName -> Set<callback>
const messageSubscribers = new Set()

let nextAlertId = 1

function playBeep({ frequency = 520, duration = 0.4, volume = 0.25 } = {}) {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext
    if (!AudioCtx) return
    const ctx = new AudioCtx()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.connect(gain)
    gain.connect(ctx.destination)

    osc.type = 'sine'
    osc.frequency.value = frequency
    gain.gain.setValueAtTime(volume, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration)

    osc.start(ctx.currentTime)
    osc.stop(ctx.currentTime + duration)

    // Clean up context after sound finishes
    setTimeout(() => ctx.close(), (duration + 0.5) * 1000)
  } catch {
    // Web Audio not supported or blocked — silently ignore
  }
}

function fireAlert(alert) {
  const message = typeof alert.getMessage === 'function'
    ? alert.getMessage()
    : (alert.message || '')

  if (alert.actions.includes('sound')) {
    playBeep(alert.sound || {})
  }

  if (alert.actions.includes('message') && message) {
    const notification = {
      id: `${alert.id}-${Date.now()}`,
      alertId: alert.id,
      label: alert.label || '',
      message,
      timestamp: new Date(),
    }
    for (const cb of messageSubscribers) {
      try { cb(notification) } catch {}
    }
  }

  if (alert.actions.includes('event') && alert.eventName) {
    const subs = eventSubscribers.get(alert.eventName)
    if (subs) {
      const payload = {
        alertId: alert.id,
        eventName: alert.eventName,
        timestamp: new Date(),
        message,
      }
      for (const cb of subs) {
        try { cb(payload) } catch {}
      }
    }
  }
}

function scheduleAlert(alert) {
  const schedule = alert.schedule || {}

  if (schedule.type === 'interval') {
    if (schedule.alignToHour) {
      // Fire at the next top-of-hour, then every hour exactly
      const now = new Date()
      const msUntilNextHour =
        (60 - now.getMinutes()) * 60_000 -
        now.getSeconds() * 1_000 -
        now.getMilliseconds()

      const timeout = setTimeout(() => {
        fireAlert(alert)
        const intervalId = setInterval(() => fireAlert(alert), 3_600_000)
        const stored = alerts.get(alert.id)
        if (stored) {
          stored._intervalId = intervalId
          stored._timeoutId = null
        }
      }, msUntilNextHour)

      alert._timeoutId = timeout
      alert._intervalId = null
    } else {
      const intervalId = setInterval(() => fireAlert(alert), schedule.ms)
      alert._intervalId = intervalId
      alert._timeoutId = null
    }
  }
}

/**
 * Register and start an alert.
 * @param {object} config
 * @returns {string} Alert ID
 */
export function createAlert(config) {
  const id = config.id || `alert-${nextAlertId++}`
  const alert = { ...config, id, _intervalId: null, _timeoutId: null }
  alerts.set(id, alert)
  scheduleAlert(alert)
  return id
}

/**
 * Remove an alert and cancel its schedule.
 * @param {string} id
 */
export function removeAlert(id) {
  const alert = alerts.get(id)
  if (!alert) return
  if (alert._intervalId) clearInterval(alert._intervalId)
  if (alert._timeoutId) clearTimeout(alert._timeoutId)
  alerts.delete(id)
}

/**
 * List all registered alerts.
 * @returns {object[]}
 */
export function listAlerts() {
  return Array.from(alerts.values()).map(({ id, label, schedule, actions, eventName }) => ({
    id, label, schedule, actions, eventName
  }))
}

/**
 * Subscribe to named events pushed by 'event'-action alerts.
 * @param {string} eventName
 * @param {Function} callback - Called with { alertId, eventName, timestamp, message }
 * @returns {Function} Unsubscribe
 */
export function subscribeToEvent(eventName, callback) {
  if (!eventSubscribers.has(eventName)) {
    eventSubscribers.set(eventName, new Set())
  }
  eventSubscribers.get(eventName).add(callback)
  return () => {
    const subs = eventSubscribers.get(eventName)
    if (subs) {
      subs.delete(callback)
      if (subs.size === 0) eventSubscribers.delete(eventName)
    }
  }
}

/**
 * Subscribe to screen message notifications.
 * @param {Function} callback - Called with { id, alertId, label, message, timestamp }
 * @returns {Function} Unsubscribe
 */
export function subscribeToMessages(callback) {
  messageSubscribers.add(callback)
  return () => messageSubscribers.delete(callback)
}

