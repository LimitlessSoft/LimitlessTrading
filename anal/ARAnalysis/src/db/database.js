import Dexie from 'dexie'

const db = new Dexie('ARAnalysisDB')

db.version(1).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key'
})

db.version(2).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol'
})

db.version(3).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol',
  conditionalFormats: '++id, name, enabled'
})

db.version(4).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol',
  conditionalFormats: '++id, name, enabled, groupId',
  formatGroups: '++id, name'
})

// Helper functions

export async function getSetting(key) {
  const setting = await db.settings.get(key)
  return setting?.value
}

export async function setSetting(key, value) {
  await db.settings.put({ key, value })
}

export async function addSecurity(symbol, name = '', exchange = 'SMART') {
  await db.securities.put({ symbol, name, exchange })
}

export async function getSecurities() {
  return db.securities.toArray()
}

export async function addPriceData(symbol, interval, data) {
  const records = data.map(item => ({
    symbol,
    interval,
    timestamp: item.timestamp,
    open: item.open,
    high: item.high,
    low: item.low,
    close: item.close,
    volume: item.volume
  }))

  // Use bulkPut to handle duplicates
  await db.priceData.bulkPut(records)
}

export async function getPriceData(symbol, interval) {
  const data = await db.priceData
    .where({ symbol, interval })
    .sortBy('timestamp')
  // Return descending (newest first)
  return data.reverse()
}

export async function clearPriceData(symbol = null, interval = null) {
  if (symbol && interval) {
    await db.priceData.where({ symbol, interval }).delete()
  } else if (symbol) {
    await db.priceData.where({ symbol }).delete()
  } else {
    await db.priceData.clear()
  }
}

export async function clearAllData() {
  await db.priceData.clear()
  await db.securities.clear()
}

// Last fetch tracking
export async function setLastFetch(symbol, interval, timestamp = Date.now()) {
  const key = `lastFetch:${symbol}:${interval}`
  await db.settings.put({ key, value: timestamp })
}

export async function getLastFetch(symbol, interval) {
  const key = `lastFetch:${symbol}:${interval}`
  const setting = await db.settings.get(key)
  return setting?.value || null
}

// Conditional formatting rules
export async function getConditionalFormats(groupId = null) {
  if (groupId) {
    return db.conditionalFormats.where({ groupId }).toArray()
  }
  return db.conditionalFormats.toArray()
}

export async function addConditionalFormat(rule) {
  return db.conditionalFormats.add(rule)
}

export async function updateConditionalFormat(id, updates) {
  return db.conditionalFormats.update(id, updates)
}

export async function deleteConditionalFormat(id) {
  return db.conditionalFormats.delete(id)
}

// Format groups
export async function getFormatGroups() {
  return db.formatGroups.toArray()
}

export async function addFormatGroup(name) {
  return db.formatGroups.add({ name })
}

export async function updateFormatGroup(id, updates) {
  return db.formatGroups.update(id, updates)
}

export async function deleteFormatGroup(id) {
  // Delete all rules in this group
  await db.conditionalFormats.where({ groupId: id }).delete()
  return db.formatGroups.delete(id)
}

export async function getActiveFormatGroup() {
  return getSetting('activeFormatGroup')
}

export async function setActiveFormatGroup(groupId) {
  return setSetting('activeFormatGroup', groupId)
}

// Favorites management
export async function addFavorite(symbol) {
  await db.favorites.put({ symbol: symbol.toUpperCase() })
}

export async function removeFavorite(symbol) {
  await db.favorites.delete(symbol.toUpperCase())
}

export async function getFavorites() {
  const favorites = await db.favorites.toArray()
  return favorites.map(f => f.symbol)
}

export async function isFavorite(symbol) {
  const fav = await db.favorites.get(symbol.toUpperCase())
  return !!fav
}

export default db
