import express from 'express'
import { getCachedCustomerEmail, getCachedLicenseStatus } from './license.js'

const router = express.Router()
const PUBLIC_API_URL = process.env.AR_PUBLIC_API_URL || 'http://localhost:5096'

function requireLicense(res) {
  if (!getCachedLicenseStatus().valid) {
    res.status(403).json({ error: 'A valid license is required' })
    return false
  }
  return true
}

async function proxyToApi(req, res, path, method, body) {
  const email = getCachedCustomerEmail()
  const headers = { 'Content-Type': 'application/json' }
  if (email) headers['X-User-Email'] = email

  try {
    const response = await fetch(`${PUBLIC_API_URL}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined
    })
    if (response.status === 204 || response.headers.get('content-length') === '0') {
      return res.status(response.status).end()
    }
    const data = await response.json()
    res.status(response.status).json(data)
  } catch {
    res.status(502).json({ error: 'Could not reach gallery service' })
  }
}

router.get('/', (req, res) => {
  const params = new URLSearchParams()
  if (req.query.type) params.set('type', req.query.type)
  const qs = params.toString()
  proxyToApi(req, res, `/api/gallery${qs ? '?' + qs : ''}`, 'GET')
})

router.post('/', (req, res) => {
  if (!requireLicense(res)) return
  proxyToApi(req, res, '/api/gallery', 'POST', req.body)
})

router.put('/:id', (req, res) => {
  if (!requireLicense(res)) return
  proxyToApi(req, res, `/api/gallery/${req.params.id}`, 'PUT', req.body)
})

router.delete('/:id', (req, res) => {
  if (!requireLicense(res)) return
  proxyToApi(req, res, `/api/gallery/${req.params.id}`, 'DELETE')
})

export default router
