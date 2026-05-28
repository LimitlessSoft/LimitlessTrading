const { app, BrowserWindow, shell, Menu } = require('electron')
const { spawn } = require('child_process')
const path = require('path')
const http = require('http')

const isDev = !app.isPackaged

let mainWindow = null
let serverProcess = null

function startServer() {
  const serverDir = isDev
    ? path.join(__dirname, '..', 'LT.Workstation.Server')
    : path.join(process.resourcesPath, 'server')

  const staticDir = isDev
    ? undefined
    : path.join(process.resourcesPath, 'app')

  serverProcess = spawn('node', ['index.js'], {
    cwd: serverDir,
    env: {
      ...process.env,
      PORT: '8744',
      ...(staticDir ? { STATIC_DIR: staticDir } : {})
    },
    stdio: 'pipe'
  })

  serverProcess.stdout.on('data', (data) => {
    console.log('[server]', data.toString().trim())
  })

  serverProcess.stderr.on('data', (data) => {
    console.error('[server]', data.toString().trim())
  })

  serverProcess.on('exit', (code) => {
    console.log(`[server] exited with code ${code}`)
  })
}

function waitForServer(url, retries, delay) {
  return new Promise((resolve, reject) => {
    const attempt = (remaining) => {
      http.get(url, (res) => {
        resolve()
      }).on('error', () => {
        if (remaining <= 0) return reject(new Error('Server did not start in time'))
        setTimeout(() => attempt(remaining - 1), delay)
      })
    }
    attempt(retries)
  })
}

async function createWindow() {
  Menu.setApplicationMenu(null)

  mainWindow = new BrowserWindow({
    width: 1600,
    height: 960,
    minWidth: 1024,
    minHeight: 600,
    title: 'LT Workstation',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    },
    titleBarStyle: process.platform === 'darwin' ? 'hiddenInset' : 'default',
    backgroundColor: '#1a202c'
  })

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url)
    return { action: 'deny' }
  })

  if (isDev) {
    mainWindow.loadURL('http://localhost:8743')
    mainWindow.webContents.openDevTools({ mode: 'detach' })
  } else {
    await waitForServer('http://localhost:8744/api/status', 30, 500).catch(() => {})
    mainWindow.loadURL('http://localhost:8744')
  }
}

app.whenReady().then(async () => {
  if (!isDev) startServer()

  if (!isDev) {
    // In production, give the server a moment before opening the window
    await new Promise((resolve) => setTimeout(resolve, 1000))
  }

  await createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('will-quit', () => {
  if (serverProcess) {
    serverProcess.kill()
    serverProcess = null
  }
})
