import { useState, useEffect } from 'react'
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
  Spinner
} from '@chakra-ui/react'
import { fetchFromCache } from '../modules/dataService'
import { getWatchgroups, getWatchgroupSymbols, getActiveWatchgroup } from '../api/userDataService'

// Check if the pattern occurred: 2 positive days followed by big move
// data[0] = most recent day (day 3 - big move day, change > threshold)
// data[1] = day before (day 2 - should be positive)
// data[2] = 2 days before (day 1 - should be positive)
// data[3] = 3 days before (baseline for day 1)
function checkPattern(data, threshold = 10) {
  if (!data || data.length < 4) return null

  const day3 = data[0]  // Most recent - check for big move (close > prev close by threshold%)
  const day2 = data[1]  // Day before - should be positive
  const day1 = data[2]  // 2 days before - should be positive
  const day0 = data[3]  // 3 days before - baseline

  // Check if day1 was positive (close > prev close)
  const day1Positive = day1?.close && day0?.close && day1.close > day0.close
  // Check if day2 was positive
  const day2Positive = day2?.close && day1?.close && day2.close > day1.close

  // Calculate day3's change (close to close)
  const day3Change = day3?.close && day2?.close && day2.close > 0
    ? ((day3.close - day2.close) / day2.close) * 100
    : 0

  const patternMatch = day1Positive && day2Positive && day3Change >= threshold

  return {
    match: patternMatch,
    day1Positive,
    day2Positive,
    day3Change,
    day1Change: day1?.close && day0?.close ? ((day1.close - day0.close) / day0.close) * 100 : 0,
    day2Change: day2?.close && day1?.close ? ((day2.close - day1.close) / day1.close) * 100 : 0,
    currentPrice: day3?.close
  }
}

function TwoUpThenBigMove({ symbol, data }) {
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [threshold, setThreshold] = useState(5)
  const [watchgroups, setWatchgroups] = useState([])
  const [selectedGroupId, setSelectedGroupId] = useState(null)
  const [groupSymbols, setGroupSymbols] = useState([])

  useEffect(() => {
    loadWatchgroups()
  }, [])

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

  const runAnalysis = async () => {
    if (!groupSymbols || groupSymbols.length === 0) return

    setLoading(true)
    const matches = []

    for (const sym of groupSymbols) {
      try {
        const symData = await fetchFromCache(sym, '1d')
        const result = checkPattern(symData, threshold)
        if (result?.match) {
          matches.push({
            symbol: sym,
            ...result
          })
        }
      } catch (err) {
        console.error(`Failed to check ${sym}:`, err)
      }
    }

    // Sort by day3 change descending
    matches.sort((a, b) => b.day3Change - a.day3Change)
    setResults(matches)
    setLoading(false)
  }

  const selectedGroup = watchgroups.find(g => g.id === selectedGroupId)

  return (
    <Box bg="gray.800" borderRadius="md" p={4}>
      <Heading size="md" color="orange.300" mb={4}>
        Two Up + Big Move
      </Heading>

      <Text fontSize="sm" color="gray.400" mb={4}>
        Finds tickers where: Day 1 positive, Day 2 positive, Day 3 (last day) change &gt; +{threshold}%. <br/>
        Usually you wouldn't want gap in current day if you want long.
      </Text>

      <HStack mb={4} spacing={4} wrap="wrap">
        <HStack>
          <Text fontSize="sm" color="gray.400">Watchgroup:</Text>
          <Select
            size="sm"
            w="150px"
            value={selectedGroupId || ''}
            onChange={(e) => {
              setSelectedGroupId(Number(e.target.value))
              setResults([])
            }}
          >
            {watchgroups.map(g => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </Select>
        </HStack>

        <HStack>
          <Text fontSize="sm" color="gray.400">Min 3rd day move:</Text>
          <Select
            size="sm"
            w="100px"
            value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
          >
            <option value={5}>+5%</option>
            <option value={10}>+10%</option>
            <option value={15}>+15%</option>
            <option value={20}>+20%</option>
          </Select>
        </HStack>

        <Button
          colorScheme="orange"
          size="sm"
          onClick={runAnalysis}
          isLoading={loading}
          isDisabled={groupSymbols.length === 0}
        >
          Find Patterns ({groupSymbols.length} tickers)
        </Button>
      </HStack>

      {loading && (
        <HStack spacing={3}>
          <Spinner size="sm" />
          <Text color="gray.400">Scanning...</Text>
        </HStack>
      )}

      {!loading && results.length > 0 && (
        <Box overflowX="auto">
          <Table variant="simple" size="sm">
            <Thead>
              <Tr>
                <Th color="gray.400" borderColor="gray.600">#</Th>
                <Th color="gray.400" borderColor="gray.600">Symbol</Th>
                <Th color="gray.400" borderColor="gray.600" textAlign="right">Price</Th>
                <Th color="gray.400" borderColor="gray.600" textAlign="right">Day 1 Chg</Th>
                <Th color="gray.400" borderColor="gray.600" textAlign="right">Day 2 Chg</Th>
                <Th color="orange.300" borderColor="gray.600" textAlign="right">Day 3 Chg</Th>
              </Tr>
            </Thead>
            <Tbody>
              {results.map((item, idx) => (
                <Tr key={item.symbol}>
                  <Td borderColor="gray.600" color="gray.500" fontSize="xs">
                    {idx + 1}
                  </Td>
                  <Td borderColor="gray.600" fontWeight="medium" color="gray.200">
                    {item.symbol}
                  </Td>
                  <Td borderColor="gray.600" color="gray.300" textAlign="right">
                    ${item.currentPrice?.toFixed(2) || '-'}
                  </Td>
                  <Td borderColor="gray.600" textAlign="right">
                    <Badge colorScheme="green">+{item.day1Change.toFixed(1)}%</Badge>
                  </Td>
                  <Td borderColor="gray.600" textAlign="right">
                    <Badge colorScheme="green">+{item.day2Change.toFixed(1)}%</Badge>
                  </Td>
                  <Td borderColor="gray.600" textAlign="right">
                    <Badge colorScheme="orange" fontSize="sm">+{item.day3Change.toFixed(1)}%</Badge>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Box>
      )}

      {!loading && results.length === 0 && groupSymbols.length > 0 && (
        <Text color="gray.500">No tickers match the pattern. Click "Find Patterns" to scan.</Text>
      )}

      {watchgroups.length === 0 && (
        <Text color="gray.400">No watchgroups available. Create a watchgroup first.</Text>
      )}
    </Box>
  )
}

export default TwoUpThenBigMove
