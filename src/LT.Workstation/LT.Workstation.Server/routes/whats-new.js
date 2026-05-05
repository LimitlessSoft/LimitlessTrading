import express from 'express'

const router = express.Router()
const PUBLIC_API_URL = process.env.AR_PUBLIC_API_URL || 'http://localhost:5096'

router.get('/', async (req, res) => {
  try {
    const params = new URLSearchParams()
    if (req.query.limit) params.set('limit', req.query.limit)
    if (req.query.offset) params.set('offset', req.query.offset)
    const qs = params.toString()
    const response = await fetch(`${PUBLIC_API_URL}/api/whats-new${qs ? '?' + qs : ''}`)
    const data = await response.json()
    res.status(response.status).json(data)
  } catch {
    res.status(502).json({ error: 'Could not reach API' })
  }
})

export default router
