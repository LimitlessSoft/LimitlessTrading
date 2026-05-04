import { existsSync, readdirSync, mkdirSync, createWriteStream, readFileSync, unlinkSync } from 'fs'
import { join } from 'path'
import { homedir } from 'os'
import { execFile } from 'child_process'
import { get as httpsGet } from 'https'

const BINARY_SEARCH_PATHS = [
  join(homedir(), 'whisper.cpp', 'build', 'bin', 'whisper-cli'),
  join(homedir(), 'whisper.cpp', 'build', 'bin', 'whisper'),
  join(homedir(), 'whisper.cpp', 'main'),
  '/usr/local/bin/whisper-cli',
  '/usr/local/bin/whisper',
  '/usr/bin/whisper-cli',
  '/usr/bin/whisper',
  '/opt/homebrew/bin/whisper-cli',
  '/opt/homebrew/bin/whisper',
]

const MODEL_SEARCH_DIRS = [
  join(homedir(), 'whisper.cpp', 'models'),
  join(homedir(), '.cache', 'whisper'),
  join(homedir(), '.local', 'share', 'whisper'),
]

export const AVAILABLE_MODELS = [
  { name: 'tiny.en',    label: 'Tiny (English only)',        sizeMb: 75   },
  { name: 'tiny',       label: 'Tiny (multilingual)',        sizeMb: 75   },
  { name: 'base.en',    label: 'Base (English only)',        sizeMb: 142  },
  { name: 'base',       label: 'Base (multilingual)',        sizeMb: 142  },
  { name: 'small.en',   label: 'Small (English only)',       sizeMb: 466  },
  { name: 'small',      label: 'Small (multilingual)',       sizeMb: 466  },
  { name: 'medium.en',  label: 'Medium (English only)',      sizeMb: 1500 },
  { name: 'medium',     label: 'Medium (multilingual)',      sizeMb: 1500 },
  { name: 'large-v3',   label: 'Large v3 (best accuracy)',   sizeMb: 3100 },
]

export function detectBinary(customPath) {
  if (customPath && existsSync(customPath)) return customPath
  for (const p of BINARY_SEARCH_PATHS) {
    if (existsSync(p)) return p
  }
  return null
}

export function detectModelsDir(customDir) {
  if (customDir && existsSync(customDir)) return customDir
  for (const dir of MODEL_SEARCH_DIRS) {
    if (existsSync(dir)) return dir
  }
  return join(homedir(), '.cache', 'whisper')
}

export function listLocalModels(modelsDir) {
  if (!existsSync(modelsDir)) return []
  try {
    return readdirSync(modelsDir)
      .filter(f => f.startsWith('ggml-') && f.endsWith('.bin'))
      .map(f => f.replace(/^ggml-/, '').replace(/\.bin$/, ''))
  } catch {
    return []
  }
}

export function transcribe({ binaryPath, modelPath, audioPath }) {
  return new Promise((resolve, reject) => {
    const outputPrefix = audioPath.replace(/\.wav$/, '')
    execFile(
      binaryPath,
      ['-m', modelPath, '-f', audioPath, '-otxt', '-of', outputPrefix],
      { timeout: 60000 },
      (err, stdout, stderr) => {
        if (err) {
          reject(new Error(stderr?.trim() || err.message))
          return
        }
        const outputFile = outputPrefix + '.txt'
        try {
          const text = readFileSync(outputFile, 'utf-8').trim()
          try { unlinkSync(outputFile) } catch {}
          resolve(text)
        } catch {
          resolve(stdout.trim())
        }
      }
    )
  })
}

export function downloadModel(modelName, modelsDir, onProgress) {
  return new Promise((resolve, reject) => {
    if (!existsSync(modelsDir)) {
      try { mkdirSync(modelsDir, { recursive: true }) } catch (e) { return reject(e) }
    }

    const destPath = join(modelsDir, `ggml-${modelName}.bin`)
    const url = `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-${modelName}.bin`

    function doGet(url, redirects = 0) {
      if (redirects > 10) return reject(new Error('Too many redirects'))
      httpsGet(url, res => {
        if (res.statusCode === 301 || res.statusCode === 302) {
          return doGet(res.headers.location, redirects + 1)
        }
        if (res.statusCode !== 200) {
          return reject(new Error(`Download failed: HTTP ${res.statusCode}`))
        }
        const total = parseInt(res.headers['content-length'] || '0', 10)
        let received = 0
        const stream = createWriteStream(destPath)
        res.on('data', chunk => {
          received += chunk.length
          if (total > 0) onProgress(Math.round(received / total * 100))
        })
        res.pipe(stream)
        stream.on('finish', () => resolve(destPath))
        stream.on('error', reject)
        res.on('error', reject)
      }).on('error', reject)
    }

    doGet(url)
  })
}
