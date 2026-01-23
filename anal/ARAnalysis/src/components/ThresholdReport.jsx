import { useMemo, useState, useEffect, Fragment } from 'react'
import {
  Box,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  Text,
  Heading,
  Badge,
  HStack,
  Button,
  Select,
  Spinner,
  VStack,
  IconButton,
  Collapse,
  Tooltip
} from '@chakra-ui/react'
import { ChevronDownIcon, ChevronRightIcon, WarningIcon } from '@chakra-ui/icons'
import { fetchFromCache, fetchOptionsChain, fetchCurrentPrices } from '../modules/dataService'
import { getWatchgroups, getWatchgroupSymbols, getActiveWatchgroup } from '../db/database'

// Time periods in trading days
const TIME_PERIODS = [
  { label: '50d', days: 50 },
  { label: '100d', days: 100 },
  { label: '200d', days: 200 },
  { label: '1yr', days: 252 },
  { label: '2yr', days: 504 },
  { label: '3yr', days: 756 }
]

// Change periods in trading days
const CHANGE_PERIODS = [
  { label: '1d', days: 1 },
  { label: '2d', days: 2 },
  { label: '3d', days: 3 },
  { label: '5d', days: 5 },
  { label: '1mo', days: 21 }
]

// Thresholds to check (upside only)
const THRESHOLDS = [0.3, 0.5, 0.8, 1.0]

// Calculate percentage change from N trading days ago
function calculateChange(data, tradingDays) {
  if (!data || data.length <= tradingDays) return null

  const currentClose = data[0]?.close
  const pastClose = data[tradingDays]?.close

  if (!currentClose || !pastClose || pastClose === 0) return null

  return ((currentClose - pastClose) / pastClose) * 100
}

// Analyze how many times price hit thresholds within a given period
function analyzeThresholds(data, numDays) {
  if (!data || data.length < 2) {
    return { counts: {}, validDays: 0 }
  }

  // Data is descending (newest first), take numDays + 1 to get numDays comparisons
  const subset = data.slice(0, numDays + 1)
  const counts = {}
  THRESHOLDS.forEach(t => { counts[t] = 0 })
  let validDays = 0

  for (let i = 0; i < subset.length - 1; i++) {
    const bar = subset[i]
    const prevBar = subset[i + 1]

    if (!prevBar?.close || prevBar.close === 0 || !bar?.high) continue

    const pctChange = ((bar.high - prevBar.close) / prevBar.close) * 100
    validDays++

    THRESHOLDS.forEach(t => {
      if (pctChange >= t) counts[t]++
    })
  }

  return { counts, validDays }
}

// Get color scheme based on hit rate percentage
function getColorScheme(hitRate) {
  if (hitRate >= 70) return 'green'
  if (hitRate >= 60) return 'blue'
  if (hitRate >= 30) return 'yellow'
  return 'gray'
}

// Single symbol report component
function SingleSymbolReport({ symbol, data }) {
  const results = useMemo(() => {
    if (!data || data.length === 0) return null

    const periodResults = {}
    TIME_PERIODS.forEach(period => {
      periodResults[period.label] = analyzeThresholds(data, period.days)
    })
    return periodResults
  }, [data])

  if (!symbol) {
    return (
      <Box bg="gray.800" borderRadius="md" p={6}>
        <Text color="gray.400">Select a symbol to view threshold report</Text>
      </Box>
    )
  }

  if (!data || data.length === 0) {
    return (
      <Box bg="gray.800" borderRadius="md" p={6}>
        <Text color="gray.400">No data available for {symbol}</Text>
      </Box>
    )
  }

  const availableDays = data.length - 1

  return (
    <Box>
      <Heading size="sm" color="blue.300" mb={3}>
        {symbol}
      </Heading>

      <Box overflowX="auto">
        <Table variant="simple" size="sm">
          <Thead>
            <Tr>
              <Th color="gray.400" borderColor="gray.600">Threshold</Th>
              {TIME_PERIODS.map(period => (
                <Th key={period.label} color="gray.400" borderColor="gray.600" textAlign="center">
                  {period.label}
                  {availableDays < period.days && (
                    <Text fontSize="xs" color="yellow.400" fontWeight="normal">
                      ({Math.min(availableDays, period.days)} avail)
                    </Text>
                  )}
                </Th>
              ))}
            </Tr>
          </Thead>
          <Tbody>
            {THRESHOLDS.map(threshold => (
              <Tr key={threshold}>
                <Td borderColor="gray.600" fontWeight="medium" color="gray.200">
                  +{threshold}%
                </Td>
                {TIME_PERIODS.map(period => {
                  const result = results?.[period.label]
                  if (!result || result.validDays === 0) {
                    return (
                      <Td key={period.label} borderColor="gray.600" textAlign="center">
                        <Text color="gray.500">-</Text>
                      </Td>
                    )
                  }

                  const count = result.counts[threshold] || 0
                  const hitRate = (count / result.validDays) * 100
                  const colorScheme = getColorScheme(hitRate)
                  const variant = hitRate >= 70 ? 'solid' : 'subtle'

                  return (
                    <Td key={period.label} borderColor="gray.600" textAlign="center">
                      <HStack justify="center" spacing={2}>
                        <Badge colorScheme={colorScheme} variant={variant} px={2}>
                          {count}/{result.validDays}
                        </Badge>
                        <Text fontSize="xs" color="gray.400">
                          ({hitRate.toFixed(0)}%)
                        </Text>
                      </HStack>
                    </Td>
                  )
                })}
              </Tr>
            ))}
          </Tbody>
        </Table>
      </Box>

      <Text mt={2} fontSize="xs" color="gray.500">
        Data available: {availableDays} trading days
      </Text>
    </Box>
  )
}

// Format expiration date from YYYYMMDD to readable format
function formatExpiration(exp) {
  if (!exp || exp.length !== 8) return exp
  const year = exp.substring(0, 4)
  const month = parseInt(exp.substring(4, 6)) - 1
  const day = exp.substring(6, 8)
  const date = new Date(year, month, day)
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

// Options chain table component
function OptionsChainTable({ chain, currentPrice }) {
  if (!chain || !chain.options || chain.options.length === 0) {
    return <Text color="gray.500">No options data</Text>
  }

  // Group options by expiration and strike
  const byExpiration = {}
  chain.options.forEach(opt => {
    if (!byExpiration[opt.expiration]) {
      byExpiration[opt.expiration] = {}
    }
    if (!byExpiration[opt.expiration][opt.strike]) {
      byExpiration[opt.expiration][opt.strike] = { call: null, put: null }
    }
    if (opt.right === 'C') {
      byExpiration[opt.expiration][opt.strike].call = opt
    } else {
      byExpiration[opt.expiration][opt.strike].put = opt
    }
  })

  // Sort expirations
  const expirations = Object.keys(byExpiration).sort()

  return (
    <VStack align="stretch" spacing={4}>
      <Text color="gray.300" fontSize="sm">
        Options Chain for {chain.symbol} | Current: ${currentPrice?.toFixed(2)}
      </Text>
      {expirations.slice(0, 4).map(exp => {
        const strikeData = byExpiration[exp]
        const strikes = Object.keys(strikeData).map(Number).sort((a, b) => a - b)

        // Filter to strikes near the money (within 20%)
        const filteredStrikes = strikes.filter(strike => {
          if (!currentPrice) return true
          const diff = Math.abs(strike - currentPrice) / currentPrice
          return diff <= 0.2
        })

        return (
          <Box key={exp}>
            <Text color="orange.300" fontSize="sm" fontWeight="bold" mb={2}>
              {formatExpiration(exp)}
            </Text>
            <Table size="sm" variant="simple">
              <Thead>
                <Tr>
                  <Th color="green.300" borderColor="gray.600" fontSize="xs" px={2} textAlign="center" colSpan={4}>CALLS</Th>
                  <Th color="yellow.300" borderColor="gray.600" fontSize="xs" px={2} textAlign="center">STRIKE</Th>
                  <Th color="red.300" borderColor="gray.600" fontSize="xs" px={2} textAlign="center" colSpan={4}>PUTS</Th>
                </Tr>
                <Tr>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>OI</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Vol</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Bid</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Ask</Th>
                  <Th color="yellow.300" borderColor="gray.600" fontSize="xs" px={2} textAlign="center" bg="gray.700"></Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Bid</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Ask</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>Vol</Th>
                  <Th color="gray.400" borderColor="gray.600" fontSize="xs" px={2}>OI</Th>
                </Tr>
              </Thead>
              <Tbody>
                {filteredStrikes.map(strike => {
                  const { call, put } = strikeData[strike]
                  const isITMCall = strike <= currentPrice
                  const isITMPut = strike >= currentPrice
                  return (
                    <Tr key={strike}>
                      {/* Call side */}
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMCall ? 'green.900' : undefined}>
                        {call?.openInterest?.toLocaleString() || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMCall ? 'green.900' : undefined}>
                        {call?.volume?.toLocaleString() || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMCall ? 'green.900' : undefined}>
                        {call?.bid?.toFixed(2) || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMCall ? 'green.900' : undefined}>
                        {call?.ask?.toFixed(2) || '-'}
                      </Td>
                      {/* Strike in center */}
                      <Td borderColor="gray.600" color="yellow.200" px={3} textAlign="center" fontWeight="bold" bg="gray.700">
                        ${strike}
                      </Td>
                      {/* Put side */}
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMPut ? 'red.900' : undefined}>
                        {put?.bid?.toFixed(2) || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMPut ? 'red.900' : undefined}>
                        {put?.ask?.toFixed(2) || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMPut ? 'red.900' : undefined}>
                        {put?.volume?.toLocaleString() || '-'}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" px={2} bg={isITMPut ? 'red.900' : undefined}>
                        {put?.openInterest?.toLocaleString() || '-'}
                      </Td>
                    </Tr>
                  )
                })}
                {filteredStrikes.length === 0 && (
                  <Tr><Td colSpan={9} color="gray.500" textAlign="center">No options near the money</Td></Tr>
                )}
              </Tbody>
            </Table>
          </Box>
        )
      })}
    </VStack>
  )
}

const PAGE_SIZE = 20

// Multi-symbol ranking report
function MultiSymbolReport() {
  const [allResults, setAllResults] = useState(null)
  const [loading, setLoading] = useState(false)
  const [selectedThreshold, setSelectedThreshold] = useState(0.5)
  const [selectedPeriod, setSelectedPeriod] = useState('100d')
  const [watchgroups, setWatchgroups] = useState([])
  const [selectedGroupId, setSelectedGroupId] = useState(null)
  const [groupSymbols, setGroupSymbols] = useState([])
  const [expandedRows, setExpandedRows] = useState({}) // symbol -> true/false
  const [optionsData, setOptionsData] = useState({}) // symbol -> options chain data
  const [loadingOptions, setLoadingOptions] = useState({}) // symbol -> true/false
  const [currentPage, setCurrentPage] = useState(1)
  const [currentPrices, setCurrentPrices] = useState({}) // symbol -> { price, fetchedAt }
  const [loadingPrices, setLoadingPrices] = useState(false)

  // Load watchgroups on mount
  useEffect(() => {
    loadWatchgroups()
  }, [])

  // Load symbols when selected group changes
  useEffect(() => {
    if (selectedGroupId) {
      getWatchgroupSymbols(selectedGroupId).then(setGroupSymbols)
    } else {
      setGroupSymbols([])
    }
  }, [selectedGroupId])

  const loadWatchgroups = async () => {
    const groups = await getWatchgroups()
    setWatchgroups(groups)
    const activeId = await getActiveWatchgroup()
    if (activeId && groups.some(g => g.id === activeId)) {
      setSelectedGroupId(activeId)
    } else if (groups.length > 0) {
      setSelectedGroupId(groups[0].id)
    }
  }

  const loadAllData = async () => {
    if (!groupSymbols || groupSymbols.length === 0) return

    setLoading(true)
    const results = []

    for (const symbol of groupSymbols) {
      try {
        const data = await fetchFromCache(symbol, '1d')
        if (data && data.length > 0) {
          const periodResults = {}
          TIME_PERIODS.forEach(period => {
            periodResults[period.label] = analyzeThresholds(data, period.days)
          })
          // Calculate changes for each period
          const changes = {}
          CHANGE_PERIODS.forEach(period => {
            changes[period.label] = calculateChange(data, period.days)
          })
          // Get current price (latest bar's close)
          const latestBar = data[0]
          const currentPrice = latestBar?.close || null
          results.push({
            symbol: symbol,
            results: periodResults,
            changes,
            dataLength: data.length,
            currentPrice
          })
        }
      } catch (err) {
        console.error(`Failed to load ${symbol}:`, err)
      }
    }

    setAllResults(results)
    setLoading(false)
  }

  // Fetch current/live prices for visible (paginated) symbols only
  const fetchLivePrices = async () => {
    if (!paginatedResults || paginatedResults.length === 0) return

    setLoadingPrices(true)
    try {
      const symbols = paginatedResults.map(r => r.symbol)
      const prices = await fetchCurrentPrices(symbols)
      const now = Date.now()
      // Store prices with timestamp
      const pricesWithTime = {}
      Object.entries(prices).forEach(([symbol, price]) => {
        pricesWithTime[symbol] = { price, fetchedAt: now }
      })
      setCurrentPrices(prev => ({ ...prev, ...pricesWithTime }))
    } catch (err) {
      console.error('Failed to fetch live prices:', err)
    }
    setLoadingPrices(false)
  }

  // Toggle row expansion and fetch options chain
  const toggleRow = async (symbol, currentPrice) => {
    const isExpanded = expandedRows[symbol]

    if (isExpanded) {
      // Collapse
      setExpandedRows(prev => ({ ...prev, [symbol]: false }))
    } else {
      // Expand and fetch options if not already loaded
      setExpandedRows(prev => ({ ...prev, [symbol]: true }))

      if (!optionsData[symbol] && !loadingOptions[symbol]) {
        setLoadingOptions(prev => ({ ...prev, [symbol]: true }))
        try {
          const chain = await fetchOptionsChain(symbol, currentPrice)
          setOptionsData(prev => ({ ...prev, [symbol]: chain }))
        } catch (err) {
          console.error(`Failed to fetch options chain for ${symbol}:`, err)
          setOptionsData(prev => ({ ...prev, [symbol]: { error: err.message } }))
        }
        setLoadingOptions(prev => ({ ...prev, [symbol]: false }))
      }
    }
  }

  // Calculate hit rate for sorting
  const getHitRate = (item, threshold, periodLabel) => {
    const result = item.results?.[periodLabel]
    if (!result || result.validDays === 0) return -1
    return (result.counts[threshold] / result.validDays) * 100
  }

  // Sort results by selected threshold/period
  const sortedResults = useMemo(() => {
    if (!allResults) return null
    return [...allResults].sort((a, b) => {
      const rateA = getHitRate(a, selectedThreshold, selectedPeriod)
      const rateB = getHitRate(b, selectedThreshold, selectedPeriod)
      return rateB - rateA
    })
  }, [allResults, selectedThreshold, selectedPeriod])

  // Pagination
  const totalPages = sortedResults ? Math.ceil(sortedResults.length / PAGE_SIZE) : 0
  const paginatedResults = useMemo(() => {
    if (!sortedResults) return null
    const startIdx = (currentPage - 1) * PAGE_SIZE
    return sortedResults.slice(startIdx, startIdx + PAGE_SIZE)
  }, [sortedResults, currentPage])

  // Reset to page 1 when results change
  useEffect(() => {
    setCurrentPage(1)
  }, [allResults, selectedThreshold, selectedPeriod])

  const selectedGroup = watchgroups.find(g => g.id === selectedGroupId)

  if (watchgroups.length === 0) {
    return (
      <Box bg="gray.800" borderRadius="md" p={6}>
        <Text color="gray.400">No watchgroups available. Create a watchgroup first.</Text>
      </Box>
    )
  }

  return (
    <Box>
      <HStack mb={4} spacing={4} wrap="wrap">
        <HStack>
          <Text fontSize="sm" color="gray.400">Watchgroup:</Text>
          <Select
            size="sm"
            w="150px"
            value={selectedGroupId || ''}
            onChange={(e) => {
              setSelectedGroupId(Number(e.target.value))
              setAllResults(null)
            }}
          >
            {watchgroups.map(g => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </Select>
        </HStack>

        <Button
          colorScheme="blue"
          size="sm"
          onClick={loadAllData}
          isLoading={loading}
          isDisabled={groupSymbols.length === 0}
        >
          Analyze {selectedGroup?.name || 'Watchgroup'} ({groupSymbols.length} tickers)
        </Button>

        {allResults && (
          <HStack>
            <Button
              colorScheme="green"
              size="sm"
              onClick={fetchLivePrices}
              isLoading={loadingPrices}
              isDisabled={!paginatedResults || paginatedResults.length === 0}
            >
              Refresh Prices ({paginatedResults?.length || 0})
            </Button>
            <Text fontSize="sm" color="gray.400">Threshold:</Text>
            <Select
              size="sm"
              w="100px"
              value={selectedThreshold}
              onChange={(e) => setSelectedThreshold(parseFloat(e.target.value))}
            >
              {THRESHOLDS.map(t => (
                <option key={t} value={t}>+{t}%</option>
              ))}
            </Select>
            <Text fontSize="sm" color="gray.400" ml={2}>Sort by:</Text>
            <Select
              size="sm"
              w="100px"
              value={selectedPeriod}
              onChange={(e) => setSelectedPeriod(e.target.value)}
            >
              {TIME_PERIODS.map(p => (
                <option key={p.label} value={p.label}>{p.label}</option>
              ))}
            </Select>
          </HStack>
        )}
      </HStack>

      {loading && (
        <HStack spacing={3}>
          <Spinner size="sm" />
          <Text color="gray.400">Loading data for all tickers...</Text>
        </HStack>
      )}

      {paginatedResults && (
        <Box overflowX="auto">
          <Table variant="simple" size="sm">
            <Thead>
              <Tr>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2}></Th>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2}>#</Th>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2}>Symbol</Th>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2} textAlign="right">Last Day Close</Th>
                <Th
                  color="cyan.300"
                  bg="cyan.900"
                  borderColor="gray.500"
                  textAlign="center"
                  colSpan={CHANGE_PERIODS.length}
                >
                  Last Day Change %
                </Th>
                <Th
                  color="green.300"
                  bg="green.900"
                  borderColor="gray.500"
                  textAlign="center"
                  rowSpan={2}
                >
                  Current Price
                </Th>
                <Th
                  color="purple.300"
                  bg="purple.900"
                  borderColor="gray.500"
                  textAlign="center"
                  colSpan={THRESHOLDS.length}
                >
                  Target Price
                </Th>
                {TIME_PERIODS.map((period, periodIdx) => (
                  <Th
                    key={period.label}
                    color={period.label === selectedPeriod ? 'white' : 'gray.300'}
                    bg={periodIdx % 2 === 0 ? 'gray.700' : 'gray.600'}
                    borderColor="gray.500"
                    textAlign="center"
                    colSpan={THRESHOLDS.length}
                  >
                    {period.label}
                  </Th>
                ))}
              </Tr>
              <Tr>
                {CHANGE_PERIODS.map(p => (
                  <Th
                    key={`change-${p.label}`}
                    color="cyan.300"
                    bg="cyan.900"
                    borderColor="gray.500"
                    textAlign="center"
                    fontSize="xs"
                    px={1}
                  >
                    {p.label}
                  </Th>
                ))}
                {THRESHOLDS.map(t => (
                  <Th
                    key={`target-${t}`}
                    color="purple.300"
                    bg="purple.900"
                    borderColor="gray.500"
                    textAlign="center"
                    fontSize="xs"
                    px={1}
                  >
                    +{t}%
                  </Th>
                ))}
                {TIME_PERIODS.map((period, periodIdx) => (
                  THRESHOLDS.map(t => (
                    <Th
                      key={`${period.label}-${t}`}
                      color={period.label === selectedPeriod && t === selectedThreshold ? 'blue.300' : 'gray.400'}
                      bg={periodIdx % 2 === 0 ? 'gray.700' : 'gray.600'}
                      borderColor="gray.500"
                      textAlign="center"
                      fontSize="xs"
                      px={1}
                    >
                      {t}%
                    </Th>
                  ))
                ))}
              </Tr>
            </Thead>
            <Tbody>
              {paginatedResults.map((item, idx) => {
                const isExpanded = expandedRows[item.symbol]
                const chainData = optionsData[item.symbol]
                const isLoadingChain = loadingOptions[item.symbol]
                const totalCols = 4 + CHANGE_PERIODS.length + 1 + THRESHOLDS.length + (TIME_PERIODS.length * THRESHOLDS.length) // +1 for Current Price
                const globalIdx = (currentPage - 1) * PAGE_SIZE + idx + 1

                return (
                  <Fragment key={item.symbol}>
                    <Tr>
                      <Td borderColor="gray.600" px={1}>
                        <IconButton
                          icon={isExpanded ? <ChevronDownIcon /> : <ChevronRightIcon />}
                          size="xs"
                          variant="ghost"
                          onClick={() => toggleRow(item.symbol, item.currentPrice)}
                          aria-label={isExpanded ? 'Collapse' : 'Expand'}
                        />
                      </Td>
                      <Td borderColor="gray.600" color="gray.500" fontSize="xs">
                        {globalIdx}
                      </Td>
                      <Td borderColor="gray.600" fontWeight="medium" color="gray.200">
                        {item.symbol}
                      </Td>
                      <Td borderColor="gray.600" color="gray.300" textAlign="right" fontSize="sm">
                        {item.currentPrice ? `$${item.currentPrice.toFixed(2)}` : '-'}
                      </Td>
                      {CHANGE_PERIODS.map(p => {
                        const change = item.changes?.[p.label]
                        const color = change === null ? 'gray.500' : change >= 0 ? 'green.300' : 'red.300'
                        return (
                          <Td key={`change-${p.label}`} bg="cyan.900" borderColor="gray.600" textAlign="center" px={1}>
                            <Text color={color} fontSize="xs" fontWeight="medium">
                              {change !== null ? `${change >= 0 ? '+' : ''}${change.toFixed(1)}%` : '-'}
                            </Text>
                          </Td>
                        )
                      })}
                      {/* Current Price */}
                      {(() => {
                        const priceData = currentPrices[item.symbol]
                        const livePrice = priceData?.price
                        const isStale = priceData?.fetchedAt && (Date.now() - priceData.fetchedAt > 5 * 60 * 1000)
                        return (
                          <Td bg="green.900" borderColor="gray.600" textAlign="right" px={2}>
                            <HStack spacing={1} justify="flex-end">
                              {isStale && (
                                <Tooltip label="Price older than 5 min" fontSize="xs">
                                  <WarningIcon color="yellow.400" boxSize={3} />
                                </Tooltip>
                              )}
                              <Text color="green.200" fontSize="sm" fontWeight="medium">
                                {livePrice ? `$${livePrice.toFixed(2)}` : '-'}
                              </Text>
                            </HStack>
                          </Td>
                        )
                      })()}
                      {/* Target Prices - use current price if available, otherwise last day close */}
                      {THRESHOLDS.map(t => {
                        const livePrice = currentPrices[item.symbol]?.price
                        const basePrice = livePrice || item.currentPrice
                        return (
                          <Td key={`target-${t}`} bg="purple.900" borderColor="gray.600" textAlign="center" px={1}>
                            <Text color="purple.200" fontSize="xs">
                              {basePrice ? `$${(basePrice * (1 + t / 100)).toFixed(2)}` : '-'}
                            </Text>
                          </Td>
                        )
                      })}
                      {TIME_PERIODS.map((period, periodIdx) => {
                        const result = item.results?.[period.label]
                        const validDays = result?.validDays || 0
                        const bgColor = periodIdx % 2 === 0 ? 'whiteAlpha.50' : 'blackAlpha.300'

                        return THRESHOLDS.map(threshold => {
                          if (!result || validDays === 0) {
                            return (
                              <Td key={`${period.label}-${threshold}`} bg={bgColor} borderColor="gray.600" textAlign="center" px={1}>
                                <Text color="gray.500" fontSize="xs">-</Text>
                              </Td>
                            )
                          }

                          const count = result.counts[threshold] || 0
                          const hitRate = (count / validDays) * 100
                          const colorScheme = getColorScheme(hitRate)
                          const variant = hitRate >= 70 ? 'solid' : 'subtle'

                          return (
                            <Td key={`${period.label}-${threshold}`} bg={bgColor} borderColor="gray.600" textAlign="center" px={1}>
                              <Badge colorScheme={colorScheme} variant={variant} fontSize="xs" px={1}>
                                {hitRate.toFixed(0)}
                              </Badge>
                            </Td>
                          )
                        })
                      })}
                    </Tr>
                    {isExpanded && (
                      <Tr key={`${item.symbol}-options`}>
                        <Td colSpan={totalCols} bg="gray.900" borderColor="gray.600" p={0}>
                          <Box p={4}>
                            {isLoadingChain && (
                              <HStack spacing={2}>
                                <Spinner size="sm" />
                                <Text color="gray.400">Loading options chain...</Text>
                              </HStack>
                            )}
                            {chainData?.error && (
                              <Text color="red.400">Error: {chainData.error}</Text>
                            )}
                            {chainData && !chainData.error && (
                              <OptionsChainTable chain={chainData} currentPrice={item.currentPrice} />
                            )}
                            {!isLoadingChain && !chainData && (
                              <Text color="gray.500">No options data available</Text>
                            )}
                          </Box>
                        </Td>
                      </Tr>
                    )}
                  </Fragment>
                )
              })}
            </Tbody>
          </Table>

          {/* Pagination controls */}
          {totalPages > 1 && (
            <HStack justify="center" mt={4} spacing={4}>
              <Button
                size="sm"
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                isDisabled={currentPage === 1}
              >
                Previous
              </Button>
              <Text color="gray.300" fontSize="sm">
                Page {currentPage} of {totalPages} ({sortedResults.length} tickers)
              </Text>
              <Button
                size="sm"
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                isDisabled={currentPage === totalPages}
              >
                Next
              </Button>
            </HStack>
          )}
        </Box>
      )}
    </Box>
  )
}

function ThresholdReport({ symbol, data }) {
  const [mode, setMode] = useState('single') // 'single' or 'watchgroup'

  return (
    <Box bg="gray.800" borderRadius="md" p={4}>
      <HStack justify="space-between" mb={4}>
        <Heading size="md" color="blue.300">
          High Over Previous Close
        </Heading>
        <HStack>
          <Button
            size="sm"
            variant={mode === 'single' ? 'solid' : 'outline'}
            colorScheme="blue"
            onClick={() => setMode('single')}
          >
            Single
          </Button>
          <Button
            size="sm"
            variant={mode === 'watchgroup' ? 'solid' : 'outline'}
            colorScheme="blue"
            onClick={() => setMode('watchgroup')}
          >
            Watchgroup
          </Button>
        </HStack>
      </HStack>

      <Text fontSize="sm" color="gray.400" mb={4}>
          Counts how many times price raised for X% <b>compared to previous day close (high &gt; prev day close)</b>. <br />
        LONG at CLOSE @ X DAYS DTE (or stocks at leverage)  <br />
        Data in cells are % for period (header) analysis is made from. <br />
        If it says 80, that means 80% of column it is in (50D) which is 40/50 days. <br />
        Before entering, make sure options have volume close to the price. <br />
        If volume too low, bad to enter as hard to sell tomorrow.
      </Text>

      {mode === 'single' ? (
        <SingleSymbolReport symbol={symbol} data={data} />
      ) : mode === 'watchgroup' ? (
        <MultiSymbolReport />
      ) : null}

      <HStack mt={4} spacing={4} fontSize="xs" color="gray.500">
        <HStack spacing={2}>
          <Badge colorScheme={getColorScheme(70)} variant="solid" size="sm">70%+</Badge>
          <Badge colorScheme={getColorScheme(60)} variant="subtle" size="sm">60-69%</Badge>
          <Badge colorScheme={getColorScheme(30)} variant="subtle" size="sm">30-59%</Badge>
          <Badge colorScheme={getColorScheme(0)} variant="subtle" size="sm">&lt;30%</Badge>
        </HStack>
      </HStack>
    </Box>
  )
}

export default ThresholdReport
