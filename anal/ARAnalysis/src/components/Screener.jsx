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
  TagLabel,
  TagCloseButton,
  Collapse,
  useDisclosure
} from '@chakra-ui/react'
import { AddIcon, DeleteIcon, SearchIcon, ChevronDownIcon, ChevronUpIcon } from '@chakra-ui/icons'
import { fetchFromCache } from '../modules/dataService'
import {
  getScreenerSymbols,
  addScreenerSymbol,
  addScreenerSymbols,
  removeScreenerSymbol
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
  const [newSymbol, setNewSymbol] = useState('')
  const [results, setResults] = useState(null)
  const [loading, setLoading] = useState(false)
  const [progress, setProgress] = useState({ current: 0, total: 0 })
  const [sortColumn, setSortColumn] = useState('changePercent')
  const [sortAsc, setSortAsc] = useState(false)
  const { isOpen: isSymbolsOpen, onToggle: onSymbolsToggle } = useDisclosure()

  // Load symbols on mount
  useEffect(() => {
    loadSymbols()
  }, [])

  const loadSymbols = async () => {
    const syms = await getScreenerSymbols()
    setSymbols(syms)
  }

  const handleAddSymbol = async () => {
    const toAdd = newSymbol.toUpperCase().trim()
    if (!toAdd) return

    // Support comma/space separated input
    const symbolsToAdd = toAdd.split(/[\s,]+/).filter(s => s.length > 0)
    if (symbolsToAdd.length > 1) {
      await addScreenerSymbols(symbolsToAdd)
    } else {
      await addScreenerSymbol(toAdd)
    }
    setNewSymbol('')
    await loadSymbols()
  }

  const handleRemoveSymbol = async (symbol) => {
    await removeScreenerSymbol(symbol)
    await loadSymbols()
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
    if (symbols.length === 0) return

    setLoading(true)
    setResults(null)
    setProgress({ current: 0, total: symbols.length })
    const matches = []

    for (let i = 0; i < symbols.length; i++) {
      const symbol = symbols[i]
      setProgress({ current: i + 1, total: symbols.length })

      try {
        const data = await fetchFromCache(symbol, '1d')
        if (data && data.length >= 2) {
          const metrics = calculateMetrics(data)
          if (metrics) {
            metrics.symbol = symbol
            // Check all conditions (AND logic)
            const passesAll = conditions.every(cond => passesCondition(metrics, cond))
            if (passesAll) {
              matches.push(metrics)
            }
          }
        }
      } catch (err) {
        console.error(`Failed to screen ${symbol}:`, err)
      }
    }

    setResults(matches)
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
        {/* Symbol list management */}
        <Box p={3} bg="gray.700" borderRadius="md">
          <HStack justify="space-between" mb={2}>
            <HStack>
              <Text color="gray.300" fontWeight="bold">Symbols</Text>
              <Badge colorScheme="blue">{symbols.length}</Badge>
            </HStack>
            <Button
              size="xs"
              variant="ghost"
              rightIcon={isSymbolsOpen ? <ChevronUpIcon /> : <ChevronDownIcon />}
              onClick={onSymbolsToggle}
            >
              {isSymbolsOpen ? 'Hide' : 'Show'}
            </Button>
          </HStack>

          <HStack mb={2}>
            <Input
              size="sm"
              placeholder="Add symbol(s) - e.g. AAPL, TSLA, MSFT"
              value={newSymbol}
              onChange={(e) => setNewSymbol(e.target.value.toUpperCase())}
              onKeyDown={(e) => e.key === 'Enter' && handleAddSymbol()}
              bg="gray.600"
              flex={1}
            />
            <Button size="sm" colorScheme="blue" leftIcon={<AddIcon />} onClick={handleAddSymbol}>
              Add
            </Button>
          </HStack>

          <Collapse in={isSymbolsOpen}>
            {symbols.length === 0 ? (
              <Text color="gray.500" fontSize="sm">No symbols added yet</Text>
            ) : (
              <Wrap spacing={1} maxH="200px" overflowY="auto">
                {symbols.map(symbol => (
                  <WrapItem key={symbol}>
                    <Tag size="sm" colorScheme="blue" borderRadius="full">
                      <TagLabel>{symbol}</TagLabel>
                      <TagCloseButton onClick={() => handleRemoveSymbol(symbol)} />
                    </Tag>
                  </WrapItem>
                ))}
              </Wrap>
            )}
          </Collapse>
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

        {/* Run button */}
        <Button
          leftIcon={<SearchIcon />}
          colorScheme="blue"
          onClick={runScreener}
          isLoading={loading}
          isDisabled={symbols.length === 0}
        >
          Screen {symbols.length} Symbols
        </Button>

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
              Screening {progress.current} of {progress.total} symbols...
            </Text>
          </Box>
        )}

        {/* Results */}
        {results && !loading && (
          <>
            <Divider borderColor="gray.600" />
            <Text color="gray.300" fontWeight="bold">
              Results: {results.length} matches out of {symbols.length} screened
            </Text>

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
          Note: Screener uses cached data. Load symbols via the main view first to populate the cache.
        </Text>
      </VStack>
    </Box>
  )
}

export default Screener
