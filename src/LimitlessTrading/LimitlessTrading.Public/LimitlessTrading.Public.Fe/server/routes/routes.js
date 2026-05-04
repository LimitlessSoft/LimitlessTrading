import express from 'express'
import { readFileSync, writeFileSync } from 'fs'
import { randomUUID } from 'crypto'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const CONFIG_PATH = join(__dirname, '../data/routes-config.json')

function readConfig() {
  try {
    const data = readFileSync(CONFIG_PATH, 'utf-8')
    return JSON.parse(data)
  } catch (err) {
    return { routes: [] }
  }
}

function writeConfig(config) {
  writeFileSync(CONFIG_PATH, JSON.stringify(config, null, 2))
}

// GET /api/routes - List all routes
router.get('/', (req, res) => {
  const config = readConfig()
  res.json(config.routes)
})

// POST /api/routes - Create route
router.post('/', (req, res) => {
  const { name, path, config: routeConfig = {} } = req.body

  if (!name || !path) {
    return res.status(400).json({ error: 'Name and path are required' })
  }

  if (!path.startsWith('/')) {
    return res.status(400).json({ error: 'Path must start with /' })
  }

  const configData = readConfig()

  // Check for path uniqueness
  if (configData.routes.some(r => r.path === path)) {
    return res.status(400).json({ error: 'Path already exists' })
  }

  const now = Date.now()
  const newRoute = {
    id: randomUUID(),
    path,
    name,
    createdAt: now,
    updatedAt: now,
    config: routeConfig
  }

  configData.routes.push(newRoute)
  writeConfig(configData)

  res.status(201).json(newRoute)
})

// PUT /api/routes/:id - Update route
router.put('/:id', (req, res) => {
  const { id } = req.params
  const { name, path, config: routeConfig } = req.body

  const configData = readConfig()
  const routeIndex = configData.routes.findIndex(r => r.id === id)

  if (routeIndex === -1) {
    return res.status(404).json({ error: 'Route not found' })
  }

  if (path && !path.startsWith('/')) {
    return res.status(400).json({ error: 'Path must start with /' })
  }

  // Check for path uniqueness if path is being changed
  if (path && configData.routes.some(r => r.path === path && r.id !== id)) {
    return res.status(400).json({ error: 'Path already exists' })
  }

  const existingRoute = configData.routes[routeIndex]
  const updatedRoute = {
    ...existingRoute,
    ...(name && { name }),
    ...(path && { path }),
    ...(routeConfig && { config: routeConfig }),
    updatedAt: Date.now()
  }

  configData.routes[routeIndex] = updatedRoute
  writeConfig(configData)

  res.json(updatedRoute)
})

// DELETE /api/routes/:id - Delete route
router.delete('/:id', (req, res) => {
  const { id } = req.params

  const configData = readConfig()
  const routeIndex = configData.routes.findIndex(r => r.id === id)

  if (routeIndex === -1) {
    return res.status(404).json({ error: 'Route not found' })
  }

  configData.routes.splice(routeIndex, 1)
  writeConfig(configData)

  res.status(204).send()
})

export default router
