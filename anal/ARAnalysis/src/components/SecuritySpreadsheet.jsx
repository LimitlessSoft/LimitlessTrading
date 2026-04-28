import React, { useMemo, useState, useEffect, useRef } from 'react'
import {
  Box,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  TableContainer,
  Text,
  Spinner,
  Center,
  HStack,
  VStack,
  Button,
  Select,
  Badge,
  IconButton,
  Tooltip,
  NumberInput,
  NumberInputField,
  NumberInputStepper,
  NumberIncrementStepper,
  NumberDecrementStepper,
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  Checkbox,
  Input,
  useToast,
  MenuDivider,
  Popover,
  PopoverTrigger,
  PopoverContent,
  PopoverHeader,
  PopoverBody,
  PopoverArrow
} from '@chakra-ui/react'
import { ViewIcon, ViewOffIcon, ChevronDownIcon, ChevronRightIcon, RepeatIcon, DeleteIcon, EditIcon, CheckIcon, CloseIcon, InfoIcon, ArrowUpIcon, ArrowDownIcon } from '@chakra-ui/icons'
import CandlestickChart from './CandlestickChart'
import { getIntradayForDay, fetchIntradayForDay, fetchFromCache } from '../modules/dataService'
import ConditionalFormatManager from './ConditionalFormatManager'
import {
  getConditionalFormats,
  getAnalysisPresets,
  addAnalysisPreset,
  updateAnalysisPreset,
  deleteAnalysisPreset,
  getSetting,
  setSetting
} from '../api/userDataService'

const ROWS_PER_PAGE_OPTIONS = [25, 50, 100, 200, 500, 1000, 2000, 5000]

// Days of week for filtering
const DAYS_OF_WEEK = [
  { id: 0, label: 'Sun', fullLabel: 'Sunday' },
  { id: 1, label: 'Mon', fullLabel: 'Monday' },
  { id: 2, label: 'Tue', fullLabel: 'Tuesday' },
  { id: 3, label: 'Wed', fullLabel: 'Wednesday' },
  { id: 4, label: 'Thu', fullLabel: 'Thursday' },
  { id: 5, label: 'Fri', fullLabel: 'Friday' },
  { id: 6, label: 'Sat', fullLabel: 'Saturday' }
]

// Column definitions
const COLUMNS = [
  { id: 'date', label: 'Date/Time', defaultVisible: true },
  { id: 'open', label: 'Open', defaultVisible: true },
  { id: 'high', label: 'High', defaultVisible: true },
  { id: 'low', label: 'Low', defaultVisible: true },
  { id: 'close', label: 'Close', defaultVisible: true },
  { id: 'volume', label: 'Volume', defaultVisible: true },
  { id: 'gap', label: 'GAP', defaultVisible: true },
  { id: 'dayChange', label: 'Day Chg', defaultVisible: true },
  { id: 'highVsPrevClose', label: 'H vs PC', defaultVisible: true, group: 'vsPrevClose' },
  { id: 'lowVsPrevClose', label: 'L vs PC', defaultVisible: true, group: 'vsPrevClose' },
  { id: 'vixOpen', label: 'VIX Open', defaultVisible: true, dailyOnly: true, group: 'vix' },
  { id: 'vixClose', label: 'VIX Close', defaultVisible: true, dailyOnly: true, group: 'vix' },
  { id: '5m', label: '5min', defaultVisible: true, dailyOnly: true, group: 'openingMins' },
  { id: '10m', label: '10min', defaultVisible: true, dailyOnly: true, group: 'openingMins' },
  { id: '15m', label: '15min', defaultVisible: true, dailyOnly: true, group: 'openingMins' },
  { id: '30m', label: '30min', defaultVisible: true, dailyOnly: true, group: 'openingMins' },
  { id: '1h', label: '1hr', defaultVisible: true, dailyOnly: true, group: 'openingMins' }
]

// Column groups
const COLUMN_GROUPS = [
  { id: 'vsPrevClose', label: 'vs Prev Close' },
  { id: 'vix', label: 'VIX', dailyOnly: true },
  { id: 'openingMins', label: 'Opening Minutes', dailyOnly: true }
]

// Helper to calculate gap (open vs prev close percentage)
function calcGap(row, prevRow) {
  if (!prevRow || !prevRow.close || prevRow.close === 0) return null
  return ((Number(row.open) - Number(prevRow.close)) / Number(prevRow.close)) * 100
}

// Helper to calculate high vs prev close percentage
function calcHighVsPrevClose(row, prevRow) {
  if (!prevRow || !prevRow.close || prevRow.close === 0) return null
  return ((Number(row.high) - Number(prevRow.close)) / Number(prevRow.close)) * 100
}

// Helper to calculate low vs prev close percentage
function calcLowVsPrevClose(row, prevRow) {
  if (!prevRow || !prevRow.close || prevRow.close === 0) return null
  return ((Number(row.low) - Number(prevRow.close)) / Number(prevRow.close)) * 100
}

// Evaluate a single condition against a row
// vix and prevVix are optional objects with { open, close } for VIX-based conditions
// prevPrevRow is the row before prevRow (for calculating prev high/low vs prev close)
// openingMins is an object with { m5, m10, m15, m30, h1 } percentage values
// prevOpeningMins is the same for the previous row
function evaluateCondition(cond, row, prevRow, vix = null, prevVix = null, prevPrevRow = null, openingMins = null, prevOpeningMins = null) {
  // Get the value to check (ensure numeric)
  let value
  switch (cond.column) {
    case 'open': value = Number(row.open); break
    case 'high': value = Number(row.high); break
    case 'low': value = Number(row.low); break
    case 'close': value = Number(row.close); break
    case 'volume': value = Number(row.volume); break
    case 'change': value = Number(row.close) - Number(row.open); break
    case 'changePercent': value = ((Number(row.close) - Number(row.open)) / Number(row.open)) * 100; break
    case 'gap': value = calcGap(row, prevRow); break
    case 'highVsPrevClose': value = calcHighVsPrevClose(row, prevRow); break
    case 'lowVsPrevClose': value = calcLowVsPrevClose(row, prevRow); break
    case 'first5m': value = openingMins?.m5 ?? null; break
    case 'first10m': value = openingMins?.m10 ?? null; break
    case 'first15m': value = openingMins?.m15 ?? null; break
    case 'first30m': value = openingMins?.m30 ?? null; break
    case 'first1h': value = openingMins?.h1 ?? null; break
    case 'vixOpen': value = vix?.open != null ? Number(vix.open) : null; break
    case 'vixClose': value = vix?.close != null ? Number(vix.close) : null; break
    case 'prevVixOpen': value = prevVix?.open != null ? Number(prevVix.open) : null; break
    case 'prevVixClose': value = prevVix?.close != null ? Number(prevVix.close) : null; break
    default: return false
  }

  // Get the comparison value (ensure numeric)
  let compareValue
  if (cond.compareToColumn) {
    switch (cond.compareToColumn) {
      case 'open': compareValue = Number(row.open); break
      case 'high': compareValue = Number(row.high); break
      case 'low': compareValue = Number(row.low); break
      case 'close': compareValue = Number(row.close); break
      case 'volume': compareValue = Number(row.volume); break
      case 'gap': compareValue = calcGap(row, prevRow); break
      case 'highVsPrevClose': compareValue = calcHighVsPrevClose(row, prevRow); break
      case 'lowVsPrevClose': compareValue = calcLowVsPrevClose(row, prevRow); break
      case 'first5m': compareValue = openingMins?.m5 ?? null; break
      case 'first10m': compareValue = openingMins?.m10 ?? null; break
      case 'first15m': compareValue = openingMins?.m15 ?? null; break
      case 'first30m': compareValue = openingMins?.m30 ?? null; break
      case 'first1h': compareValue = openingMins?.h1 ?? null; break
      case 'vixOpen': compareValue = vix?.open != null ? Number(vix.open) : null; break
      case 'vixClose': compareValue = vix?.close != null ? Number(vix.close) : null; break
      case 'prevOpen': compareValue = prevRow ? Number(prevRow.open) : null; break
      case 'prevHigh': compareValue = prevRow ? Number(prevRow.high) : null; break
      case 'prevLow': compareValue = prevRow ? Number(prevRow.low) : null; break
      case 'prevClose': compareValue = prevRow ? Number(prevRow.close) : null; break
      case 'prevVolume': compareValue = prevRow ? Number(prevRow.volume) : null; break
      case 'prevGap': compareValue = calcGap(prevRow, prevPrevRow); break
      case 'prevHighVsPrevClose': compareValue = calcHighVsPrevClose(prevRow, prevPrevRow); break
      case 'prevLowVsPrevClose': compareValue = calcLowVsPrevClose(prevRow, prevPrevRow); break
      case 'prevFirst5m': compareValue = prevOpeningMins?.m5 ?? null; break
      case 'prevFirst10m': compareValue = prevOpeningMins?.m10 ?? null; break
      case 'prevFirst15m': compareValue = prevOpeningMins?.m15 ?? null; break
      case 'prevFirst30m': compareValue = prevOpeningMins?.m30 ?? null; break
      case 'prevFirst1h': compareValue = prevOpeningMins?.h1 ?? null; break
      case 'prevVixOpen': compareValue = prevVix?.open != null ? Number(prevVix.open) : null; break
      case 'prevVixClose': compareValue = prevVix?.close != null ? Number(prevVix.close) : null; break
      default: compareValue = Number(cond.value)
    }
  } else {
    compareValue = Number(cond.value)
  }

  if (compareValue == null || isNaN(compareValue)) return false
  if (isNaN(value)) return false
  // Only reject zero for percentage operators (to avoid division by zero)
  if (compareValue === 0 && cond.operator?.endsWith('%')) return false

  // Evaluate the condition
  switch (cond.operator) {
    case '>': return value > compareValue
    case '<': return value < compareValue
    case '>=': return value >= compareValue
    case '<=': return value <= compareValue
    case '==': return value === compareValue
    case '!=': return value !== compareValue
    case 'between': return value >= cond.value && value <= cond.value2
    // Percentage-based operators: check if value differs from compareValue by X%
    case '>%': {
      const percentDiff = ((value - compareValue) / compareValue) * 100
      return percentDiff > cond.value
    }
    case '<%': {
      const percentDiff = ((value - compareValue) / compareValue) * 100
      return percentDiff < -cond.value
    }
    case '>=%': {
      const percentDiff = ((value - compareValue) / compareValue) * 100
      return percentDiff >= cond.value
    }
    case '<=%': {
      const percentDiff = ((value - compareValue) / compareValue) * 100
      return percentDiff <= -cond.value
    }
    default: return false
  }
}

// Evaluate a conditional format rule against a row (all conditions must be true - AND logic)
function evaluateRule(rule, row, prevRow, vixValue = null, prevVixValue = null, prevPrevRow = null, openingMins = null, prevOpeningMins = null) {
  if (!rule.enabled) return false

  // Support both old format (single condition) and new format (conditions array)
  const conditions = rule.conditions || [{
    column: rule.column,
    operator: rule.operator,
    value: rule.value,
    value2: rule.value2,
    compareToColumn: rule.compareToColumn
  }]

  // All conditions must be true (AND logic)
  return conditions.every(cond => evaluateCondition(cond, row, prevRow, vixValue, prevVixValue, prevPrevRow, openingMins, prevOpeningMins))
}

// Get the format to apply based on all matching rules
function getCellFormat(rules, row, prevRow, column, vixValue = null, prevVixValue = null, prevPrevRow = null, openingMins = null, prevOpeningMins = null) {
  // Find rules that match this column and evaluate to true
  const matchingRules = rules.filter(rule => {
    // Get all columns referenced in the rule's conditions
    const conditions = rule.conditions || [{
      column: rule.column,
      operator: rule.operator,
      value: rule.value,
      value2: rule.value2,
      compareToColumn: rule.compareToColumn
    }]

    // Map condition column to display column(s)
    const getTargetColumns = (condColumn) => {
      switch (condColumn) {
        case 'change':
        case 'changePercent':
          return ['close', 'dayChange']
        case 'prevVixOpen':
          return ['vixOpen']
        case 'prevVixClose':
          return ['vixClose']
        case 'first5m':
          return ['5m']
        case 'first10m':
          return ['10m']
        case 'first15m':
          return ['15m']
        case 'first30m':
          return ['30m']
        case 'first1h':
          return ['1h']
        default:
          return [condColumn]
      }
    }

    // Check if any condition targets this column
    const targetsColumn = conditions.some(cond => {
      const targetCols = getTargetColumns(cond.column)
      return targetCols.includes(column)
    })

    if (!targetsColumn) return false

    return evaluateRule(rule, row, prevRow, vixValue, prevVixValue, prevPrevRow, openingMins, prevOpeningMins)
  })

  if (matchingRules.length === 0) return null

  // Return the last matching rule's format (highest priority)
  return matchingRules[matchingRules.length - 1].format
}

function SecuritySpreadsheet({ data, symbol, interval, loading, connectionState, onLoadData }) {
  const [currentPage, setCurrentPage] = useState(0)
  const [rowsPerPage, setRowsPerPage] = useState(50)
  const [formatRules, setFormatRules] = useState([])
  const [filterEnabled, setFilterEnabled] = useState(false)
  const [contextBefore, setContextBefore] = useState(0)
  const [contextAfter, setContextAfter] = useState(0)
  const [expandedRows, setExpandedRows] = useState({})
  const [intradayData, setIntradayData] = useState({})
  const [loadingIntraday, setLoadingIntraday] = useState({})
  const [fetchingIntraday, setFetchingIntraday] = useState({})
  const [fiveMinData, setFiveMinData] = useState([])
  const [loadingOpeningMins, setLoadingOpeningMins] = useState(false)
  const [vixData, setVixData] = useState([])
  const [loadingVix, setLoadingVix] = useState(false)
  const [visibleColumns, setVisibleColumns] = useState(null)
  const [columnOrder, setColumnOrder] = useState(null)
  const [columnsLoaded, setColumnsLoaded] = useState(false)
  const [presets, setPresets] = useState([])
  const [newPresetName, setNewPresetName] = useState('')
  const [editingPresetId, setEditingPresetId] = useState(null)
  const [editingPresetName, setEditingPresetName] = useState('')
  const [selectedDays, setSelectedDays] = useState([]) // Empty = all days
  const conditionalFormatRef = useRef(null)
  const toast = useToast()

  // Load presets and column visibility on mount
  useEffect(() => {
    loadPresets()
    loadColumnVisibility()
  }, [])

  const loadColumnVisibility = async () => {
    const saved = await getSetting('columnVisibility')
    if (saved) {
      setVisibleColumns(saved)
    } else {
      // Use defaults
      setVisibleColumns(COLUMNS.reduce((acc, col) => ({ ...acc, [col.id]: col.defaultVisible }), {}))
    }

    const savedOrder = await getSetting('columnOrder')
    if (savedOrder) {
      setColumnOrder(savedOrder)
    } else {
      // Use default order from COLUMNS
      setColumnOrder(COLUMNS.map(col => col.id))
    }

    setColumnsLoaded(true)
  }

  // Save column visibility and order when they change
  useEffect(() => {
    if (columnsLoaded && visibleColumns) {
      setSetting('columnVisibility', visibleColumns)
    }
  }, [visibleColumns, columnsLoaded])

  useEffect(() => {
    if (columnsLoaded && columnOrder) {
      setSetting('columnOrder', columnOrder)
    }
  }, [columnOrder, columnsLoaded])

  const loadPresets = async () => {
    const loaded = await getAnalysisPresets()
    setPresets(loaded)
  }

  const handleSavePreset = async () => {
    if (!newPresetName.trim()) {
      toast({ title: 'Enter a preset name', status: 'warning', duration: 2000 })
      return
    }

    const preset = {
      name: newPresetName.trim(),
      rowsPerPage,
      filterEnabled,
      contextBefore,
      contextAfter,
      interval,
      visibleColumns,
      selectedDays
    }

    await addAnalysisPreset(preset)
    setNewPresetName('')
    await loadPresets()
    toast({ title: 'Preset saved', status: 'success', duration: 2000 })
  }

  const handleLoadPreset = async (preset) => {
    setRowsPerPage(preset.rowsPerPage || 50)
    setFilterEnabled(preset.filterEnabled || false)
    setContextBefore(preset.contextBefore || 0)
    setContextAfter(preset.contextAfter || 0)
    if (preset.visibleColumns) {
      setVisibleColumns(preset.visibleColumns)
    }
    setSelectedDays(preset.selectedDays || [])
    toast({ title: `Loaded: ${preset.name}`, status: 'info', duration: 2000 })
  }

  const handleDeletePreset = async (e, presetId) => {
    e.stopPropagation()
    await deleteAnalysisPreset(presetId)
    await loadPresets()
    toast({ title: 'Preset deleted', status: 'info', duration: 2000 })
  }

  const handleStartEditPreset = (e, preset) => {
    e.stopPropagation()
    setEditingPresetId(preset.id)
    setEditingPresetName(preset.name)
  }

  const handleSavePresetName = async (e) => {
    e.stopPropagation()
    if (!editingPresetName.trim()) {
      toast({ title: 'Enter a preset name', status: 'warning', duration: 2000 })
      return
    }
    await updateAnalysisPreset(editingPresetId, { name: editingPresetName.trim() })
    setEditingPresetId(null)
    setEditingPresetName('')
    await loadPresets()
    toast({ title: 'Preset renamed', status: 'success', duration: 2000 })
  }

  const handleCancelEditPreset = (e) => {
    e.stopPropagation()
    setEditingPresetId(null)
    setEditingPresetName('')
  }

  const toggleColumn = (colId) => {
    if (!visibleColumns) return
    setVisibleColumns(prev => ({ ...prev, [colId]: !prev[colId] }))
  }

  const toggleColumnGroup = (groupId) => {
    if (!visibleColumns) return
    const groupCols = COLUMNS.filter(c => c.group === groupId)
    // Check if any column in the group is visible
    const anyVisible = groupCols.some(c => visibleColumns[c.id] !== false)
    // Toggle all: if any visible, hide all; otherwise show all
    setVisibleColumns(prev => {
      const newState = { ...prev }
      groupCols.forEach(c => {
        newState[c.id] = !anyVisible
      })
      return newState
    })
  }

  const isGroupVisible = (groupId) => {
    if (!visibleColumns) return true
    const groupCols = COLUMNS.filter(c => c.group === groupId)
    return groupCols.some(c => visibleColumns[c.id] !== false)
  }

  const isGroupPartiallyVisible = (groupId) => {
    if (!visibleColumns) return false
    const groupCols = COLUMNS.filter(c => c.group === groupId)
    const visibleCount = groupCols.filter(c => visibleColumns[c.id] !== false).length
    return visibleCount > 0 && visibleCount < groupCols.length
  }

  const isColumnVisible = (colId) => {
    if (!visibleColumns) return true // Default to visible during loading
    const col = COLUMNS.find(c => c.id === colId)
    if (col?.dailyOnly && interval !== '1d') return false
    return visibleColumns[colId] !== false
  }

  // Get columns in order
  const orderedColumns = useMemo(() => {
    if (!columnOrder) return COLUMNS
    // Map order to column objects, filtering out any that don't exist
    const ordered = columnOrder
      .map(id => COLUMNS.find(c => c.id === id))
      .filter(Boolean)
    // Add any new columns that aren't in the order yet
    COLUMNS.forEach(col => {
      if (!ordered.find(c => c.id === col.id)) {
        ordered.push(col)
      }
    })
    return ordered
  }, [columnOrder])

  const moveColumnUp = (colId) => {
    if (!columnOrder) return
    const idx = columnOrder.indexOf(colId)
    if (idx <= 0) return
    const newOrder = [...columnOrder]
    ;[newOrder[idx - 1], newOrder[idx]] = [newOrder[idx], newOrder[idx - 1]]
    setColumnOrder(newOrder)
  }

  const moveColumnDown = (colId) => {
    if (!columnOrder) return
    const idx = columnOrder.indexOf(colId)
    if (idx < 0 || idx >= columnOrder.length - 1) return
    const newOrder = [...columnOrder]
    ;[newOrder[idx], newOrder[idx + 1]] = [newOrder[idx + 1], newOrder[idx]]
    setColumnOrder(newOrder)
  }

  const resetColumnOrder = () => {
    setColumnOrder(COLUMNS.map(col => col.id))
  }

  // Day of week filter functions
  const toggleDay = (dayId) => {
    setSelectedDays(prev => {
      if (prev.includes(dayId)) {
        return prev.filter(d => d !== dayId)
      } else {
        return [...prev, dayId].sort((a, b) => a - b)
      }
    })
  }

  const clearDayFilter = () => {
    setSelectedDays([])
  }

  const getDayOfWeek = (timestamp) => {
    return new Date(timestamp).getDay()
  }

  // Calculate visible column count for colSpan
  const visibleColCount = useMemo(() => {
    let count = 1 // expand button column
    COLUMNS.forEach(col => {
      if (isColumnVisible(col.id)) count++
    })
    return count
  }, [visibleColumns, interval])

  useEffect(() => {
    loadRules()
  }, [])

  // Load 5-minute data for calculating opening minutes changes
  useEffect(() => {
    const load5mData = async () => {
      if (!symbol) {
        setFiveMinData([])
        return
      }
      const data5m = await fetchFromCache(symbol, '5m')
      setFiveMinData(data5m || [])
    }
    load5mData()
  }, [symbol])

  // Load VIX daily data
  useEffect(() => {
    const loadVixData = async () => {
      const vix = await fetchFromCache('VIX', '1d')
      setVixData(vix || [])
    }
    loadVixData()
  }, [])

  // Get VIX data for a given day (returns { open, close } or null)
  // Pre-compute VIX data by day for fast lookup
  const vixByDay = useMemo(() => {
    if (!vixData || vixData.length === 0) return new Map()

    const byDay = new Map()
    for (const bar of vixData) {
      const dayKey = new Date(bar.timestamp).toDateString()
      byDay.set(dayKey, { open: bar.open, close: bar.close })
    }
    return byDay
  }, [vixData])

  const getVixForDay = (dayTimestamp) => {
    if (vixByDay.size === 0) return null
    const dayKey = new Date(dayTimestamp).toDateString()
    return vixByDay.get(dayKey) || null
  }

  // Check if VIX data exists for a day
  const hasVixData = (dayTimestamp) => {
    if (vixByDay.size === 0) return false
    const dayKey = new Date(dayTimestamp).toDateString()
    return vixByDay.has(dayKey)
  }

  // Fetch VIX data from IBKR
  const handleFetchVixData = async () => {
    if (!connectionState?.connected) {
      toast({ title: 'Not connected to IBKR', status: 'warning', duration: 3000 })
      return
    }

    setLoadingVix(true)
    try {
      toast({ title: 'Fetching VIX data...', status: 'info', duration: 2000 })

      // Refresh callback - called after each chunk is saved
      const refreshFromCache = async () => {
        const vix = await fetchFromCache('VIX', '1d')
        setVixData(vix || [])
      }

      // Fetch VIX daily data - get date range from visible rows
      const timestamps = paginatedData.map(r => r.timestamp)
      const minTimestamp = Math.min(...timestamps)
      const maxTimestamp = Math.max(...timestamps)

      await fetchIntradayForDay('VIX', minTimestamp, '1d', maxTimestamp, refreshFromCache)

      await refreshFromCache()
      toast({ title: 'VIX data loaded', status: 'success', duration: 2000 })
    } catch (err) {
      toast({
        title: 'Failed to fetch VIX data',
        description: err.response?.data?.error || err.message,
        status: 'error',
        duration: 5000
      })
    }
    setLoadingVix(false)
  }

  // Calculate first X minutes change for a given day
  // Pre-compute 5m bars grouped by day for fast lookup
  const fiveMinByDay = useMemo(() => {
    if (!fiveMinData || fiveMinData.length === 0) return new Map()

    const byDay = new Map()
    for (const bar of fiveMinData) {
      const dayKey = new Date(bar.timestamp).toDateString()
      if (!byDay.has(dayKey)) {
        byDay.set(dayKey, [])
      }
      byDay.get(dayKey).push(bar)
    }

    // Sort each day's bars ascending
    for (const [key, bars] of byDay) {
      bars.sort((a, b) => a.timestamp - b.timestamp)
    }

    return byDay
  }, [fiveMinData])

  const getOpeningMinutesChange = (dayTimestamp, minutes) => {
    if (fiveMinByDay.size === 0) return null

    const dayKey = new Date(dayTimestamp).toDateString()
    const dayBars = fiveMinByDay.get(dayKey)

    if (!dayBars || dayBars.length === 0) return null

    // Calculate how many 5-minute bars we need
    const barsNeeded = Math.ceil(minutes / 5)
    if (dayBars.length < barsNeeded) return null

    // Get the open of first bar and close of the Nth bar
    const openPrice = dayBars[0].open
    const closePrice = dayBars[barsNeeded - 1].close

    if (!openPrice || openPrice === 0) return null

    const change = ((closePrice - openPrice) / openPrice) * 100
    return change
  }

  // Check if a day has 5m data
  const hasFiveMinData = (dayTimestamp) => {
    if (fiveMinByDay.size === 0) return false
    const dayKey = new Date(dayTimestamp).toDateString()
    return fiveMinByDay.has(dayKey)
  }

  // Fetch 5m data for all visible rows that don't have data
  const handleFetchOpeningMinsData = async () => {
    if (!connectionState?.connected || !symbol) {
      toast({ title: 'Not connected to IBKR', status: 'warning', duration: 3000 })
      return
    }

    setLoadingOpeningMins(true)
    try {
      // Find rows that need data
      const rowsNeedingData = paginatedData.filter(row => !hasFiveMinData(row.timestamp))

      if (rowsNeedingData.length === 0) {
        // Just reload from cache in case it was updated elsewhere
        const data5m = await fetchFromCache(symbol, '5m')
        setFiveMinData(data5m || [])
        toast({ title: 'Data already loaded', status: 'info', duration: 2000 })
        setLoadingOpeningMins(false)
        return
      }

      // Find the date range we need to fetch
      const timestamps = rowsNeedingData.map(r => r.timestamp)
      const minTimestamp = Math.min(...timestamps)
      const maxTimestamp = Math.max(...timestamps)

      toast({ title: `Fetching 5m data for ${rowsNeedingData.length} days...`, status: 'info', duration: 2000 })

      // Refresh callback - called after each chunk is saved
      const refreshFromCache = async () => {
        const data5m = await fetchFromCache(symbol, '5m')
        setFiveMinData(data5m || [])
      }

      // Fetch data for the range with progress callback
      await fetchIntradayForDay(symbol, minTimestamp, '5m', maxTimestamp, refreshFromCache)

      // Final reload from cache
      await refreshFromCache()
      toast({ title: 'Data loaded', status: 'success', duration: 2000 })
    } catch (err) {
      toast({
        title: 'Failed to fetch data',
        description: err.response?.data?.error || err.message,
        status: 'error',
        duration: 5000
      })
    }
    setLoadingOpeningMins(false)
  }

  const loadRules = async () => {
    const rules = await getConditionalFormats()
    setFormatRules(rules)
  }

  const formatDate = (timestamp) => {
    const date = new Date(timestamp)
    if (interval === '1d') {
      return date.toLocaleDateString()
    }
    return date.toLocaleString()
  }

  const formatPrice = (price) => {
    if (price == null) return '-'
    return price.toFixed(2)
  }

  // Check if a row matches any enabled rule
  // Get opening mins object for a row
  const getOpeningMinsForRow = (timestamp) => {
    return {
      m5: getOpeningMinutesChange(timestamp, 5),
      m10: getOpeningMinutesChange(timestamp, 10),
      m15: getOpeningMinutesChange(timestamp, 15),
      m30: getOpeningMinutesChange(timestamp, 30),
      h1: getOpeningMinutesChange(timestamp, 60)
    }
  }

  const rowMatchesAnyRule = (row, rowIndex) => {
    if (formatRules.length === 0) return false
    const prevRow = data[rowIndex + 1]
    const prevPrevRow = data[rowIndex + 2]
    const vix = getVixForDay(row.timestamp)
    const prevVix = prevRow ? getVixForDay(prevRow.timestamp) : null
    const openingMins = getOpeningMinsForRow(row.timestamp)
    const prevOpeningMins = prevRow ? getOpeningMinsForRow(prevRow.timestamp) : null
    return formatRules.some(rule => evaluateRule(rule, row, prevRow, vix, prevVix, prevPrevRow, openingMins, prevOpeningMins))
  }

  // Filter data based on rules and day of week
  // Store original index for correct prevRow lookup
  const filteredData = useMemo(() => {
    let dataWithIndex = data.map((row, index) => ({ ...row, _originalIndex: index, _isMatch: false }))

    // Apply day of week filter first
    if (selectedDays.length > 0) {
      dataWithIndex = dataWithIndex.filter(row => selectedDays.includes(getDayOfWeek(row.timestamp)))
    }

    // If no conditional filter, return day-filtered data
    if (!filterEnabled || formatRules.length === 0) {
      return dataWithIndex
    }

    // Find all matching indices (using filtered data indices)
    const matchingIndices = new Set()
    dataWithIndex.forEach((row, index) => {
      if (rowMatchesAnyRule(row, row._originalIndex)) {
        matchingIndices.add(index)
        // Mark as actual match
        row._isMatch = true
      }
    })

    // Expand to include context rows
    const includedIndices = new Set()
    matchingIndices.forEach(index => {
      // Add rows before (data is descending, so "before" in time means higher index)
      for (let i = index; i <= Math.min(index + contextBefore, dataWithIndex.length - 1); i++) {
        includedIndices.add(i)
      }
      // Add rows after (data is descending, so "after" in time means lower index)
      for (let i = index; i >= Math.max(index - contextAfter, 0); i--) {
        includedIndices.add(i)
      }
    })

    return dataWithIndex
      .filter((_, index) => includedIndices.has(index))
      .map(row => ({
        ...row,
        _isMatch: matchingIndices.has(dataWithIndex.findIndex(r => r._originalIndex === row._originalIndex))
      }))
  }, [data, filterEnabled, formatRules, contextBefore, contextAfter, selectedDays])

  // Clear all filters
  const clearAllFilters = () => {
    setFilterEnabled(false)
    setSelectedDays([])
    setContextBefore(0)
    setContextAfter(0)
    // Clear conditional formatting selection
    conditionalFormatRef.current?.clearSelection()
  }

  const hasActiveFilters = filterEnabled || selectedDays.length > 0 || formatRules.length > 0

  const paginatedData = useMemo(() => {
    const start = currentPage * rowsPerPage
    const end = start + rowsPerPage
    return filteredData.slice(start, end)
  }, [filteredData, currentPage, rowsPerPage])

  // Check if any visible rows are missing VIX data
  const visibleRowsMissingVixData = useMemo(() => {
    if (!paginatedData || paginatedData.length === 0) return false
    return paginatedData.some(row => !hasVixData(row.timestamp))
  }, [paginatedData, vixData])

  // Calculate format statistics per column for visible rows only (performance optimization)
  const formatStats = useMemo(() => {
    if (formatRules.length === 0 || paginatedData.length === 0) return null

    const columns = ['open', 'high', 'low', 'close', 'volume', 'gap', 'dayChange', 'highVsPrevClose', 'lowVsPrevClose', '5m', '10m', '15m', '30m', '1h', 'vixOpen', 'vixClose']
    const stats = {}
    columns.forEach(col => { stats[col] = 0 })

    paginatedData.forEach(row => {
      const originalIndex = row._originalIndex
      const prevRow = data[originalIndex + 1]
      const prevPrevRow = data[originalIndex + 2]
      const vix = getVixForDay(row.timestamp)
      const prevVix = prevRow ? getVixForDay(prevRow.timestamp) : null
      const openingMins = getOpeningMinsForRow(row.timestamp)
      const prevOpeningMins = prevRow ? getOpeningMinsForRow(prevRow.timestamp) : null

      columns.forEach(column => {
        const format = getCellFormat(formatRules, row, prevRow, column, vix, prevVix, prevPrevRow, openingMins, prevOpeningMins)
        if (format) {
          stats[column]++
        }
      })
    })

    // Only include columns with matches
    const result = {}
    columns.forEach(col => {
      if (stats[col] > 0) {
        result[col] = stats[col]
      }
    })

    return Object.keys(result).length > 0 ? result : null
  }, [filteredData, formatRules, data, vixData])

  const totalPages = Math.ceil(filteredData.length / rowsPerPage)

  // Reset to first page when filter changes
  useEffect(() => {
    setCurrentPage(0)
  }, [filterEnabled, formatRules, selectedDays])

  const handlePageChange = (newPage) => {
    setCurrentPage(Math.max(0, Math.min(newPage, totalPages - 1)))
  }

  const handleRowsPerPageChange = (e) => {
    setRowsPerPage(Number(e.target.value))
    setCurrentPage(0)
  }

  // Calculate the date range based on context rows (trading days, not calendar days)
  const getContextDateRange = (row) => {
    // Handle case where _originalIndex might be undefined
    const originalIndex = row._originalIndex ?? data.findIndex(d => d.timestamp === row.timestamp)
    if (originalIndex < 0) {
      return { startTimestamp: row.timestamp, endTimestamp: row.timestamp }
    }

    // Data is sorted descending (newest first)
    // contextBefore = older rows = higher indices
    // contextAfter = newer rows = lower indices
    const startIndex = Math.min(originalIndex + contextBefore, data.length - 1)
    const endIndex = Math.max(originalIndex - contextAfter, 0)

    // Get timestamps from actual rows (these are trading days)
    const startTimestamp = data[startIndex]?.timestamp || row.timestamp
    const endTimestamp = data[endIndex]?.timestamp || row.timestamp

    return { startTimestamp, endTimestamp }
  }

  const handleToggleExpand = async (row) => {
    const rowKey = row.timestamp

    if (expandedRows[rowKey]) {
      // Collapse
      setExpandedRows(prev => ({ ...prev, [rowKey]: false }))
    } else {
      // Expand and fetch intraday data
      setExpandedRows(prev => ({ ...prev, [rowKey]: true }))

      // Check if we already have data
      if (!intradayData[rowKey]) {
        setLoadingIntraday(prev => ({ ...prev, [rowKey]: true }))
        try {
          const { startTimestamp, endTimestamp } = getContextDateRange(row)
          const dayData = await getIntradayForDay(symbol, startTimestamp, '5m', endTimestamp)
          setIntradayData(prev => ({ ...prev, [rowKey]: dayData }))
        } catch {
          setIntradayData(prev => ({ ...prev, [rowKey]: [] }))
        }
        setLoadingIntraday(prev => ({ ...prev, [rowKey]: false }))
      }
    }
  }

  // Clear expanded rows when symbol changes
  useEffect(() => {
    setExpandedRows({})
    setIntradayData({})
  }, [symbol])

  // Clear intraday data cache when context changes (so re-expanding fetches new range)
  useEffect(() => {
    setIntradayData({})
  }, [contextBefore, contextAfter])

  const handleFetchIntraday = async (row) => {
    const timestamp = row.timestamp
    setFetchingIntraday(prev => ({ ...prev, [timestamp]: true }))
    try {
      const { startTimestamp, endTimestamp } = getContextDateRange(row)
      const dayData = await fetchIntradayForDay(symbol, startTimestamp, '5m', endTimestamp)
      setIntradayData(prev => ({ ...prev, [timestamp]: dayData }))
    } catch {
    }
    setFetchingIntraday(prev => ({ ...prev, [timestamp]: false }))
  }

  // Get format for a specific cell
  const getFormat = (row, rowIndex, column) => {
    if (formatRules.length === 0) return {}

    // Get prev row using original index (data is sorted descending, so prev in time is next in array)
    const originalIndex = row._originalIndex
    const prevRow = data[originalIndex + 1]
    const prevPrevRow = data[originalIndex + 2]

    // Get VIX data for current and previous row
    const vix = getVixForDay(row.timestamp)
    const prevVix = prevRow ? getVixForDay(prevRow.timestamp) : null

    // Get opening mins data for current and previous row
    const openingMins = getOpeningMinsForRow(row.timestamp)
    const prevOpeningMins = prevRow ? getOpeningMinsForRow(prevRow.timestamp) : null

    const format = getCellFormat(formatRules, row, prevRow, column, vix, prevVix, prevPrevRow, openingMins, prevOpeningMins)
    if (!format) return {}

    const hasBg = format.bgColor && format.bgColor !== 'transparent'

    return {
      sx: {
        backgroundColor: hasBg ? `${format.bgColor} !important` : undefined,
        color: format.textColor ? `${format.textColor} !important` : undefined,
        fontWeight: format.bold ? 'bold !important' : undefined
      }
    }
  }

  // Render column header based on column ID
  const renderColumnHeader = (colId) => {
    if (!isColumnVisible(colId)) return null

    switch (colId) {
      case 'date':
        return <Th key={colId} color="gray.300">Date/Time</Th>
      case 'open':
        return <Th key={colId} color="gray.300" isNumeric>Open</Th>
      case 'high':
        return <Th key={colId} color="gray.300" isNumeric>High</Th>
      case 'low':
        return <Th key={colId} color="gray.300" isNumeric>Low</Th>
      case 'close':
        return <Th key={colId} color="gray.300" isNumeric>Close</Th>
      case 'volume':
        return <Th key={colId} color="gray.300" isNumeric>Volume</Th>
      case 'gap':
        return <Th key={colId} color="gray.300" isNumeric title="Gap (Open vs Prev Close %)">GAP</Th>
      case 'dayChange':
        return <Th key={colId} color="gray.300" isNumeric title="Day Change (Close vs Open %)">Day Chg</Th>
      case 'highVsPrevClose':
        return <Th key={colId} color="gray.300" isNumeric title="High vs Previous Close %">H vs PC</Th>
      case 'lowVsPrevClose':
        return <Th key={colId} color="gray.300" isNumeric title="Low vs Previous Close %">L vs PC</Th>
      case 'vixOpen':
        return (
          <Th key={colId} color="gray.300" isNumeric title="VIX Open">
            <HStack spacing={1} justify="flex-end">
              <Text>VIX O</Text>
              <Tooltip label={visibleRowsMissingVixData ? "Load VIX data for visible rows" : "VIX data loaded"}>
                <IconButton
                  aria-label="Load VIX data"
                  icon={<RepeatIcon />}
                  size="xs"
                  variant="ghost"
                  colorScheme="orange"
                  isLoading={loadingVix}
                  onClick={handleFetchVixData}
                  isDisabled={!connectionState?.connected || !visibleRowsMissingVixData}
                />
              </Tooltip>
            </HStack>
          </Th>
        )
      case 'vixClose':
        return (
          <Th key={colId} color="gray.300" isNumeric title="VIX Close">
            <HStack spacing={1} justify="flex-end">
              <Text>VIX C</Text>
              <Tooltip label={visibleRowsMissingVixData ? "Load VIX data for visible rows" : "VIX data loaded"}>
                <IconButton
                  aria-label="Load VIX data"
                  icon={<RepeatIcon />}
                  size="xs"
                  variant="ghost"
                  colorScheme="orange"
                  isLoading={loadingVix}
                  onClick={handleFetchVixData}
                  isDisabled={!connectionState?.connected || !visibleRowsMissingVixData}
                />
              </Tooltip>
            </HStack>
          </Th>
        )
      case '5m':
      case '10m':
      case '15m':
      case '30m':
      case '1h':
        const minLabel = colId === '1h' ? '1h' : colId
        return (
          <Th key={colId} color="gray.300" isNumeric title={`First ${minLabel}`}>
            <HStack spacing={1} justify="flex-end">
              <Text>{minLabel}</Text>
              <Tooltip label="Load 5min data for visible rows">
                <IconButton
                  aria-label="Load 5min data"
                  icon={<RepeatIcon />}
                  size="xs"
                  variant="ghost"
                  colorScheme="blue"
                  isLoading={loadingOpeningMins}
                  onClick={handleFetchOpeningMinsData}
                  isDisabled={!connectionState?.connected}
                />
              </Tooltip>
            </HStack>
          </Th>
        )
      default:
        return null
    }
  }

  // Render column cell based on column ID
  const renderColumnCell = (colId, row, index) => {
    if (!isColumnVisible(colId)) return null
    const prevRow = data[row._originalIndex + 1]

    switch (colId) {
      case 'date':
        return (
          <Td key={colId} color="gray.300">
            {filterEnabled && !row._isMatch && <Text as="span" color="gray.500" mr={1}>•</Text>}
            {formatDate(row.timestamp)}
          </Td>
        )
      case 'open':
        return <Td key={colId} isNumeric {...getFormat(row, index, 'open')}>{formatPrice(row.open)}</Td>
      case 'high':
        return <Td key={colId} isNumeric {...getFormat(row, index, 'high')}>{formatPrice(row.high)}</Td>
      case 'low':
        return <Td key={colId} isNumeric {...getFormat(row, index, 'low')}>{formatPrice(row.low)}</Td>
      case 'close':
        return <Td key={colId} isNumeric {...getFormat(row, index, 'close')}>{formatPrice(row.close)}</Td>
      case 'volume':
        return <Td key={colId} isNumeric {...getFormat(row, index, 'volume')}>{row.volume?.toLocaleString() || '-'}</Td>
      case 'gap':
        return (
          <Td key={colId} isNumeric {...getFormat(row, index, 'gap')}>
            {(() => {
              if (!prevRow || !prevRow.close || prevRow.close === 0) return '-'
              const gap = ((row.open - prevRow.close) / prevRow.close) * 100
              const color = gap >= 0 ? 'green.400' : 'red.400'
              return <Text color={color}>{gap >= 0 ? '+' : ''}{gap.toFixed(2)}%</Text>
            })()}
          </Td>
        )
      case 'dayChange':
        return (
          <Td key={colId} isNumeric {...getFormat(row, index, 'dayChange')}>
            {(() => {
              if (!row.open || row.open === 0) return '-'
              const change = ((row.close - row.open) / row.open) * 100
              const color = change >= 0 ? 'green.400' : 'red.400'
              return <Text color={color}>{change >= 0 ? '+' : ''}{change.toFixed(2)}%</Text>
            })()}
          </Td>
        )
      case 'highVsPrevClose':
        return (
          <Td key={colId} isNumeric {...getFormat(row, index, 'highVsPrevClose')}>
            {(() => {
              if (!prevRow || !prevRow.close || prevRow.close === 0) return '-'
              const change = ((row.high - prevRow.close) / prevRow.close) * 100
              const color = change >= 0 ? 'green.400' : 'red.400'
              return <Text color={color}>{change >= 0 ? '+' : ''}{change.toFixed(2)}%</Text>
            })()}
          </Td>
        )
      case 'lowVsPrevClose':
        return (
          <Td key={colId} isNumeric {...getFormat(row, index, 'lowVsPrevClose')}>
            {(() => {
              if (!prevRow || !prevRow.close || prevRow.close === 0) return '-'
              const change = ((row.low - prevRow.close) / prevRow.close) * 100
              const color = change >= 0 ? 'green.400' : 'red.400'
              return <Text color={color}>{change >= 0 ? '+' : ''}{change.toFixed(2)}%</Text>
            })()}
          </Td>
        )
      case 'vixOpen':
        return (
          <Td key={colId} isNumeric>
            {(() => {
              const vix = getVixForDay(row.timestamp)
              if (vix === null) return '-'
              return <Text color="orange.400">{Number(vix.open).toFixed(2)}</Text>
            })()}
          </Td>
        )
      case 'vixClose':
        return (
          <Td key={colId} isNumeric>
            {(() => {
              const vix = getVixForDay(row.timestamp)
              if (vix === null) return '-'
              return <Text color="orange.400">{Number(vix.close).toFixed(2)}</Text>
            })()}
          </Td>
        )
      case '5m':
      case '10m':
      case '15m':
      case '30m':
      case '1h':
        const mins = colId === '1h' ? 60 : parseInt(colId)
        return (
          <Td key={colId} isNumeric {...getFormat(row, index, colId)}>
            {(() => {
              const change = getOpeningMinutesChange(row.timestamp, mins)
              if (change === null) return '-'
              const color = change >= 0 ? 'green.400' : 'red.400'
              return <Text color={color}>{change >= 0 ? '+' : ''}{change.toFixed(2)}%</Text>
            })()}
          </Td>
        )
      default:
        return null
    }
  }

  if (loading) {
    return (
      <Center h="400px" bg="gray.800" borderRadius="md">
        <Spinner size="xl" color="blue.400" />
      </Center>
    )
  }

  if (!symbol) {
    return (
      <Center h="400px" bg="gray.800" borderRadius="md">
        <Text color="gray.500" fontSize="lg">
          Select a security to view price data
        </Text>
      </Center>
    )
  }

  if (data.length === 0) {
    return (
      <Center h="400px" bg="gray.800" borderRadius="md">
        <VStack spacing={4}>
          <Text color="gray.500" fontSize="lg">No data for {symbol}</Text>
          <Button
            colorScheme="blue"
            leftIcon={<RepeatIcon />}
            onClick={onLoadData}
            isDisabled={!connectionState?.connected}
          >
            Load from IBKR
          </Button>
          {!connectionState?.connected && (
            <Text color="gray.600" fontSize="sm">Connect to IBKR first</Text>
          )}
        </VStack>
      </Center>
    )
  }

  return (
    <Box bg="gray.800" borderRadius="md" overflow="hidden">
      {/* Header */}
      <HStack p={3} bg="gray.700" justify="space-between">
        <HStack>
          <Badge colorScheme="blue" fontSize="md" px={2}>
            {symbol}
          </Badge>
          <Badge colorScheme="purple" fontSize="sm">
            {interval}
          </Badge>
          <Text color="gray.400" fontSize="sm">
            {data.length} bars
          </Text>
        </HStack>
        <HStack>
          <ConditionalFormatManager onRulesChange={setFormatRules} clearRef={conditionalFormatRef} />
          {formatStats && (
            <Popover trigger="hover" placement="bottom">
              <PopoverTrigger>
                <IconButton
                  aria-label="Format statistics"
                  icon={<InfoIcon />}
                  size="sm"
                  variant="ghost"
                  colorScheme="purple"
                />
              </PopoverTrigger>
              <PopoverContent bg="gray.700" borderColor="gray.600" w="auto" minW="180px">
                <PopoverArrow bg="gray.700" />
                <PopoverHeader borderColor="gray.600" fontWeight="bold" color="gray.200">
                  Format Stats ({paginatedData.length} visible)
                </PopoverHeader>
                <PopoverBody>
                  <VStack align="stretch" spacing={1}>
                    {Object.entries(formatStats).map(([col, count]) => (
                      <HStack key={col} justify="space-between">
                        <Text color="gray.300" fontSize="sm">
                          {col === 'gap' ? 'Gap' :
                           col === 'highVsPrevClose' ? 'H vs PC' :
                           col === 'lowVsPrevClose' ? 'L vs PC' :
                           col === '5m' ? 'First 5m' :
                           col === '10m' ? 'First 10m' :
                           col === '15m' ? 'First 15m' :
                           col === '30m' ? 'First 30m' :
                           col === '1h' ? 'First 1h' :
                           col === 'vixOpen' ? 'VIX O' :
                           col === 'vixClose' ? 'VIX C' :
                           col === 'prevVixOpen' ? 'Prev VIX O' :
                           col === 'prevVixClose' ? 'Prev VIX C' :
                           col.charAt(0).toUpperCase() + col.slice(1)}
                        </Text>
                        <Badge colorScheme="purple">{count}</Badge>
                      </HStack>
                    ))}
                    <HStack justify="space-between" pt={1} borderTop="1px solid" borderColor="gray.600">
                      <Text color="gray.200" fontSize="sm" fontWeight="bold">Total</Text>
                      <Badge colorScheme="green">
                        {Object.values(formatStats).reduce((a, b) => a + b, 0)}
                      </Badge>
                    </HStack>
                  </VStack>
                </PopoverBody>
              </PopoverContent>
            </Popover>
          )}
          <Tooltip label={filterEnabled ? "Show all rows" : "Filter to matching rows"}>
            <IconButton
              aria-label="Toggle filter"
              icon={filterEnabled ? <ViewIcon /> : <ViewOffIcon />}
              size="sm"
              variant={filterEnabled ? "solid" : "outline"}
              colorScheme={filterEnabled ? "green" : "gray"}
              onClick={() => setFilterEnabled(!filterEnabled)}
              isDisabled={formatRules.length === 0}
            />
          </Tooltip>
          {filterEnabled && (
            <>
              <HStack spacing={1}>
                <Tooltip label="Rows before match">
                  <Text fontSize="xs" color="gray.400">B:</Text>
                </Tooltip>
                <NumberInput
                  size="xs"
                  w="50px"
                  min={0}
                  max={50}
                  value={contextBefore}
                  onChange={(_, val) => setContextBefore(val || 0)}
                >
                  <NumberInputField bg="gray.700" px={2} />
                  <NumberInputStepper>
                    <NumberIncrementStepper />
                    <NumberDecrementStepper />
                  </NumberInputStepper>
                </NumberInput>
              </HStack>
              <HStack spacing={1}>
                <Tooltip label="Rows after match">
                  <Text fontSize="xs" color="gray.400">A:</Text>
                </Tooltip>
                <NumberInput
                  size="xs"
                  w="50px"
                  min={0}
                  max={50}
                  value={contextAfter}
                  onChange={(_, val) => setContextAfter(val || 0)}
                >
                  <NumberInputField bg="gray.700" px={2} />
                  <NumberInputStepper>
                    <NumberIncrementStepper />
                    <NumberDecrementStepper />
                  </NumberInputStepper>
                </NumberInput>
              </HStack>
              <Badge colorScheme="green" fontSize="xs">
                {filteredData.length} / {data.length}
              </Badge>
            </>
          )}
          {/* Day of week filter */}
          <Menu closeOnSelect={false}>
            <MenuButton
              as={Button}
              size="sm"
              variant={selectedDays.length > 0 ? "solid" : "outline"}
              colorScheme={selectedDays.length > 0 ? "orange" : "gray"}
            >
              {selectedDays.length > 0
                ? selectedDays.map(d => DAYS_OF_WEEK[d].label).join(', ')
                : 'Days'}
            </MenuButton>
            <MenuList bg="gray.700" minW="120px">
              {DAYS_OF_WEEK.filter(d => d.id >= 1 && d.id <= 5).map(day => (
                <MenuItem
                  key={day.id}
                  bg="transparent"
                  _hover={{ bg: 'gray.600' }}
                >
                  <Checkbox
                    isChecked={selectedDays.includes(day.id)}
                    onChange={() => toggleDay(day.id)}
                    colorScheme="orange"
                    mr={2}
                  >
                    {day.fullLabel}
                  </Checkbox>
                </MenuItem>
              ))}
              {selectedDays.length > 0 && (
                <>
                  <MenuDivider />
                  <MenuItem
                    bg="transparent"
                    _hover={{ bg: 'gray.600' }}
                    onClick={clearDayFilter}
                  >
                    <Text color="gray.400">Clear days</Text>
                  </MenuItem>
                </>
              )}
            </MenuList>
          </Menu>
          {/* Clear all filters */}
          {hasActiveFilters && (
            <Tooltip label="Clear all filters">
              <IconButton
                aria-label="Clear all filters"
                icon={<CloseIcon />}
                size="sm"
                variant="ghost"
                colorScheme="red"
                onClick={clearAllFilters}
              />
            </Tooltip>
          )}
          {/* Show filtered count when day filter is active but conditional filter is not */}
          {selectedDays.length > 0 && !filterEnabled && (
            <Badge colorScheme="orange" fontSize="xs">
              {filteredData.length} / {data.length}
            </Badge>
          )}
          <Text color="gray.400" fontSize="sm">Rows:</Text>
          <Select
            size="sm"
            w="80px"
            value={rowsPerPage}
            onChange={handleRowsPerPageChange}
            bg="gray.600"
          >
            {ROWS_PER_PAGE_OPTIONS.map(opt => (
              <option key={opt} value={opt}>{opt}</option>
            ))}
          </Select>
          <Menu closeOnSelect={false}>
            <MenuButton as={Button} size="sm" variant="outline" colorScheme="gray">
              Columns
            </MenuButton>
            <MenuList bg="gray.700" minW="220px" maxH="400px" overflowY="auto">
              {/* All columns in order */}
              {orderedColumns.map((col, idx) => (
                (!col.dailyOnly || interval === '1d') && (
                  <HStack
                    key={col.id}
                    px={3}
                    py={1}
                    _hover={{ bg: 'gray.600' }}
                    justify="space-between"
                  >
                    <Checkbox
                      isChecked={visibleColumns?.[col.id] !== false}
                      onChange={() => toggleColumn(col.id)}
                      colorScheme="blue"
                      size="sm"
                    >
                      <Text fontSize="sm">{col.label}</Text>
                    </Checkbox>
                    <HStack spacing={0}>
                      <IconButton
                        aria-label="Move up"
                        icon={<ArrowUpIcon />}
                        size="xs"
                        variant="ghost"
                        isDisabled={idx === 0}
                        onClick={() => moveColumnUp(col.id)}
                      />
                      <IconButton
                        aria-label="Move down"
                        icon={<ArrowDownIcon />}
                        size="xs"
                        variant="ghost"
                        isDisabled={idx === orderedColumns.length - 1}
                        onClick={() => moveColumnDown(col.id)}
                      />
                    </HStack>
                  </HStack>
                )
              ))}
              <MenuDivider />
              {/* Column groups visibility */}
              {COLUMN_GROUPS.map(group => (
                (!group.dailyOnly || interval === '1d') && (
                  <MenuItem
                    key={group.id}
                    bg="transparent"
                    _hover={{ bg: 'gray.600' }}
                  >
                    <Checkbox
                      isChecked={isGroupVisible(group.id)}
                      isIndeterminate={isGroupPartiallyVisible(group.id)}
                      onChange={() => toggleColumnGroup(group.id)}
                      colorScheme="blue"
                      size="sm"
                    >
                      <Text fontSize="sm" fontWeight="bold">{group.label} (all)</Text>
                    </Checkbox>
                  </MenuItem>
                )
              ))}
              <MenuDivider />
              <MenuItem
                bg="transparent"
                _hover={{ bg: 'gray.600' }}
                onClick={resetColumnOrder}
              >
                <Text fontSize="sm" color="gray.400">Reset order</Text>
              </MenuItem>
            </MenuList>
          </Menu>
          <Menu closeOnSelect={false}>
            <MenuButton as={Button} size="sm" variant="outline" colorScheme="teal">
              Presets
            </MenuButton>
            <MenuList bg="gray.700" minW="200px">
              <Box px={3} py={2}>
                <HStack>
                  <Input
                    size="sm"
                    placeholder="Preset name"
                    value={newPresetName}
                    onChange={(e) => setNewPresetName(e.target.value)}
                    bg="gray.600"
                    onKeyDown={(e) => e.key === 'Enter' && handleSavePreset()}
                  />
                  <Button size="sm" colorScheme="green" onClick={handleSavePreset}>
                    Save
                  </Button>
                </HStack>
              </Box>
              {presets.length > 0 && <MenuDivider />}
              {presets.map(preset => (
                <HStack
                  key={preset.id}
                  px={3}
                  py={2}
                  _hover={{ bg: 'gray.600' }}
                  cursor="pointer"
                  justify="space-between"
                >
                  {editingPresetId === preset.id ? (
                    <>
                      <Input
                        size="sm"
                        value={editingPresetName}
                        onChange={(e) => setEditingPresetName(e.target.value)}
                        onClick={(e) => e.stopPropagation()}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') handleSavePresetName(e)
                          if (e.key === 'Escape') handleCancelEditPreset(e)
                        }}
                        bg="gray.600"
                        autoFocus
                        flex={1}
                      />
                      <HStack spacing={1}>
                        <Box
                          as="span"
                          color="green.400"
                          cursor="pointer"
                          _hover={{ color: 'green.300' }}
                          onClick={handleSavePresetName}
                          p={1}
                        >
                          <CheckIcon boxSize={3} />
                        </Box>
                        <Box
                          as="span"
                          color="gray.400"
                          cursor="pointer"
                          _hover={{ color: 'gray.300' }}
                          onClick={handleCancelEditPreset}
                          p={1}
                        >
                          <CloseIcon boxSize={3} />
                        </Box>
                      </HStack>
                    </>
                  ) : (
                    <>
                      <Text onClick={() => handleLoadPreset(preset)} flex={1}>{preset.name}</Text>
                      <HStack spacing={1}>
                        <Box
                          as="span"
                          color="blue.400"
                          cursor="pointer"
                          _hover={{ color: 'blue.300' }}
                          onClick={(e) => handleStartEditPreset(e, preset)}
                          p={1}
                        >
                          <EditIcon boxSize={3} />
                        </Box>
                        <Box
                          as="span"
                          color="red.400"
                          cursor="pointer"
                          _hover={{ color: 'red.300' }}
                          onClick={(e) => handleDeletePreset(e, preset.id)}
                          p={1}
                        >
                          <DeleteIcon boxSize={3} />
                        </Box>
                      </HStack>
                    </>
                  )}
                </HStack>
              ))}
              {presets.length === 0 && (
                <MenuItem isDisabled>No presets saved</MenuItem>
              )}
            </MenuList>
          </Menu>
        </HStack>
      </HStack>

      {/* Table */}
      <TableContainer>
        <Table variant="striped" size="sm">
          <Thead position="sticky" top={0} bg="gray.700" zIndex={1}>
            <Tr>
              <Th color="gray.300" w="40px"></Th>
              {orderedColumns.map(col => renderColumnHeader(col.id))}
            </Tr>
          </Thead>
          <Tbody>
            {paginatedData.map((row, index) => {
              // Check if there are hidden rows before this one
              const pageStartIndex = currentPage * rowsPerPage
              const prevRowInPage = index > 0 ? paginatedData[index - 1] :
                (pageStartIndex > 0 ? filteredData[pageStartIndex - 1] : null)
              const hiddenCount = prevRowInPage ?
                (row._originalIndex - prevRowInPage._originalIndex - 1) : 0
              const showGapIndicator = filterEnabled && hiddenCount > 0

              const isContextRow = filterEnabled && !row._isMatch

              return (
                <React.Fragment key={row.id || index}>
                  {showGapIndicator && (
                    <Tr>
                      <Td colSpan={visibleColCount} py={1} bg="gray.700" borderY="1px dashed" borderColor="gray.500">
                        <Text fontSize="xs" color="gray.400" textAlign="center">
                          ··· {hiddenCount} hidden row{hiddenCount > 1 ? 's' : ''} ···
                        </Text>
                      </Td>
                    </Tr>
                  )}
                  <Tr opacity={isContextRow ? 0.5 : 1} bg={isContextRow ? 'gray.750' : undefined}>
                    <Td p={1}>
                      <IconButton
                        aria-label="Expand row"
                        icon={expandedRows[row.timestamp] ? <ChevronDownIcon /> : <ChevronRightIcon />}
                        size="xs"
                        variant="ghost"
                        onClick={() => handleToggleExpand(row)}
                      />
                    </Td>
                    {orderedColumns.map(col => renderColumnCell(col.id, row, index))}
                  </Tr>
                  {expandedRows[row.timestamp] && (
                    <Tr>
                      <Td colSpan={visibleColCount} bg="gray.900" p={4}>
                        <CandlestickChart
                          data={intradayData[row.timestamp]}
                          loading={loadingIntraday[row.timestamp]}
                          symbol={symbol}
                          date={formatDate(row.timestamp)}
                          onFetchData={() => handleFetchIntraday(row)}
                          canFetch={connectionState?.connected}
                          fetching={fetchingIntraday[row.timestamp]}
                        />
                      </Td>
                    </Tr>
                  )}
                </React.Fragment>
              )
            })}
          </Tbody>
        </Table>
      </TableContainer>

      {/* Pagination */}
      {totalPages > 1 && (
        <HStack p={3} bg="gray.700" justify="center" spacing={4}>
          <Button
            size="sm"
            onClick={() => handlePageChange(0)}
            isDisabled={currentPage === 0}
          >
            First
          </Button>
          <Button
            size="sm"
            onClick={() => handlePageChange(currentPage - 1)}
            isDisabled={currentPage === 0}
          >
            Prev
          </Button>
          <Text color="gray.300" fontSize="sm">
            Page {currentPage + 1} of {totalPages}
          </Text>
          <Button
            size="sm"
            onClick={() => handlePageChange(currentPage + 1)}
            isDisabled={currentPage >= totalPages - 1}
          >
            Next
          </Button>
          <Button
            size="sm"
            onClick={() => handlePageChange(totalPages - 1)}
            isDisabled={currentPage >= totalPages - 1}
          >
            Last
          </Button>
        </HStack>
      )}
    </Box>
  )
}

export default SecuritySpreadsheet
