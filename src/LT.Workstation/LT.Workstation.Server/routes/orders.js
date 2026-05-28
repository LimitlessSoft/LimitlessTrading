import express from 'express'
import { placeOrder } from '../ibkrClient.js'

const router = express.Router()

const VALID_ORDER_TYPES = new Set(['MKT', 'LMT', 'STP', 'STP_LMT'])
const VALID_ACTIONS = new Set(['BUY', 'SELL'])

router.post('/', async (req, res) => {
  const { action, symbol, quantity, orderType, limitPrice, stopPrice } = req.body

  if (!VALID_ACTIONS.has(action)) {
    return res.status(400).json({ error: `Invalid action: ${action}` })
  }
  if (!symbol || typeof symbol !== 'string') {
    return res.status(400).json({ error: 'symbol is required' })
  }
  if (!quantity || isNaN(Number(quantity)) || Number(quantity) <= 0) {
    return res.status(400).json({ error: 'quantity must be a positive number' })
  }
  if (!VALID_ORDER_TYPES.has(orderType)) {
    return res.status(400).json({ error: `Invalid orderType: ${orderType}` })
  }
  if ((orderType === 'LMT' || orderType === 'STP_LMT') && (limitPrice == null || isNaN(Number(limitPrice)))) {
    return res.status(400).json({ error: 'limitPrice is required for LMT and STP_LMT orders' })
  }
  if ((orderType === 'STP' || orderType === 'STP_LMT') && (stopPrice == null || isNaN(Number(stopPrice)))) {
    return res.status(400).json({ error: 'stopPrice is required for STP and STP_LMT orders' })
  }

  try {
    const result = await placeOrder({ action, symbol, quantity, orderType, limitPrice, stopPrice })
    res.json(result)
  } catch (err) {
    res.status(503).json({ error: err.message })
  }
})

export default router
