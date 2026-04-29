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
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'

const DotsIcon = () => (
  <svg viewBox="0 0 16 16" width="1em" height="1em" fill="currentColor">
    <circle cx="8" cy="3" r="1.5" />
    <circle cx="8" cy="8" r="1.5" />
    <circle cx="8" cy="13" r="1.5" />
  </svg>
)
import FillIcon from './FillIcon'
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'
import { getGlobalChartSettings, saveGlobalChartSettings } from '../modules/globalChartSettings'

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
function parseSnapshotExpression(expr) {
  const match = expr?.match(/^@\{([^:}]+):([^:}]+)(?::(\d+))?\}$/)
  if (!match) return null
  const [, symbol, property, intervalStr] = match
  const interval = parseInt(intervalStr) || 60
  return { url: `/api/bars/${symbol}/${property}?interval=${interval}`, symbol, property, valueKey: 'value', interval }
}

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1
}

const SERIES_COLORS = ['#4299E1', '#48BB78', '#F6AD55', '#F56565', '#9F7AEA', '#ED64A6']
const LEGEND_HEIGHT = 18

function fmtY(v) {
  const abs = Math.abs(v)
  if (abs >= 1e6) return (v / 1e6).toFixed(1) + 'M'
  if (abs >= 1e3) return (v / 1e3).toFixed(1) + 'K'
  return Number.isInteger(v) ? String(v) : v.toFixed(2)
}

// Pure SVG renderer — each series has { timestamp, value }[] and is normalized independently
function GraphSVG({ seriesEntries, width, height, tooltipPosition = 'cursor' }) {
  const [hoverX, setHoverX] = useState(null)
  const [hoverY, setHoverY] = useState(null)
  const svgRef = useRef(null)

  const padding = { top: 8, right: FONT_SIZE * 5, bottom: 28, left: 8 }
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
    if (!data.length) return { el: null, config, hoverVal: null, scale: null }

    const vals = data.map(d => d.value).filter(isFinite)
    if (!vals.length) return { el: null, config, hoverVal: null, scale: null }

    const minV = Math.min(...vals)
    const maxV = Math.max(...vals)
    const isBars = config.viewType === 'bars'
    // Bars always start from 0; lines use min–max with padding
    const effMin = isBars ? 0 : minV - ((maxV - minV) || 1) * 0.08
    const effMax = isBars ? maxV * 1.08 : maxV + ((maxV - minV) || 1) * 0.08
    const effRange = (effMax - effMin) || 1

    const valToY = v => padding.top + chartH - ((v - effMin) / effRange) * chartH

    const pointMap = new Map(data.map(d => [d.timestamp, d]))

    let hoverVal = null
    if (hoverTs !== null) {
      const pt = pointMap.get(hoverTs)
      if (pt) hoverVal = pt.value
    }

    let el
    if (config.viewType === 'bars') {
      const n = allTs.length
      const barSlot = chartW / n
      const barW = Math.max(1, barSlot * 0.7)
      el = (
        <g key={config.id}>
          {data.map(d => {
            const idx = allTs.indexOf(d.timestamp)
            const cx = padding.left + (idx + 0.5) * barSlot
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

    return { el, config, hoverVal, scale: { effMin, effMax, effRange, valToY } }
  })

  const hasBarSeries = seriesEntries.some(e => e.config.viewType === 'bars')
  const slotX = (ts) => padding.left + (allTs.indexOf(ts) + 0.5) * (chartW / allTs.length)
  const axisX = hasBarSeries ? slotX : tsToX

  const maxLabels = Math.max(2, Math.floor(chartW / 80))
  const step = Math.max(1, Math.ceil(allTs.length / maxLabels))
  const axisTs = allTs.filter((_, i) => i % step === 0)

  // Compute Y grid ticks from the first rendered series that has scale info
  const refScale = rendered.find(r => r.scale)?.scale ?? null
  const gridTicks = (() => {
    if (!refScale) return []
    const { effMin, effMax } = refScale
    const range = effMax - effMin
    const rough = range / 4
    const mag = Math.pow(10, Math.floor(Math.log10(rough || 1)))
    const step = [1, 2, 2.5, 5, 10].map(c => c * mag).find(c => c >= rough) || rough
    const start = Math.ceil(effMin / step) * step
    const ticks = []
    for (let t = start; t <= effMax + step * 0.01; t = Math.round((t + step) * 1e10) / 1e10) {
      ticks.push(t)
      if (ticks.length > 8) break
    }
    return ticks
  })()

  return (
    <svg
      ref={svgRef}
      width={width}
      height={height}
      style={{ display: 'block' }}
      onMouseMove={e => {
        const rect = svgRef.current.getBoundingClientRect()
        setHoverX(e.clientX - rect.left)
        setHoverY(e.clientY - rect.top)
      }}
      onMouseLeave={() => { setHoverX(null); setHoverY(null) }}
    >
      {gridTicks.map(tick => {
        const y = refScale.valToY(tick)
        if (y < padding.top - 1 || y > padding.top + chartH + 1) return null
        return (
          <g key={tick}>
            <line x1={padding.left} y1={y} x2={padding.left + chartW} y2={y} stroke="#2D3748" strokeWidth={1} />
            <text x={padding.left + chartW + 3} y={y + 3} fontSize={FONT_SIZE} fill="#4A5568" textAnchor="start">{fmtY(tick)}</text>
          </g>
        )
      })}

      {rendered.map(({ el }) => el)}

      {hoverTs !== null && (
        <line
          x1={axisX(hoverTs)} y1={padding.top}
          x2={axisX(hoverTs)} y2={padding.top + chartH}
          stroke="#718096" strokeWidth={1} strokeDasharray="3,3"
        />
      )}

      {hoverTs !== null && (() => {
        const rows = rendered.filter(r => r.hoverVal != null && isFinite(r.hoverVal))
        if (!rows.length) return null
        const lineH = FONT_SIZE + 4
        const tw = 110
        const th = lineH * (rows.length + 1) + 8
        let tx, ty
        if (tooltipPosition === 'corner') {
          tx = padding.left + chartW - tw - 4
          ty = padding.top + 4
        } else {
          const lx = axisX(hoverTs)
          tx = lx + 10 + tw > width ? lx - tw - 10 : lx + 10
          ty = Math.min(Math.max((hoverY ?? padding.top) - th / 2, padding.top), padding.top + chartH - th)
        }
        return (
          <g pointerEvents="none">
            <rect x={tx} y={ty} width={tw} height={th} rx={3} fill="#1A202C" stroke="#4A5568" strokeWidth={1} opacity={0.95} />
            <text x={tx + 6} y={ty + FONT_SIZE + 2} fontSize={FONT_SIZE} fill="#718096">
              {new Date(hoverTs).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
            </text>
            {rows.map((r, i) => (
              <g key={r.config.id}>
                <rect x={tx + 6} y={ty + lineH * (i + 1) + 6} width={6} height={6} fill={r.config.color} rx={1} />
                <text x={tx + 16} y={ty + lineH * (i + 1) + FONT_SIZE + 2} fontSize={FONT_SIZE} fill="#CBD5E0">
                  {(r.config.name || r.config.expression || '?') + ': ' + fmtY(r.hoverVal)}
                </text>
              </g>
            ))}
          </g>
        )
      })()}

      {axisTs.map(ts => (
        <text key={ts} x={axisX(ts)} y={height - 6}
          textAnchor="middle" fontSize={FONT_SIZE} fill="#718096"
        >
          {formatAxisTs(ts)}
        </text>
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
  const [globalSettings, setGlobalSettings] = useState(getGlobalChartSettings)

  useEffect(() => {
    const handler = (e) => setGlobalSettings(e.detail)
    window.addEventListener('globalChartSettingsChanged', handler)
    return () => window.removeEventListener('globalChartSettingsChanged', handler)
  }, [])
  const [editConfig, setEditConfig] = useState(null)
  const boxRef = useRef(null)
  const valuesRef = useRef([])
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()

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

  // Build combined expression text for the resolver — live series only
  const liveSeries = series.filter(s => s.mode !== 'snapshot')
  const allExpressions = liveSeries.map(s => s.expression).filter(Boolean).join(' ')
  const { resolvedValue } = useWidgetResolveText(allExpressions || null)

  // Compute current values and sample into rolling history when they change (live series only)
  const currentValues = liveSeries.map(s => s.expression ? resolvedValue(s.expression) : null)
  const valuesKey = currentValues.map(v => v ?? 'x').join(',')

  useEffect(() => {
    const now = Date.now()
    setHistory(prev => {
      const updated = new Map(prev)
      let changed = false
      liveSeries.forEach((s, i) => {
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

  // Reset history when series configurations change
  const seriesConfigKey = JSON.stringify(series.map(s => ({ id: s.id, mode: s.mode, expression: s.expression })))
  useEffect(() => {
    setHistory(new Map())
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seriesConfigKey])

  // Snapshot series: parse expression to determine URL, fetch on interval
  const snapshotSeries = series.filter(s => s.mode === 'snapshot' && s.expression)
  const snapshotKey = JSON.stringify(snapshotSeries.map(s => ({ id: s.id, expression: s.expression })))
  useEffect(() => {
    if (!snapshotSeries.length) return
    const timers = []
    snapshotSeries.forEach(s => {
      const parsed = parseSnapshotExpression(s.expression)
      if (!parsed) return
      const { url, valueKey, interval } = parsed
      const fetchSnapshot = async () => {
        try {
          const res = await fetch(url)
          if (!res.ok) return
          const data = await res.json()
          if (!Array.isArray(data)) return
          const points = data
            .map(d => ({ timestamp: d.timestamp, value: d[valueKey] }))
            .filter(d => d.timestamp != null && d.value != null && isFinite(d.value))
          setHistory(prev => { const m = new Map(prev); m.set(s.id, points); return m })
        } catch (e) {
          console.error(`[graph snapshot] fetch error:`, e)
        }
      }
      fetchSnapshot()
      timers.push(setInterval(fetchSnapshot, Math.max(10, interval) * 1000))
    })
    return () => timers.forEach(clearInterval)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [snapshotKey])

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
      mode: 'live',
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

  const hasSnapshotSeries = series.some(s => s.mode === 'snapshot')

  const handleClearSnapshots = async () => {
    const snapshots = series.filter(s => s.mode === 'snapshot' && s.expression)
    await Promise.allSettled(
      snapshots.map(s => {
        const parsed = parseSnapshotExpression(s.expression)
        if (!parsed) return Promise.resolve()
        return fetch(`/api/bars/${parsed.symbol}/${parsed.property}?interval=${parsed.interval}`, { method: 'DELETE' })
      })
    )
    setHistory(prev => {
      const m = new Map(prev)
      snapshots.forEach(s => m.set(s.id, []))
      return m
    })
  }

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
            height={displayHeight - style.borderWidth * 2 - LEGEND_HEIGHT}
            tooltipPosition={globalSettings.graphTooltipPosition ?? 'cursor'}
          />
        )}

        <HStack
          position="absolute" bottom={`${style.borderWidth}px`} left={0} right={0}
          height={`${LEGEND_HEIGHT}px`} px={2} spacing={3}
          pointerEvents="none"
        >
          {seriesEntries.map(({ config, data }) => {
            const latestVal = data.length ? data[data.length - 1].value : null
            const formatted = latestVal != null && isFinite(latestVal) ? fmtY(latestVal) : '—'
            return (
              <HStack key={config.id} spacing={1}>
                <Box w="8px" h="8px" bg={config.color} borderRadius="sm" flexShrink={0} />
                <Text fontSize={`${FONT_SIZE}px`} color="gray.300" whiteSpace="nowrap">
                  {(config.name || config.expression || '?') + ' ' + formatted}
                </Text>
              </HStack>
            )
          })}
        </HStack>

        {hasSnapshotSeries && (
          <Box position="absolute" top="2px" left="2px" zIndex={10} onMouseDown={e => e.stopPropagation()}>
            <Menu placement="bottom-start" size="sm">
              <MenuButton
                as={IconButton}
                icon={<DotsIcon />}
                size="xs"
                variant="ghost"
                color="gray.400"
                _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
                aria-label="Snapshot actions"
                onClick={e => e.stopPropagation()}
              />
              <MenuList minW="140px" bg="gray.800" borderColor="gray.600" fontSize="sm">
                <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} color="red.300"
                  onClick={handleClearSnapshots}
                >
                  Clear snapshots
                </MenuItem>
              </MenuList>
            </Menu>
          </Box>
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
                onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
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
                      <HStack>
                        <FormControl maxW="120px">
                          <FormLabel fontSize="xs">Mode</FormLabel>
                          <Select size="sm" value={s.mode || 'live'}
                            onChange={e => updateSeries(s.id, { mode: e.target.value })}>
                            <option value="live">Live</option>
                            <option value="snapshot">Snapshot</option>
                          </Select>
                        </FormControl>
                        <FormControl>
                          <FormLabel fontSize="xs">Name (optional)</FormLabel>
                          <Input size="sm" value={s.name || ''}
                            onChange={e => updateSeries(s.id, { name: e.target.value })}
                            placeholder="Call Vol 5m"
                          />
                        </FormControl>
                      </HStack>
                      <FormControl>
                        <FormLabel fontSize="xs">Expression</FormLabel>
                        <Input size="sm" value={s.expression || ''}
                          onChange={e => updateSeries(s.id, { expression: e.target.value })}
                          placeholder={(s.mode || 'live') === 'snapshot' ? '@{SPY:callVol:300}' : '@{SPY:last:5}  @{SPY:callVol:300}'}
                          fontFamily="mono"
                        />
                      </FormControl>
                      <HStack>
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

                <Box borderTopWidth={1} borderColor="gray.600" pt={3}>
                  <Text fontSize="sm" fontWeight="medium" mb={2}>Global</Text>
                  <FormControl>
                    <FormLabel fontSize="xs">Tooltip position</FormLabel>
                    <HStack spacing={1}>
                      {['cursor', 'corner'].map(opt => (
                        <Button key={opt} size="xs"
                          bg={(globalSettings.graphTooltipPosition ?? 'cursor') === opt ? 'blue.600' : 'transparent'}
                          color={(globalSettings.graphTooltipPosition ?? 'cursor') === opt ? 'white' : 'gray.400'}
                          borderWidth={1} borderColor="gray.600"
                          onClick={() => saveGlobalChartSettings({ ...globalSettings, graphTooltipPosition: opt })}
                        >
                          {opt}
                        </Button>
                      ))}
                    </HStack>
                  </FormControl>
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
      { confirmDeleteDialog }
    </>
  )
}

export default TemplatePageGraph
