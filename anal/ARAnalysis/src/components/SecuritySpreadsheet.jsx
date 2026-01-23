import React, { useMemo, useState, useEffect } from 'react'
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
  Button,
  Select,
  Badge,
  IconButton,
  Tooltip,
  NumberInput,
  NumberInputField,
  NumberInputStepper,
  NumberIncrementStepper,
  NumberDecrementStepper
} from '@chakra-ui/react'
import { ViewIcon, ViewOffIcon, ChevronDownIcon, ChevronRightIcon } from '@chakra-ui/icons'
import CandlestickChart from './CandlestickChart'
import { getIntradayForDay, fetchIntradayForDay } from '../modules/dataService'
import ConditionalFormatManager from './ConditionalFormatManager'
import { getConditionalFormats } from '../db/database'

const ROWS_PER_PAGE_OPTIONS = [25, 50, 100, 200]

// Evaluate a single condition against a row
function evaluateCondition(cond, row, prevRow) {
  // Get the value to check
  let value
  switch (cond.column) {
    case 'open': value = row.open; break
    case 'high': value = row.high; break
    case 'low': value = row.low; break
    case 'close': value = row.close; break
    case 'volume': value = row.volume; break
    case 'change': value = row.close - row.open; break
    case 'changePercent': value = ((row.close - row.open) / row.open) * 100; break
    default: return false
  }

  // Get the comparison value
  let compareValue
  if (cond.compareToColumn) {
    switch (cond.compareToColumn) {
      case 'open': compareValue = row.open; break
      case 'high': compareValue = row.high; break
      case 'low': compareValue = row.low; break
      case 'close': compareValue = row.close; break
      case 'volume': compareValue = row.volume; break
      case 'prevOpen': compareValue = prevRow?.open; break
      case 'prevHigh': compareValue = prevRow?.high; break
      case 'prevLow': compareValue = prevRow?.low; break
      case 'prevClose': compareValue = prevRow?.close; break
      case 'prevVolume': compareValue = prevRow?.volume; break
      default: compareValue = cond.value
    }
  } else {
    compareValue = cond.value
  }

  if (compareValue == null || compareValue === 0) return false

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
function evaluateRule(rule, row, prevRow) {
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
  return conditions.every(cond => evaluateCondition(cond, row, prevRow))
}

// Get the format to apply based on all matching rules
function getCellFormat(rules, row, prevRow, column) {
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

    // Check if any condition targets this column
    const targetsColumn = conditions.some(cond => {
      const condColumn = cond.column
      if (condColumn === column) return true
      // For change/changePercent, apply to close column
      if ((condColumn === 'change' || condColumn === 'changePercent') && column === 'close') return true
      return false
    })

    if (!targetsColumn) return false

    return evaluateRule(rule, row, prevRow)
  })

  if (matchingRules.length === 0) return null

  // Return the last matching rule's format (highest priority)
  return matchingRules[matchingRules.length - 1].format
}

function SecuritySpreadsheet({ data, symbol, interval, loading, connectionState }) {
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

  useEffect(() => {
    loadRules()
  }, [])

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
  const rowMatchesAnyRule = (row, rowIndex) => {
    if (formatRules.length === 0) return false
    const prevRow = data[rowIndex + 1]
    return formatRules.some(rule => evaluateRule(rule, row, prevRow))
  }

  // Filter data based on rules if filtering is enabled
  // Store original index for correct prevRow lookup
  const filteredData = useMemo(() => {
    const dataWithIndex = data.map((row, index) => ({ ...row, _originalIndex: index, _isMatch: false }))

    if (!filterEnabled || formatRules.length === 0) {
      return dataWithIndex
    }

    // Find all matching indices
    const matchingIndices = new Set()
    dataWithIndex.forEach((row, index) => {
      if (rowMatchesAnyRule(row, index)) {
        matchingIndices.add(index)
        // Mark as actual match
        row._isMatch = true
      }
    })

    // Expand to include context rows
    const includedIndices = new Set()
    matchingIndices.forEach(index => {
      // Add rows before (data is descending, so "before" in time means higher index)
      for (let i = index; i <= Math.min(index + contextBefore, data.length - 1); i++) {
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
        _isMatch: matchingIndices.has(row._originalIndex)
      }))
  }, [data, filterEnabled, formatRules, contextBefore, contextAfter])

  const paginatedData = useMemo(() => {
    const start = currentPage * rowsPerPage
    const end = start + rowsPerPage
    return filteredData.slice(start, end)
  }, [filteredData, currentPage, rowsPerPage])

  const totalPages = Math.ceil(filteredData.length / rowsPerPage)

  // Reset to first page when filter changes
  useEffect(() => {
    setCurrentPage(0)
  }, [filterEnabled, formatRules])

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
        } catch (err) {
          console.error('Failed to load intraday data:', err)
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
    } catch (err) {
      console.error('Failed to fetch intraday data:', err)
    }
    setFetchingIntraday(prev => ({ ...prev, [timestamp]: false }))
  }

  // Get format for a specific cell
  const getFormat = (row, rowIndex, column) => {
    if (formatRules.length === 0) return {}

    // Get prev row using original index (data is sorted descending, so prev in time is next in array)
    const originalIndex = row._originalIndex
    const prevRow = data[originalIndex + 1]

    const format = getCellFormat(formatRules, row, prevRow, column)
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
        <Text color="gray.500" fontSize="lg">
          No data available for {symbol}. Use Data Manager to load data.
        </Text>
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
          <ConditionalFormatManager onRulesChange={setFormatRules} />
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
        </HStack>
      </HStack>

      {/* Table */}
      <TableContainer maxH="600px" overflowY="auto">
        <Table variant="striped" size="sm">
          <Thead position="sticky" top={0} bg="gray.700" zIndex={1}>
            <Tr>
              <Th color="gray.300" w="40px"></Th>
              <Th color="gray.300">Date/Time</Th>
              <Th color="gray.300" isNumeric>Open</Th>
              <Th color="gray.300" isNumeric>High</Th>
              <Th color="gray.300" isNumeric>Low</Th>
              <Th color="gray.300" isNumeric>Close</Th>
              <Th color="gray.300" isNumeric>Volume</Th>
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
                      <Td colSpan={7} py={1} bg="gray.700" borderY="1px dashed" borderColor="gray.500">
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
                    <Td color="gray.300">
                      {isContextRow && <Text as="span" color="gray.500" mr={1}>•</Text>}
                      {formatDate(row.timestamp)}
                    </Td>
                    <Td isNumeric {...getFormat(row, index, 'open')}>{formatPrice(row.open)}</Td>
                    <Td isNumeric {...getFormat(row, index, 'high')}>{formatPrice(row.high)}</Td>
                    <Td isNumeric {...getFormat(row, index, 'low')}>{formatPrice(row.low)}</Td>
                    <Td isNumeric {...getFormat(row, index, 'close')}>{formatPrice(row.close)}</Td>
                    <Td isNumeric {...getFormat(row, index, 'volume')}>
                      {row.volume?.toLocaleString() || '-'}
                    </Td>
                  </Tr>
                  {expandedRows[row.timestamp] && (
                    <Tr>
                      <Td colSpan={7} bg="gray.900" p={4}>
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
