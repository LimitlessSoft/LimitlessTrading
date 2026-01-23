import { useMemo, useState, useEffect } from 'react'
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
  VStack
} from '@chakra-ui/react'
import { fetchFromCache } from '../modules/dataService'

// Time periods in trading days
const TIME_PERIODS = [
  { label: '50d', days: 50 },
  { label: '100d', days: 100 },
  { label: '200d', days: 200 },
  { label: '1yr', days: 252 },
  { label: '2yr', days: 504 },
  { label: '3yr', days: 756 }
]

// Thresholds to check (upside only)
const THRESHOLDS = [0.3, 0.5, 0.8, 1.0]

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

// Multi-symbol ranking report
function MultiSymbolReport({ securities }) {
  const [allResults, setAllResults] = useState(null)
  const [loading, setLoading] = useState(false)
  const [selectedThreshold, setSelectedThreshold] = useState(0.5)
  const [selectedPeriod, setSelectedPeriod] = useState('1yr')

  const loadAllData = async () => {
    if (!securities || securities.length === 0) return

    setLoading(true)
    const results = []

    for (const security of securities) {
      try {
        const data = await fetchFromCache(security.symbol, '1d')
        if (data && data.length > 0) {
          const periodResults = {}
          TIME_PERIODS.forEach(period => {
            periodResults[period.label] = analyzeThresholds(data, period.days)
          })
          results.push({
            symbol: security.symbol,
            results: periodResults,
            dataLength: data.length
          })
        }
      } catch (err) {
        console.error(`Failed to load ${security.symbol}:`, err)
      }
    }

    setAllResults(results)
    setLoading(false)
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

  if (!securities || securities.length === 0) {
    return (
      <Box bg="gray.800" borderRadius="md" p={6}>
        <Text color="gray.400">No cached securities available</Text>
      </Box>
    )
  }

  return (
    <Box>
      <HStack mb={4} spacing={4} wrap="wrap">
        <Button
          colorScheme="blue"
          size="sm"
          onClick={loadAllData}
          isLoading={loading}
        >
          Analyze All ({securities.length} tickers)
        </Button>

        {allResults && (
          <HStack>
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

      {sortedResults && (
        <Box overflowX="auto">
          <Table variant="simple" size="sm">
            <Thead>
              <Tr>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2}>#</Th>
                <Th color="gray.400" borderColor="gray.600" rowSpan={2}>Symbol</Th>
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
              {sortedResults.map((item, idx) => {
                return (
                  <Tr key={item.symbol}>
                    <Td borderColor="gray.600" color="gray.500" fontSize="xs">
                      {idx + 1}
                    </Td>
                    <Td borderColor="gray.600" fontWeight="medium" color="gray.200">
                      {item.symbol}
                    </Td>
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
                )
              })}
            </Tbody>
          </Table>
        </Box>
      )}
    </Box>
  )
}

function ThresholdReport({ symbol, data, securities }) {
  const [mode, setMode] = useState('single') // 'single' or 'all'

  return (
    <Box bg="gray.800" borderRadius="md" p={4}>
      <HStack justify="space-between" mb={4}>
        <Heading size="md" color="blue.300">
          Threshold Report
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
            variant={mode === 'all' ? 'solid' : 'outline'}
            colorScheme="blue"
            onClick={() => setMode('all')}
          >
            All Tickers
          </Button>
        </HStack>
      </HStack>

      <Text fontSize="sm" color="gray.400" mb={4}>
        Counts how many times price raised for X% compared to previous day close. <br />
        LONG the CLOSE @ X DAYS DTE (or stocks at leverage)  <br />
          Data in cells are % for period (header) analysis is made from
      </Text>

      {mode === 'single' ? (
        <SingleSymbolReport symbol={symbol} data={data} />
      ) : (
        <MultiSymbolReport securities={securities} />
      )}

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
