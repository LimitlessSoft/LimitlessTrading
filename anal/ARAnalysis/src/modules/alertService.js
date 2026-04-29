import { Cron } from 'croner'
import { getPersistedAlerts, persistAlert, deletePersistedAlert } from '../db/database'

const alerts = new Map()
const eventSubscribers = new Map()
const messageSubscribers = new Set()
const listSubscribers = new Set()

function notifyListChange() {
  for (const cb of listSubscribers) {
    try { cb() } catch {}
  }
}

let nextAlertId = 1

;(async () => {
  try {
    const saved = await getPersistedAlerts()
    for (const config of saved) {
      const alert = {
        ...config,
        getMessage: config.message ? () => config.message : undefined,
        _cronJob: null,
      }
      alerts.set(alert.id, alert)
      scheduleAlert(alert)
      const match = alert.id.match(/^alert-(\d+)$/)
      if (match) {
        const n = parseInt(match[1], 10)
        if (n >= nextAlertId) nextAlertId = n + 1
      }
    }
    notifyListChange()
  } catch {
    // DB unavailable — start with no alerts
  }
})()

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
    setTimeout(() => ctx.close(), (duration + 0.5) * 1000)
  } catch {
    // Web Audio not supported or blocked
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
      const payload = { alertId: alert.id, eventName: alert.eventName, timestamp: new Date(), message }
      for (const cb of subs) {
        try { cb(payload) } catch {}
      }
    }
  }
}

function scheduleAlert(alert) {
  const { schedule } = alert
  if (!schedule?.expression) return
  try {
    alert._cronJob = new Cron(schedule.expression, () => fireAlert(alert))
  } catch {
    // Invalid cron expression — skip
  }
}

export function createAlert(config) {
  const id = config.id || `alert-${nextAlertId++}`
  const alert = { ...config, id, _cronJob: null }
  alerts.set(id, alert)
  scheduleAlert(alert)
  persistAlert(alert).catch(() => {})
  notifyListChange()
  return id
}

export function removeAlert(id) {
  const alert = alerts.get(id)
  if (!alert) return
  alert._cronJob?.stop()
  alerts.delete(id)
  deletePersistedAlert(id).catch(() => {})
  notifyListChange()
}

export function listAlerts() {
  return Array.from(alerts.values()).map(({ id, label, schedule, actions, eventName }) => ({
    id, label, schedule, actions, eventName
  }))
}

export function getNextRun(expression) {
  try {
    return new Cron(expression).nextRun()
  } catch {
    return null
  }
}

export function isValidCron(expression) {
  try {
    new Cron(expression)
    return true
  } catch {
    return false
  }
}

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

export function subscribeToMessages(callback) {
  messageSubscribers.add(callback)
  return () => messageSubscribers.delete(callback)
}

export function subscribeToAlertList(callback) {
  listSubscribers.add(callback)
  return () => listSubscribers.delete(callback)
}
