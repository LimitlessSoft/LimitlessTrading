import { useMemo, useState } from 'react'
import { Box, Text, HStack, Spinner, Center, Portal, IconButton } from '@chakra-ui/react'
import { CloseIcon } from '@chakra-ui/icons'

// Trading day boundaries in minutes from midnight (Eastern Time)
const PRE_MARKET_START = 4 * 60        // 4:00 AM ET
const MARKET_OPEN_MINUTES = 9 * 60 + 30 // 9:30 AM ET
const MARKET_CLOSE_MINUTES = 16 * 60    // 4:00 PM ET

// Total trading day span in minutes (4 AM to 4 PM = 12 hours = 720 minutes)
const TRADING_DAY_MINUTES = MARKET_CLOSE_MINUTES - PRE_MARKET_START

// Convert timestamp to minutes from midnight in Eastern time
function getEasternMinutes(timestamp) {
  const date = new Date(timestamp)
  const eastern = new Date(date.toLocaleString('en-US', { timeZone: 'America/New_York' }))
  return eastern.getHours() * 60 + eastern.getMinutes()
}

// Check if a timestamp is pre-market (before 9:30 AM ET)
function isPreMarket(timestamp) {
  const minutes = getEasternMinutes(timestamp)
  return minutes < MARKET_OPEN_MINUTES
}

// Calculate VWAP from candle data
function calculateVWAP(data) {
  let cumulativeTPV = 0 // cumulative (typical price * volume)
  let cumulativeVolume = 0

  return data.map(bar => {
    const typicalPrice = (bar.high + bar.low + bar.close) / 3
    cumulativeTPV += typicalPrice * (bar.volume || 0)
    cumulativeVolume += bar.volume || 0
    return {
      timestamp: bar.timestamp,
      vwap: cumulativeVolume > 0 ? cumulativeTPV / cumulativeVolume : null
    }
  })
}

// Reusable chart SVG component
function ChartSVG({ width, height, sortedData, vwapData, minPrice, priceRange, hasData, vwapValue, isLarge }) {
  const padding = isLarge
    ? { top: 20, right: 60, bottom: 30, left: 20 }
    : { top: 5, right: 5, bottom: 15, left: 5 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom

  // Fixed time axis: map minutes from day start (4 AM) to x position
  const minutesToX = (minutes) => {
    const normalizedMinutes = minutes - PRE_MARKET_START
    return padding.left + (normalizedMinutes / TRADING_DAY_MINUTES) * chartWidth
  }

  // Convert timestamp to x position
  const timestampToX = (timestamp) => {
    const minutes = getEasternMinutes(timestamp)
    return minutesToX(minutes)
  }

  // For 5-min bars, calculate candle width based on fixed time scale
  const barIntervalMinutes = 5
  const candleWidth = Math.max(1, (barIntervalMinutes / TRADING_DAY_MINUTES) * chartWidth * (isLarge ? 0.8 : 0.7))

  const priceToY = (price) => {
    return padding.top + chartHeight - ((price - minPrice) / priceRange) * chartHeight
  }

  // Generate VWAP line path
  const vwapPath = vwapData
    .filter(v => v.vwap !== null)
    .map((v, i) => {
      const x = timestampToX(v.timestamp)
      const y = priceToY(v.vwap)
      return `${i === 0 ? 'M' : 'L'} ${x} ${y}`
    })
    .join(' ')

  // Calculate x position for market open
  const marketOpenX = minutesToX(MARKET_OPEN_MINUTES)

  // Font sizes based on chart size
  const fontSize = isLarge ? 12 : 8
  const vwapFontSize = isLarge ? 11 : 8

  return (
    <svg
      width="100%"
      height={height}
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="xMidYMid meet"
    >
      {/* Background */}
      <rect x={0} y={0} width={width} height={height} fill="#1a202c" />

      {/* Pre-market background shading (4 AM to 9:30 AM) */}
      <rect
        x={padding.left}
        y={padding.top}
        width={marketOpenX - padding.left}
        height={chartHeight}
        fill="#805ad510"
      />

      {/* Market open line (9:30 AM) */}
      <line
        x1={marketOpenX}
        y1={padding.top}
        x2={marketOpenX}
        y2={padding.top + chartHeight}
        stroke="#805ad5"
        strokeWidth={1}
        strokeDasharray="2,2"
        opacity={0.4}
      />

      {/* Price grid lines for large view */}
      {isLarge && hasData && (() => {
        const priceStep = priceRange / 4
        const lines = []
        for (let i = 1; i < 4; i++) {
          const price = minPrice + priceStep * i
          const y = priceToY(price)
          lines.push(
            <g key={`grid-${i}`}>
              <line
                x1={padding.left}
                y1={y}
                x2={width - padding.right}
                y2={y}
                stroke="#4a5568"
                strokeWidth={0.5}
                opacity={0.3}
              />
              <text
                x={width - padding.right + 5}
                y={y}
                fill="#718096"
                fontSize={10}
                dominantBaseline="middle"
              >
                {price.toFixed(2)}
              </text>
            </g>
          )
        }
        return lines
      })()}

      {/* Candlesticks */}
      {sortedData.map((bar, i) => {
        const x = timestampToX(bar.timestamp)
        const isUp = bar.close >= bar.open
        const isPre = isPreMarket(bar.timestamp)

        // Different colors for pre-market
        const upColor = isPre ? '#9f7aea' : '#48bb78'
        const downColor = isPre ? '#b794f4' : '#f56565'
        const color = isUp ? upColor : downColor

        const highY = priceToY(bar.high)
        const lowY = priceToY(bar.low)
        const openY = priceToY(bar.open)
        const closeY = priceToY(bar.close)

        const bodyTop = Math.min(openY, closeY)
        const bodyHeight = Math.max(1, Math.abs(closeY - openY))

        return (
          <g key={i}>
            {/* Wick */}
            <line
              x1={x}
              y1={highY}
              x2={x}
              y2={lowY}
              stroke={color}
              strokeWidth={isLarge ? 1 : 0.5}
              opacity={isPre ? 0.7 : 1}
            />
            {/* Body */}
            <rect
              x={x - candleWidth / 2}
              y={bodyTop}
              width={candleWidth}
              height={bodyHeight}
              fill={color}
              opacity={isPre ? 0.7 : 1}
            />
          </g>
        )
      })}

      {/* VWAP line */}
      {vwapPath && (
        <path
          d={vwapPath}
          fill="none"
          stroke="#4299e1"
          strokeWidth={isLarge ? 2 : 1.5}
          opacity={0.8}
        />
      )}

      {/* Current VWAP value label */}
      {hasData && vwapValue && (
        <text
          x={width - padding.right + (isLarge ? 5 : 0)}
          y={priceToY(vwapValue)}
          fill="#4299e1"
          fontSize={vwapFontSize}
          textAnchor={isLarge ? "start" : "end"}
          dominantBaseline="middle"
        >
          {vwapValue.toFixed(2)}
        </text>
      )}

      {/* Fixed time labels for trading day */}
      <text
        x={padding.left}
        y={height - (isLarge ? 10 : 3)}
        fill="#718096"
        fontSize={fontSize}
        textAnchor="start"
      >
        4:00
      </text>
      <text
        x={marketOpenX}
        y={height - (isLarge ? 10 : 3)}
        fill="#718096"
        fontSize={fontSize}
        textAnchor="middle"
      >
        9:30
      </text>
      <text
        x={width - padding.right}
        y={height - (isLarge ? 10 : 3)}
        fill="#718096"
        fontSize={fontSize}
        textAnchor="end"
      >
        16:00
      </text>
    </svg>
  )
}

function MiniIntradayChart({ data, loading, symbol, vwapValue }) {
  const [isFullscreen, setIsFullscreen] = useState(false)

  const chartConfig = useMemo(() => {
    const validData = (data || []).filter(d =>
      d &&
      typeof d.open === 'number' && !isNaN(d.open) &&
      typeof d.high === 'number' && !isNaN(d.high) &&
      typeof d.low === 'number' && !isNaN(d.low) &&
      typeof d.close === 'number' && !isNaN(d.close)
    )

    const sortedData = [...validData].sort((a, b) => a.timestamp - b.timestamp)

    // Calculate price range (use data if available, otherwise placeholder)
    let minPrice, maxPrice, priceRange
    if (sortedData.length > 0) {
      const prices = sortedData.flatMap(d => [d.high, d.low])
      minPrice = Math.min(...prices)
      maxPrice = Math.max(...prices)
      priceRange = maxPrice - minPrice || 1
      const pricePadding = priceRange * 0.05
      minPrice -= pricePadding
      maxPrice += pricePadding
      priceRange += pricePadding * 2
    } else {
      // No data yet - use placeholder range
      minPrice = 0
      maxPrice = 100
      priceRange = 100
    }

    // Calculate VWAP
    const vwapData = calculateVWAP(sortedData)

    return {
      data: sortedData,
      vwapData,
      minPrice,
      maxPrice,
      priceRange,
      hasData: sortedData.length > 0
    }
  }, [data])

  if (loading) {
    return (
      <Center h="100px" bg="gray.900" borderRadius="md">
        <Spinner size="sm" color="blue.400" />
      </Center>
    )
  }

  const { data: sortedData, vwapData, minPrice, priceRange, hasData } = chartConfig

  return (
    <Box>
      <HStack justify="space-between" mb={1}>
        <Text fontSize="9px" color="gray.500">
          5min • {sortedData.length} bars
        </Text>
        <HStack spacing={2} fontSize="9px">
          <HStack spacing={1}>
            <Box w="8px" h="2px" bg="blue.400" />
            <Text color="gray.500">VWAP</Text>
          </HStack>
          <HStack spacing={1}>
            <Box w="8px" h="2px" bg="purple.400" opacity={0.6} />
            <Text color="gray.500">Pre</Text>
          </HStack>
        </HStack>
      </HStack>

      <Box
        bg="gray.900"
        borderRadius="md"
        overflow="hidden"
        cursor="pointer"
        onClick={() => setIsFullscreen(true)}
        position="relative"
        _hover={{ opacity: 0.8 }}
        transition="opacity 0.15s"
      >
        <ChartSVG
          width={200}
          height={80}
          sortedData={sortedData}
          vwapData={vwapData}
          minPrice={minPrice}
          priceRange={priceRange}
          hasData={hasData}
          vwapValue={vwapValue}
          isLarge={false}
        />
      </Box>

      {/* Full screen popup */}
      {isFullscreen && (
        <Portal>
          <Box
            position="fixed"
            top={0}
            left={0}
            right={0}
            bottom={0}
            bg="blackAlpha.800"
            zIndex={9999}
            display="flex"
            alignItems="center"
            justifyContent="center"
            onClick={() => setIsFullscreen(false)}
          >
            <Box
              bg="gray.800"
              borderRadius="lg"
              p={4}
              maxW="90vw"
              maxH="90vh"
              boxShadow="dark-lg"
              position="relative"
              onClick={(e) => e.stopPropagation()}
            >
              <IconButton
                icon={<CloseIcon />}
                size="sm"
                position="absolute"
                top={2}
                right={2}
                onClick={() => setIsFullscreen(false)}
                aria-label="Close"
                variant="ghost"
                colorScheme="gray"
                _hover={{ bg: 'gray.700' }}
              />

              <HStack justify="space-between" mb={3} pr={8}>
                <Text fontSize="lg" fontWeight="bold" color="white">
                  {symbol} - Intraday Chart
                </Text>
                <HStack spacing={4} fontSize="sm">
                  <HStack spacing={1}>
                    <Box w="12px" h="3px" bg="blue.400" />
                    <Text color="gray.400">VWAP</Text>
                  </HStack>
                  <HStack spacing={1}>
                    <Box w="12px" h="3px" bg="purple.400" opacity={0.6} />
                    <Text color="gray.400">Pre-Market</Text>
                  </HStack>
                  <Text color="gray.500">
                    {sortedData.length} bars
                  </Text>
                </HStack>
              </HStack>

              <Box bg="gray.900" borderRadius="md" overflow="hidden">
                <ChartSVG
                  width={1200}
                  height={500}
                  sortedData={sortedData}
                  vwapData={vwapData}
                  minPrice={minPrice}
                  priceRange={priceRange}
                  hasData={hasData}
                  vwapValue={vwapValue}
                  isLarge={true}
                />
              </Box>
            </Box>
          </Box>
        </Portal>
      )}
    </Box>
  )
}

export default MiniIntradayChart
