import { useMemo, useRef, useState, useEffect } from 'react'
import { Box, Spinner, Center, Text, Button, VStack, HStack } from '@chakra-ui/react'
import { DownloadIcon } from '@chakra-ui/icons'

function CandlestickChart({ data, loading, symbol, date, onFetchData, canFetch, fetching }) {
  const containerRef = useRef(null)
  const [tooltip, setTooltip] = useState(null)
  const [containerWidth, setContainerWidth] = useState(800)

  useEffect(() => {
    const updateWidth = () => {
      if (containerRef.current) {
        setContainerWidth(containerRef.current.clientWidth)
      }
    }

    updateWidth()
    window.addEventListener('resize', updateWidth)

    // Also observe container size changes
    const resizeObserver = new ResizeObserver(updateWidth)
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current)
    }

    return () => {
      window.removeEventListener('resize', updateWidth)
      resizeObserver.disconnect()
    }
  }, [data])

  const chartConfig = useMemo(() => {
    if (!data || data.length === 0) return null

    // Filter out invalid bars
    const validData = data.filter(d =>
      d &&
      typeof d.open === 'number' && !isNaN(d.open) &&
      typeof d.high === 'number' && !isNaN(d.high) &&
      typeof d.low === 'number' && !isNaN(d.low) &&
      typeof d.close === 'number' && !isNaN(d.close)
    )

    if (validData.length === 0) return null

    const sortedData = [...validData].sort((a, b) => a.timestamp - b.timestamp)

    // Calculate price range
    const prices = sortedData.flatMap(d => [d.high, d.low])
    const minPrice = Math.min(...prices)
    const maxPrice = Math.max(...prices)

    // Handle edge case where all prices are the same
    const priceRange = maxPrice - minPrice || 1
    const pricePadding = priceRange * 0.1

    // Calculate volume range
    const volumes = sortedData.map(d => d.volume || 0)
    const maxVolume = Math.max(...volumes) || 1

    // Calculate date range
    const firstDate = new Date(sortedData[0].timestamp).toLocaleDateString()
    const lastDate = new Date(sortedData[sortedData.length - 1].timestamp).toLocaleDateString()
    const dateRange = firstDate === lastDate ? firstDate : `${firstDate} - ${lastDate}`

    return {
      data: sortedData,
      minPrice: minPrice - pricePadding,
      maxPrice: maxPrice + pricePadding,
      maxVolume,
      priceRange: priceRange + pricePadding * 2,
      dateRange
    }
  }, [data])

  const formatTime = (timestamp) => {
    const date = new Date(timestamp)
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  }

  const formatPrice = (price) => {
    return price?.toFixed(2) || '-'
  }

  if (loading) {
    return (
      <Center h="300px" bg="gray.900">
        <Spinner size="lg" color="blue.400" />
      </Center>
    )
  }

  if (!data || data.length === 0 || !chartConfig) {
    return (
      <Center h="300px" bg="gray.900">
        <VStack spacing={3}>
          <Text color="gray.500">No intraday data available for this day</Text>
          {onFetchData && (
            <Button
              leftIcon={<DownloadIcon />}
              colorScheme="blue"
              size="sm"
              onClick={onFetchData}
              isLoading={fetching}
              loadingText="Fetching..."
              isDisabled={!canFetch}
            >
              Fetch 5min data from IBKR
            </Button>
          )}
          {!canFetch && (
            <Text color="gray.600" fontSize="xs">Connect to IBKR to fetch data</Text>
          )}
        </VStack>
      </Center>
    )
  }

  const width = containerWidth || 800
  const height = 300
  const padding = { top: 20, right: 60, bottom: 40, left: 10 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom
  const candleAreaHeight = chartHeight * 0.75
  const volumeAreaHeight = chartHeight * 0.2
  const volumeTop = padding.top + candleAreaHeight + chartHeight * 0.05

  const { data: sortedData, minPrice, maxPrice, priceRange, maxVolume, dateRange } = chartConfig

  const candleWidth = Math.max(2, Math.min(10, (chartWidth / sortedData.length) * 0.8))
  const candleSpacing = chartWidth / sortedData.length

  const priceToY = (price) => {
    return padding.top + candleAreaHeight - ((price - minPrice) / priceRange) * candleAreaHeight
  }

  const volumeToHeight = (volume) => {
    if (maxVolume === 0) return 0
    return (volume / maxVolume) * volumeAreaHeight
  }

  return (
    <Box>
      <HStack justify="space-between" mb={2}>
        <Text fontSize="sm" color="gray.400">
          {symbol} - {dateRange || date} (5min candles, {sortedData.length} bars)
        </Text>
        {tooltip && (
          <HStack spacing={4} fontSize="xs" color="gray.300">
            <Text>Time: {formatTime(tooltip.timestamp)}</Text>
            <Text>O: {formatPrice(tooltip.open)}</Text>
            <Text>H: {formatPrice(tooltip.high)}</Text>
            <Text>L: {formatPrice(tooltip.low)}</Text>
            <Text>C: {formatPrice(tooltip.close)}</Text>
            <Text>V: {tooltip.volume?.toLocaleString()}</Text>
          </HStack>
        )}
      </HStack>

      <Box
        ref={containerRef}
        w="100%"
        overflowX="auto"
        bg="gray.900"
        borderRadius="md"
      >
        <svg
          width="100%"
          height={height}
          viewBox={`0 0 ${width} ${height}`}
          preserveAspectRatio="xMinYMin meet"
          style={{ display: 'block' }}
        >
          {/* Background */}
          <rect x={0} y={0} width="100%" height={height} fill="#1a202c" />

          {/* Grid lines */}
          {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => {
            const y = padding.top + candleAreaHeight * ratio
            const price = maxPrice - (priceRange * ratio)
            return (
              <g key={i}>
                <line
                  x1={padding.left}
                  y1={y}
                  x2={width - padding.right}
                  y2={y}
                  stroke="#2d3748"
                  strokeWidth={1}
                />
                <text
                  x={width - padding.right + 5}
                  y={y + 4}
                  fill="#a0aec0"
                  fontSize={10}
                >
                  {price.toFixed(2)}
                </text>
              </g>
            )
          })}

          {/* Candlesticks */}
          {sortedData.map((bar, i) => {
            const x = padding.left + i * candleSpacing + candleSpacing / 2
            const isUp = bar.close >= bar.open
            const color = isUp ? '#48bb78' : '#f56565'

            const highY = priceToY(bar.high)
            const lowY = priceToY(bar.low)
            const openY = priceToY(bar.open)
            const closeY = priceToY(bar.close)

            const bodyTop = Math.min(openY, closeY)
            const bodyHeight = Math.max(1, Math.abs(closeY - openY))

            return (
              <g
                key={i}
                onMouseEnter={() => setTooltip(bar)}
                onMouseLeave={() => setTooltip(null)}
                style={{ cursor: 'crosshair' }}
              >
                {/* Wick */}
                <line
                  x1={x}
                  y1={highY}
                  x2={x}
                  y2={lowY}
                  stroke={color}
                  strokeWidth={1}
                />
                {/* Body */}
                <rect
                  x={x - candleWidth / 2}
                  y={bodyTop}
                  width={candleWidth}
                  height={bodyHeight}
                  fill={isUp ? color : color}
                  stroke={color}
                  strokeWidth={1}
                />
                {/* Hover area */}
                <rect
                  x={x - candleSpacing / 2}
                  y={padding.top}
                  width={candleSpacing}
                  height={candleAreaHeight}
                  fill="transparent"
                />
              </g>
            )
          })}

          {/* Volume bars */}
          {sortedData.map((bar, i) => {
            const x = padding.left + i * candleSpacing + candleSpacing / 2
            const isUp = bar.close >= bar.open
            const color = isUp ? '#48bb7880' : '#f5656580'
            const barHeight = volumeToHeight(bar.volume || 0)

            return (
              <rect
                key={`vol-${i}`}
                x={x - candleWidth / 2}
                y={volumeTop + volumeAreaHeight - barHeight}
                width={candleWidth}
                height={barHeight}
                fill={color}
              />
            )
          })}

          {/* Time labels */}
          {sortedData.filter((_, i) => i % Math.ceil(sortedData.length / 8) === 0).map((bar, i) => {
            const idx = sortedData.indexOf(bar)
            const x = padding.left + idx * candleSpacing + candleSpacing / 2
            return (
              <text
                key={`time-${i}`}
                x={x}
                y={height - 5}
                fill="#a0aec0"
                fontSize={10}
                textAnchor="middle"
              >
                {formatTime(bar.timestamp)}
              </text>
            )
          })}
        </svg>
      </Box>
    </Box>
  )
}

export default CandlestickChart
