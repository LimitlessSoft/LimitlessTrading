import { Router } from 'express'

const router = Router()

// FOMC decision dates - 2:00 PM ET on the second day of each meeting
// Update annually when Fed releases new calendar
const FOMC_DATES = [
  // 2026
  '2026-01-28T14:00:00-05:00', // EST
  '2026-03-18T14:00:00-04:00', // EDT
  '2026-04-29T14:00:00-04:00',
  '2026-06-17T14:00:00-04:00',
  '2026-07-29T14:00:00-04:00',
  '2026-09-16T14:00:00-04:00',
  '2026-10-28T14:00:00-04:00',
  '2026-12-09T14:00:00-05:00', // EST
  // 2027 (add when available)
]

// Get FOMC dates endpoint
router.get('/dates', (req, res) => {
  const now = new Date()
  const futureDates = FOMC_DATES.filter(d => new Date(d) > now)
  res.json({ dates: futureDates })
})

export default router
