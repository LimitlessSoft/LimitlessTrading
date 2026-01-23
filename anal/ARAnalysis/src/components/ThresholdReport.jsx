import { useMemo } from 'react'
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
  HStack
} from '@chakra-ui/react'

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

function ThresholdReport({ symbol, data }) {
  // Calculate results for all time periods
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

  const availableDays = data.length - 1 // -1 because we need previous bar for calculation

  return (
    <Box bg="gray.800" borderRadius="md" p={4}>
      <Heading size="md" color="blue.300" mb={4}>
        Threshold Report - {symbol}
      </Heading>

      <Text fontSize="sm" color="gray.400" mb={4}>
          Counts how many times price raised for X% compared to previous day close. <br />
          LONG the CLOSE @ X DAYS DTE (or stocks at leverage)
      </Text>

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

      <HStack mt={4} spacing={4} fontSize="xs" color="gray.500">
        <Text>Data available: {availableDays} trading days</Text>
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
