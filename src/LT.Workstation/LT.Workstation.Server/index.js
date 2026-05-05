import express from 'express'
import cors from 'cors'
import { createRequire } from 'module'
import { existsSync } from 'fs'
import path from 'path'
import marketRoutes from './routes/market.js'
import fomcRoutes from './routes/fomc.js'
import treasuryRoutes from './routes/treasury.js'
import fearGreedRoutes from './routes/fearGreed.js'
import routesRoutes from './routes/routes.js'
import userDataRoutes from './routes/userData.js'
import accountRoutes from './routes/account.js'
import aiRoutes from './routes/ai.js'
import transcribeRoutes from './routes/transcribe.js'
import licenseRoutes from './routes/license.js'
import feedbackRoutes from './routes/feedback.js'
import { getConnectionStatus } from './ibkrClient.js'

const require = createRequire(import.meta.url)
const { version } = require('./package.json')

const app = express()
const PORT = process.env.PORT || 8744

app.use(cors())
app.use(express.json())

// API Routes
app.use('/api', marketRoutes)
app.use('/api/fomc', fomcRoutes)
app.use('/api/treasury', treasuryRoutes)
app.use('/api/fear-greed', fearGreedRoutes)
app.use('/api/routes', routesRoutes)
app.use('/api/user-data', userDataRoutes)
app.use('/api/account', accountRoutes)
app.use('/api/ai', aiRoutes)
app.use('/api/ai', transcribeRoutes)
app.use('/api/license', licenseRoutes)
app.use('/api/feedback', feedbackRoutes)

// Version
app.get('/api/version', (_req, res) => res.json({ version }))

// Health check
app.get('/health', (req, res) => {
  res.json({ status: 'ok', timestamp: new Date().toISOString() })
})

// Status endpoint
app.get('/api/status', (req, res) => {
  const status = getConnectionStatus()
  res.json(status)
})

// Serve bundled frontend in Electron production builds
if (process.env.STATIC_DIR && existsSync(process.env.STATIC_DIR)) {
  app.use(express.static(process.env.STATIC_DIR))
  app.get('*', (_req, res) => {
    res.sendFile(path.join(process.env.STATIC_DIR, 'index.html'))
  })
}

// Error handling
app.use((err, req, res, next) => {
  console.error('Server error:', err)
  res.status(500).json({ error: err.message })
})

app.listen(PORT, () => {
  console.log(`Limitless Trading server running on port ${PORT}`)
  console.log(`API available at http://localhost:${PORT}/api`)
})
