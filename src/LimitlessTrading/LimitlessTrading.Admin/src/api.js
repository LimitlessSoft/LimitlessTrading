const BASE = import.meta.env.VITE_API_BASE ?? ''

function authHeaders() {
  const token = localStorage.getItem('lt_admin_token')
  return token
    ? { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
    : { 'Content-Type': 'application/json' }
}

export async function login(email, password) {
  const r = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  })
  if (!r.ok) throw new Error('Invalid credentials')
  const { token } = await r.json()
  localStorage.setItem('lt_admin_token', token)
}

export function logout() {
  localStorage.removeItem('lt_admin_token')
}

export function isLoggedIn() {
  return !!localStorage.getItem('lt_admin_token')
}

export async function fetchEntries() {
  const r = await fetch(`${BASE}/api/whats-new?limit=50`, { headers: authHeaders() })
  return r.json()
}

export async function createEntry(data) {
  const r = await fetch(`${BASE}/api/whats-new`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify(data)
  })
  if (!r.ok) throw new Error(await r.text())
  return r.json()
}

export async function updateEntry(id, data) {
  const r = await fetch(`${BASE}/api/whats-new/${id}`, {
    method: 'PUT',
    headers: authHeaders(),
    body: JSON.stringify(data)
  })
  if (!r.ok) throw new Error(await r.text())
  return r.json()
}

export async function deleteEntry(id) {
  const r = await fetch(`${BASE}/api/whats-new/${id}`, {
    method: 'DELETE',
    headers: authHeaders()
  })
  if (!r.ok) throw new Error(await r.text())
}
