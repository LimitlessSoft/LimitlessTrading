import express from 'express'
import cors from 'cors'
import marketRoutes from './routes/market.js'
import fomcRoutes from './routes/fomc.js'
import treasuryRoutes from './routes/treasury.js'
import fearGreedRoutes from './routes/fearGreed.js'
import routesRoutes from './routes/routes.js'
import { getConnectionStatus } from './ibkrClient.js'

const app = express()
const PORT = process.env.PORT || 3001

app.use(cors())
app.use(express.json())

// API Routes
app.use('/api', marketRoutes)
app.use('/api/fomc', fomcRoutes)
app.use('/api/treasury', treasuryRoutes)
app.use('/api/fear-greed', fearGreedRoutes)
app.use('/api/routes', routesRoutes)

// Health check
app.get('/health', (req, res) => {
  res.json({ status: 'ok', timestamp: new Date().toISOString() })
})

// Status endpoint
app.get('/api/status', (req, res) => {
  const status = getConnectionStatus()
  res.json(status)
})

// Error handling
app.use((err, req, res, next) => {
  console.error('Server error:', err)
  res.status(500).json({ error: err.message })
})

app.listen(PORT, () => {
  console.log(`ARAnalysis server running on port ${PORT}`)
  console.log(`API available at http://localhost:${PORT}/api`)
})
