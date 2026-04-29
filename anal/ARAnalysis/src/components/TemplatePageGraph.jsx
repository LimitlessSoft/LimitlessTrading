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
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
import FillIcon from './FillIcon'
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 4
const MIN_HEIGHT = 100
const DEFAULT_HEIGHT = 220
const DEFAULT_WIDTH = 10
const DEFAULT_MAX_HISTORY = 200
const FONT_SIZE = 9

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1
}

const SERIES_COLORS = ['#4299E1', '#48BB78', '#F6AD55', '#F56565', '#9F7AEA', '#ED64A6']

// Pure SVG renderer — each series has { timestamp, value }[] and is normalized independently
function GraphSVG({ seriesEntries, width, height }) {
  const [hoverX, setHoverX] = useState(null)
  const svgRef = useRef(null)

  const padding = { top: 24, right: FONT_SIZE * 5, bottom: 28, left: 8 }
  const chartW = width - padding.left - padding.right
  const chartH = height - padding.top - padding.bottom

  const allTs = [
    ...new Set(seriesEntries.flatMap(({ data }) => data.map(d => d.timestamp)))
  ].sort((a, b) => a - b)

  if (allTs.length === 0 || chartW <= 0 || chartH <= 0) {
    return (
      <svg width={width} height={height}>
        <text x={width / 2} y={height / 2} textAnchor="middle" fill="#4A5568" fontSize={FONT_SIZE}>
          {seriesEntries.length === 0 ? 'No series — click ⚙ to configure' : 'Waiting for data…'}
        </text>
      </svg>
    )
  }

  const minT = allTs[0]
  const maxT = allTs[allTs.length - 1]
  const timeRange = maxT - minT

  const tsToX = t => timeRange === 0
    ? padding.left + chartW / 2
    : padding.left + ((t - minT) / timeRange) * chartW

  let hoverTs = null
  if (hoverX !== null) {
    const t = timeRange === 0 ? minT : minT + ((hoverX - padding.left) / chartW) * timeRange
    let bestIdx = 0, bestDist = Infinity
    allTs.forEach((ts, i) => {
      const d = Math.abs(ts - t)
      if (d < bestDist) { bestDist = d; bestIdx = i }
    })
    hoverTs = allTs[bestIdx]
  }

  const spanMs = maxT - minT
  const isIntraday = spanMs < 7 * 24 * 3600 * 1000
  const formatAxisTs = ts => {
    const d = new Date(ts)
    return isIntraday
      ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : d.toLocaleDateString([], { month: 'short', day: 'numeric' })
  }

  const rendered = seriesEntries.map(({ config, data }) => {
    if (!data.length) return { el: null, config, hoverVal: null }

    const vals = data.map(d => d.value).filter(isFinite)
    if (!vals.length) return { el: null, config, hoverVal: null }

    const minV = Math.min(...vals)
    const maxV = Math.max(...vals)
    const vRange = (maxV - minV) || 1
    const pad = vRange * 0.08
    const effMin = minV - pad
    const effRange = vRange + pad * 2

    const valToY = v => padding.top + chartH - ((v - effMin) / effRange) * chartH

    const pointMap = new Map(data.map(d => [d.timestamp, d]))

    let hoverVal = null
    if (hoverTs !== null) {
      const pt = pointMap.get(hoverTs)
      if (pt) hoverVal = pt.value
    }

    let el
    if (config.viewType === 'bars') {
      const barSlot = chartW / allTs.length
      const barW = Math.max(1, barSlot * 0.7)
      el = (
        <g key={config.id}>
          {data.map(d => {
            const cx = tsToX(d.timestamp)
            const y = valToY(d.value)
            const highlighted = hoverTs !== null && d.timestamp === hoverTs
            return (
              <rect key={d.timestamp}
                x={cx - barW / 2} y={y}
                width={barW} height={Math.max(1, padding.top + chartH - y)}
                fill={config.color} opacity={highlighted ? 1 : 0.75}
              />
            )
          })}
        </g>
      )
    } else {
      const segments = []
      let seg = []
      allTs.forEach(ts => {
        const d = pointMap.get(ts)
        if (d !== undefined && isFinite(d.value)) {
          seg.push(`${tsToX(ts).toFixed(1)},${valToY(d.value).toFixed(1)}`)
        } else {
          if (seg.length) { segments.push(seg); seg = [] }
        }
      })
      if (seg.length) segments.push(seg)

      const hoverDot = hoverTs !== null ? (() => {
        const d = pointMap.get(hoverTs)
        if (!d || !isFinite(d.value)) return null
        return <circle cx={tsToX(hoverTs)} cy={valToY(d.value)} r={3} fill={config.color} />
      })() : null

      // Single point: draw a dot since a polyline needs ≥2 points
      const singleDot = allTs.length === 1 ? (() => {
        const d = pointMap.get(allTs[0])
        if (!d || !isFinite(d.value)) return null
        return <circle cx={tsToX(allTs[0])} cy={valToY(d.value)} r={4} fill={config.color} />
      })() : null

      el = (
        <g key={config.id}>
          {singleDot}
          {segments.map((pts, i) => (
            <polyline key={i} points={pts.join(' ')} fill="none" stroke={config.color} strokeWidth={1.5} />
          ))}
          {hoverDot}
        </g>
      )
    }

    return { el, config, hoverVal }
  })

  const maxLabels = Math.max(2, Math.floor(chartW / 80))
  const step = Math.max(1, Math.ceil(allTs.length / maxLabels))
  const axisTs = allTs.filter((_, i) => i % step === 0)

  const legendItems = rendered.map(({ config, hoverVal }, i) => {
    const data = seriesEntries[i]?.data
    const latestVal = data?.length ? data[data.length - 1].value : null
    const displayVal = hoverVal !== null && hoverVal !== undefined ? hoverVal : latestVal
    const formatted = displayVal != null && isFinite(displayVal) ? Number(displayVal).toFixed(2) : '—'
    return { config, formatted }
  })

  return (
    <svg
      ref={svgRef}
      width={width}
      height={height}
      style={{ display: 'block' }}
      onMouseMove={e => {
        const rect = svgRef.current.getBoundingClientRect()
        setHoverX(e.clientX - rect.left)
      }}
      onMouseLeave={() => setHoverX(null)}
    >
      {[0, 0.25, 0.5, 0.75, 1.0].map(f => (
        <line key={f}
          x1={padding.left} y1={padding.top + chartH * f}
          x2={padding.left + chartW} y2={padding.top + chartH * f}
          stroke="#2D3748" strokeWidth={1}
        />
      ))}
      <line x1={padding.left + chartW} y1={padding.top}
        x2={padding.left + chartW} y2={padding.top + chartH}
        stroke="#2D3748" strokeWidth={1} />

      {rendered.map(({ el }) => el)}

      {hoverTs !== null && (
        <line
          x1={tsToX(hoverTs)} y1={padding.top}
          x2={tsToX(hoverTs)} y2={padding.top + chartH}
          stroke="#718096" strokeWidth={1} strokeDasharray="3,3"
        />
      )}

      {axisTs.map(ts => (
        <text key={ts} x={tsToX(ts)} y={height - 6}
          textAnchor="middle" fontSize={FONT_SIZE} fill="#718096"
        >
          {formatAxisTs(ts)}
        </text>
      ))}

      {legendItems.map(({ config, formatted }, i) => (
        <g key={config.id}>
          <rect x={i * 100 + padding.left} y={5} width={8} height={8} fill={config.color} rx={1} />
          <text x={i * 100 + padding.left + 11} y={13} fontSize={FONT_SIZE} fill="#CBD5E0">
            {(config.name || config.expression || '?') + ' ' + formatted}
          </text>
        </g>
      ))}
    </svg>
  )
}

function TemplatePageGraph({
  label, layoutY, onDelete, onUpdate, onExport,
  onDragPreview, onDragStateChange, onMoveWidget,
  activeDrag, parentPanelId, isEditing, containerRef
}) {
  const [history, setHistory] = useState(new Map()) // seriesId -> { timestamp, value }[]
  const [containerWidth, setContainerWidth] = useState(0)
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const boxRef = useRef(null)
  const valuesRef = useRef([])
  const { isOpen, onOpen, onClose } = useDisclosure()

  const width = label.width || DEFAULT_WIDTH
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const series = label.series || []
  const maxHistory = label.maxHistory || DEFAULT_MAX_HISTORY
  const style = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  // Build combined expression text for the resolver — includes all series expressions
  const allExpressions = series.map(s => s.expression).filter(Boolean).join(' ')
  const { resolvedValue } = useWidgetResolveText(allExpressions || null)

  // Compute current values and sample into rolling history when they change
  const currentValues = series.map(s => s.expression ? resolvedValue(s.expression) : null)
  const valuesKey = currentValues.map(v => v ?? 'x').join(',')

  useEffect(() => {
    const now = Date.now()
    setHistory(prev => {
      const updated = new Map(prev)
      let changed = false
      series.forEach((s, i) => {
        const val = currentValues[i]
        if (val === null || !isFinite(val)) return
        const existing = updated.get(s.id) || []
        const last = existing[existing.length - 1]
        if (!last || last.value !== val) {
          updated.set(s.id, [...existing, { timestamp: now, value: val }].slice(-maxHistory))
          changed = true
        }
      })
      return changed ? updated : prev
    })
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [valuesKey])

  // Reset history when series configurations change (expressions changed)
  const seriesConfigKey = JSON.stringify(series.map(s => ({ id: s.id, expression: s.expression })))
  useEffect(() => {
    setHistory(new Map())
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seriesConfigKey])

  // Track rendered pixel width
  useEffect(() => {
    if (!boxRef.current) return
    const ro = new ResizeObserver(entries => setContainerWidth(entries[0].contentRect.width))
    ro.observe(boxRef.current)
    return () => ro.disconnect()
  }, [])

  // Clear preview once label prop catches up
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

  const findDropTarget = (mx, my) => {
    const header = document.querySelector('[data-header-dropzone]')
    if (header) {
      const r = header.getBoundingClientRect()
      if (mx >= r.left && mx <= r.right && my >= r.top && my <= r.bottom) return 'header'
    }
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const pid = panel.getAttribute('data-panel-id')
      if (pid === label.id) continue
      const r = panel.getBoundingClientRect()
      if (mx >= r.left && mx <= r.right && my >= r.top && my <= r.bottom) return pid
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
    let lastMX = e.clientX
    let lastMY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const onMove = mv => {
      const dSeg = Math.round((mv.clientX - startMouseX) / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - displayWidth, startX + dSeg))
      const newVisualY = Math.max(0, startVisualY + (mv.clientY - startMouseY))
      currentX = newX
      currentY = newVisualY
      lastMX = mv.clientX
      lastMY = mv.clientY
      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
      onDragStateChange?.(true, { x: mv.clientX, y: mv.clientY })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsMoving(false)
      onDragStateChange?.(false, null)

      const dropTarget = findDropTarget(lastMX, lastMY)
      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveWidget?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
        const main = containerRef?.current
        if (main) {
          const r = main.getBoundingClientRect()
          if (lastMX >= r.left && lastMX <= r.right && lastMY >= r.top && lastMY <= r.bottom) {
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

  const handleResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return

    const startMX = e.clientX
    const startMY = e.clientY
    const startW = width
    const startH = height
    let curW = width
    let curH = height

    setIsResizing(true)

    const onMove = mv => {
      const dSeg = Math.round((mv.clientX - startMX) / info.segmentSize)
      curW = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startW + dSeg))
      curH = Math.max(MIN_HEIGHT, startH + (mv.clientY - startMY))
      setPreviewSize({ width: curW, height: curH })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsResizing(false)
      const changes = {}
      if (curW !== width) changes.width = curW
      if (curH !== height) changes.height = curH
      if (Object.keys(changes).length > 0) onUpdate(label.id, changes)
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleOpenEdit = () => {
    setEditConfig({
      series: series.map(s => ({ ...s })),
      maxHistory,
      style: { ...DEFAULT_STYLE, ...label.style }
    })
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      series: editConfig.series,
      maxHistory: editConfig.maxHistory,
      style: editConfig.style
    })
    onClose()
  }

  const handleAddSeries = () => {
    const newS = {
      id: crypto.randomUUID(),
      name: '',
      expression: '',
      color: SERIES_COLORS[editConfig.series.length % SERIES_COLORS.length],
      viewType: 'line'
    }
    setEditConfig({ ...editConfig, series: [...editConfig.series, newS] })
  }

  const updateSeries = (id, changes) =>
    setEditConfig({
      ...editConfig,
      series: editConfig.series.map(s => s.id === id ? { ...s, ...changes } : s)
    })

  const deleteSeries = (id) =>
    setEditConfig({ ...editConfig, series: editConfig.series.filter(s => s.id !== id) })

  const seriesEntries = series.map(s => ({ config: s, data: history.get(s.id) || [] }))

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
        borderRadius="md"
        overflow="hidden"
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        borderWidth={`${style.borderWidth}px`}
        borderColor={style.borderColor}
        borderStyle="solid"
        outline={isEditing ? '2px dashed' : 'none'}
        outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : undefined}
        outlineOffset="-2px"
        opacity={isMoving ? 0.7 : 1}
        cursor={isEditing ? 'move' : 'default'}
        onMouseDown={isEditing ? handleMoveStart : undefined}
        zIndex={isMoving ? 100 : 1}
      >
        {containerWidth > 0 && (
          <GraphSVG
            seriesEntries={seriesEntries}
            width={containerWidth}
            height={displayHeight - style.borderWidth * 2}
          />
        )}

        <HStack position="absolute" top="2px" right="2px" spacing={0} zIndex={10}>
          {isEditing && (
            <Tooltip label="Export graph" placement="top" hasArrow>
              <IconButton icon={<DownloadIcon />} size="xs" variant="ghost" aria-label="Export graph"
                onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </Tooltip>
          )}
          <IconButton icon={<SettingsIcon />} size="xs" variant="ghost" aria-label="Edit graph"
            color="gray.400" _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
            onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
            onMouseDown={(e) => e.stopPropagation()}
          />
          {isEditing && (
            <>
              <Tooltip label={label.dock === 'fill' ? 'Remove fill' : 'Fill parent'} placement="top" hasArrow>
                <IconButton icon={<FillIcon />} size="xs" variant="ghost" aria-label="Fill parent"
                  color={label.dock === 'fill' ? 'blue.300' : 'gray.400'}
                  _hover={{ color: label.dock === 'fill' ? 'blue.200' : 'white', bg: 'whiteAlpha.200' }}
                  onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { dock: label.dock === 'fill' ? undefined : 'fill' }) }}
                  onMouseDown={(e) => e.stopPropagation()}
                />
              </Tooltip>
              <IconButton icon={<CloseIcon />} size="xs" variant="ghost" aria-label="Delete graph"
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </>
          )}
        </HStack>
        {isEditing && (
          <Box position="absolute" right={0} bottom={0} width="12px" height="12px"
            cursor="nwse-resize" bg="whiteAlpha.200" _hover={{ bg: 'whiteAlpha.400' }}
            onMouseDown={handleResize}
          />
        )}
      </Box>

      <Modal isOpen={isOpen} onClose={onClose} size="lg">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Graph</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <HStack justify="space-between">
                  <Text fontSize="sm" fontWeight="medium">Series</Text>
                  <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddSeries}>
                    Add Series
                  </Button>
                </HStack>

                {editConfig.series.length === 0 && (
                  <Text fontSize="sm" color="gray.500" textAlign="center" py={4}>
                    No series — click Add Series to begin.
                  </Text>
                )}

                {editConfig.series.map((s, idx) => (
                  <Box key={s.id} p={3} borderWidth={1} borderColor="gray.600" borderRadius="md">
                    <HStack justify="space-between" mb={2}>
                      <Text fontSize="xs" color="gray.400">Series {idx + 1}</Text>
                      <IconButton
                        icon={<DeleteIcon />} size="xs" variant="ghost" colorScheme="red"
                        aria-label="Remove series" onClick={() => deleteSeries(s.id)}
                      />
                    </HStack>
                    <VStack spacing={2} align="stretch">
                      <FormControl>
                        <FormLabel fontSize="xs">Expression</FormLabel>
                        <Input size="sm" value={s.expression || ''}
                          onChange={e => updateSeries(s.id, { expression: e.target.value })}
                          placeholder="@{SPY:last:5}  @{SPY:callVol:300}  %[@{SPY:putVol} / @{SPY:callVol}:300]"
                          fontFamily="mono"
                        />
                      </FormControl>
                      <HStack>
                        <FormControl>
                          <FormLabel fontSize="xs">Name (optional)</FormLabel>
                          <Input size="sm" value={s.name || ''}
                            onChange={e => updateSeries(s.id, { name: e.target.value })}
                            placeholder="SPY last"
                          />
                        </FormControl>
                        <FormControl>
                          <FormLabel fontSize="xs">View</FormLabel>
                          <Select size="sm" value={s.viewType || 'line'}
                            onChange={e => updateSeries(s.id, { viewType: e.target.value })}>
                            <option value="line">Line</option>
                            <option value="bars">Bars</option>
                          </Select>
                        </FormControl>
                        <FormControl maxW="72px">
                          <FormLabel fontSize="xs">Color</FormLabel>
                          <Input type="color" size="sm" value={s.color || SERIES_COLORS[0]}
                            onChange={e => updateSeries(s.id, { color: e.target.value })}
                            height="32px" p={1}
                          />
                        </FormControl>
                      </HStack>
                    </VStack>
                  </Box>
                ))}

                <Box borderTopWidth={1} borderColor="gray.600" pt={3}>
                  <Text fontSize="sm" fontWeight="medium" mb={2}>Settings</Text>
                  <HStack align="flex-end">
                    <FormControl maxW="120px">
                      <FormLabel fontSize="xs">Max history points</FormLabel>
                      <Input size="sm" type="number" min={10} max={2000}
                        value={editConfig.maxHistory}
                        onChange={e => setEditConfig({ ...editConfig, maxHistory: parseInt(e.target.value) || DEFAULT_MAX_HISTORY })}
                      />
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="xs">Background</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.bgColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, bgColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="xs">Border Color</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.borderColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Border px</FormLabel>
                      <Input size="sm" type="number" min={0} max={5}
                        value={editConfig.style.borderWidth}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderWidth: parseInt(e.target.value) || 0 } })}
                      />
                    </FormControl>
                  </HStack>
                </Box>
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

export default TemplatePageGraph
