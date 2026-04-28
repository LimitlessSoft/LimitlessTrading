import { useState, useEffect, useCallback, useRef, useMemo } from 'react'
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
  reorderLiveWatchlist,
  getLiveWatchlistInterval
} from '../api/userDataService'
import { getIntradayForDay, fetchIntradayForDay, getHistoricalMedian5mVolume } from '../modules/dataService'
import LiveIndicator from './LiveIndicator'
import MiniIntradayChart from './MiniIntradayChart'
import { API_BASE, BACKEND_URL } from '../api'

// Known index symbols (trade on US exchanges)
const US_INDEX_SYMBOLS = ['VIX', 'SPX', 'NDX', 'DJX', 'RUT']

function isMarketClosed() {
  const now = new Date()
  const eastern = new Date(now.toLocaleString('en-US', { timeZone: 'America/New_York' }))
  const day = eastern.getDay()
  const timeInMinutes = eastern.getHours() * 60 + eastern.getMinutes()
  if (day === 0 || day === 6) return true
  const preMarketOpen = 4 * 60     // 4:00 AM
  const afterMarketClose = 20 * 60 // 8:00 PM
  return timeInMinutes < preMarketOpen || timeInMinutes >= afterMarketClose
}

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
  const [historicalMedian, setHistoricalMedian] = useState(null)

  // Fetch intraday data when expanded
  useEffect(() => {
    // Fetch if expanded AND (no data OR empty data) AND not currently loading
    const hasData = intradayData && intradayData.length > 0
    if (isExpanded && !hasData && !intradayLoading && isConnected) {
      onFetchIntraday(symbol)
    }
  }, [isExpanded, intradayData, intradayLoading, onFetchIntraday, symbol, isConnected])

  // Fetch historical median 5m volume when expanded
  useEffect(() => {
    if (!isExpanded) return

    const fetchHistoricalMedian = async () => {
      try {
        const result = await getHistoricalMedian5mVolume(symbol, 10, '5m')
        setHistoricalMedian(result)
      } catch {
      }
    }

    fetchHistoricalMedian()
  }, [isExpanded, symbol])

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

  // Calculate 5-minute volume metrics from intraday data
  const { last5mVol, median5mVol, volRatio, candleSecondsLeft, candleElapsedPct, relativeVolRatio, sampleDays } = useMemo(() => {
    if (!intradayData || intradayData.length === 0) {
      return { last5mVol: null, median5mVol: null, volRatio: null, candleSecondsLeft: null, candleElapsedPct: null, relativeVolRatio: null, sampleDays: null }
    }

    // Get the most recent bar's volume and timestamp
    const lastBar = intradayData[intradayData.length - 1]
    const last5mVol = lastBar?.volume ?? null
    const lastBarTimestamp = lastBar?.timestamp ?? null

    // Calculate time until candle close (5-minute candles)
    let candleSecondsLeft = null
    let candleElapsedPct = null
    if (lastBarTimestamp) {
      const now = Date.now()
      const candleDuration = 5 * 60 * 1000 // 5 minutes in ms
      const candleEnd = lastBarTimestamp + candleDuration
      const msLeft = Math.max(0, candleEnd - now)
      candleSecondsLeft = Math.floor(msLeft / 1000)
      const elapsed = candleDuration - msLeft
      candleElapsedPct = Math.min(1, elapsed / candleDuration)
    }

    // Use historical median if available, otherwise null
    const median5mVol = historicalMedian?.median5mVol ?? null
    const sampleDays = historicalMedian?.sampleDays ?? null

    // Volume ratio (how current bar compares to historical median)
    const volRatio = last5mVol != null && median5mVol != null && median5mVol > 0
      ? last5mVol / median5mVol
      : null

    // Relative volume ratio (adjusted for elapsed time in current candle)
    // If 50% through candle with 50% of median volume, relative ratio = 100%
    const relativeVolRatio = volRatio != null && candleElapsedPct != null && candleElapsedPct > 0.1
      ? volRatio / candleElapsedPct
      : null

    return { last5mVol, median5mVol, volRatio, candleSecondsLeft, candleElapsedPct, relativeVolRatio, sampleDays }
  }, [intradayData, historicalMedian])

  // Update candle countdown every second
  const [candleCountdown, setCandleCountdown] = useState(null)
  useEffect(() => {
    if (!intradayData || intradayData.length === 0) {
      setCandleCountdown(null)
      return
    }

    const lastBar = intradayData[intradayData.length - 1]
    if (!lastBar?.timestamp) {
      setCandleCountdown(null)
      return
    }

    const updateCountdown = () => {
      const now = Date.now()
      const candleDuration = 5 * 60 * 1000
      const candleEnd = lastBar.timestamp + candleDuration
      const msLeft = Math.max(0, candleEnd - now)
      setCandleCountdown(Math.floor(msLeft / 1000))
    }

    updateCountdown()
    const timer = setInterval(updateCountdown, 1000)
    return () => clearInterval(timer)
  }, [intradayData])

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

  const subscriptionError = quote?.subscriptionError

  if (subscriptionError) {
    return (
      <Box
        {...cardBase}
        borderColor="orange.800"
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
        <Tooltip label="TWS: Edit → Global Configuration → API → Settings — enable API market data" placement="top" fontSize="xs">
          <Badge colorScheme="orange" fontSize="8px" variant="subtle" cursor="help">No API data</Badge>
        </Tooltip>
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

      {/* Compact details - sorted by value (highest first) */}
      <VStack align="stretch" spacing={0} fontSize="10px" color="gray.500">
        {[
          { key: 'O', label: "Today's opening price at market open (9:30 AM ET)", value: open },
          { key: 'H', label: "Highest price reached during today's session", value: high },
          { key: 'L', label: "Lowest price reached during today's session", value: low },
          { key: 'PC', label: "Yesterday's closing price - used to calculate daily change %", value: prevClose },
          { key: 'VW', label: 'VWAP - Volume Weighted Average Price. Average price weighted by volume, used by institutions as a trading benchmark', value: vwap }
        ]
          .filter(item => item.value != null)
          .sort((a, b) => (b.value || 0) - (a.value || 0))
          .map(item => (
            <HStack key={item.key} justify="space-between">
              <Tooltip label={item.label} placement="left" fontSize="xs">
                <Text cursor="help">{item.key}</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatPrice(item.value)}</Text>
            </HStack>
          ))}
        <HStack justify="space-between">
          <Tooltip label="Total shares traded today across all exchanges" placement="left" fontSize="xs">
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

          {/* 5-Minute Volume Stats */}
          {(last5mVol != null || median5mVol != null) && (
            <VStack align="stretch" spacing={0}>
              <HStack justify="space-between">
                <Tooltip label="Volume traded in the current/last 5-minute candle. Green if 1.5x+ avg, red if below 0.5x avg" placement="left" fontSize="xs">
                  <Text cursor="help">5m Vol</Text>
                </Tooltip>
                <Text
                  fontFamily="mono"
                  color={volRatio != null ? (volRatio >= 1.5 ? 'green.300' : volRatio <= 0.5 ? 'red.300' : 'gray.400') : 'gray.400'}
                >
                  {formatVolume(last5mVol)}
                </Text>
              </HStack>
              <HStack justify="space-between">
                <Tooltip label={`Median 5-min volume from market open to now, calculated from ${sampleDays || '?'} previous trading days. Compares apples-to-apples by only using bars from 9:30 AM to current time of day`} placement="left" fontSize="xs">
                  <Text cursor="help">Med 5m</Text>
                </Tooltip>
                <Text fontFamily="mono" color="gray.400">
                  {formatVolume(median5mVol)}{sampleDays ? ` (${sampleDays}d)` : ''}
                </Text>
              </HStack>
              {candleCountdown != null && (
                <HStack justify="space-between">
                  <Tooltip label="Time remaining until the current 5-minute candle closes and a new one begins" placement="left" fontSize="xs">
                    <Text cursor="help">Closes</Text>
                  </Tooltip>
                  <Text fontFamily="mono" color="cyan.300">
                    {Math.floor(candleCountdown / 60)}:{String(candleCountdown % 60).padStart(2, '0')}
                  </Text>
                </HStack>
              )}
              {relativeVolRatio != null && (
                <HStack justify="space-between">
                  <Tooltip label="Relative Volume - Projects current candle's final volume based on time elapsed vs historical median. Example: 2 min into candle (40% elapsed) with 60% of median volume = 150% RVol (on pace for 1.5x typical). Green ≥150%, Red ≤50%" placement="left" fontSize="xs">
                    <Text cursor="help">RVol</Text>
                  </Tooltip>
                  <Text
                    fontFamily="mono"
                    color={relativeVolRatio >= 1.5 ? 'green.300' : relativeVolRatio <= 0.5 ? 'red.300' : 'gray.400'}
                  >
                    {(relativeVolRatio * 100).toFixed(0)}%
                  </Text>
                </HStack>
              )}
            </VStack>
          )}

          {/* Stats */}
          <VStack align="stretch" spacing={0}>
            <HStack justify="space-between">
              <Tooltip label="Market Capitalization - Total value of all outstanding shares (share price × shares outstanding)" placement="left" fontSize="xs">
                <Text cursor="help">MCap</Text>
              </Tooltip>
              <Text fontFamily="mono" color="blue.300">{formatMarketCap(marketCap)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="Average Daily Volume - Typical number of shares traded per day, averaged over recent trading days" placement="left" fontSize="xs">
                <Text cursor="help">AvgV</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatVolume(avgVolume)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="52-Week High - Highest price reached in the past 52 weeks (1 year)" placement="left" fontSize="xs">
                <Text cursor="help">52H</Text>
              </Tooltip>
              <Text fontFamily="mono" color="gray.400">{formatPrice(week52High)}</Text>
            </HStack>
            <HStack justify="space-between">
              <Tooltip label="52-Week Low - Lowest price reached in the past 52 weeks (1 year)" placement="left" fontSize="xs">
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

const DEFAULT_REFRESH_INTERVAL = 15 // seconds

function LiveWatchlist({ isConnected, refreshInterval: propRefreshInterval }) {
  const [symbols, setSymbols] = useState([])
  const [quotes, setQuotes] = useState({})
  const [loading, setLoading] = useState(false)
  const [newSymbol, setNewSymbol] = useState('')
  const [isAdding, setIsAdding] = useState(false)
  const [expandedSymbols, setExpandedSymbols] = useState(new Set())
  const [lastFetchTime, setLastFetchTime] = useState(null)
  const [intradayData, setIntradayData] = useState({}) // { symbol: data[] }
  const [intradayLoading, setIntradayLoading] = useState({}) // { symbol: boolean }
  const [refreshInterval, setRefreshInterval] = useState(propRefreshInterval || DEFAULT_REFRESH_INTERVAL)
  const [isStreaming, setIsStreaming] = useState(false)
  const [isDelayed, setIsDelayed] = useState(false)
  const [reconnectCount, setReconnectCount] = useState(0)
  const toast = useToast()

  // Refs for request management
  const eventSourceRef = useRef(null)
  const reconnectTimerRef = useRef(null)
  const symbolsRef = useRef(symbols)
  const expandedSymbolsRef = useRef(expandedSymbols)

  // Load refresh interval from settings on mount
  useEffect(() => {
    const loadInterval = async () => {
      const interval = await getLiveWatchlistInterval()
      setRefreshInterval(interval)
    }
    loadInterval()
  }, [])

  // Update if prop changes
  useEffect(() => {
    if (propRefreshInterval) {
      setRefreshInterval(propRefreshInterval)
    }
  }, [propRefreshInterval])

  // Keep refs in sync with state
  useEffect(() => {
    symbolsRef.current = symbols
  }, [symbols])

  useEffect(() => {
    expandedSymbolsRef.current = expandedSymbols
  }, [expandedSymbols])

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
    } catch {
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

  // Subscribe to symbols on the backend
  const subscribeSymbols = useCallback(async (symbolList) => {
    if (!isConnected || symbolList.length === 0) return

    try {
      await fetch(`${API_BASE}/subscribe`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ symbols: symbolList })
      })
    } catch {
    }
  }, [isConnected])

  // Unsubscribe from symbols on the backend
  const unsubscribeSymbols = useCallback(async (symbolList) => {
    try {
      await fetch(`${API_BASE}/unsubscribe`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ symbols: symbolList })
      })
    } catch {
    }
  }, [])

  // Set up SSE streaming for real-time quotes
  useEffect(() => {
    if (!isConnected || symbols.length === 0) {
      // Close existing connection if no symbols or disconnected
      if (eventSourceRef.current) {
        eventSourceRef.current.close()
        eventSourceRef.current = null
        setIsStreaming(false)
      }
      return
    }

    // Subscribe to all current symbols
    subscribeSymbols(symbols)

    // Set up SSE connection — connect directly to backend to bypass Vite proxy buffering
    if (!eventSourceRef.current) {
      const eventSource = new EventSource(`${BACKEND_URL}/api/stream-quotes`)

      eventSource.onopen = () => {
        setIsStreaming(true)
        setLoading(false) // SSE connected, no longer "connecting"
        setLastFetchTime(Date.now())
      }

      eventSource.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data)

          if (data.type === 'snapshot') {
            setQuotes(prev => ({ ...prev, ...data.quotes }))
            setLoading(false)
            if (data.delayed) setIsDelayed(true)
            else setIsDelayed(false)
          } else if (data.type === 'update') {
            setQuotes(prev => ({ ...prev, [data.symbol]: data.quote }))
            setLastFetchTime(Date.now())
            // Clear delayed state if a live quote arrives without a subscription error
            if (!data.quote?.subscriptionError) setIsDelayed(false)
          }
        } catch {
        }
      }

      eventSource.onerror = () => {
        setIsStreaming(false)
        setLoading(false)
        eventSource.close()
        eventSourceRef.current = null
        // Reconnect after 3 seconds
        reconnectTimerRef.current = setTimeout(() => {
          setReconnectCount(c => c + 1)
        }, 3000)
      }

      eventSourceRef.current = eventSource
      setLoading(true)

      // Fallback: clear loading after 4s in case onopen never fires
      const loadingTimeout = setTimeout(() => setLoading(false), 4000)
      eventSource.addEventListener('open', () => clearTimeout(loadingTimeout), { once: true })
    }

    return () => {
      // Don't close on every re-render, only on unmount
    }
  }, [isConnected, symbols, subscribeSymbols, reconnectCount])

  // Clean up SSE on unmount
  useEffect(() => {
    return () => {
      if (reconnectTimerRef.current) clearTimeout(reconnectTimerRef.current)
      if (eventSourceRef.current) {
        eventSourceRef.current.close()
        eventSourceRef.current = null
      }
    }
  }, [])

  // Refresh intraday data periodically for expanded symbols
  useEffect(() => {
    if (!isConnected || expandedSymbols.size === 0) return

    const refreshIntraday = async () => {
      const today = new Date()
      today.setHours(0, 0, 0, 0)
      const startTimestamp = today.getTime()

      for (const symbol of expandedSymbols) {
        try {
          const intradayResult = await fetchIntradayForDay(symbol, startTimestamp, '5m')
          setIntradayData(prev => ({ ...prev, [symbol]: intradayResult || [] }))
        } catch {
        }
      }
    }

    // Initial fetch
    refreshIntraday()

    // Refresh intraday every 60 seconds (charts don't need real-time updates)
    const interval = setInterval(refreshIntraday, 60000)

    return () => clearInterval(interval)
  }, [isConnected, expandedSymbols])

  // Manual refresh function (for refresh button)
  const manualRefresh = useCallback(async () => {
    if (!isConnected || symbols.length === 0) return

    setLoading(true)
    try {
      // Re-subscribe to all symbols to force refresh
      await subscribeSymbols(symbols)
    } finally {
      setLoading(false)
    }
  }, [isConnected, symbols, subscribeSymbols])

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

    // Subscribe to the new symbol for real-time updates
    if (isConnected) {
      subscribeSymbols([symbol])
    }
  }

  const handleRemoveSymbol = async (symbol) => {
    await removeFromLiveWatchlist(symbol)
    setSymbols(symbols.filter(s => s !== symbol))

    // Unsubscribe from removed symbol
    unsubscribeSymbols([symbol])

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

  if (isMarketClosed() && symbols.length > 0 && !isAdding) {
    return (
      <Box
        bg="gray.850"
        borderBottom="1px solid"
        borderColor="gray.700"
        px={4}
        py={1}
      >
        <HStack spacing={2}>
          <Text color="gray.500" fontSize="xs" fontWeight="medium">Live Watchlist</Text>
          <Badge colorScheme="gray" variant="subtle" fontSize="9px">Market Closed</Badge>
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
              isStreaming={isStreaming}
              lastFetchTime={lastFetchTime}
              refreshInterval={refreshInterval}
            />
          )}
          {isDelayed && (
            <Tooltip label="Live data unavailable — showing 15-minute delayed quotes. Check TWS Market Data Connections to enable live API data.">
              <Badge colorScheme="yellow" variant="subtle" fontSize="9px" cursor="help">15m delay</Badge>
            </Tooltip>
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
              onClick={manualRefresh}
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
