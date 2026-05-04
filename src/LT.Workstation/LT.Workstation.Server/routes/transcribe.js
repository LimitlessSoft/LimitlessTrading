import express from 'express'
import multer from 'multer'
import { readFileSync, writeFileSync, unlinkSync } from 'fs'
import { join } from 'path'
import { tmpdir, homedir } from 'os'
import { fileURLToPath } from 'url'
import { dirname } from 'path'
import { randomUUID } from 'crypto'
import {
  AVAILABLE_MODELS,
  detectBinary,
  detectModelsDir,
  listLocalModels,
  transcribe,
  downloadModel,
} from '../ai/whisper.js'

const router = express.Router()

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const DATA_PATH = join(__dirname, '../data/user-data.json')

function readSettings() {
  try {
    return JSON.parse(readFileSync(DATA_PATH, 'utf-8')).settings || {}
  } catch {
    return {}
  }
}

const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: 25 * 1024 * 1024 },
})

router.get('/whisper/status', (req, res) => {
  const settings = readSettings()
  const customBinaryPath = settings['ai.whisper.binaryPath'] || ''
  const customModelsDir = settings['ai.whisper.modelsDir'] || ''
  const activeModel = settings['ai.whisper.model'] || ''

  const binaryPath = detectBinary(customBinaryPath)
  const modelsDir = detectModelsDir(customModelsDir)
  const localModels = listLocalModels(modelsDir)

  res.json({
    binaryPath,
    modelsDir,
    localModels,
    activeModel,
    customBinaryPath,
    customModelsDir,
    availableModels: AVAILABLE_MODELS,
  })
})

router.get('/whisper/download-model', (req, res) => {
  const { name } = req.query
  if (!name || !AVAILABLE_MODELS.find(m => m.name === name)) {
    return res.status(400).json({ error: 'Invalid model name' })
  }

  const settings = readSettings()
  const modelsDir = detectModelsDir(settings['ai.whisper.modelsDir'] || '')

  res.setHeader('Content-Type', 'text/event-stream')
  res.setHeader('Cache-Control', 'no-cache')
  res.setHeader('Connection', 'keep-alive')
  res.flushHeaders()

  function send(data) {
    res.write(`data: ${JSON.stringify(data)}\n\n`)
  }

  downloadModel(name, modelsDir, progress => send({ type: 'progress', progress }))
    .then(() => {
      send({ type: 'done' })
      res.end()
    })
    .catch(err => {
      send({ type: 'error', message: err.message })
      res.end()
    })
})

router.post('/transcribe', upload.single('audio'), async (req, res) => {
  if (!req.file) return res.status(400).json({ error: 'No audio provided' })

  const settings = readSettings()
  const binaryPath = detectBinary(settings['ai.whisper.binaryPath'] || '')
  if (!binaryPath) {
    return res.status(400).json({ error: 'whisper-cli not found. Configure voice input in AI settings.' })
  }

  const modelsDir = detectModelsDir(settings['ai.whisper.modelsDir'] || '')
  const localModels = listLocalModels(modelsDir)
  const modelName = settings['ai.whisper.model'] || localModels[0]
  if (!modelName) {
    return res.status(400).json({ error: 'No whisper model found. Download one in AI settings.' })
  }

  const modelPath = join(modelsDir, `ggml-${modelName}.bin`)
  const audioPath = join(tmpdir(), `whisper-${randomUUID()}.wav`)

  try {
    writeFileSync(audioPath, req.file.buffer)
    const text = await transcribe({ binaryPath, modelPath, audioPath })
    res.json({ text })
  } catch (err) {
    res.status(500).json({ error: err.message })
  } finally {
    try { unlinkSync(audioPath) } catch {}
  }
})

export default router
