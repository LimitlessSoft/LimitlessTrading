import { useRef, useState, useEffect, useCallback } from 'react'
import {
  Box,
  Text,
  IconButton,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  Button,
  FormControl,
  FormLabel,
  Input,
  Select,
  HStack,
  VStack,
  Spinner,
  Center,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, RepeatIcon } from '@chakra-ui/icons'
import { fetchFromCache, loadHistoricalData, INTERVALS } from '../modules/dataService'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 4
const MIN_HEIGHT = 100
const DEFAULT_HEIGHT = 220
const DEFAULT_WIDTH = 10

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1
}

// Candlestick SVG — renders inside the chart control
function ChartSVG({ data, width, height, symbol, interval }) {
  const [tooltip, setTooltip] = useState(null)

  const padding = { top: 12, right: 50, bottom: 28, left: 8 }
  const chartWidth  = width  - padding.left - padding.right
  const chartHeight = height - padding.top  - padding.bottom
  const candleAreaH = chartHeight * 0.78
  const volumeAreaH = chartHeight * 0.16
  const volumeTop   = padding.top + candleAreaH + chartHeight * 0.06

  const prices = data.flatMap(d => [d.high, d.low])
  const minP    = Math.min(...prices)
  const maxP    = Math.max(...prices)
  const pRange  = (maxP - minP) || 1
  const pad     = pRange * 0.08
  const effectiveMin = minP - pad
  const effectiveRange = pRange + pad * 2

  const maxVol = Math.max(...data.map(d => d.volume || 0)) || 1

  const priceToY  = p  => padding.top + candleAreaH - ((p - effectiveMin) / effectiveRange) * candleAreaH
  const volToH    = v  => (v / maxVol) * volumeAreaH
  const candleW   = Math.max(1, Math.min(8, (chartWidth / data.length) * 0.75))
  const spacing   = chartWidth / data.length

  const formatPrice = p => p?.toFixed(2) ?? '-'
  const formatTime  = ts => new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  const formatDate  = ts => new Date(ts).toLocaleDateString([], { month: 'short', day: 'numeric' })
  const isIntraday  = ['1m','2m','3m','5m','15m','30m','1h'].includes(interval)

  return (
    <Box position="relative" w="100%" h="100%">
      {tooltip && (
        <HStack
          position="absolute"
          top="2px"
          left="8px"
          spacing={3}
          fontSize="10px"
          color="gray.300"
          pointerEvents="none"
          zIndex={5}
        >
          <Text>{isIntraday ? formatTime(tooltip.timestamp) : formatDate(tooltip.timestamp)}</Text>
          <Text>O:{formatPrice(tooltip.open)}</Text>
          <Text>H:{formatPrice(tooltip.high)}</Text>
          <Text>L:{formatPrice(tooltip.low)}</Text>
          <Text color={tooltip.close >= tooltip.open ? 'green.300' : 'red.300'}>
            C:{formatPrice(tooltip.close)}
          </Text>
        </HStack>
      )}
      <svg
        width="100%"
        height={height}
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="xMinYMin meet"
        style={{ display: 'block' }}
      >
        <rect x={0} y={0} width={width} height={height} fill="#1a202c" />

        {/* Grid lines */}
        {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => {
          const y     = padding.top + candleAreaH * ratio
          const price = (effectiveMin + effectiveRange) - effectiveRange * ratio
          return (
            <g key={i}>
              <line x1={padding.left} y1={y} x2={width - padding.right} y2={y}
                stroke="#2d3748" strokeWidth={1} />
              <text x={width - padding.right + 4} y={y + 3} fill="#718096" fontSize={9}>
                {price.toFixed(2)}
              </text>
            </g>
          )
        })}

        {/* Candlesticks */}
        {data.map((bar, i) => {
          const x      = padding.left + i * spacing + spacing / 2
          const isUp   = bar.close >= bar.open
          const color  = isUp ? '#48bb78' : '#f56565'
          const highY  = priceToY(bar.high)
          const lowY   = priceToY(bar.low)
          const bodyT  = Math.min(priceToY(bar.open), priceToY(bar.close))
          const bodyH  = Math.max(1, Math.abs(priceToY(bar.close) - priceToY(bar.open)))
          const volH   = volToH(bar.volume || 0)

          return (
            <g key={i}
              onMouseEnter={() => setTooltip(bar)}
              onMouseLeave={() => setTooltip(null)}
            >
              <line x1={x} y1={highY} x2={x} y2={lowY} stroke={color} strokeWidth={1} />
              <rect x={x - candleW / 2} y={bodyT} width={candleW} height={bodyH}
                fill={color} stroke={color} strokeWidth={0.5} />
              <rect
                x={x - candleW / 2}
                y={volumeTop + volumeAreaH - volH}
                width={candleW} height={volH}
                fill={isUp ? '#48bb7860' : '#f5656560'}
              />
              {/* Wider hover area */}
              <rect x={x - spacing / 2} y={padding.top} width={spacing} height={candleAreaH}
                fill="transparent" style={{ cursor: 'crosshair' }} />
            </g>
          )
        })}

        {/* Time labels */}
        {data
          .filter((_, i) => i % Math.max(1, Math.ceil(data.length / 6)) === 0)
          .map((bar) => {
            const i = data.indexOf(bar)
            const x = padding.left + i * spacing + spacing / 2
            const label = isIntraday ? formatTime(bar.timestamp) : formatDate(bar.timestamp)
            return (
              <text key={`t-${i}`} x={x} y={height - 4} fill="#718096" fontSize={9} textAnchor="middle">
                {label}
              </text>
            )
          })}
      </svg>
    </Box>
  )
}

function TemplatePageChart({ label, layoutY, onDelete, onUpdate, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving,   setIsMoving]   = useState(false)
  const [previewPos,  setPreviewPos]  = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [data,    setData]    = useState([])
  const [loading, setLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(null)
  const [editSymbol,   setEditSymbol]   = useState('')
  const [editInterval, setEditInterval] = useState('5m')
  const [containerWidth, setContainerWidth] = useState(400)
  const boxRef = useRef(null)
  const cancelRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const symbol   = label.symbol   || ''
  const interval = label.interval || '5m'
  const width    = label.width    || DEFAULT_WIDTH
  const height   = label.height   || DEFAULT_HEIGHT
  const x        = label.x || 0
  const style    = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth  = previewSize?.width  ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX      = previewPos?.x ?? x
  const displayY      = previewPos?.y ?? layoutY ?? 0

  // Track control width for the chart SVG viewBox
  useEffect(() => {
    if (!boxRef.current) return
    const ro = new ResizeObserver(entries => {
      setContainerWidth(entries[0].contentRect.width)
    })
    ro.observe(boxRef.current)
    return () => ro.disconnect()
  }, [])

  // Load cached data when symbol or interval changes
  useEffect(() => {
    if (!symbol) { setData([]); return }
    fetchFromCache(symbol, interval).then(cached => {
      const sorted = [...(cached || [])].sort((a, b) => a.timestamp - b.timestamp)
      setData(sorted)
    })
  }, [symbol, interval])

  // Clear preview position once label catches up
  useEffect(() => {
    if (previewPos && !isMoving && label.x === previewPos.x) setPreviewPos(null)
  }, [label.x, previewPos, isMoving])

  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  const getContainerInfo = () => {
    const container = containerRef?.current || boxRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    return { width: rect.width, segmentSize: rect.width / TOTAL_SEGMENTS, rect }
  }

  const handleMoveStart = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startX = x
    const startVisualY = layoutY ?? 0
    let currentX = x
    let currentY = label.y || 0

    setIsMoving(true)

    const onMove = (mv) => {
      const dSeg = Math.round((mv.clientX - startMouseX) / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - displayWidth, startX + dSeg))
      const newVisualY = Math.max(0, startVisualY + (mv.clientY - startMouseY))
      currentX = newX
      currentY = newVisualY
      setPreviewPos({ x: newX, y: newVisualY })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsMoving(false)
      if (currentX !== x || currentY !== (label.y || 0)) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
      }
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleHorizontalResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return

    const startX = e.clientX
    const startW = width
    let currentW = width
    setIsResizing(true)

    const onMove = (mv) => {
      const dSeg = Math.round((mv.clientX - startX) / info.segmentSize)
      currentW = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startW + dSeg))
      setPreviewSize(prev => ({ height: prev?.height ?? height, width: currentW }))
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsResizing(false)
      if (currentW !== width) onUpdate(label.id, { width: currentW })
      else setPreviewSize(null)
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleVerticalResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const startY = e.clientY
    const startH = height
    let currentH = height
    setIsResizing(true)

    const onMove = (mv) => {
      currentH = Math.max(MIN_HEIGHT, startH + (mv.clientY - startY))
      setPreviewSize(prev => ({ width: prev?.width ?? width, height: currentH }))
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsResizing(false)
      if (currentH !== height) onUpdate(label.id, { height: currentH })
      else setPreviewSize(null)
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleCornerResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startW = width
    const startH = height
    let currentW = width
    let currentH = height
    setIsResizing(true)

    const onMove = (mv) => {
      const dSeg = Math.round((mv.clientX - startMouseX) / info.segmentSize)
      currentW = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startW + dSeg))
      currentH = Math.max(MIN_HEIGHT, startH + (mv.clientY - startMouseY))
      setPreviewSize({ width: currentW, height: currentH })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsResizing(false)
      if (currentW !== width || currentH !== height) {
        onUpdate(label.id, { width: currentW, height: currentH })
      } else {
        setPreviewSize(null)
      }
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleLoadData = useCallback(() => {
    if (!symbol || loading) return
    if (cancelRef.current) cancelRef.current()

    setLoading(true)
    setLoadProgress(0)

    const { cancel } = loadHistoricalData(
      symbol,
      interval,
      (partialData, progress) => {
        setLoadProgress(Math.round(progress * 100))
        const sorted = [...partialData].sort((a, b) => a.timestamp - b.timestamp)
        setData(sorted)
      },
      (finalData) => {
        const sorted = [...finalData].sort((a, b) => a.timestamp - b.timestamp)
        setData(sorted)
        setLoading(false)
        setLoadProgress(null)
        cancelRef.current = null
      },
      (err) => {
        console.error('[TemplatePageChart] load error', err)
        setLoading(false)
        setLoadProgress(null)
        cancelRef.current = null
      }
    )
    cancelRef.current = cancel
  }, [symbol, interval, loading])

  // Cancel any in-flight load on unmount
  useEffect(() => () => { cancelRef.current?.() }, [])

  const handleOpenSettings = () => {
    setEditSymbol(symbol)
    setEditInterval(interval)
    onOpen()
  }

  const handleSaveSettings = () => {
    const newSymbol = editSymbol.trim().toUpperCase()
    if (!newSymbol) return
    onUpdate(label.id, { symbol: newSymbol, interval: editInterval })
    onClose()
  }

  // Visible bars — last 300 for performance
  const visibleData = data.slice(-300)

  return (
    <>
      <Box
        ref={boxRef}
        position="absolute"
        left={`${(displayX / TOTAL_SEGMENTS) * 100}%`}
        top={`${displayY}px`}
        width={`calc(${(displayWidth / TOTAL_SEGMENTS) * 100}% - 8px)`}
        height={`${displayHeight}px`}
        bg={style.bgColor}
        borderWidth={`${style.borderWidth}px`}
        borderColor={style.borderColor}
        borderStyle="solid"
        borderRadius="md"
        overflow="hidden"
        outline={isEditing ? '1px dashed' : 'none'}
        outlineColor={isEditing ? 'gray.600' : undefined}
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        opacity={isMoving ? 0.7 : 1}
        cursor={isEditing ? (isMoving ? 'grabbing' : 'move') : 'default'}
        zIndex={isMoving ? 100 : 1}
        onMouseDown={isEditing && !isResizing ? handleMoveStart : undefined}
      >
        {/* Symbol label */}
        {symbol && (
          <Text
            position="absolute"
            top="4px"
            left="8px"
            fontSize="11px"
            fontWeight="bold"
            color="gray.400"
            pointerEvents="none"
            zIndex={3}
          >
            {symbol} · {interval}
          </Text>
        )}

        {/* Chart content */}
        {loading ? (
          <Center h="100%">
            <VStack spacing={2}>
              <Spinner size="sm" color="blue.400" />
              {loadProgress !== null && (
                <Text fontSize="xs" color="gray.500">{loadProgress}%</Text>
              )}
            </VStack>
          </Center>
        ) : !symbol ? (
          <Center h="100%">
            <Text fontSize="xs" color="gray.600">No symbol — open settings</Text>
          </Center>
        ) : visibleData.length === 0 ? (
          <Center h="100%">
            <VStack spacing={2}>
              <Text fontSize="xs" color="gray.600">No data for {symbol} ({interval})</Text>
              <Button size="xs" colorScheme="blue" leftIcon={<RepeatIcon />} onClick={handleLoadData}>
                Load from IBKR
              </Button>
            </VStack>
          </Center>
        ) : (
          <ChartSVG
            data={visibleData}
            width={containerWidth || 400}
            height={displayHeight}
            symbol={symbol}
            interval={interval}
          />
        )}

        {/* Edit mode controls */}
        {isEditing && (
          <>
            <HStack position="absolute" top="2px" right="2px" spacing={0} zIndex={10}>
              <IconButton
                icon={<RepeatIcon />}
                size="xs"
                variant="ghost"
                aria-label="Reload data"
                color="gray.400"
                _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
                onClick={(e) => { e.stopPropagation(); handleLoadData() }}
                onMouseDown={(e) => e.stopPropagation()}
                isLoading={loading}
              />
              <IconButton
                icon={<SettingsIcon />}
                size="xs"
                variant="ghost"
                aria-label="Chart settings"
                color="gray.400"
                _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
                onClick={(e) => { e.stopPropagation(); handleOpenSettings() }}
                onMouseDown={(e) => e.stopPropagation()}
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete chart"
                color="gray.400"
                _hover={{ color: 'red.400', bg: 'whiteAlpha.200' }}
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </HStack>

            {/* Right resize */}
            <Box
              position="absolute" right="-3px" top="10px" bottom="10px" width="6px"
              cursor="ew-resize" zIndex={10} borderRadius="full"
              _hover={{ bg: 'blue.400' }}
              onMouseDown={(e) => { e.stopPropagation(); handleHorizontalResize(e) }}
            />
            {/* Bottom resize */}
            <Box
              position="absolute" bottom="-3px" left="10px" right="10px" height="6px"
              cursor="ns-resize" zIndex={10} borderRadius="full"
              _hover={{ bg: 'blue.400' }}
              onMouseDown={(e) => { e.stopPropagation(); handleVerticalResize(e) }}
            />
            {/* Corner resize */}
            <Box
              position="absolute" right="-3px" bottom="-3px" width="10px" height="10px"
              cursor="nwse-resize" zIndex={11}
              _hover={{ bg: 'blue.400' }}
              onMouseDown={(e) => { e.stopPropagation(); handleCornerResize(e) }}
            />
          </>
        )}
      </Box>

      {/* Settings modal */}
      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Chart Settings</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel fontSize="sm">Symbol</FormLabel>
                <Input
                  value={editSymbol}
                  onChange={(e) => setEditSymbol(e.target.value.toUpperCase())}
                  placeholder="e.g. SPY"
                  onKeyDown={(e) => { if (e.key === 'Enter') handleSaveSettings() }}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize="sm">Interval</FormLabel>
                <Select value={editInterval} onChange={(e) => setEditInterval(e.target.value)}>
                  {INTERVALS.map(iv => (
                    <option key={iv.value} value={iv.value}>{iv.label}</option>
                  ))}
                </Select>
              </FormControl>
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
            <Button colorScheme="blue" onClick={handleSaveSettings}
              isDisabled={!editSymbol.trim()}>
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageChart
