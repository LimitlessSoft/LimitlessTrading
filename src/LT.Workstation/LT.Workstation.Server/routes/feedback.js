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
    const data = await response.json()
    res.status(response.status).json(data)
  } catch {
    res.status(502).json({ error: 'Could not reach feedback service' })
  }
}

router.get('/', (req, res) => {
  const params = new URLSearchParams()
  if (req.query.category) params.set('category', req.query.category)
  if (req.query.status) params.set('status', req.query.status)
  if (req.query.sort) params.set('sort', req.query.sort)
  if (req.query.search) params.set('search', req.query.search)
  const qs = params.toString()
  proxyToApi(req, res, `/feedback${qs ? '?' + qs : ''}`, 'GET')
})

router.post('/', (req, res) => requireLicense(res) && proxyToApi(req, res, '/feedback', 'POST', req.body))

router.get('/:id', (req, res) => proxyToApi(req, res, `/feedback/${req.params.id}`, 'GET'))

router.patch('/:id', (req, res) => proxyToApi(req, res, `/feedback/${req.params.id}`, 'PATCH', req.body))

router.post('/:id/upvote', (req, res) => requireLicense(res) && proxyToApi(req, res, `/feedback/${req.params.id}/upvote`, 'POST'))

router.post('/:id/pin', (req, res) => proxyToApi(req, res, `/feedback/${req.params.id}/pin`, 'POST'))

router.post('/:id/comments', (req, res) => requireLicense(res) && proxyToApi(req, res, `/feedback/${req.params.id}/comments`, 'POST', req.body))

router.patch('/:id/comments/:commentId', (req, res) =>
  requireLicense(res) && proxyToApi(req, res, `/feedback/${req.params.id}/comments/${req.params.commentId}`, 'PATCH', req.body))

export default router
