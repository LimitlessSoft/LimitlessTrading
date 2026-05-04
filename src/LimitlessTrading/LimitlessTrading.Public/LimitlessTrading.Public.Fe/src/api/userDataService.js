import { API_BASE as BASE } from '../api'
const BASE_URL = `${BASE}/user-data`

async function get(path) {
  const res = await fetch(`${BASE_URL}${path}`)
  if (!res.ok) throw new Error(`GET ${path} failed`)
  return res.json()
}

async function post(path, body) {
  const res = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  if (!res.ok) throw new Error(`POST ${path} failed`)
  return res.status === 204 ? null : res.json()
}

async function put(path, body) {
  const res = await fetch(`${BASE_URL}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
  if (!res.ok) throw new Error(`PUT ${path} failed`)
  return res.json()
}

async function del(path) {
  const res = await fetch(`${BASE_URL}${path}`, { method: 'DELETE' })
  if (!res.ok) throw new Error(`DELETE ${path} failed`)
}

// Live Watchlist

export async function getLiveWatchlist() {
  const items = await get('/watchlist')
  return items.sort((a, b) => (a.order || 0) - (b.order || 0)).map(i => i.symbol)
}

export async function addToLiveWatchlist(symbol) {
  await post('/watchlist', { symbol })
}

export async function removeFromLiveWatchlist(symbol) {
  await del(`/watchlist/${symbol.toUpperCase()}`)
}

export async function reorderLiveWatchlist(symbols) {
  await put('/watchlist/reorder', { symbols })
}

export async function getLiveWatchlistInterval() {
  const { value } = await get('/settings/liveWatchlistInterval')
  return value || 15
}

export async function setLiveWatchlistInterval(seconds) {
  await put('/settings/liveWatchlistInterval', { value: seconds })
}

// Favorites

export async function getFavorites() {
  return get('/favorites')
}

export async function addFavorite(symbol) {
  await post('/favorites', { symbol })
}

export async function removeFavorite(symbol) {
  await del(`/favorites/${symbol.toUpperCase()}`)
}

export async function isFavorite(symbol) {
  const favorites = await getFavorites()
  return favorites.includes(symbol.toUpperCase())
}

// Settings

export async function getSetting(key) {
  const { value } = await get(`/settings/${key}`)
  return value
}

export async function setSetting(key, value) {
  await put(`/settings/${key}`, { value })
}

// Conditional Formats

export async function getConditionalFormats(groupId = null) {
  return get(groupId ? `/conditional-formats?groupId=${groupId}` : '/conditional-formats')
}

export async function addConditionalFormat(rule) {
  const result = await post('/conditional-formats', rule)
  return result.id
}

export async function updateConditionalFormat(id, updates) {
  await put(`/conditional-formats/${id}`, updates)
}

export async function deleteConditionalFormat(id) {
  await del(`/conditional-formats/${id}`)
}

// Format Groups

export async function getFormatGroups() {
  return get('/format-groups')
}

export async function addFormatGroup(name) {
  const result = await post('/format-groups', { name })
  return result.id
}

export async function updateFormatGroup(id, updates) {
  await put(`/format-groups/${id}`, updates)
}

export async function deleteFormatGroup(id) {
  await del(`/format-groups/${id}`)
}

export async function getActiveFormatGroup() {
  return getSetting('activeFormatGroup')
}

export async function setActiveFormatGroup(groupId) {
  return setSetting('activeFormatGroup', groupId)
}

// Active Watchgroup

export async function getActiveWatchgroup() {
  return getSetting('activeWatchgroup')
}

export async function setActiveWatchgroup(groupId) {
  return setSetting('activeWatchgroup', groupId)
}

// Analysis Presets

export async function getAnalysisPresets() {
  return get('/analysis-presets')
}

export async function addAnalysisPreset(preset) {
  const result = await post('/analysis-presets', preset)
  return result.id
}

export async function updateAnalysisPreset(id, updates) {
  await put(`/analysis-presets/${id}`, updates)
}

export async function deleteAnalysisPreset(id) {
  await del(`/analysis-presets/${id}`)
}

// Watchgroups

export async function getWatchgroups() {
  return get('/watchgroups')
}

export async function createWatchgroup(name) {
  const result = await post('/watchgroups', { name })
  return result.id
}

export async function renameWatchgroup(id, name) {
  return put(`/watchgroups/${id}`, { name })
}

export async function deleteWatchgroup(id) {
  await del(`/watchgroups/${id}`)
}

export async function getWatchgroupSymbols(groupId) {
  return get(`/watchgroups/${groupId}/symbols`)
}

export async function addToWatchgroup(groupId, symbol) {
  await post(`/watchgroups/${groupId}/symbols`, { symbol })
}

export async function removeFromWatchgroup(groupId, symbol) {
  await del(`/watchgroups/${groupId}/symbols/${symbol.toUpperCase()}`)
}

export async function getSymbolWatchgroups(symbol) {
  return get(`/watchgroups/by-symbol/${symbol.toUpperCase()}`)
}

// Threshold Report Settings

export async function getThresholdReportSettings() {
  return getSetting('thresholdReportSettings')
}

export async function saveThresholdReportSettings(settings) {
  return setSetting('thresholdReportSettings', settings)
}
