import { useState, useEffect, useRef } from 'react'
import { listAlerts, triggerAlert, subscribeToAlertList, setCombinedCronCallback } from '../modules/alertService'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'
import { testRule } from '../modules/conditionalStyles'

function getWatchedAlerts() {
  return listAlerts().filter(a => {
    if (a.enabled === false) return false
    const t = a.schedule?.type
    if (t === 'expression') return true
    if (t === 'combined') return a.schedule.conditions.some(c => c.type === 'expression')
    return false
  })
}

function evalExprConditions(schedule, resolveText) {
  const conditions = schedule.type === 'expression'
    ? [schedule]
    : schedule.conditions.filter(c => c.type === 'expression')

  const results = conditions.map(c => testRule(resolveText(c.expression), c))

  if (results.length === 0) return true // no expression conditions (pure cron combined)
  return schedule.type === 'combined' && schedule.combinator === 'or'
    ? results.some(Boolean)
    : results.every(Boolean)
}

export default function AlertExpressionWatcher() {
  const [watchedAlerts, setWatchedAlerts] = useState(getWatchedAlerts)

  useEffect(() => subscribeToAlertList(() => setWatchedAlerts(getWatchedAlerts())), [])

  const allText = watchedAlerts
    .flatMap(a => {
      const cs = a.schedule.type === 'expression' ? [a.schedule] : a.schedule.conditions
      return cs.filter(c => c.type === 'expression').map(c => c.expression)
    })
    .join(' ')

  const { resolveText } = useWidgetResolveText(allText)

  const firedRef = useRef(new Set())
  const resolveTextRef = useRef(resolveText)
  resolveTextRef.current = resolveText

  const watchedAlertsRef = useRef(watchedAlerts)
  watchedAlertsRef.current = watchedAlerts

  // Continuous evaluation for expression conditions
  useEffect(() => {
    for (const alert of watchedAlerts) {
      // Combined AND with cron sub-conditions: only fire on cron tick, not continuously
      if (
        alert.schedule.type === 'combined' &&
        alert.schedule.combinator === 'and' &&
        alert.schedule.conditions.some(c => c.type === 'cron')
      ) continue

      const met = evalExprConditions(alert.schedule, resolveText)
      if (met && !firedRef.current.has(alert.id)) {
        firedRef.current.add(alert.id)
        triggerAlert(alert.id)
      } else if (!met) {
        firedRef.current.delete(alert.id)
      }
    }
  }, [resolveText, watchedAlerts])

  // Handle cron-triggered combined AND: check expression conditions at cron tick time
  useEffect(() => {
    setCombinedCronCallback((alertId) => {
      const alert = watchedAlertsRef.current.find(a => a.id === alertId)
      if (!alert) return
      const met = evalExprConditions(alert.schedule, resolveTextRef.current)
      if (met) triggerAlert(alertId)
    })
    return () => setCombinedCronCallback(null)
  }, [])

  return null
}
