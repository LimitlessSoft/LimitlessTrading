const MAX = 50

const consoleLogs = []
const networkRequests = []

function ts() {
  return new Date().toISOString()
}

function serialize(a) {
  if (a instanceof Error) return `${a.name}: ${a.message}`
  try { return typeof a === 'object' && a !== null ? JSON.stringify(a) : String(a) } catch { return String(a) }
}

function pushCapped(arr, entry) {
  arr.push(entry)
  if (arr.length > MAX) arr.shift()
}

function formatBytes(n) {
  if (n == null) return null
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`
  return `${(n / (1024 * 1024)).toFixed(1)} MB`
}

function bodySize(body) {
  if (body == null) return null
  if (typeof body === 'string') return body.length
  if (body instanceof ArrayBuffer) return body.byteLength
  if (body instanceof Blob) return body.size
  if (body instanceof URLSearchParams) return body.toString().length
  return null
}

for (const level of ['log', 'warn', 'error', 'info', 'debug']) {
  const orig = console[level]
  console[level] = (...args) => {
    pushCapped(consoleLogs, { time: ts(), level, message: args.map(serialize).join(' ') })
    orig.apply(console, args)
  }
}

const origFetch = window.fetch
window.fetch = async function (...args) {
  const req = args[0] instanceof Request ? args[0] : null
  const url = req ? req.url : String(args[0] ?? '?')
  const method = (req ? req.method : args[1]?.method ?? 'GET').toUpperCase()
  const reqBodySize = formatBytes(bodySize(req ? req.body : args[1]?.body))
  const start = Date.now()
  try {
    const res = await origFetch.apply(this, args)
    const contentLength = res.headers.get('content-length')
    pushCapped(networkRequests, {
      time: ts(),
      method,
      url,
      status: res.status,
      statusText: res.statusText || null,
      ms: Date.now() - start,
      responseSize: formatBytes(contentLength != null ? parseInt(contentLength, 10) : null),
      requestSize: reqBodySize,
      error: null,
    })
    return res
  } catch (err) {
    pushCapped(networkRequests, {
      time: ts(),
      method,
      url,
      status: 'ERR',
      statusText: null,
      ms: Date.now() - start,
      responseSize: null,
      requestSize: reqBodySize,
      error: err?.message ?? String(err),
    })
    throw err
  }
}

const OrigXHR = window.XMLHttpRequest
window.XMLHttpRequest = function () {
  const xhr = new OrigXHR()
  let method = 'GET', url = '?', start = Date.now(), reqBodySize = null
  const origOpen = xhr.open.bind(xhr)
  xhr.open = function (m, u, ...rest) {
    method = m.toUpperCase()
    url = String(u)
    start = Date.now()
    return origOpen(m, u, ...rest)
  }
  const origSend = xhr.send.bind(xhr)
  xhr.send = function (body) {
    reqBodySize = formatBytes(bodySize(body))
    return origSend(body)
  }
  xhr.addEventListener('loadend', () => {
    const contentLength = xhr.getResponseHeader('content-length')
    pushCapped(networkRequests, {
      time: ts(),
      method,
      url,
      status: xhr.status || 'ERR',
      statusText: xhr.statusText || null,
      ms: Date.now() - start,
      responseSize: formatBytes(contentLength != null ? parseInt(contentLength, 10) : null),
      requestSize: reqBodySize,
      error: xhr.status === 0 ? 'Network error' : null,
    })
  })
  return xhr
}

export function getConsoleLogs() { return [...consoleLogs] }
export function getNetworkRequests() { return [...networkRequests] }
