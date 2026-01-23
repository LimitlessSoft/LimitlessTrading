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

db.version(5).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol',
  conditionalFormats: '++id, name, enabled, groupId',
  formatGroups: '++id, name',
  analysisPresets: '++id, name'
})

db.version(6).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol',
  conditionalFormats: '++id, name, enabled, groupId',
  formatGroups: '++id, name',
  analysisPresets: '++id, name',
  watchgroups: '++id, name',
  watchgroupMembers: '++id, [groupId+symbol], groupId, symbol'
}).upgrade(async tx => {
  // Migrate existing favorites to a "Favorites" watchgroup
  const favorites = await tx.table('favorites').toArray()
  if (favorites.length > 0) {
    const groupId = await tx.table('watchgroups').add({ name: 'Favorites' })
    const members = favorites.map(f => ({ groupId, symbol: f.symbol }))
    await tx.table('watchgroupMembers').bulkAdd(members)
  }
})

// Default screener symbols (combined from major indices)
const DEFAULT_SCREENER_SYMBOLS = [
  'AAPL', 'MSFT', 'AMZN', 'NVDA', 'GOOGL', 'GOOG', 'META', 'BRK.B', 'UNH', 'XOM',
  'LLY', 'JPM', 'JNJ', 'V', 'PG', 'MA', 'AVGO', 'HD', 'CVX', 'MRK', 'ABBV', 'COST',
  'PEP', 'ADBE', 'WMT', 'KO', 'MCD', 'CSCO', 'CRM', 'BAC', 'PFE', 'TMO', 'ACN',
  'NFLX', 'AMD', 'LIN', 'ABT', 'DIS', 'DHR', 'ORCL', 'CMCSA', 'VZ', 'INTC', 'WFC',
  'PM', 'TXN', 'INTU', 'COP', 'QCOM', 'NKE', 'UNP', 'IBM', 'CAT', 'NOW', 'SPGI',
  'HON', 'LOW', 'AMGN', 'UPS', 'GE', 'BA', 'RTX', 'ISRG', 'DE', 'AMAT', 'ELV',
  'PLD', 'GS', 'BKNG', 'SYK', 'BLK', 'MDLZ', 'AXP', 'SBUX', 'GILD', 'LMT', 'ADI',
  'TJX', 'MMC', 'VRTX', 'ADP', 'REGN', 'CVS', 'ETN', 'C', 'SCHW', 'LRCX', 'CI',
  'MU', 'PANW', 'ZTS', 'TMUS', 'BSX', 'FI', 'MO', 'CB', 'SO', 'SLB', 'TSLA',
  'PYPL', 'CSX', 'SNPS', 'KLAC', 'CDNS', 'MELI', 'MAR', 'ORLY', 'MNST', 'CTAS',
  'NXPI', 'FTNT', 'WDAY', 'MRVL', 'ADSK', 'ABNB', 'DXCM', 'KDP', 'PCAR', 'AEP',
  'MCHP', 'CPRT', 'ODFL', 'KHC', 'PAYX', 'LULU', 'EXC', 'AZN', 'IDXX', 'ROST',
  'FAST', 'CSGP', 'VRSK', 'EA', 'CTSH', 'GEHC', 'XEL', 'BKR', 'DDOG', 'BIIB',
  'FANG', 'TEAM', 'ANSS', 'DLTR', 'WBD', 'ZS', 'ILMN', 'ALGN', 'EBAY', 'WBA',
  'ENPH', 'DOW', 'MMM', 'TRV', 'SQ', 'SHOP', 'SNOW', 'PLTR', 'UBER'
]

db.version(7).stores({
  securities: 'symbol, name, exchange',
  priceData: '++id, [symbol+interval+timestamp], symbol, interval, timestamp',
  settings: 'key',
  favorites: 'symbol',
  conditionalFormats: '++id, name, enabled, groupId',
  formatGroups: '++id, name',
  analysisPresets: '++id, name',
  watchgroups: '++id, name',
  watchgroupMembers: '++id, [groupId+symbol], groupId, symbol',
  screenerSymbols: 'symbol'
}).upgrade(async tx => {
  // Initialize screener symbols with defaults
  const symbols = DEFAULT_SCREENER_SYMBOLS.map(s => ({ symbol: s }))
  await tx.table('screenerSymbols').bulkAdd(symbols)
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

// Analysis presets
export async function getAnalysisPresets() {
  return db.analysisPresets.toArray()
}

export async function addAnalysisPreset(preset) {
  return db.analysisPresets.add(preset)
}

export async function updateAnalysisPreset(id, updates) {
  return db.analysisPresets.update(id, updates)
}

export async function deleteAnalysisPreset(id) {
  return db.analysisPresets.delete(id)
}

// Watchgroups management
export async function getWatchgroups() {
  return db.watchgroups.toArray()
}

export async function createWatchgroup(name) {
  return db.watchgroups.add({ name })
}

export async function renameWatchgroup(id, name) {
  return db.watchgroups.update(id, { name })
}

export async function deleteWatchgroup(id) {
  // Delete all members in this group
  await db.watchgroupMembers.where({ groupId: id }).delete()
  return db.watchgroups.delete(id)
}

export async function getWatchgroupSymbols(groupId) {
  const members = await db.watchgroupMembers.where({ groupId }).toArray()
  return members.map(m => m.symbol)
}

export async function addToWatchgroup(groupId, symbol) {
  const upperSymbol = symbol.toUpperCase()
  // Check if already exists
  const existing = await db.watchgroupMembers
    .where({ groupId, symbol: upperSymbol })
    .first()
  if (!existing) {
    await db.watchgroupMembers.add({ groupId, symbol: upperSymbol })
  }
}

export async function removeFromWatchgroup(groupId, symbol) {
  const upperSymbol = symbol.toUpperCase()
  await db.watchgroupMembers
    .where({ groupId, symbol: upperSymbol })
    .delete()
}

export async function getSymbolWatchgroups(symbol) {
  const upperSymbol = symbol.toUpperCase()
  const members = await db.watchgroupMembers.where({ symbol: upperSymbol }).toArray()
  const groupIds = members.map(m => m.groupId)
  if (groupIds.length === 0) return []
  return db.watchgroups.where('id').anyOf(groupIds).toArray()
}

export async function getActiveWatchgroup() {
  return getSetting('activeWatchgroup')
}

export async function setActiveWatchgroup(groupId) {
  return setSetting('activeWatchgroup', groupId)
}

// Screener symbols management
export async function getScreenerSymbols() {
  const symbols = await db.screenerSymbols.toArray()
  return symbols.map(s => s.symbol).sort()
}

export async function addScreenerSymbol(symbol) {
  const upperSymbol = symbol.toUpperCase().trim()
  if (!upperSymbol) return
  const existing = await db.screenerSymbols.get(upperSymbol)
  if (!existing) {
    await db.screenerSymbols.add({ symbol: upperSymbol })
  }
}

export async function addScreenerSymbols(symbols) {
  const records = symbols
    .map(s => s.toUpperCase().trim())
    .filter(s => s.length > 0)
    .map(s => ({ symbol: s }))
  await db.screenerSymbols.bulkPut(records)
}

export async function removeScreenerSymbol(symbol) {
  await db.screenerSymbols.delete(symbol.toUpperCase())
}

export async function clearScreenerSymbols() {
  await db.screenerSymbols.clear()
}

export default db
