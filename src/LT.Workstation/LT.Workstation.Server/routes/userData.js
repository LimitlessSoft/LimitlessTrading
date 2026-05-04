import express from 'express'
import { readFileSync, writeFileSync } from 'fs'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const DATA_PATH = join(__dirname, '../data/user-data.json')

const DEFAULT_DATA = {
  liveWatchlist: [],
  favorites: [],
  settings: {},
  conditionalFormats: [],
  formatGroups: [],
  analysisPresets: [],
  watchgroups: [],
  watchgroupMembers: [],
  nextIds: { conditionalFormats: 1, formatGroups: 1, analysisPresets: 1, watchgroups: 1, watchgroupMembers: 1 }
}

function readData() {
  try {
    const data = JSON.parse(readFileSync(DATA_PATH, 'utf-8'))
    return { ...DEFAULT_DATA, ...data, nextIds: { ...DEFAULT_DATA.nextIds, ...data.nextIds } }
  } catch {
    return { ...DEFAULT_DATA }
  }
}

function writeData(data) {
  writeFileSync(DATA_PATH, JSON.stringify(data, null, 2))
}

// Live Watchlist

router.get('/watchlist', (req, res) => {
  const data = readData()
  res.json(data.liveWatchlist.sort((a, b) => (a.order || 0) - (b.order || 0)))
})

router.post('/watchlist', (req, res) => {
  const { symbol } = req.body
  if (!symbol) return res.status(400).json({ error: 'symbol required' })
  const upper = symbol.toUpperCase().trim()
  const data = readData()
  const existing = data.liveWatchlist.find(i => i.symbol === upper)
  if (existing) return res.json(existing)
  const maxOrder = data.liveWatchlist.reduce((max, i) => Math.max(max, i.order || 0), -1)
  const item = { symbol: upper, order: maxOrder + 1 }
  data.liveWatchlist.push(item)
  writeData(data)
  res.status(201).json(item)
})

router.delete('/watchlist/:symbol', (req, res) => {
  const upper = req.params.symbol.toUpperCase()
  const data = readData()
  data.liveWatchlist = data.liveWatchlist.filter(i => i.symbol !== upper)
  writeData(data)
  res.status(204).send()
})

router.put('/watchlist/reorder', (req, res) => {
  const { symbols } = req.body
  if (!Array.isArray(symbols)) return res.status(400).json({ error: 'symbols array required' })
  const data = readData()
  const updated = symbols.map((s, i) => ({ symbol: s.toUpperCase(), order: i }))
  const included = new Set(updated.map(i => i.symbol))
  data.liveWatchlist = [...updated, ...data.liveWatchlist.filter(i => !included.has(i.symbol))]
  writeData(data)
  res.json(updated)
})

// Favorites

router.get('/favorites', (req, res) => {
  const data = readData()
  res.json(data.favorites)
})

router.post('/favorites', (req, res) => {
  const { symbol } = req.body
  if (!symbol) return res.status(400).json({ error: 'symbol required' })
  const upper = symbol.toUpperCase()
  const data = readData()
  if (!data.favorites.includes(upper)) {
    data.favorites.push(upper)
    writeData(data)
  }
  res.status(201).json({ symbol: upper })
})

router.delete('/favorites/:symbol', (req, res) => {
  const upper = req.params.symbol.toUpperCase()
  const data = readData()
  data.favorites = data.favorites.filter(s => s !== upper)
  writeData(data)
  res.status(204).send()
})

// Settings

router.get('/settings', (req, res) => {
  const data = readData()
  res.json(data.settings)
})

router.get('/settings/:key', (req, res) => {
  const data = readData()
  res.json({ key: req.params.key, value: data.settings[req.params.key] ?? null })
})

router.put('/settings/:key', (req, res) => {
  const { value } = req.body
  const data = readData()
  data.settings[req.params.key] = value
  writeData(data)
  res.json({ key: req.params.key, value })
})

// Conditional Formats

router.get('/conditional-formats', (req, res) => {
  const data = readData()
  const { groupId } = req.query
  const formats = groupId
    ? data.conditionalFormats.filter(f => f.groupId === Number(groupId))
    : data.conditionalFormats
  res.json(formats)
})

router.post('/conditional-formats', (req, res) => {
  const data = readData()
  const id = data.nextIds.conditionalFormats++
  const rule = { ...req.body, id }
  data.conditionalFormats.push(rule)
  writeData(data)
  res.status(201).json(rule)
})

router.put('/conditional-formats/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  const idx = data.conditionalFormats.findIndex(f => f.id === id)
  if (idx === -1) return res.status(404).json({ error: 'Not found' })
  data.conditionalFormats[idx] = { ...data.conditionalFormats[idx], ...req.body, id }
  writeData(data)
  res.json(data.conditionalFormats[idx])
})

router.delete('/conditional-formats/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  data.conditionalFormats = data.conditionalFormats.filter(f => f.id !== id)
  writeData(data)
  res.status(204).send()
})

// Format Groups

router.get('/format-groups', (req, res) => {
  const data = readData()
  res.json(data.formatGroups)
})

router.post('/format-groups', (req, res) => {
  const { name } = req.body
  if (!name) return res.status(400).json({ error: 'name required' })
  const data = readData()
  const id = data.nextIds.formatGroups++
  const group = { id, name }
  data.formatGroups.push(group)
  writeData(data)
  res.status(201).json(group)
})

router.put('/format-groups/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  const idx = data.formatGroups.findIndex(g => g.id === id)
  if (idx === -1) return res.status(404).json({ error: 'Not found' })
  data.formatGroups[idx] = { ...data.formatGroups[idx], ...req.body, id }
  writeData(data)
  res.json(data.formatGroups[idx])
})

router.delete('/format-groups/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  data.conditionalFormats = data.conditionalFormats.filter(f => f.groupId !== id)
  data.formatGroups = data.formatGroups.filter(g => g.id !== id)
  writeData(data)
  res.status(204).send()
})

// Analysis Presets

router.get('/analysis-presets', (req, res) => {
  const data = readData()
  res.json(data.analysisPresets)
})

router.post('/analysis-presets', (req, res) => {
  const data = readData()
  const id = data.nextIds.analysisPresets++
  const preset = { ...req.body, id }
  data.analysisPresets.push(preset)
  writeData(data)
  res.status(201).json(preset)
})

router.put('/analysis-presets/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  const idx = data.analysisPresets.findIndex(p => p.id === id)
  if (idx === -1) return res.status(404).json({ error: 'Not found' })
  data.analysisPresets[idx] = { ...data.analysisPresets[idx], ...req.body, id }
  writeData(data)
  res.json(data.analysisPresets[idx])
})

router.delete('/analysis-presets/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  data.analysisPresets = data.analysisPresets.filter(p => p.id !== id)
  writeData(data)
  res.status(204).send()
})

// Watchgroups

router.get('/watchgroups', (req, res) => {
  const data = readData()
  res.json(data.watchgroups)
})

router.post('/watchgroups', (req, res) => {
  const { name } = req.body
  if (!name) return res.status(400).json({ error: 'name required' })
  const data = readData()
  const id = data.nextIds.watchgroups++
  const group = { id, name }
  data.watchgroups.push(group)
  writeData(data)
  res.status(201).json(group)
})

router.put('/watchgroups/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  const idx = data.watchgroups.findIndex(g => g.id === id)
  if (idx === -1) return res.status(404).json({ error: 'Not found' })
  data.watchgroups[idx] = { ...data.watchgroups[idx], ...req.body, id }
  writeData(data)
  res.json(data.watchgroups[idx])
})

router.delete('/watchgroups/:id', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  data.watchgroupMembers = data.watchgroupMembers.filter(m => m.groupId !== id)
  data.watchgroups = data.watchgroups.filter(g => g.id !== id)
  writeData(data)
  res.status(204).send()
})

// Watchgroup Members

router.get('/watchgroups/:id/symbols', (req, res) => {
  const id = Number(req.params.id)
  const data = readData()
  const symbols = data.watchgroupMembers
    .filter(m => m.groupId === id)
    .map(m => m.symbol)
  res.json(symbols)
})

router.post('/watchgroups/:id/symbols', (req, res) => {
  const groupId = Number(req.params.id)
  const { symbol } = req.body
  if (!symbol) return res.status(400).json({ error: 'symbol required' })
  const upper = symbol.toUpperCase().trim()
  const data = readData()
  const exists = data.watchgroupMembers.some(m => m.groupId === groupId && m.symbol === upper)
  if (!exists) {
    const id = data.nextIds.watchgroupMembers++
    data.watchgroupMembers.push({ id, groupId, symbol: upper })
    writeData(data)
  }
  res.status(201).json({ groupId, symbol: upper })
})

router.delete('/watchgroups/:id/symbols/:symbol', (req, res) => {
  const groupId = Number(req.params.id)
  const symbol = req.params.symbol.toUpperCase()
  const data = readData()
  data.watchgroupMembers = data.watchgroupMembers.filter(
    m => !(m.groupId === groupId && m.symbol === symbol)
  )
  writeData(data)
  res.status(204).send()
})

router.get('/watchgroups/by-symbol/:symbol', (req, res) => {
  const symbol = req.params.symbol.toUpperCase()
  const data = readData()
  const groupIds = data.watchgroupMembers
    .filter(m => m.symbol === symbol)
    .map(m => m.groupId)
  const groups = data.watchgroups.filter(g => groupIds.includes(g.id))
  res.json(groups)
})

export default router
