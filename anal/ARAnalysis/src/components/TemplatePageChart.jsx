import { useRef, useState, useEffect, useLayoutEffect, useCallback, memo } from 'react'
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
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  Portal,
  Switch,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, RepeatIcon } from '@chakra-ui/icons'
import FillIcon from './FillIcon'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { fetchFromCache, loadHistoricalData, refreshRecentData, fetchOlderData, clearAllCache, cacheNeedsRefresh, INTERVALS } from '../modules/dataService'
import { subscribeQuote, subscribeSSEStatus } from '../modules/symbolQuoteService'
import {
  getGlobalChartSettings,
  saveGlobalChartSettings,
  isRTHTimestamp,
  isMarketOpen
} from '../modules/globalChartSettings'
import { LINK_GROUPS, getLinkGroupColor } from '../modules/widgetLinkService'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 4
const MIN_HEIGHT = 100
const DEFAULT_HEIGHT = 220
const DEFAULT_WIDTH = 10

const DEFAULT_MAX_BARS = 300
const MIN_BARS = 20
const MAX_BARS = 1000

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1
}

// Candlestick SVG — renders inside the chart control
const ChartSVG = memo(function ChartSVG({ data, width, height, symbol, interval, rthBgColor, outsideRthBgColor, fontSize, volumeMultiplier,
  onSymbolClick, quickInputOpen, quickSymbol, quickInputRef, onQuickSymbolChange, onQuickSymbolKeyDown, onQuickSymbolCommit,
  onIntervalChange, ohlcPosition, liveConnected, marketOpen, showMarketIndicator }) {
  const [tooltip,    setTooltip]    = useState(null)
  const [crosshairY, setCrosshairY] = useState(null)
  const [mouseX,     setMouseX]     = useState(null)

  const padding = { top: 40, right: fontSize * 4 + 4, bottom: 28, left: 8 }
  const axisGap = 6
  const chartWidth  = width  - padding.left - padding.right - axisGap
  const chartHeight = height - padding.top  - padding.bottom
  const candleAreaH = chartHeight * 0.78
  const volumeAreaH = chartHeight * 0.16
  const volumeTop   = padding.top + candleAreaH + chartHeight * 0.06

  let minP = Infinity, maxP = -Infinity, maxVol = 0
  for (const d of data) {
    if (d.high > maxP) maxP = d.high
    if (d.low  < minP) minP = d.low
    const v = Math.max(0, d.volume ?? 0)
    if (v > maxVol) maxVol = v
  }
  if (minP === Infinity) { minP = 0; maxP = 1 }
  if (maxVol === 0) maxVol = 1
  const pRange  = (maxP - minP) || 1
  const pad     = pRange * 0.08
  const effectiveMin = minP - pad
  const effectiveRange = pRange + pad * 2

  const priceToY  = p  => padding.top + candleAreaH - ((p - effectiveMin) / effectiveRange) * candleAreaH
  const volToH    = v  => Math.min(volumeAreaH, (v / maxVol) * volumeAreaH * (volumeMultiplier ?? 1))
  const spacing   = chartWidth / data.length
  const candleW   = Math.max(1, spacing * 0.6)

  const formatPrice = p => p?.toFixed(2) ?? '-'
  const formatTime  = ts => new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  const formatDate  = ts => new Date(ts).toLocaleDateString([], { month: 'short', day: 'numeric' })
  const isIntraday  = ['1m','2m','3m','5m','15m','30m','1h'].includes(interval)

  return (
    <Box position="relative" w="100%" h="100%">
      {/* Top bar: symbol + interval only */}
      <HStack
        position="absolute"
        top="4px"
        left="8px"
        right={`${padding.right + 62}px`}
        spacing={2}
        fontSize={`${fontSize + 1}px`}
        pointerEvents="none"
        zIndex={5}
      >
        {quickInputOpen ? (
          <Input
            ref={quickInputRef}
            value={quickSymbol}
            onChange={(e) => onQuickSymbolChange(e.target.value.toUpperCase())}
            onKeyDown={onQuickSymbolKeyDown}
            onBlur={onQuickSymbolCommit}
            size="xs"
            width="80px"
            fontWeight="bold"
            pointerEvents="auto"
            onMouseDown={(e) => e.stopPropagation()}
            bg="transparent"
            borderColor="blue.400"
            _focus={{ borderColor: 'blue.300', boxShadow: 'none' }}
            px={1}
          />
        ) : (
          <Text
            fontWeight="bold"
            color="gray.400"
            whiteSpace="nowrap"
            cursor="pointer"
            pointerEvents="auto"
            _hover={{ color: 'gray.200' }}
            onClick={onSymbolClick}
          >
            {symbol}
          </Text>
        )}
        <Menu isLazy>
          <MenuButton
            as={Button}
            variant="unstyled"
            fontWeight="bold"
            color="gray.600"
            fontSize={`${fontSize + 1}px`}
            h="auto"
            minW="auto"
            p={0}
            lineHeight="inherit"
            cursor="pointer"
            pointerEvents="auto"
            _hover={{ color: 'gray.400' }}
            onMouseDown={(e) => e.stopPropagation()}
          >
            · {interval}
          </MenuButton>
          <Portal>
            <MenuList bg="gray.800" borderColor="gray.600" minW="100px" zIndex={2000} fontSize="sm">
              {INTERVALS.map(iv => (
                <MenuItem
                  key={iv.value}
                  bg="gray.800"
                  _hover={{ bg: 'gray.700' }}
                  color={iv.value === interval ? 'blue.300' : 'white'}
                  fontWeight={iv.value === interval ? 'bold' : 'normal'}
                  onClick={() => onIntervalChange(iv.value)}
                >
                  {iv.label}
                </MenuItem>
              ))}
            </MenuList>
          </Portal>
        </Menu>
        {showMarketIndicator && (
          <Tooltip
            label={marketOpen ? 'Market open' : 'Market closed'}
            placement="bottom"
            hasArrow
            openDelay={300}
          >
            <Box
              w="6px"
              h="6px"
              borderRadius="full"
              bg={marketOpen ? 'green.400' : 'gray.600'}
              flexShrink={0}
              pointerEvents="auto"
            />
          </Tooltip>
        )}
      </HStack>
      <svg
        width="100%"
        height={height}
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="xMinYMin meet"
        style={{ display: 'block' }}
        onMouseMove={(e) => {
          const rect = e.currentTarget.getBoundingClientRect()
          const scaleX = width / rect.width
          const scaleY = height / rect.height
          const svgX = (e.clientX - rect.left) * scaleX
          const svgY = (e.clientY - rect.top) * scaleY
          setMouseX(svgX)
          if (svgY >= padding.top && svgY <= height - padding.bottom) {
            setCrosshairY(svgY)
          } else {
            setCrosshairY(null)
          }
        }}
        onMouseLeave={() => { setCrosshairY(null); setTooltip(null); setMouseX(null) }}
      >
        {/* Base background — full chart area including header/price label strips */}
        <rect x={0} y={0} width={width} height={height} fill="#1a202c" />

        {/* Session background — candle+volume area only, between left and right padding */}
        <rect
          x={padding.left} y={padding.top}
          width={chartWidth} height={candleAreaH + chartHeight * 0.06 + volumeAreaH}
          fill={isIntraday ? outsideRthBgColor : rthBgColor}
          rx={4} ry={4}
        />

        {/* Clip RTH bands to the rounded session area */}
        <defs>
          <clipPath id={`session-clip-${symbol}`}>
            <rect x={padding.left} y={padding.top}
              width={chartWidth} height={candleAreaH + chartHeight * 0.06 + volumeAreaH}
              rx={4} ry={4} />
          </clipPath>
        </defs>

        {/* RTH session background bands (intraday only) */}
        {isIntraday && (
          <g clipPath={`url(#session-clip-${symbol})`}>
            {data.map((bar, i) => {
              if (!isRTHTimestamp(bar.timestamp)) return null
              const bx = padding.left + i * spacing
              return (
                <rect key={`rth-${i}`} x={bx} y={padding.top} width={spacing}
                  height={candleAreaH + chartHeight * 0.06 + volumeAreaH}
                  fill={rthBgColor} />
              )
            })}
          </g>
        )}

        {/* Grid lines */}
        {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => {
          const y     = padding.top + candleAreaH * ratio
          const price = (effectiveMin + effectiveRange) - effectiveRange * ratio
          return (
            <g key={i}>
              <line x1={padding.left} y1={y} x2={padding.left + chartWidth} y2={y}
                stroke="#2d3748" strokeWidth={1} />
              <text x={width - padding.right + 4} y={y + 3} fill="#718096" fontSize={fontSize}>
                {price.toFixed(2)}
              </text>
            </g>
          )
        })}

        {/* Day separators */}
        {data.map((bar, i) => {
          if (i === 0) return null
          const prev = data[i - 1]
          if (Math.floor(prev.timestamp / 86400000) === Math.floor(bar.timestamp / 86400000)) return null
          const x = padding.left + i * spacing
          return (
            <line key={`day-${i}`}
              x1={x} y1={padding.top} x2={x} y2={height - padding.bottom}
              stroke="#4A5568" strokeWidth={1} strokeDasharray="4 4" opacity={0.6}
            />
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
          const volH   = volToH(Math.max(0, bar.volume ?? 0))

          return (
            <g key={i}
              onMouseEnter={() => setTooltip({ bar, cx: padding.left + i * spacing + spacing / 2 })}
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
        {(() => {
          const labelW = fontSize * 5.5
          const maxLabels = Math.max(2, Math.floor(chartWidth / (labelW * 1.5)))
          const labelStep = Math.max(1, Math.ceil(data.length / maxLabels))
          return data.map((bar, i) => {
            if (i % labelStep !== 0) return null
            const x = padding.left + i * spacing + spacing / 2
            const label = isIntraday ? formatTime(bar.timestamp) : formatDate(bar.timestamp)
            return (
              <text key={`t-${i}`} x={x} y={height - 4} fill="#718096" fontSize={fontSize} textAnchor="middle">
                {label}
              </text>
            )
          })
        })()}

        {/* Last price line */}
        {(() => {
          const lastBar = data[data.length - 1]
          if (!lastBar) return null
          const lastPrice = lastBar.close
          const lastY = priceToY(lastPrice)
          if (lastY < padding.top || lastY > height - padding.bottom) return null
          const isUp = lastBar.close >= lastBar.open
          const lineColor = liveConnected === false ? '#718096' : (isUp ? '#48bb78' : '#f56565')
          return (
            <g pointerEvents="none">
              <line x1={padding.left} y1={lastY} x2={padding.left + chartWidth} y2={lastY}
                stroke={lineColor} strokeWidth={1} strokeDasharray="3 3" opacity={0.6} />
              <rect x={width - padding.right} y={lastY - fontSize * 0.8}
                width={padding.right - 2} height={fontSize * 1.6}
                fill={lineColor} rx={2} />
              <text x={width - padding.right + 3} y={lastY + fontSize * 0.45}
                fill="#1a202c" fontSize={fontSize - 1} fontWeight="bold">
                {lastPrice.toFixed(2)}
              </text>
            </g>
          )
        })()}

        {/* Crosshair */}
        {tooltip && crosshairY !== null && (() => {
          const cx = tooltip.cx
          const price = effectiveMin + effectiveRange * (1 - (crosshairY - padding.top) / candleAreaH)
          return (
            <g pointerEvents="none">
              <line x1={cx} y1={padding.top} x2={cx} y2={height - padding.bottom}
                stroke="#718096" strokeWidth={1} strokeDasharray="3 3" opacity={0.7} />
              <line x1={padding.left} y1={crosshairY} x2={padding.left + chartWidth} y2={crosshairY}
                stroke="#718096" strokeWidth={1} strokeDasharray="3 3" opacity={0.7} />
              <rect x={width - padding.right} y={crosshairY - fontSize * 0.8}
                width={padding.right - 2} height={fontSize * 1.6}
                fill="#4A5568" rx={2} />
              <text x={width - padding.right + 3} y={crosshairY + fontSize * 0.45}
                fill="#e2e8f0" fontSize={fontSize - 1}>
                {price > 0 ? price.toFixed(2) : ''}
              </text>
            </g>
          )
        })()}

        {/* OHLC tooltip — floating near cursor or fixed top-left */}
        {tooltip && (() => {
          const bar = tooltip.bar
          const isUp = bar.close >= bar.open
          const lines = [
            { text: isIntraday ? formatTime(bar.timestamp) : formatDate(bar.timestamp), color: '#a0aec0' },
            { text: `O:${formatPrice(bar.open)}`, color: '#a0aec0' },
            { text: `H:${formatPrice(bar.high)}`, color: '#a0aec0' },
            { text: `L:${formatPrice(bar.low)}`, color: '#a0aec0' },
            { text: `C:${formatPrice(bar.close)}`, color: isUp ? '#68d391' : '#fc8181' },
          ]
          const ttW = fontSize * 5.5
          const ttH = lines.length * (fontSize + 3) + 8

          if (ohlcPosition === 'corner') {
            const ttX = padding.left + 2
            const ttY = padding.top + 2
            return (
              <g pointerEvents="none">
                <rect x={ttX} y={ttY} width={ttW} height={ttH} fill="#2d3748" stroke="#4A5568" strokeWidth={1} rx={3} opacity={0.88} />
                {lines.map((line, i) => (
                  <text key={i} x={ttX + 6} y={ttY + 4 + (i + 1) * (fontSize + 3) - 2}
                    fill={line.color} fontSize={fontSize}>
                    {line.text}
                  </text>
                ))}
              </g>
            )
          }

          if (mouseX === null || crosshairY === null) return null
          const offset = 12
          const flipLeft = mouseX + offset + ttW > width - padding.right
          const ttX = flipLeft ? mouseX - offset - ttW : mouseX + offset
          const ttY = Math.min(Math.max(crosshairY - ttH / 2, padding.top), height - padding.bottom - ttH)
          return (
            <g pointerEvents="none">
              <rect x={ttX} y={ttY} width={ttW} height={ttH} fill="#2d3748" stroke="#4A5568" strokeWidth={1} rx={3} opacity={0.92} />
              {lines.map((line, i) => (
                <text key={i} x={ttX + 6} y={ttY + 4 + (i + 1) * (fontSize + 3) - 2}
                  fill={line.color} fontSize={fontSize}>
                  {line.text}
                </text>
              ))}
            </g>
          )
        })()}
      </svg>
    </Box>
  )
})

function TemplatePageChart({ label, layoutY, onDelete, onUpdate, onLinkSync, onExport, onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving,   setIsMoving]   = useState(false)
  const [isPanning,  setIsPanning]  = useState(false)
  const [previewPos,  setPreviewPos]  = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [data,    setData]    = useState([])
  const [loading, setLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(null)
  const [editSymbol,     setEditSymbol]     = useState('')
  const [editInterval,   setEditInterval]   = useState('5m')
  const [globalSettings, setGlobalSettings] = useState(getGlobalChartSettings)
  const [editGlobal,      setEditGlobal]      = useState(null)
  const [editOutsideRTH,  setEditOutsideRTH]  = useState('global')
  const [fontSizeDraft,   setFontSizeDraft]   = useState('')
  const [volMultDraft,    setVolMultDraft]    = useState('')
  const [containerWidth, setContainerWidth] = useState(400)
  const [viewOffset,      setViewOffset]      = useState(0)
  const [quickSymbol,     setQuickSymbol]     = useState('')
  const [quickInputOpen,  setQuickInputOpen]  = useState(false)
  const [maxBars,         setMaxBars]         = useState(DEFAULT_MAX_BARS)
  const [isFetchingOlder, setIsFetchingOlder] = useState(false)
  const [hasMoreHistory,  setHasMoreHistory]  = useState(true)
  const [liveConnected,   setLiveConnected]   = useState(true)
  const [marketOpen,      setMarketOpen]      = useState(isMarketOpen)
  const quickInputRef   = useRef(null)
  const boxRef          = useRef(null)
  const cancelRef       = useRef(null)
  const olderCancelRef  = useRef(null)
  const isHoveredRef    = useRef(false)
  const loadGenRef      = useRef(0)
  const globalSettingsRef = useRef(globalSettings)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()

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
  useLayoutEffect(() => {
    if (!boxRef.current) return
    setContainerWidth(boxRef.current.getBoundingClientRect().width)
    const ro = new ResizeObserver(entries => {
      setContainerWidth(entries[0].contentRect.width)
    })
    ro.observe(boxRef.current)
    return () => ro.disconnect()
  }, [])

  // Load cached data when symbol or interval changes; auto-fetch from IBKR if cache empty
  useEffect(() => {
    if (!symbol) { setData([]); return }
    if (cancelRef.current) { cancelRef.current(); cancelRef.current = null }
    if (olderCancelRef.current) { olderCancelRef.current(); olderCancelRef.current = null }
    const gen = ++loadGenRef.current
    setLoading(true)
    setViewOffset(0)
    setIsFetchingOlder(false)
    setHasMoreHistory(true)
    fetchFromCache(symbol, interval).then(cached => {
      if (gen !== loadGenRef.current) return
      const sorted = [...(cached || [])].sort((a, b) => a.timestamp - b.timestamp)
      if (sorted.length === 0) {
        setLoadProgress(0)
        const { cancel } = loadHistoricalData(
          symbol,
          interval,
          (partialData, progress) => {
            if (gen !== loadGenRef.current) return
            setLoadProgress(progress)
            setData([...partialData].sort((a, b) => a.timestamp - b.timestamp))
          },
          (finalData) => {
            if (gen !== loadGenRef.current) return
            setData([...finalData].sort((a, b) => a.timestamp - b.timestamp))
            setLoading(false)
            setLoadProgress(null)
            cancelRef.current = null
          },
          (err) => {
            if (gen !== loadGenRef.current) return
            setLoading(false)
            setLoadProgress(null)
            cancelRef.current = null
          }
        )
        cancelRef.current = cancel
      } else {
        setData(sorted)
        setLoading(false)
        if (cacheNeedsRefresh(cached)) {
          refreshRecentData(symbol, interval)
            .then(freshData => {
              if (gen !== loadGenRef.current) return
              setData([...freshData].sort((a, b) => a.timestamp - b.timestamp))
            })
            .catch(() => {})
        }
      }
    })
  }, [symbol, interval])

  const INTRADAY_INTERVALS = ['1m','2m','3m','5m','15m','30m','1h']

  // SSE live price: update current bar's close/high/low in real time
  useEffect(() => {
    if (!symbol || !INTRADAY_INTERVALS.includes(interval)) return
    const unsub = subscribeQuote(symbol, 1, (_sym, quote) => {
      const livePrice = quote.last
      if (!livePrice) return
      setData(prev => {
        if (!prev.length) return prev
        const last = prev[prev.length - 1]
        if (last.close === livePrice && livePrice <= last.high && livePrice >= last.low) return prev
        return [...prev.slice(0, -1), {
          ...last,
          close: livePrice,
          high: Math.max(last.high, livePrice),
          low: Math.min(last.low, livePrice),
        }]
      })
    })
    return unsub
  }, [symbol, interval])

  // Track SSE connection state for live-price indicator
  useEffect(() => {
    if (!INTRADAY_INTERVALS.includes(interval)) { setLiveConnected(true); return }
    return subscribeSSEStatus(setLiveConnected)
  }, [interval])

  // Refresh market-open indicator every minute
  useEffect(() => {
    const timer = setInterval(() => setMarketOpen(isMarketOpen()), 60000)
    return () => clearInterval(timer)
  }, [])

  // Periodic fetch to pull in newly completed bars (every 60s)
  useEffect(() => {
    if (!symbol || !INTRADAY_INTERVALS.includes(interval)) return
    const gen = loadGenRef.current
    const timer = setInterval(() => {
      if (loadGenRef.current !== gen) return
      refreshRecentData(symbol, interval, 1)
        .then(fresh => {
          if (loadGenRef.current !== gen) return
          setData([...fresh].sort((a, b) => a.timestamp - b.timestamp))
        })
        .catch(() => {})
    }, 60000)
    return () => clearInterval(timer)
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

  const findPanelAtPosition = (mouseX, mouseY) => {
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const panelId = panel.getAttribute('data-panel-id')
      const rect = panel.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) {
        return panelId
      }
    }
    return null
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
    let lastMouseX = e.clientX
    let lastMouseY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const onMove = (mv) => {
      const dSeg = Math.round((mv.clientX - startMouseX) / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - displayWidth, startX + dSeg))
      const newVisualY = Math.max(0, startVisualY + (mv.clientY - startMouseY))
      currentX = newX
      currentY = newVisualY
      lastMouseX = mv.clientX
      lastMouseY = mv.clientY
      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
      onDragStateChange?.(true, { x: mv.clientX, y: mv.clientY })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsMoving(false)
      onDragStateChange?.(false, null)

      const targetPanelId = findPanelAtPosition(lastMouseX, lastMouseY)

      if (targetPanelId && targetPanelId !== parentPanelId) {
        onMoveWidget?.(label.id, parentPanelId, targetPanelId)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (targetPanelId === null && parentPanelId !== null) {
        const mainContainer = containerRef?.current
        if (mainContainer) {
          const rect = mainContainer.getBoundingClientRect()
          if (lastMouseX >= rect.left && lastMouseX <= rect.right && lastMouseY >= rect.top && lastMouseY <= rect.bottom) {
            onMoveWidget?.(label.id, parentPanelId, null)
            setPreviewPos(null)
            onDragPreview?.(null)
            return
          }
        }
        if (currentX !== x || currentY !== (label.y || 0)) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
      } else if (currentX !== x || currentY !== (label.y || 0)) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
        onDragPreview?.(null)
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
    if (cancelRef.current) { cancelRef.current(); cancelRef.current = null }
    const gen = ++loadGenRef.current

    setLoading(true)
    setLoadProgress(null)

    refreshRecentData(symbol, interval)
      .then(finalData => {
        if (gen !== loadGenRef.current) return
        setData([...finalData].sort((a, b) => a.timestamp - b.timestamp))
      })
      .catch(() => {})
      .finally(() => {
        if (gen !== loadGenRef.current) return
        setLoading(false)
      })
  }, [symbol, interval, loading])

  const handleFullLoad = useCallback(() => {
    if (!symbol || loading) return
    if (cancelRef.current) { cancelRef.current(); cancelRef.current = null }
    const gen = ++loadGenRef.current

    setLoading(true)
    setLoadProgress(0)

    const { cancel } = loadHistoricalData(
      symbol,
      interval,
      (partialData, progress) => {
        if (gen !== loadGenRef.current) return
        setLoadProgress(progress)
        setData([...partialData].sort((a, b) => a.timestamp - b.timestamp))
      },
      (finalData) => {
        if (gen !== loadGenRef.current) return
        setData([...finalData].sort((a, b) => a.timestamp - b.timestamp))
        setLoading(false)
        setLoadProgress(null)
        cancelRef.current = null
      },
      (err) => {
        if (gen !== loadGenRef.current) return
        setLoading(false)
        setLoadProgress(null)
        cancelRef.current = null
      }
    )
    cancelRef.current = cancel
  }, [symbol, interval, loading])

  // Cancel any in-flight load on unmount
  useEffect(() => () => { cancelRef.current?.(); olderCancelRef.current?.() }, [])

  // Lazy-load older history when user pans near the oldest cached bar
  const PREFETCH_THRESHOLD = 150
  useEffect(() => {
    if (!data.length || isFetchingOlder || !hasMoreHistory || loading) return
    const clampedOff = Math.min(viewOffset, Math.max(0, data.length - 10))
    const startIdx = Math.max(0, data.length - clampedOff - maxBars)
    if (startIdx >= PREFETCH_THRESHOLD) return

    const oldestTimestamp = data[0].timestamp
    setIsFetchingOlder(true)
    const { cancel } = fetchOlderData(
      symbol,
      interval,
      oldestTimestamp,
      (updatedData) => { setData(updatedData) },
      (finalData, hadData) => {
        setData(finalData)
        setIsFetchingOlder(false)
        if (!hadData) setHasMoreHistory(false)
        olderCancelRef.current = null
      },
      () => {
        setIsFetchingOlder(false)
        olderCancelRef.current = null
      }
    )
    olderCancelRef.current = cancel
  }, [viewOffset, maxBars, data.length, isFetchingOlder, hasMoreHistory, loading, symbol, interval])

  const handlePanStart = (e) => {
    if (e.button !== 0) return
    e.preventDefault()
    const startX = e.clientX
    const startOffset = viewOffset
    const pixelsPerBar = (containerWidth || 400) / maxBars
    setIsPanning(true)

    const onMove = (mv) => {
      const dir = globalSettingsRef.current.invertDrag ? -1 : 1
      const bars = Math.round((mv.clientX - startX) / pixelsPerBar) * dir
      setViewOffset(Math.max(0, startOffset - bars))
    }
    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsPanning(false)
    }
    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  // Sync global chart settings from other chart instances
  useEffect(() => {
    const handler = (e) => { setGlobalSettings(e.detail); globalSettingsRef.current = e.detail }
    window.addEventListener('globalChartSettingsChanged', handler)
    return () => window.removeEventListener('globalChartSettingsChanged', handler)
  }, [])

  useEffect(() => { globalSettingsRef.current = globalSettings }, [globalSettings])

  // Wheel: zoom or pan depending on shiftScrollZoom setting
  // shiftScrollZoom=true (default): Ctrl+scroll=zoom, Shift+scroll=pan, plain=passthrough
  // shiftScrollZoom=false: plain scroll=zoom, Ctrl+scroll=pan
  useEffect(() => {
    const el = boxRef.current
    if (!el) return
    const handler = (e) => {
      const s = globalSettingsRef.current
      if (s.shiftScrollZoom) {
        if (!e.shiftKey && !e.ctrlKey) return
        e.preventDefault()
        if (e.ctrlKey) {
          setMaxBars(prev => {
            const factor = e.deltaY > 0 ? 1.15 : 1 / 1.15
            return Math.round(Math.min(MAX_BARS, Math.max(MIN_BARS, prev * factor)))
          })
        } else if (e.shiftKey) {
          const dir = s.invertScroll ? -1 : 1
          const step = Math.max(5, Math.round(DEFAULT_MAX_BARS * 0.08))
          setViewOffset(prev => Math.max(0, prev + (e.deltaY > 0 ? -dir * step : dir * step)))
        }
      } else {
        e.preventDefault()
        if (e.ctrlKey) {
          const dir = s.invertScroll ? -1 : 1
          const step = Math.max(5, Math.round(DEFAULT_MAX_BARS * 0.08))
          setViewOffset(prev => Math.max(0, prev + (e.deltaY > 0 ? -dir * step : dir * step)))
        } else {
          setMaxBars(prev => {
            const factor = e.deltaY > 0 ? 1.15 : 1 / 1.15
            return Math.round(Math.min(MAX_BARS, Math.max(MIN_BARS, prev * factor)))
          })
        }
      }
    }
    el.addEventListener('wheel', handler, { passive: false })
    return () => el.removeEventListener('wheel', handler)
  }, [])

  // End key: jump to most-recent data (rightmost) when hovering this chart
  useEffect(() => {
    const el = boxRef.current
    if (!el) return
    const onEnter = () => { isHoveredRef.current = true }
    const onLeave = () => { isHoveredRef.current = false }
    const onKeyDown = (e) => {
      if (!isHoveredRef.current) return
      if (e.key === 'End') { e.preventDefault(); setViewOffset(0) }
    }
    el.addEventListener('mouseenter', onEnter)
    el.addEventListener('mouseleave', onLeave)
    window.addEventListener('keydown', onKeyDown)
    return () => {
      el.removeEventListener('mouseenter', onEnter)
      el.removeEventListener('mouseleave', onLeave)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [])

  const handleOpenSettings = () => {
    setEditSymbol(symbol)
    setEditInterval(interval)
    setEditOutsideRTH(label.outsideRTH ?? 'global')
    setEditGlobal({ ...globalSettings })
    setFontSizeDraft(String(globalSettings.fontSize ?? 9))
    setVolMultDraft(String(globalSettings.volumeMultiplier ?? 1))
    onOpen()
  }

  const handleSymbolLabelClick = (e) => {
    e.stopPropagation()
    setQuickSymbol(symbol)
    setQuickInputOpen(true)
    setTimeout(() => quickInputRef.current?.select(), 0)
  }

  const handleQuickSymbolCommit = () => {
    const s = quickSymbol.trim().toUpperCase()
    if (s && s !== symbol) {
      if (label.linkGroup && onLinkSync) {
        onLinkSync(label.linkGroup, { symbol: s }, label.id)
      } else {
        onUpdate(label.id, { symbol: s })
      }
    }
    setQuickInputOpen(false)
  }

  const handleQuickSymbolKeyDown = (e) => {
    if (e.key === 'Enter') handleQuickSymbolCommit()
    if (e.key === 'Escape') setQuickInputOpen(false)
  }

  const handleIntervalChange = (newInterval) => {
    onUpdate(label.id, { interval: newInterval })
  }

  const handleSaveSettings = () => {
    const newSymbol = editSymbol.trim().toUpperCase()
    if (!newSymbol) return
    if (label.linkGroup && onLinkSync && newSymbol !== symbol) {
      onLinkSync(label.linkGroup, { symbol: newSymbol }, label.id, { interval: editInterval, outsideRTH: editOutsideRTH })
    } else {
      onUpdate(label.id, { symbol: newSymbol, interval: editInterval, outsideRTH: editOutsideRTH })
    }
    if (editGlobal) {
      saveGlobalChartSettings(editGlobal)
      setGlobalSettings(editGlobal)
    }
    onClose()
  }

  // Visible bars — 300-bar window, shifted by viewOffset from the end
  const clampedOffset = Math.min(viewOffset, Math.max(0, data.length - 10))
  const endIdx   = data.length - clampedOffset
  const startIdx = Math.max(0, endIdx - maxBars)
  const visibleData = data.slice(startIdx, endIdx)

  const resolvedOutsideRTH = (label.outsideRTH == null || label.outsideRTH === 'global')
    ? (globalSettings.outsideRTH ?? true)
    : label.outsideRTH
  const displayData = resolvedOutsideRTH ? visibleData : visibleData.filter(b => isRTHTimestamp(b.timestamp))

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
        cursor={isEditing ? (isMoving ? 'grabbing' : 'move') : (isPanning ? 'grabbing' : 'grab')}
        zIndex={isMoving ? 100 : 1}
        onMouseDown={isEditing && !isResizing ? handleMoveStart : (!isEditing && displayData.length > 0 ? handlePanStart : undefined)}
      >
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
        ) : displayData.length === 0 ? (
          <Center h="100%">
            <VStack spacing={2}>
              <Text fontSize="xs" color="gray.600">No data for {symbol} ({interval})</Text>
              <Button size="xs" colorScheme="blue" leftIcon={<RepeatIcon />} onClick={handleLoadData}>
                Load from IBKR
              </Button>
            </VStack>
          </Center>
        ) : (
          <>
            {isFetchingOlder && (
              <Box position="absolute" bottom="28px" left="10px" zIndex={6} pointerEvents="none">
                <Spinner size="xs" color="gray.600" />
              </Box>
            )}
          <ChartSVG
            data={displayData}
            width={containerWidth || 400}
            height={displayHeight}
            symbol={symbol}
            interval={interval}
            rthBgColor={globalSettings.rthBgColor}
            outsideRthBgColor={globalSettings.outsideRthBgColor}
            fontSize={globalSettings.fontSize ?? 9}
            volumeMultiplier={globalSettings.volumeMultiplier ?? 1}
            ohlcPosition={globalSettings.ohlcPosition ?? 'mouse'}
            liveConnected={liveConnected}
            marketOpen={marketOpen}
            showMarketIndicator={globalSettings.showMarketIndicator ?? true}
            onSymbolClick={handleSymbolLabelClick}
            quickInputOpen={quickInputOpen}
            quickSymbol={quickSymbol}
            quickInputRef={quickInputRef}
            onQuickSymbolChange={(v) => setQuickSymbol(v)}
            onQuickSymbolKeyDown={handleQuickSymbolKeyDown}
            onQuickSymbolCommit={handleQuickSymbolCommit}
            onIntervalChange={handleIntervalChange}
          />
          </>
        )}

        {/* Edit mode controls */}
        {/* Settings always visible */}
        <HStack position="absolute" top="2px" right="2px" spacing={0} zIndex={10}>
          <Menu isLazy>
            <Tooltip label={label.linkGroup ? `Linked — Group ${label.linkGroup}` : 'Link group'} placement="top" hasArrow>
              <MenuButton
                as={IconButton}
                size="xs"
                variant="ghost"
                aria-label="Link group"
                onMouseDown={(e) => e.stopPropagation()}
                icon={
                  <Box
                    w="10px" h="10px" borderRadius="full"
                    bg={getLinkGroupColor(label.linkGroup) ?? 'transparent'}
                    border="1.5px solid"
                    borderColor={getLinkGroupColor(label.linkGroup) ?? 'gray.500'}
                  />
                }
                _hover={{ bg: 'whiteAlpha.200' }}
              />
            </Tooltip>
            <MenuList bg="gray.800" borderColor="gray.700" minW="120px" fontSize="sm" zIndex={200}>
              {Object.entries(LINK_GROUPS).map(([id, group]) => (
                <MenuItem
                  key={id}
                  bg="gray.800"
                  _hover={{ bg: 'gray.700' }}
                  onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { linkGroup: Number(id) }) }}
                  icon={<Box w="10px" h="10px" borderRadius="full" bg={group.color} flexShrink={0} />}
                >
                  Group {id}
                </MenuItem>
              ))}
              {label.linkGroup != null && (
                <MenuItem
                  bg="gray.800"
                  _hover={{ bg: 'gray.700' }}
                  onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { linkGroup: null }) }}
                >
                  Unlink
                </MenuItem>
              )}
            </MenuList>
          </Menu>
          {symbol && (
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
          )}
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
          {isEditing && (
            <>
              <Tooltip label={label.dock === 'fill' ? 'Remove fill' : 'Fill parent'} placement="top" hasArrow>
                <IconButton
                  icon={<FillIcon />}
                  size="xs"
                  variant="ghost"
                  aria-label="Fill parent"
                  color={label.dock === 'fill' ? 'blue.300' : 'gray.400'}
                  _hover={{ color: label.dock === 'fill' ? 'blue.200' : 'white', bg: 'whiteAlpha.200' }}
                  onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { dock: label.dock === 'fill' ? undefined : 'fill' }) }}
                  onMouseDown={(e) => e.stopPropagation()}
                />
              </Tooltip>
              <Tooltip label="Export chart" placement="top" hasArrow>
                <IconButton
                  icon={<DownloadIcon />}
                  size="xs"
                  variant="ghost"
                  aria-label="Export chart"
                  color="gray.400"
                  _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
                  onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                  onMouseDown={(e) => e.stopPropagation()}
                />
              </Tooltip>
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete chart"
                color="gray.400"
                _hover={{ color: 'red.400', bg: 'whiteAlpha.200' }}
                onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </>
          )}
        </HStack>

        {isEditing && (
          <>

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
              <FormControl>
                <HStack justify="space-between">
                  <FormLabel fontSize="sm" mb={0}>Outside RTH</FormLabel>
                  <HStack spacing={0} borderRadius="md" overflow="hidden" border="1px solid" borderColor="gray.600">
                    {[['global', 'Global settings'], [true, 'True'], [false, 'False']].map(([val, optLabel]) => (
                      <Button
                        key={String(val)}
                        size="xs"
                        variant="unstyled"
                        px={3}
                        py={1}
                        h="auto"
                        borderRadius={0}
                        bg={editOutsideRTH === val ? (val === 'global' ? 'gray.600' : 'blue.600') : 'transparent'}
                        color={editOutsideRTH === val ? 'white' : 'gray.400'}
                        _hover={{ color: 'white' }}
                        onClick={() => setEditOutsideRTH(val)}
                      >
                        {optLabel}
                      </Button>
                    ))}
                  </HStack>
                </HStack>
              </FormControl>

              {/* Global chart settings */}
              {editGlobal && (
                <Box w="100%" pt={2} borderTop="1px solid" borderColor="gray.600">
                  <Text fontSize="xs" color="gray.400" fontWeight="semibold" mb={3} textTransform="uppercase" letterSpacing="wider">
                    Global Chart Settings
                  </Text>
                  <VStack spacing={3} align="stretch">
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>RTH Background</FormLabel>
                        <HStack spacing={2}>
                          <Box
                            as="input"
                            type="color"
                            value={editGlobal.rthBgColor}
                            onChange={(e) => setEditGlobal(g => ({ ...g, rthBgColor: e.target.value }))}
                            w="36px"
                            h="28px"
                            borderRadius="sm"
                            cursor="pointer"
                            border="none"
                            bg="transparent"
                            p={0}
                          />
                          <Button size="xs" variant="ghost" color="gray.500"
                            onClick={() => setEditGlobal(g => ({ ...g, rthBgColor: globalSettings.rthBgColor }))}>
                            Reset
                          </Button>
                        </HStack>
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Outside RTH Background</FormLabel>
                        <HStack spacing={2}>
                          <Box
                            as="input"
                            type="color"
                            value={editGlobal.outsideRthBgColor}
                            onChange={(e) => setEditGlobal(g => ({ ...g, outsideRthBgColor: e.target.value }))}
                            w="36px"
                            h="28px"
                            borderRadius="sm"
                            cursor="pointer"
                            border="none"
                            bg="transparent"
                            p={0}
                          />
                          <Button size="xs" variant="ghost" color="gray.500"
                            onClick={() => setEditGlobal(g => ({ ...g, outsideRthBgColor: globalSettings.outsideRthBgColor }))}>
                            Reset
                          </Button>
                        </HStack>
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Font Size</FormLabel>
                        <HStack spacing={2}>
                          <Box
                            as="input"
                            type="number"
                            min={6}
                            max={16}
                            value={fontSizeDraft}
                            onChange={(e) => setFontSizeDraft(e.target.value)}
                            onBlur={(e) => {
                              const v = Math.max(6, Math.min(16, Number(e.target.value) || 9))
                              setFontSizeDraft(String(v))
                              setEditGlobal(g => ({ ...g, fontSize: v }))
                            }}
                            style={{ width: '52px', background: '#2d3748', color: 'white', border: '1px solid #4a5568', borderRadius: '4px', padding: '2px 6px', fontSize: '13px' }}
                          />
                          <Button size="xs" variant="ghost" color="gray.500"
                            onClick={() => { const v = globalSettings.fontSize ?? 9; setFontSizeDraft(String(v)); setEditGlobal(g => ({ ...g, fontSize: v })) }}>
                            Reset
                          </Button>
                        </HStack>
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Volume Multiplier</FormLabel>
                        <HStack spacing={2}>
                          <Box
                            as="input"
                            type="number"
                            min={0.1}
                            max={20}
                            step={0.1}
                            value={volMultDraft}
                            onChange={(e) => setVolMultDraft(e.target.value)}
                            onBlur={(e) => {
                              const v = Math.max(0.1, Math.min(20, Number(e.target.value) || 1))
                              setVolMultDraft(String(v))
                              setEditGlobal(g => ({ ...g, volumeMultiplier: v }))
                            }}
                            style={{ width: '52px', background: '#2d3748', color: 'white', border: '1px solid #4a5568', borderRadius: '4px', padding: '2px 6px', fontSize: '13px' }}
                          />
                          <Button size="xs" variant="ghost" color="gray.500"
                            onClick={() => { setVolMultDraft('1'); setEditGlobal(g => ({ ...g, volumeMultiplier: 1 })) }}>
                            Reset
                          </Button>
                        </HStack>
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Market Indicator</FormLabel>
                        <Switch
                          isChecked={editGlobal.showMarketIndicator ?? true}
                          onChange={(e) => setEditGlobal(g => ({ ...g, showMarketIndicator: e.target.checked }))}
                          colorScheme="blue"
                        />
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Outside RTH</FormLabel>
                        <Switch
                          isChecked={editGlobal.outsideRTH ?? true}
                          onChange={(e) => setEditGlobal(g => ({ ...g, outsideRTH: e.target.checked }))}
                          colorScheme="blue"
                        />
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Invert Scroll</FormLabel>
                        <Switch
                          isChecked={editGlobal.invertScroll ?? false}
                          onChange={(e) => setEditGlobal(g => ({ ...g, invertScroll: e.target.checked }))}
                          colorScheme="blue"
                        />
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Ctrl+Scroll to Zoom</FormLabel>
                        <Switch
                          isChecked={editGlobal.shiftScrollZoom ?? true}
                          onChange={(e) => setEditGlobal(g => ({ ...g, shiftScrollZoom: e.target.checked }))}
                          colorScheme="blue"
                        />
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>Invert Drag</FormLabel>
                        <Switch
                          isChecked={editGlobal.invertDrag ?? false}
                          onChange={(e) => setEditGlobal(g => ({ ...g, invertDrag: e.target.checked }))}
                          colorScheme="blue"
                        />
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <HStack justify="space-between">
                        <FormLabel fontSize="sm" mb={0}>OHLC Tooltip Position</FormLabel>
                        <HStack spacing={0} borderRadius="md" overflow="hidden" border="1px solid" borderColor="gray.600">
                          {['mouse', 'corner'].map(opt => (
                            <Button
                              key={opt}
                              size="xs"
                              variant="unstyled"
                              px={3}
                              py={1}
                              h="auto"
                              borderRadius={0}
                              bg={(editGlobal.ohlcPosition ?? 'mouse') === opt ? 'blue.600' : 'transparent'}
                              color={(editGlobal.ohlcPosition ?? 'mouse') === opt ? 'white' : 'gray.400'}
                              _hover={{ color: 'white' }}
                              onClick={() => setEditGlobal(g => ({ ...g, ohlcPosition: opt }))}
                            >
                              {opt === 'mouse' ? 'Mouse' : 'Corner'}
                            </Button>
                          ))}
                        </HStack>
                      </HStack>
                    </FormControl>
                  </VStack>
                </Box>
              )}
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
      { confirmDeleteDialog }
    </>
  )
}

export default TemplatePageChart
