import { useState, useEffect, useMemo } from 'react'
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
  VStack,
  Button,
  Select,
  Input,
  IconButton,
  Divider,
  Progress,
  Wrap,
  WrapItem,
  Tag,
  TagLabel
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon, SearchIcon, StarIcon } from '@chakra-ui/icons'
import { fetchForScreener } from '../modules/dataService'
import {
  getWatchgroups,
  getActiveWatchgroup,
  getWatchgroupSymbols,
  addToWatchgroup,
  removeFromWatchgroup
} from '../db/database'

// Available columns for screening
const COLUMNS = [
  { value: 'close', label: 'Close', format: 'price' },
  { value: 'open', label: 'Open', format: 'price' },
  { value: 'high', label: 'High', format: 'price' },
  { value: 'low', label: 'Low', format: 'price' },
  { value: 'volume', label: 'Volume', format: 'number' },
  { value: 'changePercent', label: 'Change %', format: 'percent' },
  { value: 'gap', label: 'Gap %', format: 'percent' },
  { value: 'range', label: 'Day Range %', format: 'percent' },
  { value: 'highVsPrevClose', label: 'High vs Prev Close %', format: 'percent' },
  { value: 'lowVsPrevClose', label: 'Low vs Prev Close %', format: 'percent' },
  { value: 'avgVolume5', label: 'Avg Volume (5d)', format: 'number' },
  { value: 'avgVolume20', label: 'Avg Volume (20d)', format: 'number' },
  { value: 'relativeVolume', label: 'Relative Volume', format: 'decimal' },
  { value: 'atr14', label: 'ATR (14d)', format: 'price' },
  { value: 'atrPercent', label: 'ATR %', format: 'percent' }
]

const OPERATORS = [
  { value: '>', label: '>' },
  { value: '<', label: '<' },
  { value: '>=', label: '>=' },
  { value: '<=', label: '<=' },
  { value: '==', label: '=' },
  { value: 'between', label: 'Between' }
]

const DEFAULT_CONDITION = {
  column: 'changePercent',
  operator: '>',
  value: 0,
  value2: 0
}

// Max data age options (in trading days)
const DATA_AGE_OPTIONS = [
  { value: 1, label: 'Today' },
  { value: 2, label: '2 trading days' },
  { value: 5, label: '1 week' },
  { value: 10, label: '2 weeks' },
  { value: 999, label: 'Any (no refetch)' }
]

// Calculate derived values from price data
function calculateMetrics(data) {
  if (!data || data.length < 2) return null

  const latest = data[0]
  const prev = data[1]

  if (!latest || !prev) return null

  const metrics = {
    symbol: latest.symbol,
    close: latest.close,
    open: latest.open,
    high: latest.high,
    low: latest.low,
    volume: latest.volume,
    changePercent: prev.close ? ((latest.close - prev.close) / prev.close) * 100 : 0,
    gap: prev.close ? ((latest.open - prev.close) / prev.close) * 100 : 0,
    range: latest.low ? ((latest.high - latest.low) / latest.low) * 100 : 0,
    highVsPrevClose: prev.close ? ((latest.high - prev.close) / prev.close) * 100 : 0,
    lowVsPrevClose: prev.close ? ((latest.low - prev.close) / prev.close) * 100 : 0
  }

  // Calculate average volumes
  const volumes5 = data.slice(0, 5).map(d => d.volume).filter(v => v > 0)
  const volumes20 = data.slice(0, 20).map(d => d.volume).filter(v => v > 0)
  metrics.avgVolume5 = volumes5.length > 0 ? volumes5.reduce((a, b) => a + b, 0) / volumes5.length : 0
  metrics.avgVolume20 = volumes20.length > 0 ? volumes20.reduce((a, b) => a + b, 0) / volumes20.length : 0
  metrics.relativeVolume = metrics.avgVolume20 > 0 ? latest.volume / metrics.avgVolume20 : 0

  // Calculate ATR (14-day)
  if (data.length >= 15) {
    let atrSum = 0
    for (let i = 0; i < 14; i++) {
      const curr = data[i]
      const prevBar = data[i + 1]
      if (curr && prevBar) {
        const tr = Math.max(
          curr.high - curr.low,
          Math.abs(curr.high - prevBar.close),
          Math.abs(curr.low - prevBar.close)
        )
        atrSum += tr
      }
    }
    metrics.atr14 = atrSum / 14
    metrics.atrPercent = latest.close > 0 ? (metrics.atr14 / latest.close) * 100 : 0
  } else {
    metrics.atr14 = 0
    metrics.atrPercent = 0
  }

  return metrics
}

// Check if metrics pass a condition
function passesCondition(metrics, condition) {
  const value = metrics[condition.column]
  if (value === undefined || value === null) return false

  const target = parseFloat(condition.value) || 0
  const target2 = parseFloat(condition.value2) || 0

  switch (condition.operator) {
    case '>': return value > target
    case '<': return value < target
    case '>=': return value >= target
    case '<=': return value <= target
    case '==': return Math.abs(value - target) < 0.0001
    case 'between': return value >= Math.min(target, target2) && value <= Math.max(target, target2)
    default: return false
  }
}

// Format value for display
function formatValue(value, format) {
  if (value === null || value === undefined) return '-'
  switch (format) {
    case 'price': return `$${value.toFixed(2)}`
    case 'percent': return `${value.toFixed(2)}%`
    case 'number': return value >= 1000000 ? `${(value / 1000000).toFixed(2)}M` : value >= 1000 ? `${(value / 1000).toFixed(1)}K` : value.toFixed(0)
    case 'decimal': return value.toFixed(2)
    default: return value.toString()
  }
}

function Screener() {
  const [conditions, setConditions] = useState([{ ...DEFAULT_CONDITION }])
  const [symbols, setSymbols] = useState([])
  const [results, setResults] = useState(null)
  const [failedSymbols, setFailedSymbols] = useState([])
  const [loading, setLoading] = useState(false)
  const [progress, setProgress] = useState({ current: 0, total: 0 })
  const [sortColumn, setSortColumn] = useState('changePercent')
  const [sortAsc, setSortAsc] = useState(false)
  const [watchgroups, setWatchgroups] = useState([])
  const [sourceGroupId, setSourceGroupId] = useState(null) // Watchgroup to screen
  const [targetGroupId, setTargetGroupId] = useState(null) // Watchgroup to add results to
  const [addingToWatchlist, setAddingToWatchlist] = useState(false)
  const [maxDataAge, setMaxDataAge] = useState(1) // Default: 1 trading day (today)

  // Load watchgroups on mount
  useEffect(() => {
    loadWatchgroups()
  }, [])

  // Load symbols when source group changes
  useEffect(() => {
    if (sourceGroupId) {
      loadSymbols()
    }
  }, [sourceGroupId])

  const loadSymbols = async () => {
    if (!sourceGroupId) {
      setSymbols([])
      return
    }
    const syms = await getWatchgroupSymbols(sourceGroupId)
    setSymbols(syms)
  }

  const loadWatchgroups = async () => {
    const groups = await getWatchgroups()
    setWatchgroups(groups)
    const activeId = await getActiveWatchgroup()
    if (activeId && groups.some(g => g.id === activeId)) {
      setSourceGroupId(activeId)
      setTargetGroupId(activeId)
    } else if (groups.length > 0) {
      setSourceGroupId(groups[0].id)
      setTargetGroupId(groups[0].id)
    }
  }

  const handleAddResultsToWatchlist = async () => {
    if (!results || results.length === 0 || !targetGroupId) return

    setAddingToWatchlist(true)
    for (const item of results) {
      await addToWatchgroup(targetGroupId, item.symbol)
    }
    setAddingToWatchlist(false)
  }

  const sourceGroup = watchgroups.find(g => g.id === sourceGroupId)
  const targetGroup = watchgroups.find(g => g.id === targetGroupId)

  const handleRemoveFailedFromSource = async () => {
    if (!sourceGroupId || failedSymbols.length === 0) return
    for (const symbol of failedSymbols) {
      await removeFromWatchgroup(sourceGroupId, symbol)
    }
    await loadSymbols()
    setFailedSymbols([])
  }

  const handleAddCondition = () => {
    setConditions([...conditions, { ...DEFAULT_CONDITION }])
  }

  const handleRemoveCondition = (index) => {
    if (conditions.length > 1) {
      setConditions(conditions.filter((_, i) => i !== index))
    }
  }

  const handleConditionChange = (index, field, value) => {
    const newConditions = [...conditions]
    newConditions[index] = { ...newConditions[index], [field]: value }
    setConditions(newConditions)
  }

  const runScreener = async () => {
    // Filter out empty/invalid symbols
    const validSymbols = symbols.filter(s => s && s.trim() !== '')
    if (validSymbols.length === 0) return

    // Refresh active watchgroup before screening
    await loadWatchgroups()

    setLoading(true)
    setResults(null)
    setFailedSymbols([])
    setProgress({ current: 0, total: validSymbols.length, failed: 0, cached: 0, fetched: 0 })

    const matches = []
    const failed = []
    let completedCount = 0
    let cachedCount = 0
    let fetchedCount = 0
    const BATCH_SIZE = 10 // Parallel requests per batch

    // Process symbols in parallel batches
    for (let batchStart = 0; batchStart < validSymbols.length; batchStart += BATCH_SIZE) {
      const batchEnd = Math.min(batchStart + BATCH_SIZE, validSymbols.length)
      const batch = validSymbols.slice(batchStart, batchEnd)

      // Fetch all symbols in batch in parallel
      const batchResults = await Promise.all(
        batch.map(async (symbol) => {
          try {
            const result = await fetchForScreener(symbol, maxDataAge)
            const data = result.data
            const fromCache = result.fromCache
            if (data && data.length >= 2) {
              const metrics = calculateMetrics(data)
              if (metrics) {
                metrics.symbol = symbol
                // Check all conditions (AND logic)
                const passesAll = conditions.every(cond => passesCondition(metrics, cond))
                return { success: true, symbol, match: passesAll ? metrics : null, fromCache }
              }
            }
            return { success: false, symbol, fromCache: false }
          } catch (err) {
            console.error(`Failed to screen ${symbol}:`, err.message)
            return { success: false, symbol, fromCache: false }
          }
        })
      )

      // Process batch results
      for (const result of batchResults) {
        completedCount++
        if (!result.success) {
          failed.push(result.symbol)
        } else {
          if (result.fromCache) {
            cachedCount++
          } else {
            fetchedCount++
          }
          if (result.match) {
            matches.push(result.match)
          }
        }
      }

      setProgress({ current: completedCount, total: validSymbols.length, failed: failed.length, cached: cachedCount, fetched: fetchedCount })
    }

    setResults(matches)
    setFailedSymbols(failed)
    setLoading(false)
  }

  const handleSort = (column) => {
    if (sortColumn === column) {
      setSortAsc(!sortAsc)
    } else {
      setSortColumn(column)
      setSortAsc(false)
    }
  }

  const sortedResults = useMemo(() => {
    if (!results) return null
    return [...results].sort((a, b) => {
      const valA = a[sortColumn] ?? 0
      const valB = b[sortColumn] ?? 0
      return sortAsc ? valA - valB : valB - valA
    })
  }, [results, sortColumn, sortAsc])

  // Display columns in results table
  const displayColumns = ['close', 'changePercent', 'gap', 'volume', 'relativeVolume', 'atrPercent']

  return (
    <Box bg="gray.800" borderRadius="md" p={4}>
      <Heading size="md" color="blue.300" mb={4}>
        Screener
      </Heading>

      <VStack spacing={4} align="stretch">
        {/* Watchgroup selector */}
        <Box p={3} bg="gray.700" borderRadius="md">
          <HStack spacing={4}>
            <HStack>
              <Text color="gray.300" fontWeight="bold" whiteSpace="nowrap">Screen from:</Text>
              <Select
                size="sm"
                w="200px"
                value={sourceGroupId || ''}
                onChange={(e) => setSourceGroupId(Number(e.target.value))}
                bg="gray.600"
              >
                {watchgroups.map(g => (
                  <option key={g.id} value={g.id}>{g.name}</option>
                ))}
              </Select>
              <Badge colorScheme="blue">{symbols.length} symbols</Badge>
            </HStack>
          </HStack>
        </Box>

        {/* Conditions */}
        <Box p={3} bg="gray.700" borderRadius="md">
          <HStack justify="space-between" mb={3}>
            <Text color="gray.300" fontWeight="bold">Conditions (AND)</Text>
            <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddCondition}>
              Add
            </Button>
          </HStack>

          <VStack spacing={2} align="stretch">
            {conditions.map((cond, index) => (
              <HStack key={index} spacing={2}>
                {index > 0 && (
                  <Badge colorScheme="blue" fontSize="xs">AND</Badge>
                )}
                <Select
                  size="sm"
                  w="160px"
                  value={cond.column}
                  onChange={(e) => handleConditionChange(index, 'column', e.target.value)}
                  bg="gray.600"
                >
                  {COLUMNS.map(col => (
                    <option key={col.value} value={col.value}>{col.label}</option>
                  ))}
                </Select>
                <Select
                  size="sm"
                  w="100px"
                  value={cond.operator}
                  onChange={(e) => handleConditionChange(index, 'operator', e.target.value)}
                  bg="gray.600"
                >
                  {OPERATORS.map(op => (
                    <option key={op.value} value={op.value}>{op.label}</option>
                  ))}
                </Select>
                <Input
                  size="sm"
                  w="80px"
                  type="number"
                  value={cond.value}
                  onChange={(e) => handleConditionChange(index, 'value', e.target.value)}
                  bg="gray.600"
                />
                {cond.operator === 'between' && (
                  <>
                    <Text color="gray.400" fontSize="sm">and</Text>
                    <Input
                      size="sm"
                      w="80px"
                      type="number"
                      value={cond.value2}
                      onChange={(e) => handleConditionChange(index, 'value2', e.target.value)}
                      bg="gray.600"
                    />
                  </>
                )}
                {conditions.length > 1 && (
                  <IconButton
                    aria-label="Remove condition"
                    icon={<DeleteIcon />}
                    size="xs"
                    colorScheme="red"
                    variant="ghost"
                    onClick={() => handleRemoveCondition(index)}
                  />
                )}
              </HStack>
            ))}
          </VStack>
        </Box>

        {/* Run button and data age selector */}
        <HStack spacing={4}>
          <Button
            leftIcon={<SearchIcon />}
            colorScheme="blue"
            onClick={runScreener}
            isLoading={loading}
            isDisabled={symbols.length === 0}
          >
            Screen {symbols.length} Symbols
          </Button>
          <HStack spacing={2}>
            <Text color="gray.400" fontSize="sm" whiteSpace="nowrap">Data from:</Text>
            <Select
              size="sm"
              w="140px"
              value={maxDataAge}
              onChange={(e) => setMaxDataAge(Number(e.target.value))}
              bg="gray.700"
            >
              {DATA_AGE_OPTIONS.map(opt => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </Select>
          </HStack>
        </HStack>

        {/* Progress */}
        {loading && (
          <Box>
            <Progress
              value={(progress.current / progress.total) * 100}
              size="sm"
              colorScheme="blue"
              hasStripe
              isAnimated
            />
            <Text fontSize="xs" color="gray.400" mt={1}>
              Screening {progress.current} of {progress.total} symbols
              <Text as="span" color="green.400"> ({progress.cached} cached)</Text>
              <Text as="span" color="blue.400"> ({progress.fetched} fetched)</Text>
              {progress.failed > 0 && <Text as="span" color="red.400"> ({progress.failed} failed)</Text>}
            </Text>
          </Box>
        )}

        {/* Results */}
        {results && !loading && (
          <>
            <Divider borderColor="gray.600" />
            <HStack justify="space-between" wrap="wrap">
              <Text color="gray.300" fontWeight="bold">
                Results: {results.length} matches out of {symbols.length - failedSymbols.length} screened
              </Text>
              {results.length > 0 && watchgroups.length > 0 && (
                <HStack>
                  <Button
                    size="sm"
                    leftIcon={<StarIcon />}
                    colorScheme="yellow"
                    variant="outline"
                    onClick={handleAddResultsToWatchlist}
                    isLoading={addingToWatchlist}
                  >
                    Add {results.length} to
                  </Button>
                  <Select
                    size="sm"
                    w="150px"
                    value={targetGroupId || ''}
                    onChange={(e) => setTargetGroupId(Number(e.target.value))}
                    bg="gray.700"
                  >
                    {watchgroups.map(g => (
                      <option key={g.id} value={g.id}>{g.name}</option>
                    ))}
                  </Select>
                </HStack>
              )}
            </HStack>

            {/* Failed symbols */}
            {failedSymbols.length > 0 && (
              <Box p={2} bg="red.900" borderRadius="md">
                <HStack justify="space-between" mb={1}>
                  <Text color="red.300" fontSize="sm" fontWeight="bold">
                    Failed ({failedSymbols.length}):
                  </Text>
                  <Button
                    size="xs"
                    colorScheme="red"
                    variant="outline"
                    leftIcon={<DeleteIcon />}
                    onClick={handleRemoveFailedFromSource}
                  >
                    Remove failed from "{sourceGroup?.name}"
                  </Button>
                </HStack>
                <Wrap spacing={1}>
                  {failedSymbols.map(symbol => (
                    <WrapItem key={symbol}>
                      <Tag size="sm" colorScheme="red" borderRadius="full">
                        <TagLabel>{symbol}</TagLabel>
                      </Tag>
                    </WrapItem>
                  ))}
                </Wrap>
              </Box>
            )}

            {results.length === 0 ? (
              <Text color="gray.500">No symbols matched your criteria</Text>
            ) : (
              <Box overflowX="auto">
                <Table variant="simple" size="sm">
                  <Thead>
                    <Tr>
                      <Th color="gray.400" borderColor="gray.600">#</Th>
                      <Th color="gray.400" borderColor="gray.600">Symbol</Th>
                      {displayColumns.map(colKey => {
                        const col = COLUMNS.find(c => c.value === colKey)
                        return (
                          <Th
                            key={colKey}
                            color={sortColumn === colKey ? 'blue.300' : 'gray.400'}
                            borderColor="gray.600"
                            cursor="pointer"
                            onClick={() => handleSort(colKey)}
                            textAlign="right"
                          >
                            {col?.label || colKey}
                            {sortColumn === colKey && (sortAsc ? ' ↑' : ' ↓')}
                          </Th>
                        )
                      })}
                    </Tr>
                  </Thead>
                  <Tbody>
                    {sortedResults.map((item, idx) => (
                      <Tr key={item.symbol}>
                        <Td borderColor="gray.600" color="gray.500" fontSize="xs">
                          {idx + 1}
                        </Td>
                        <Td borderColor="gray.600" fontWeight="medium" color="gray.200">
                          {item.symbol}
                        </Td>
                        {displayColumns.map(colKey => {
                          const col = COLUMNS.find(c => c.value === colKey)
                          const value = item[colKey]
                          let colorScheme = 'gray'
                          if (colKey === 'changePercent' || colKey === 'gap') {
                            colorScheme = value > 0 ? 'green' : value < 0 ? 'red' : 'gray'
                          }
                          return (
                            <Td
                              key={colKey}
                              borderColor="gray.600"
                              textAlign="right"
                              color={colorScheme === 'green' ? 'green.300' : colorScheme === 'red' ? 'red.300' : 'gray.300'}
                            >
                              {formatValue(value, col?.format)}
                            </Td>
                          )
                        })}
                      </Tr>
                    ))}
                  </Tbody>
                </Table>
              </Box>
            )}
          </>
        )}

        <Text fontSize="xs" color="gray.500">
          Uses cached data if within max age. Fetches from IBKR only if cache is older or missing.
        </Text>
      </VStack>
    </Box>
  )
}

export default Screener
