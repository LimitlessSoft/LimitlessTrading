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
  HStack,
  VStack,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
import { getConnectionStatus } from '../modules/dataService'
import { useSymbolQuotes } from '../hooks/useSymbolQuotes'
import { getExchangeTime, getExchangeStatus } from '../modules/exchangeService'
import WidgetTooltip from './WidgetTooltip'
import WidgetTooltipEditor from './WidgetTooltipEditor'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2
const MIN_HEIGHT = 60
const DEFAULT_HEIGHT = 160
const DEFAULT_WIDTH = 6

const DEFAULT_SEGMENTS = [
  { from: 0, to: 25, color: '#48BB78' },
  { from: 25, to: 50, color: '#ECC94B' },
  { from: 50, to: 75, color: '#ED8936' },
  { from: 75, to: 100, color: '#F56565' }
]

const DEFAULT_STYLE = {
  bgColor: 'transparent',
  borderColor: '#718096',
  borderWidth: 0
}

// Gauge arc spans -135° to +135° (270° total), same as FearGreedGauge
const ARC_START = -135
const ARC_END = 135
const ARC_TOTAL = ARC_END - ARC_START

// Arc spans from bottom-left (-135°) through top (0°) to bottom-right (+135°).
// Arc top sits at cy - r; endpoints sit at cy + r*sin(45°) = cy + r*0.707.
// viewBox must satisfy: cy - r >= padding and cy + r*0.707 + labelRoom <= SVG_H.
const SVG_W = 200
const SVG_H = 160
const CX = 100
const CY = 90   // top of arc = 90 - 78 = 12, endpoints y ≈ 145
const RADIUS = 78
const TRACK_W = 12

function createArc(cx, cy, r, startDeg, endDeg) {
  const startRad = (startDeg - 90) * Math.PI / 180
  const endRad = (endDeg - 90) * Math.PI / 180
  const x1 = cx + r * Math.cos(startRad)
  const y1 = cy + r * Math.sin(startRad)
  const x2 = cx + r * Math.cos(endRad)
  const y2 = cy + r * Math.sin(endRad)
  const largeArc = endDeg - startDeg > 180 ? 1 : 0
  return `M ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2}`
}

function GaugeSVG({ value, min, max, segments, title }) {
  const cx = CX
  const cy = CY
  const radius = RADIUS
  const trackWidth = TRACK_W

  const clampedValue = value !== null ? Math.min(max, Math.max(min, value)) : null
  const fraction = clampedValue !== null ? (clampedValue - min) / (max - min || 1) : null
  const needleAngle = fraction !== null ? ARC_START + fraction * ARC_TOTAL : ARC_START

  const needleLen = radius - 18
  const needleRad = (needleAngle - 90) * Math.PI / 180
  const needleX = cx + needleLen * Math.cos(needleRad)
  const needleY = cy + needleLen * Math.sin(needleRad)

  // Convert each segment's [from, to] range to arc degree positions
  const segsWithDeg = (segments && segments.length > 0 ? segments : DEFAULT_SEGMENTS).map(seg => {
    const fStart = (seg.from - min) / (max - min || 1)
    const fEnd = (seg.to - min) / (max - min || 1)
    return {
      startDeg: ARC_START + Math.max(0, fStart) * ARC_TOTAL,
      endDeg: ARC_START + Math.min(1, fEnd) * ARC_TOTAL,
      color: seg.color
    }
  }).filter(s => s.endDeg > s.startDeg)

  const displayValue = value !== null ? (Number.isInteger(value) ? value : value.toFixed(2)) : '--'

  return (
    <svg
      width="100%"
      height="100%"
      viewBox={`0 0 ${SVG_W} ${SVG_H}`}
      preserveAspectRatio="xMidYMid meet"
      style={{ display: 'block' }}
    >
      {/* Background track */}
      <path
        d={createArc(cx, cy, radius, ARC_START, ARC_END)}
        fill="none"
        stroke="#2D3748"
        strokeWidth={trackWidth + 2}
        strokeLinecap="round"
      />

      {/* Colored segments */}
      {segsWithDeg.map((seg, i) => (
        <path
          key={i}
          d={createArc(cx, cy, radius, seg.startDeg, seg.endDeg)}
          fill="none"
          stroke={seg.color}
          strokeWidth={trackWidth}
          strokeLinecap={i === 0 ? 'round' : 'butt'}
        />
      ))}

      {/* Tick marks at 0%, 25%, 50%, 75%, 100% */}
      {[0, 0.25, 0.5, 0.75, 1].map((t) => {
        const deg = ARC_START + t * ARC_TOTAL
        const rad = (deg - 90) * Math.PI / 180
        const outerX = cx + (radius + 4) * Math.cos(rad)
        const outerY = cy + (radius + 4) * Math.sin(rad)
        const innerX = cx + (radius - trackWidth - 4) * Math.cos(rad)
        const innerY = cy + (radius - trackWidth - 4) * Math.sin(rad)
        return (
          <line
            key={t}
            x1={innerX}
            y1={innerY}
            x2={outerX}
            y2={outerY}
            stroke="#4A5568"
            strokeWidth="1.5"
          />
        )
      })}

      {/* Min / Max labels */}
      <text
        x={cx + (radius + 6) * Math.cos((ARC_START - 90) * Math.PI / 180)}
        y={cy + (radius + 6) * Math.sin((ARC_START - 90) * Math.PI / 180) + 4}
        textAnchor="middle"
        fill="#718096"
        fontSize="10"
        fontFamily="monospace"
      >
        {min}
      </text>
      <text
        x={cx + (radius + 6) * Math.cos((ARC_END - 90) * Math.PI / 180)}
        y={cy + (radius + 6) * Math.sin((ARC_END - 90) * Math.PI / 180) + 4}
        textAnchor="middle"
        fill="#718096"
        fontSize="10"
        fontFamily="monospace"
      >
        {max}
      </text>

      {/* Needle */}
      {fraction !== null && (
        <line
          x1={cx}
          y1={cy}
          x2={needleX}
          y2={needleY}
          stroke="white"
          strokeWidth="2.5"
          strokeLinecap="round"
        />
      )}

      {/* Center dot */}
      <circle cx={cx} cy={cy} r="4" fill="white" />

      {/* Value text */}
      <text
        x={cx}
        y={cy + 22}
        textAnchor="middle"
        fill="white"
        fontSize="15"
        fontWeight="bold"
        fontFamily="monospace"
      >
        {displayValue}
      </text>

      {/* Title */}
      {title && (
        <text
          x={cx}
          y={SVG_H - 4}
          textAnchor="middle"
          fill="#A0AEC0"
          fontSize="11"
          fontFamily="sans-serif"
        >
          {title}
        </text>
      )}
    </svg>
  )
}

// Placeholder patterns
const PLACEHOLDER_REGEX_GLOBAL = /\$\{(\w+)(?::(\d+))?\}/g
const SYMBOL_QUOTE_REGEX = /@\{([A-Za-z0-9.^]+):(\w+)(?::(\d+))?\}/

function TemplatePageGauge({ label, layoutY, onDelete, onUpdate, onExport, onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const [currentValue, setCurrentValue] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const width = label.width || DEFAULT_WIDTH
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0
  const style = { ...DEFAULT_STYLE, ...label.style }
  const placeholder = label.placeholder || ''
  const min = label.min ?? 0
  const max = label.max ?? 100
  const title = label.title || ''
  const segments = label.segments || DEFAULT_SEGMENTS
  const tooltipRows = label.tooltipRows || []
  const tooltipFooter = label.tooltipFooter || ''

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  // Detect placeholder types
  const hasSystemPlaceholders = placeholder.includes('${')

  const symbolQuoteMatch = placeholder ? placeholder.match(SYMBOL_QUOTE_REGEX) : null
  const symbolQuoteInfo = symbolQuoteMatch ? {
    symbol: symbolQuoteMatch[1].toUpperCase(),
    property: symbolQuoteMatch[2].toLowerCase(),
    interval: symbolQuoteMatch[3] ? parseInt(symbolQuoteMatch[3], 10) : 5
  } : null

  // Combine all texts that may contain @{} placeholders so useSymbolQuotes subscribes to everything
  const allPlaceholderText = [
    placeholder,
    ...tooltipRows.map(r => `${r.detail || ''} ${r.value || ''}`)
  ].join(' ')
  const hasAnySymbolQuotes = allPlaceholderText.includes('@{')

  const { getValue: getQuoteValue, resolveText: resolveSymbolText } = useSymbolQuotes(
    hasAnySymbolQuotes ? allPlaceholderText : null
  )

  const symbolQuoteValue = symbolQuoteInfo
    ? getQuoteValue(symbolQuoteInfo.symbol, symbolQuoteInfo.property)
    : null

  // Get min refresh interval from system placeholders
  const getSystemRefreshInterval = useCallback(() => {
    let minInterval = Infinity
    for (const match of placeholder.matchAll(PLACEHOLDER_REGEX_GLOBAL)) {
      const interval = match[2] ? parseInt(match[2], 10) : 5
      if (interval < minInterval) minInterval = interval
    }
    return minInterval === Infinity ? null : minInterval
  }, [placeholder])

  const fetchSystemValue = useCallback(async () => {
    if (!placeholder) return

    if (placeholder.includes('tws_connection_status')) {
      try {
        const status = await getConnectionStatus()
        setCurrentValue(status.connected ? `Connected (${status.port})` : 'Disconnected')
      } catch {
        setCurrentValue(null)
      }
      return
    }

    const match = placeholder.match(/^\s*\$\{([A-Za-z0-9]+):(time|status)/)
    if (match) {
      const exchange = match[1].toUpperCase()
      const property = match[2]
      setCurrentValue(property === 'time' ? getExchangeTime(exchange) : getExchangeStatus(exchange))
    }
  }, [placeholder])

  useEffect(() => {
    if (!hasSystemPlaceholders) return
    const interval = getSystemRefreshInterval()
    if (!interval) return
    fetchSystemValue()
    const id = setInterval(fetchSystemValue, interval * 1000)
    return () => clearInterval(id)
  }, [hasSystemPlaceholders, fetchSystemValue, getSystemRefreshInterval])

  // Resolve the gauge's numeric value
  const resolvedNumeric = (() => {
    if (symbolQuoteValue !== null) {
      const n = parseFloat(symbolQuoteValue)
      return isNaN(n) ? null : n
    }
    if (currentValue !== null) {
      const n = parseFloat(currentValue)
      return isNaN(n) ? null : n
    }
    return null
  })()

  const displayValue = resolvedNumeric !== null
    ? (Number.isInteger(resolvedNumeric) ? String(resolvedNumeric) : resolvedNumeric.toFixed(2))
    : '--'

  const resolveRowText = (text) => resolveSymbolText ? resolveSymbolText(text || '') : (text || '')

  // Sync preview state when label prop catches up
  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  useEffect(() => {
    if (previewPos && !isMoving && label.x === previewPos.x) {
      setPreviewPos(null)
      onDragPreview?.(null)
    }
  }, [label.x, previewPos, isMoving, onDragPreview])

  const getContainerInfo = () => {
    const container = containerRef?.current || boxRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    return { width: rect.width, segmentSize: rect.width / TOTAL_SEGMENTS, rect }
  }

  const findDropTarget = (mouseX, mouseY) => {
    const header = document.querySelector('[data-header-dropzone]')
    if (header) {
      const rect = header.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) {
        return 'header'
      }
    }
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const panelId = panel.getAttribute('data-panel-id')
      if (panelId === label.id) continue
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
    let currentY = y
    let lastMouseX = e.clientX
    let lastMouseY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startMouseX
      const deltaY = moveEvent.clientY - startMouseY
      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - width, startX + deltaSegments))
      const newVisualY = Math.max(0, startVisualY + deltaY)
      currentX = newX
      currentY = newVisualY
      lastMouseX = moveEvent.clientX
      lastMouseY = moveEvent.clientY
      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
      onDragStateChange?.(true, { x: moveEvent.clientX, y: moveEvent.clientY })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsMoving(false)
      onDragStateChange?.(false, null)

      const dropTarget = findDropTarget(lastMouseX, lastMouseY)

      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveWidget?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
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
        if (currentX !== x || currentY !== y) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
      } else if (currentX !== x || currentY !== y) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startWidth = width
    const startHeight = height
    let currentWidth = width
    let currentHeight = height

    setIsResizing(true)

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startMouseX
      const deltaY = moveEvent.clientY - startMouseY
      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      const newHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
      currentWidth = newWidth
      currentHeight = newHeight
      setPreviewSize({ width: newWidth, height: newHeight })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsResizing(false)
      const changes = {}
      if (currentWidth !== width) changes.width = currentWidth
      if (currentHeight !== height) changes.height = currentHeight
      if (Object.keys(changes).length > 0) {
        onUpdate(label.id, changes)
      }
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleOpenEdit = () => {
    setEditConfig({
      placeholder,
      min,
      max,
      title,
      tooltipRows: tooltipRows.map(r => ({ ...r })),
      tooltipFooter,
      segments: segments.length > 0 ? segments.map(s => ({ ...s })) : DEFAULT_SEGMENTS.map(s => ({ ...s })),
      style: { ...DEFAULT_STYLE, ...label.style }
    })
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      placeholder: editConfig.placeholder,
      min: parseFloat(editConfig.min) || 0,
      max: parseFloat(editConfig.max) || 100,
      title: editConfig.title,
      tooltipRows: editConfig.tooltipRows.filter(r => r.name || r.detail || r.value),
      tooltipFooter: editConfig.tooltipFooter,
      segments: editConfig.segments.filter(s => s.color),
      style: editConfig.style
    })
    onClose()
  }


  const handleAddSegment = () => {
    const last = editConfig.segments[editConfig.segments.length - 1]
    const newFrom = last ? last.to : 0
    setEditConfig({
      ...editConfig,
      segments: [...editConfig.segments, { from: newFrom, to: newFrom + 25, color: '#718096' }]
    })
  }

  const handleRemoveSegment = (index) => {
    setEditConfig({ ...editConfig, segments: editConfig.segments.filter((_, i) => i !== index) })
  }

  const handleUpdateSegment = (index, field, value) => {
    const updated = [...editConfig.segments]
    updated[index] = { ...updated[index], [field]: field === 'color' ? value : parseFloat(value) || 0 }
    setEditConfig({ ...editConfig, segments: updated })
  }

  return (
    <>
      <WidgetTooltip
        title={title || 'Gauge'}
        value={displayValue}
        rows={tooltipRows.map(r => ({
          name: r.name,
          detail: resolveRowText(r.detail),
          value: resolveRowText(r.value)
        }))}
        footer={resolveRowText(tooltipFooter)}
      >
        <Box
          ref={boxRef}
          position="absolute"
          left={`${(displayX / TOTAL_SEGMENTS) * 100}%`}
          top={`${displayY}px`}
          width={`calc(${(displayWidth / TOTAL_SEGMENTS) * 100}% - 8px)`}
          height={`${displayHeight}px`}
          bg={style.bgColor}
          borderRadius="md"
          borderWidth={`${style.borderWidth}px`}
          borderColor={style.borderColor}
          borderStyle="solid"
          outline={isEditing ? '2px dashed' : 'none'}
          outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : undefined}
          outlineOffset="-2px"
          overflow="hidden"
          transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
          opacity={isMoving ? 0.7 : 1}
          cursor={isEditing ? 'move' : 'default'}
          onMouseDown={isEditing ? handleMoveStart : undefined}
          zIndex={isMoving ? 100 : 1}
        >
          <GaugeSVG
            value={resolvedNumeric}
            min={min}
            max={max}
            segments={segments}
            title={title}
          />

          {isEditing && (
            <>
              <Tooltip label="Export gauge" placement="top" hasArrow>
                <IconButton
                  icon={<DownloadIcon />}
                  size="xs"
                  variant="ghost"
                  aria-label="Export gauge"
                  onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                  onMouseDown={(e) => e.stopPropagation()}
                  position="absolute"
                  top={0}
                  right={12}
                />
              </Tooltip>
              <IconButton
                icon={<SettingsIcon />}
                size="xs"
                variant="ghost"
                aria-label="Edit gauge"
                onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
                onMouseDown={(e) => e.stopPropagation()}
                position="absolute"
                top={0}
                right={6}
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete gauge"
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
                position="absolute"
                top={0}
                right={0}
              />
              <Box
                position="absolute"
                right={0}
                bottom={0}
                width="12px"
                height="12px"
                cursor="nwse-resize"
                bg="whiteAlpha.200"
                _hover={{ bg: 'whiteAlpha.400' }}
                onMouseDown={handleResize}
              />
            </>
          )}
        </Box>
      </WidgetTooltip>

      <Modal isOpen={isOpen} onClose={onClose} size="lg">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Gauge</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Value Source</FormLabel>
                  <Input
                    value={editConfig.placeholder}
                    onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                    placeholder="@{VIX:last:5} or @{SPY:changePercent:3}"
                    fontFamily="monospace"
                    fontSize="sm"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Title</FormLabel>
                  <Input
                    value={editConfig.title}
                    onChange={(e) => setEditConfig({ ...editConfig, title: e.target.value })}
                    placeholder="VIX, RSI, etc."
                  />
                </FormControl>

                <WidgetTooltipEditor
                  rows={editConfig.tooltipRows}
                  footer={editConfig.tooltipFooter}
                  onChange={({ rows, footer }) => setEditConfig({ ...editConfig, tooltipRows: rows, tooltipFooter: footer })}
                />

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Min</FormLabel>
                    <Input
                      type="number"
                      value={editConfig.min}
                      onChange={(e) => setEditConfig({ ...editConfig, min: e.target.value })}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Max</FormLabel>
                    <Input
                      type="number"
                      value={editConfig.max}
                      onChange={(e) => setEditConfig({ ...editConfig, max: e.target.value })}
                    />
                  </FormControl>
                </HStack>

                <Box>
                  <HStack justify="space-between" mb={2}>
                    <FormLabel fontSize="sm" mb={0}>Color Segments</FormLabel>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddSegment}>
                      Add
                    </Button>
                  </HStack>
                  <VStack spacing={2} align="stretch">
                    {editConfig.segments.map((seg, index) => (
                      <HStack key={index} spacing={2}>
                        <Input
                          size="sm"
                          type="number"
                          placeholder="From"
                          value={seg.from}
                          onChange={(e) => handleUpdateSegment(index, 'from', e.target.value)}
                          flex={1}
                        />
                        <Text fontSize="sm" color="gray.400">–</Text>
                        <Input
                          size="sm"
                          type="number"
                          placeholder="To"
                          value={seg.to}
                          onChange={(e) => handleUpdateSegment(index, 'to', e.target.value)}
                          flex={1}
                        />
                        <Input
                          type="color"
                          value={seg.color}
                          onChange={(e) => handleUpdateSegment(index, 'color', e.target.value)}
                          width="50px"
                          height="32px"
                          p={1}
                        />
                        <IconButton
                          icon={<DeleteIcon />}
                          size="xs"
                          variant="ghost"
                          colorScheme="red"
                          onClick={() => handleRemoveSegment(index)}
                          aria-label="Remove segment"
                        />
                      </HStack>
                    ))}
                  </VStack>
                </Box>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background</FormLabel>
                    <Input
                      type="color"
                      value={editConfig.style.bgColor === 'transparent' ? '#1a202c' : editConfig.style.bgColor}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, bgColor: e.target.value } })}
                      height="40px"
                      p={1}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Border Color</FormLabel>
                    <Input
                      type="color"
                      value={editConfig.style.borderColor}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderColor: e.target.value } })}
                      height="40px"
                      p={1}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Border Width</FormLabel>
                    <Input
                      type="number"
                      min={0}
                      max={4}
                      value={editConfig.style.borderWidth}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderWidth: parseInt(e.target.value) || 0 } })}
                    />
                  </FormControl>
                </HStack>
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
            <Button colorScheme="blue" onClick={handleSaveConfig}>Save</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageGauge
