import { useState, useEffect, useCallback } from 'react'
import {
  Box,
  HStack,
  VStack,
  Text,
  Badge,
  Spinner,
  IconButton,
  Input,
  Tooltip,
  Flex,
  Spacer,
  useToast
} from '@chakra-ui/react'
import { AddIcon, CloseIcon, RepeatIcon, ChevronLeftIcon, ChevronRightIcon, ChevronDownIcon, ChevronUpIcon, ViewIcon, ViewOffIcon } from '@chakra-ui/icons'
import {
  getLiveWatchlist,
  addToLiveWatchlist,
  removeFromLiveWatchlist,
  reorderLiveWatchlist
} from '../db/database'
import { getIntradayForDay, fetchIntradayForDay } from '../modules/dataService'
import LiveIndicator from './LiveIndicator'
import MiniIntradayChart from './MiniIntradayChart'

const API_BASE = 'http://localhost:3001/api'

// Known index symbols (trade on US exchanges)
const US_INDEX_SYMBOLS = ['VIX', 'SPX', 'NDX', 'DJX', 'RUT']

// Get market status for a symbol
// Returns: { status: string, colorScheme: string }
function getMarketStatusForSymbol(symbol) {
  const now = new Date()
  const upperSymbol = symbol.toUpperCase()

  // For now, assume US market hours for most symbols
  // TODO: Could expand to detect forex, futures, crypto, etc.
  const eastern = new Date(now.toLocaleString('en-US', { timeZone: 'America/New_York' }))
  const day = eastern.getDay()
  const hours = eastern.getHours()
  const minutes = eastern.getMinutes()
  const timeInMinutes = hours * 60 + minutes

  const marketOpen = 9 * 60 + 30   // 9:30 AM
  const marketClose = 16 * 60      // 4:00 PM
  const preMarketOpen = 4 * 60     // 4:00 AM
  const afterMarketClose = 20 * 60 // 8:00 PM

  // Weekend
  if (day === 0 || day === 6) {
    return { status: 'Wknd', colorScheme: 'gray' }
  }

  // Regular hours
  if (timeInMinutes >= marketOpen && timeInMinutes < marketClose) {
    return { status: 'Open', colorScheme: 'green' }
  }

  // Pre-market
  if (timeInMinutes >= preMarketOpen && timeInMinutes < marketOpen) {
    return { status: 'Pre', colorScheme: 'yellow' }
  }

  // After-hours
  if (timeInMinutes >= marketClose && timeInMinutes < afterMarketClose) {
    return { status: 'AH', colorScheme: 'yellow' }
  }

  return { status: 'Closed', colorScheme: 'gray' }
}

function LiveWatchlistCard({ symbol, quote, onRemove, onMoveLeft, onMoveRight, canMoveLeft, canMoveRight, isLoading, isExpanded, onToggleExpand, intradayData, intradayLoading, onFetchIntraday, isConnected }) {
  const [isHovered, setIsHovered] = useState(false)

  // Fetch intraday data when expanded
  useEffect(() => {
    // Fetch if expanded AND (no data OR empty data) AND not currently loading
    const hasData = intradayData && intradayData.length > 0
    if (isExpanded && !hasData && !intradayLoading && isConnected) {
      onFetchIntraday(symbol)
    }
  }, [isExpanded, intradayData, intradayLoading, onFetchIntraday, symbol, isConnected])

  const cardBase = {
    bg: 'gray.800',
    borderRadius: 'md',
    p: 2,
    w: '100%',
    borderWidth: '1px',
    position: 'relative',
    transition: 'all 0.15s'
  }

  const formatPrice = (p) => p != null ? p.toFixed(2) : '--'
  const formatPct = (p) => p != null ? (p >= 0 ? '+' : '') + p.toFixed(2) + '%' : '--'
  const formatVolume = (v) => {
    if (v == null) return '--'
    if (v >= 1000000000) return (v / 1000000000).toFixed(1) + 'B'
    if (v >= 1000000) return (v / 1000000).toFixed(1) + 'M'
    if (v >= 1000) return (v / 1000).toFixed(1) + 'K'
    return v.toString()
  }
  const formatMarketCap = (v) => {
    if (v == null) return '--'
    if (v >= 1000000000000) return '$' + (v / 1000000000000).toFixed(2) + 'T'
    if (v >= 1000000000) return '$' + (v / 1000000000).toFixed(2) + 'B'
    if (v >= 1000000) return '$' + (v / 1000000).toFixed(2) + 'M'
    return '$' + v.toFixed(0)
  }

  if (!quote && !isLoading) {
    return (
      <Box
        {...cardBase}
        borderColor="gray.700"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        <HStack justify="space-between" mb={1}>
          <Badge colorScheme="gray" fontSize="xs">{symbol}</Badge>
          <IconButton
            icon={<CloseIcon />}
            size="xs"
            variant="ghost"
            colorScheme="red"
            onClick={() => onRemove(symbol)}
            aria-label="Remove"
            h="16px"
            minW="16px"
          />
        </HStack>
        <Text color="gray.500" fontSize="xs">No data</Text>
        {isHovered && (
          <ReorderButtons
            onMoveLeft={onMoveLeft}
            onMoveRight={onMoveRight}
            canMoveLeft={canMoveLeft}
            canMoveRight={canMoveRight}
          />
        )}
      </Box>
    )
  }

  if (isLoading && !quote) {
    return (
      <Box
        {...cardBase}
        borderColor="gray.700"
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      >
        <HStack justify="space-between" mb={1}>
          <Badge colorScheme="blue" fontSize="xs">{symbol}</Badge>
          <IconButton
            icon={<CloseIcon />}
            size="xs"
            variant="ghost"
            colorScheme="red"
            onClick={() => onRemove(symbol)}
            aria-label="Remove"
            h="16px"
            minW="16px"
          />
        </HStack>
        <Spinner size="xs" color="blue.400" />
        {isHovered && (
          <ReorderButtons
            onMoveLeft={onMoveLeft}
            onMoveRight={onMoveRight}
            canMoveLeft={canMoveLeft}
            canMoveRight={canMoveRight}
          />
        )}
      </Box>
    )
  }

  const last = quote?.last
  const prevClose = quote?.prevClose
  const open = quote?.open
  const vwap = quote?.vwap
  const high = quote?.high
  const low = quote?.low
  const volume = quote?.volume
  const marketCap = quote?.marketCap
  const avgVolume = quote?.avgVolume
  const week52High = quote?.week52High
  const week52Low = quote?.week52Low

  const changeFromClose = last && prevClose ? last - prevClose : null
  const changePctFromClose = last && prevClose ? ((last - prevClose) / prevClose) * 100 : null

  const changeColor = changePctFromClose > 0 ? 'green' : changePctFromClose < 0 ? 'red' : 'gray'
  const marketStatus = getMarketStatusForSymbol(symbol)

  return (
    <Box
      {...cardBase}
      borderColor={`${changeColor}.600`}
      borderLeftWidth="3px"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <HStack justify="space-between" mb={1}>
        <HStack spacing={1}>
          <Badge colorScheme={changeColor} fontSize="xs" fontWeight="bold">
            {symbol}
          </Badge>
          {marketStatus.status !== 'Open' && (
            <Tooltip label={`Market ${marketStatus.status === 'Pre' ? 'Pre-Market' : marketStatus.status === 'AH' ? 'After-Hours' : marketStatus.status}`} fontSize="xs">
              <Badge
                colorScheme={marketStatus.colorScheme}
                variant="outline"
                fontSize="8px"
                px={1}
                py={0}
                lineHeight="1.4"
              >
                {marketStatus.status}
              </Badge>
            </Tooltip>
          )}
        </HStack>
        <IconButton
          icon={<CloseIcon />}
          size="xs"
          variant="ghost"
          colorScheme="red"
          onClick={() => onRemove(symbol)}
          aria-label="Remove"
          opacity={0.5}
          _hover={{ opacity: 1 }}
          h="16px"
          minW="16px"
        />
      </HStack>

      {/* Last price */}
      <Text
        fontSize="md"
        fontWeight="bold"
        fontFamily="mono"
        color={`${changeColor}.300`}
        lineHeight="1.2"
      >
        {formatPrice(last)}
      </Text>

      {/* Change % */}
      <Text fontSize="xs" fontFamily="mono" color={`${changeColor}.400`} mb={1}>
        {formatPct(changePctFromClose)}
      </Text>

      {/* Compact details */}
      <VStack align="stretch" spacing={0} fontSize="10px" color="gray.500">
        <HStack justify="space-between">
          <Tooltip label="Open" placement="left" fontSize="xs">
            <Text cursor="help">O</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatPrice(open)}</Text>
        </HStack>
        <HStack justify="space-between">
          <Tooltip label="High" placement="left" fontSize="xs">
            <Text cursor="help">H</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatPrice(high)}</Text>
        </HStack>
        <HStack justify="space-between">
          <Tooltip label="Low" placement="left" fontSize="xs">
            <Text cursor="help">L</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatPrice(low)}</Text>
        </HStack>
        <HStack justify="space-between">
          <Tooltip label="Previous Close" placement="left" fontSize="xs">
            <Text cursor="help">PC</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatPrice(prevClose)}</Text>
        </HStack>
        <HStack justify="space-between">
          <Tooltip label="VWAP (Volume Weighted Avg Price)" placement="left" fontSize="xs">
            <Text cursor="help">VW</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatPrice(vwap)}</Text>
        </HStack>
        <HStack justify="space-between">
          <Tooltip label="Today's Trading Volume" placement="left" fontSize="xs">
            <Text cursor="help">Vol</Text>
          </Tooltip>
          <Text fontFamily="mono" color="gray.400">{formatVolume(volume)}</Text>
        </HStack>
      </VStack>

      {/* Expanded details */}
      {isExpanded && (
        <VStack align="stretch" spacing={2} fontSize="10px" color="gray.500" mt={2} pt={2} borderTop="1px solid" borderColor="gray.700">
          {/* Mini intraday chart */}
          <MiniIntradayChart
            data={intradayData}
            loading={intradayLoading}
            symbol={symbol}
            vwapValue={vwap}
          />

          {/* Stats */}
          <VStack align="stretch" spacing={0}>
            <HStack justify="space-between">
              <Tooltip label="Market Capitalization" placement="left" fontSize="xs">
                <Text cursor="help">MCap</Text>
              </Tooltip>
              <Text fontFamily="mono" color="blue.300">{formatMarketCap(marketCap)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="Average Daily Volume" placement="left" fontSize="xs">
                <Text cursor="help">AvgV</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatVolume(avgVolume)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="52-Week High" placement="left" fontSize="xs">
                <Text cursor="help">52H</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatPrice(week52High)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="52-Week Low" placement="left" fontSize="xs">
                <Text cursor="help">52L</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatPrice(week52Low)}</Text>
            </HStack>
          </VStack>
        </VStack>
      )}

      {/* Expand button */}
      <Box
        as="button"
        w="100%"
        mt={1}
        mb={isHovered ? 3 : 0}
        py={0.5}
        bg="gray.750"
        borderRadius="sm"
        onClick={onToggleExpand}
        _hover={{ bg: 'gray.700' }}
        display="flex"
        alignItems="center"
        justifyContent="center"
        transition="margin 0.15s"
      >
        {isExpanded ? (
          <ChevronUpIcon color="gray.500" boxSize={3} />
        ) : (
          <ChevronDownIcon color="gray.500" boxSize={3} />
        )}
      </Box>

      {isHovered && (
        <ReorderButtons
          onMoveLeft={onMoveLeft}
          onMoveRight={onMoveRight}
          canMoveLeft={canMoveLeft}
          canMoveRight={canMoveRight}
        />
      )}
    </Box>
  )
}

function ReorderButtons({ onMoveLeft, onMoveRight, canMoveLeft, canMoveRight }) {
  return (
    <HStack
      position="absolute"
      bottom="-8px"
      left="50%"
      transform="translateX(-50%)"
      spacing={0}
      bg="gray.700"
      borderRadius="md"
      p="1px"
      boxShadow="md"
    >
      <IconButton
        icon={<ChevronLeftIcon />}
        size="xs"
        variant="ghost"
        onClick={onMoveLeft}
        isDisabled={!canMoveLeft}
        aria-label="Move left"
        h="18px"
        minW="22px"
        fontSize="14px"
      />
      <IconButton
        icon={<ChevronRightIcon />}
        size="xs"
        variant="ghost"
        onClick={onMoveRight}
        isDisabled={!canMoveRight}
        aria-label="Move right"
        h="18px"
        minW="22px"
        fontSize="14px"
      />
    </HStack>
  )
}

const REFRESH_INTERVAL = 15 // seconds

function LiveWatchlist({ isConnected }) {
  const [symbols, setSymbols] = useState([])
  const [quotes, setQuotes] = useState({})
  const [loading, setLoading] = useState(false)
  const [newSymbol, setNewSymbol] = useState('')
  const [isAdding, setIsAdding] = useState(false)
  const [expandedSymbols, setExpandedSymbols] = useState(new Set())
  const [lastFetchTime, setLastFetchTime] = useState(null)
  const [intradayData, setIntradayData] = useState({}) // { symbol: data[] }
  const [intradayLoading, setIntradayLoading] = useState({}) // { symbol: boolean }
  const toast = useToast()

  const toggleExpanded = (symbol) => {
    setExpandedSymbols(prev => {
      const next = new Set(prev)
      if (next.has(symbol)) {
        next.delete(symbol)
      } else {
        next.add(symbol)
      }
      return next
    })
  }

  const toggleAllExpanded = () => {
    if (expandedSymbols.size === symbols.length) {
      // All expanded, collapse all
      setExpandedSymbols(new Set())
    } else {
      // Expand all
      setExpandedSymbols(new Set(symbols))
    }
  }

  const allExpanded = symbols.length > 0 && expandedSymbols.size === symbols.length

  // Fetch intraday data for a symbol (current day)
  const fetchIntraday = useCallback(async (symbol, forceRefresh = false) => {
    if (intradayLoading[symbol]) return

    setIntradayLoading(prev => ({ ...prev, [symbol]: true }))

    try {
      // Get start of today in local time
      const today = new Date()
      today.setHours(0, 0, 0, 0)
      const startTimestamp = today.getTime()

      let data = null

      // First try to get from cache (unless force refresh)
      if (!forceRefresh) {
        data = await getIntradayForDay(symbol, startTimestamp, '5m')
      }

      // If no data, stale, or force refresh - fetch from IBKR
      // Also fetch if we have very few bars (might be missing pre-market)
      if (isConnected && (!data || data.length === 0 || data.length < 5 || forceRefresh)) {
        data = await fetchIntradayForDay(symbol, startTimestamp, '5m')
      }

      setIntradayData(prev => ({ ...prev, [symbol]: data || [] }))
    } catch (err) {
      console.error(`Failed to fetch intraday data for ${symbol}:`, err)
      setIntradayData(prev => ({ ...prev, [symbol]: [] }))
    } finally {
      setIntradayLoading(prev => ({ ...prev, [symbol]: false }))
    }
  }, [isConnected, intradayLoading])

  const loadSymbols = useCallback(async () => {
    const saved = await getLiveWatchlist()
    setSymbols(saved)
  }, [])

  useEffect(() => {
    loadSymbols()
  }, [loadSymbols])

  const fetchQuotes = useCallback(async () => {
    if (!isConnected || symbols.length === 0) return

    setLoading(true)
    setLastFetchTime(Date.now()) // Set at start so timer aligns with interval
    try {
      const response = await fetch(`${API_BASE}/live-quotes`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ symbols })
      })

      if (!response.ok) {
        throw new Error('Failed to fetch quotes')
      }

      const data = await response.json()
      setQuotes(data)
    } catch (err) {
      console.error('Failed to fetch live quotes:', err)
    } finally {
      setLoading(false)
    }
  }, [isConnected, symbols])

  useEffect(() => {
    if (!isConnected || symbols.length === 0) return

    fetchQuotes()
    const interval = setInterval(fetchQuotes, REFRESH_INTERVAL * 1000)
    return () => clearInterval(interval)
  }, [isConnected, symbols, fetchQuotes])

  const handleAddSymbol = async () => {
    const symbol = newSymbol.trim().toUpperCase()
    if (!symbol) return

    if (symbols.includes(symbol)) {
      toast({
        title: 'Already exists',
        description: `${symbol} is already in the watchlist`,
        status: 'warning',
        duration: 2000
      })
      return
    }

    await addToLiveWatchlist(symbol)
    setSymbols([...symbols, symbol])
    setNewSymbol('')
    setIsAdding(false)

    if (isConnected) {
      try {
        const response = await fetch(`${API_BASE}/live-quotes`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ symbols: [symbol] })
        })
        if (response.ok) {
          const data = await response.json()
          setQuotes(prev => ({ ...prev, ...data }))
        }
      } catch (err) {
        console.error('Failed to fetch quote for new symbol:', err)
      }
    }
  }

  const handleRemoveSymbol = async (symbol) => {
    await removeFromLiveWatchlist(symbol)
    setSymbols(symbols.filter(s => s !== symbol))
    setQuotes(prev => {
      const next = { ...prev }
      delete next[symbol]
      return next
    })
  }

  const handleMoveLeft = async (index) => {
    if (index <= 0) return
    const newSymbols = [...symbols]
    ;[newSymbols[index - 1], newSymbols[index]] = [newSymbols[index], newSymbols[index - 1]]
    setSymbols(newSymbols)
    await reorderLiveWatchlist(newSymbols)
  }

  const handleMoveRight = async (index) => {
    if (index >= symbols.length - 1) return
    const newSymbols = [...symbols]
    ;[newSymbols[index], newSymbols[index + 1]] = [newSymbols[index + 1], newSymbols[index]]
    setSymbols(newSymbols)
    await reorderLiveWatchlist(newSymbols)
  }

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleAddSymbol()
    } else if (e.key === 'Escape') {
      setIsAdding(false)
      setNewSymbol('')
    }
  }

  if (symbols.length === 0 && !isAdding) {
    return (
      <Box
        bg="gray.850"
        borderBottom="1px solid"
        borderColor="gray.700"
        px={4}
        py={2}
      >
        <HStack>
          <Text color="gray.500" fontSize="sm">Live Watchlist</Text>
          <Tooltip label="Add symbol">
            <IconButton
              icon={<AddIcon />}
              size="xs"
              variant="ghost"
              colorScheme="blue"
              onClick={() => setIsAdding(true)}
              aria-label="Add symbol"
            />
          </Tooltip>
        </HStack>
      </Box>
    )
  }

  return (
    <Box
      bg="gray.850"
      borderBottom="1px solid"
      borderColor="gray.700"
      px={4}
      py={2}
    >
      <Flex align="center" mb={symbols.length > 0 ? 2 : 0}>
        <HStack spacing={2}>
          <Text color="gray.400" fontSize="sm" fontWeight="medium">Live Watchlist</Text>
          {symbols.length > 0 && (
            <LiveIndicator
              isConnected={isConnected}
              isLoading={loading}
              lastFetchTime={lastFetchTime}
              refreshInterval={REFRESH_INTERVAL}
            />
          )}
        </HStack>
        <Spacer />
        <HStack spacing={2}>
          {isAdding ? (
            <HStack>
              <Input
                size="xs"
                placeholder="Symbol"
                value={newSymbol}
                onChange={(e) => setNewSymbol(e.target.value.toUpperCase())}
                onKeyDown={handleKeyDown}
                autoFocus
                w="70px"
                bg="gray.700"
                borderColor="gray.600"
              />
              <IconButton
                icon={<AddIcon />}
                size="xs"
                colorScheme="green"
                onClick={handleAddSymbol}
                aria-label="Add"
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                onClick={() => { setIsAdding(false); setNewSymbol('') }}
                aria-label="Cancel"
              />
            </HStack>
          ) : (
            <Tooltip label="Add symbol">
              <IconButton
                icon={<AddIcon />}
                size="xs"
                variant="ghost"
                colorScheme="blue"
                onClick={() => setIsAdding(true)}
                aria-label="Add symbol"
              />
            </Tooltip>
          )}
          {symbols.length > 0 && (
            <Tooltip label={allExpanded ? "Collapse all" : "Expand all"}>
              <IconButton
                icon={allExpanded ? <ViewOffIcon /> : <ViewIcon />}
                size="xs"
                variant="ghost"
                colorScheme="blue"
                onClick={toggleAllExpanded}
                aria-label={allExpanded ? "Collapse all" : "Expand all"}
              />
            </Tooltip>
          )}
          <Tooltip label="Refresh">
            <IconButton
              icon={<RepeatIcon />}
              size="xs"
              variant="ghost"
              colorScheme="blue"
              onClick={fetchQuotes}
              isDisabled={!isConnected || symbols.length === 0}
              aria-label="Refresh"
            />
          </Tooltip>
        </HStack>
      </Flex>

      {symbols.length > 0 && (
        <Box
          display="grid"
          gridTemplateColumns={{ base: 'repeat(4, 1fr)', lg: 'repeat(6, 1fr)', xl: 'repeat(8, 1fr)' }}
          gap={2}
          pb={3}
        >
          {symbols.map((symbol, index) => (
            <LiveWatchlistCard
              key={symbol}
              symbol={symbol}
              quote={quotes[symbol]}
              onRemove={handleRemoveSymbol}
              onMoveLeft={() => handleMoveLeft(index)}
              onMoveRight={() => handleMoveRight(index)}
              canMoveLeft={index > 0}
              canMoveRight={index < symbols.length - 1}
              isLoading={loading && !quotes[symbol]}
              isExpanded={expandedSymbols.has(symbol)}
              onToggleExpand={() => toggleExpanded(symbol)}
              intradayData={intradayData[symbol]}
              intradayLoading={intradayLoading[symbol]}
              onFetchIntraday={fetchIntraday}
              isConnected={isConnected}
            />
          ))}
        </Box>
      )}
    </Box>
  )
}

export default LiveWatchlist
