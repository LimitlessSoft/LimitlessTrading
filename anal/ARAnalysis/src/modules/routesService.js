const API_BASE = 'http://localhost:3001/api/routes'

export async function fetchRoutes() {
  const response = await fetch(API_BASE)
  if (!response.ok) {
    throw new Error('Failed to fetch routes')
  }
  return response.json()
}

export async function createRoute(name, path) {
  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, path })
  })
  if (!response.ok) {
    const error = await response.json()
    throw new Error(error.error || 'Failed to create route')
  }
  return response.json()
}

export async function updateRoute(id, updates) {
  const response = await fetch(`${API_BASE}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(updates)
  })
  if (!response.ok) {
    const error = await response.json()
    throw new Error(error.error || 'Failed to update route')
  }
  return response.json()
}

export async function deleteRoute(id) {
  const response = await fetch(`${API_BASE}/${id}`, {
    method: 'DELETE'
  })
  if (!response.ok) {
    throw new Error('Failed to delete route')
  }
}
