import { useState, useEffect, useRef } from 'react'
import {
  Box,
  HStack,
  VStack,
  Input,
  Button,
  Select,
  ButtonGroup,
  Checkbox,
  Tag,
  TagLabel,
  TagCloseButton,
  Text,
  Alert,
  AlertIcon,
  Wrap,
  WrapItem,
  FormLabel,
  FormControl,
  Divider,
  Badge,
  Popover,
  PopoverTrigger,
  PopoverContent,
  PopoverBody,
  PopoverArrow,
  IconButton
} from '@chakra-ui/react'
import { StarIcon, InfoOutlineIcon } from '@chakra-ui/icons'
import { createChart, LineSeries } from 'lightweight-charts'
import { fetchOptionsParams, fetchOptionHistory, fetchStockHistory, fetchLiveQuotes } from '../modules/dataService'

const PERIODS = [
  { label: '3 Days (1h)', barSize: '1 hour', duration: '3 D' },
  { label: '1 Week (1h)', barSize: '1 hour', duration: '1 W' },
  { label: '1 Month (1d)', barSize: '1 day', duration: '1 M' },
  { label: '3 Months (1d)', barSize: '1 day', duration: '3 M' },
  { label: '1 Year (1d)', barSize: '1 day', duration: '1 Y' }
]

const OPTION_COLORS = [
  '#4FC3F7', '#81C784', '#FFB74D', '#F06292', '#CE93D8',
  '#80DEEA', '#A5D6A7', '#FFCC80', '#EF9A9A', '#B39DDB'
]

const UNDERLYING_COLOR = '#FFFFFF'
const UNDERLYING_SERIES_ID = '__underlying__'

function formatExpiration(exp) {
  if (exp.length === 8) return `${exp.slice(0, 4)}-${exp.slice(4, 6)}-${exp.slice(6, 8)}`
  return exp
}

function toChartData(bars) {
  return bars
    .filter(d => d.close != null && d.close > 0)
    .map(d => ({ time: Math.floor(d.timestamp / 1000), value: d.close }))
    .sort((a, b) => a.time - b.time)
}

export default function OptionsAnalysis({ isConnected }) {
  const [symbol, setSymbol] = useState('')
  const [inputSymbol, setInputSymbol] = useState('')
  const [params, setParams] = useState(null)
  const [paramsLoading, setParamsLoading] = useState(false)
  const [paramsError, setParamsError] = useState(null)

  const [expiration, setExpiration] = useState('')
  const [right, setRight] = useState('C')
  const [selectedStrikes, setSelectedStrikes] = useState([])
  const [priceRef, setPriceRef] = useState('')
  const [periodIndex, setPeriodIndex] = useState(0)

  const [neighbors, setNeighbors] = useState(5)
  const [beyondInterval, setBeyondInterval] = useState(5)
  const [maxCount, setMaxCount] = useState(30)

  const [lines, setLines] = useState([]) // option lines
  const [underlyingLoaded, setUnderlyingLoaded] = useState(false)
  const [adding, setAdding] = useState(false)
  const [addError, setAddError] = useState(null)
  const [loadStatus, setLoadStatus] = useState('')

  const [presets, setPresets] = useState(() => {
    try { return JSON.parse(localStorage.getItem('options_presets') || '[]') } catch { return [] }
  })
  const [presetName, setPresetName] = useState('')
  const pendingRestoreRef = useRef(null) // preset fields to restore after params load

  const chartContainerRef = useRef(null)
  const chartRef = useRef(null)
  const seriesMapRef = useRef(new Map()) // id -> series
  const tooltipRef = useRef(null)
  const linesRef = useRef([]) // always-current copy of lines for crosshair callback
  const underlyingDataRef = useRef([]) // sorted underlying bars for nearest-time lookup
  const barSizeRef = useRef('1 hour') // current bar size for crosshair tolerance
  const shiftHeldRef = useRef(false)

  const atmStrike = (() => {
    const ref = parseFloat(priceRef)
    if (!ref || isNaN(ref) || !params?.strikes?.length) return null
    return params.strikes.reduce((closest, s) =>
      Math.abs(s - ref) < Math.abs(closest - ref) ? s : closest
    )
  })()

  // Build visible strikes: N neighbors each side, then every `beyondInterval`th, capped at maxCount
  const visibleStrikes = (() => {
    if (!params?.strikes) return []
    const sorted = [...params.strikes].sort((a, b) => a - b)
    const ref = parseFloat(priceRef)
    if (!ref || isNaN(ref)) return sorted.slice(0, maxCount)

    const atmIdx = sorted.reduce((best, s, i) =>
      Math.abs(s - ref) < Math.abs(sorted[best] - ref) ? i : best, 0)

    const indices = new Set([atmIdx])
    for (let d = 1; d <= neighbors; d++) {
      if (atmIdx - d >= 0) indices.add(atmIdx - d)
      if (atmIdx + d < sorted.length) indices.add(atmIdx + d)
    }
    if (beyondInterval > 0) {
      for (let mult = 1; indices.size < maxCount; mult++) {
        const lo = atmIdx - neighbors - mult * beyondInterval
        const hi = atmIdx + neighbors + mult * beyondInterval
        let added = false
        if (lo >= 0) { indices.add(lo); added = true }
        if (hi < sorted.length) { indices.add(hi); added = true }
        if (!added) break
      }
    }
    return [...indices].sort((a, b) => a - b).map(i => sorted[i]).slice(0, maxCount)
  })()

  // Init chart with dual price scales
  useEffect(() => {
    if (!chartContainerRef.current) return

    const chart = createChart(chartContainerRef.current, {
      layout: { background: { color: '#1A202C' }, textColor: '#A0AEC0' },
      grid: { vertLines: { color: '#2D3748' }, horzLines: { visible: false } },
      crosshair: { vertLine: { color: '#4A5568' }, horzLine: { visible: false, labelVisible: false } },
      leftPriceScale: { visible: true, borderColor: '#4A5568' },
      rightPriceScale: { visible: true, borderColor: '#4A5568' },
      timeScale: { borderColor: '#4A5568', timeVisible: true },
      width: chartContainerRef.current.clientWidth,
      height: 420
    })

    chartRef.current = chart

    chart.subscribeCrosshairMove((param) => {
      const tooltip = tooltipRef.current
      if (!tooltip) return

      if (!param.time || !param.point || param.point.x < 0 || param.point.y < 0 || shiftHeldRef.current) {
        tooltip.style.display = 'none'
        return
      }

      // Nearest-time lookup for underlying (avoids gaps causing null)
      let underlyingPrice = null
      const uData = underlyingDataRef.current
      if (uData.length > 0) {
        let lo = 0, hi = uData.length - 1, best = 0
        while (lo <= hi) {
          const mid = (lo + hi) >> 1
          if (Math.abs(uData[mid].time - param.time) < Math.abs(uData[best].time - param.time)) best = mid
          if (uData[mid].time < param.time) lo = mid + 1
          else hi = mid - 1
        }
        underlyingPrice = uData[best].value
      }

      const rows = []
      for (const line of linesRef.current) {
        const series = seriesMapRef.current.get(line.id)
        if (!series) continue

        let optPrice = null
        const exact = param.seriesData.get(series)
        if (exact) {
          optPrice = exact.value
        } else if (line.chartData?.length > 0) {
          // nearest-time fallback — covers overnight gaps for hourly data (~17h) and weekends for daily
          const tolerance = barSizeRef.current === '1 day' ? 4 * 86400 : 20 * 3600
          const cd = line.chartData
          let lo = 0, hi = cd.length - 1, best = 0
          while (lo <= hi) {
            const mid = (lo + hi) >> 1
            if (Math.abs(cd[mid].time - param.time) < Math.abs(cd[best].time - param.time)) best = mid
            if (cd[mid].time < param.time) lo = mid + 1
            else hi = mid - 1
          }
          if (Math.abs(cd[best].time - param.time) <= tolerance) optPrice = cd[best].value
        }
        if (optPrice === null) continue

        const intrinsic = underlyingPrice !== null
          ? Math.max(0, line.right === 'C' ? underlyingPrice - line.strike : line.strike - underlyingPrice)
          : null
        rows.push({ ...line, optPrice, intrinsic, extrinsic: intrinsic !== null ? optPrice - intrinsic : null })
      }

      if (rows.length === 0) { tooltip.style.display = 'none'; return }

      // Sort by strike ascending
      rows.sort((a, b) => a.strike - b.strike)

      const dateStr = new Date(param.time * 1000).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })

      const fmt = (n) => n >= 1000 ? `$${(n/1000).toFixed(1)}k` : `$${n.toFixed(0)}`
      const shares100Cost = underlyingPrice !== null ? underlyingPrice * 100 : null

      let html = `<div style="color:#718096;font-size:12px;margin-bottom:5px">${dateStr}</div>`
      if (underlyingPrice !== null) {
        html += `<div style="color:#63B3ED;font-size:14px;font-weight:600;margin-bottom:2px">Stock $${underlyingPrice.toFixed(2)}</div>`
        html += `<div style="color:#718096;font-size:12px;margin-bottom:6px">100 shares = <b style="color:#A0AEC0">${fmt(shares100Cost)}</b></div>`
      }

      // Header row
      const cols = underlyingPrice !== null
        ? '60px 48px 48px 68px 44px'
        : '60px 48px 48px'
      html += `<div style="display:grid;grid-template-columns:${cols};gap:3px;color:#718096;font-size:11px;font-weight:600;margin-bottom:3px">
        <span>Strike</span><span style="text-align:right">Prem</span><span style="text-align:right">Δ prev</span>${underlyingPrice !== null ? '<span style="text-align:right">Overpay</span><span style="text-align:right">Prem%</span>' : ''}
      </div>`

      const mktCost = underlyingPrice !== null ? underlyingPrice * 100 : null

      for (let i = 0; i < rows.length; i++) {
        const row = rows[i]
        const diff = i > 0 ? row.optPrice - rows[i - 1].optPrice : null
        const diffStr = diff !== null
          ? `<span style="color:${diff >= 0 ? '#68D391' : '#FC8181'}">${diff >= 0 ? '+' : ''}${diff.toFixed(2)}</span>`
          : '<span style="color:#4A5568">—</span>'
        const strikeCost = row.strike * 100
        const premCost = row.optPrice * 100
        const totalRealizeCost = strikeCost + premCost
        const costDiff = mktCost !== null ? totalRealizeCost - mktCost : null
        const diffColor = costDiff === null ? '#A0AEC0' : (costDiff > 0 ? '#FC8181' : '#68D391')

        html += `<div style="display:grid;grid-template-columns:${cols};gap:3px;font-size:13px;padding:3px 0;border-top:1px solid #2D3748;align-items:center">
          <span style="color:${row.color};font-weight:600">$${row.strike}${row.right}</span>
          <span style="text-align:right">$${row.optPrice.toFixed(2)}</span>
          <span style="text-align:right">${diffStr}</span>
          ${underlyingPrice !== null ? `
          <span style="text-align:right"><span style="color:${diffColor};font-weight:600">${costDiff >= 0 ? '+' : ''}${fmt(Math.abs(costDiff))}</span><br><span style="color:#718096;font-size:11px">+${fmt(premCost)} prem</span></span>
          <span style="text-align:right;color:#A0AEC0">${costDiff !== null && costDiff !== 0 ? ((premCost / Math.abs(costDiff)) * 100).toFixed(1) + '%' : '—'}</span>` : ''}
        </div>`
      }

      // Max spread
      if (rows.length > 1) {
        const maxP = Math.max(...rows.map(r => r.optPrice))
        const minP = Math.min(...rows.map(r => r.optPrice))
        html += `<div style="margin-top:6px;padding-top:5px;border-top:1px solid #4A5568;font-size:12px;color:#A0AEC0">
          Strike spread: <b style="color:#E2E8F0">$${(maxP - minP).toFixed(2)}</b>
        </div>`
      }

      tooltip.innerHTML = html
      tooltip.style.display = 'block'

      const containerWidth = chartContainerRef.current?.clientWidth ?? 600
      const containerHeight = chartContainerRef.current?.clientHeight ?? 420
      const tw = underlyingPrice !== null ? 360 : 180
      const th = tooltip.offsetHeight

      const left = param.point.x + tw + 20 > containerWidth ? param.point.x - tw - 10 : param.point.x + 15
      const top = Math.min(Math.max(0, param.point.y - 10), containerHeight - th - 4)

      tooltip.style.left = `${left}px`
      tooltip.style.top = `${top}px`
    })

    const ro = new ResizeObserver(entries => {
      if (entries[0]) chart.applyOptions({ width: entries[0].contentRect.width })
    })
    ro.observe(chartContainerRef.current)

    const lastMousePos = { x: 0, y: 0 }

    const onMouseMove = (e) => {
      lastMousePos.x = e.clientX
      lastMousePos.y = e.clientY
      shiftHeldRef.current = e.shiftKey
      if (e.shiftKey && tooltipRef.current) tooltipRef.current.style.display = 'none'
    }

    const onKeyDown = (e) => {
      if (e.key === 'Shift' && tooltipRef.current) tooltipRef.current.style.display = 'none'
      shiftHeldRef.current = e.shiftKey
    }

    const onKeyUp = (e) => {
      shiftHeldRef.current = e.shiftKey
      if (!e.shiftKey && chartContainerRef.current) {
        chartContainerRef.current.dispatchEvent(new MouseEvent('mousemove', {
          bubbles: true, clientX: lastMousePos.x, clientY: lastMousePos.y
        }))
      }
    }

    chartContainerRef.current.addEventListener('mousemove', onMouseMove)
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('keyup', onKeyUp)

    return () => {
      chartContainerRef.current?.removeEventListener('mousemove', onMouseMove)
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('keyup', onKeyUp)
      ro.disconnect()
      chart.remove()
      chartRef.current = null
      seriesMapRef.current.clear()
    }
  }, [])

  useEffect(() => { linesRef.current = lines }, [lines])

  // Sync lines → chart series
  useEffect(() => {
    if (!chartRef.current) return
    const chart = chartRef.current
    const existingIds = new Set(seriesMapRef.current.keys())
    const allIds = new Set([...lines.map(l => l.id), ...(underlyingLoaded ? [UNDERLYING_SERIES_ID] : [])])

    // Remove stale series
    for (const id of existingIds) {
      if (!allIds.has(id)) {
        chart.removeSeries(seriesMapRef.current.get(id))
        seriesMapRef.current.delete(id)
      }
    }

    // Add new option series
    for (const line of lines) {
      if (!seriesMapRef.current.has(line.id)) {
        const series = chart.addSeries(LineSeries, {
          color: line.color,
          lineWidth: 2,
          title: line.label,
          priceScaleId: 'right',
          priceLineVisible: false,
          lastValueVisible: false
        })
        series.setData(toChartData(line.data))
        seriesMapRef.current.set(line.id, series)
      }
    }

    if (lines.length > 0 || underlyingLoaded) {
      chart.timeScale().fitContent()
    }
  }, [lines, underlyingLoaded])

  const addUnderlyingSeries = (data) => {
    if (!chartRef.current || seriesMapRef.current.has(UNDERLYING_SERIES_ID)) return
    const series = chartRef.current.addSeries(LineSeries, {
      color: UNDERLYING_COLOR,
      lineWidth: 1,
      title: symbol,
      priceScaleId: 'left',
      priceLineVisible: false,
      lastValueVisible: false
    })
    const chartData = toChartData(data)
    series.setData(chartData)
    underlyingDataRef.current = chartData.slice().sort((a, b) => a.time - b.time)
    seriesMapRef.current.set(UNDERLYING_SERIES_ID, series)
    setUnderlyingLoaded(true)
    chartRef.current.timeScale().fitContent()
  }

  const handleLoadParams = async () => {
    if (!inputSymbol.trim()) return
    setParamsLoading(true)
    setParamsError(null)
    setParams(null)
    setExpiration('')
    setSelectedStrikes([])
    setLines([])
    setUnderlyingLoaded(false)
    if (seriesMapRef.current.size > 0 && chartRef.current) {
      for (const [, s] of seriesMapRef.current) chartRef.current.removeSeries(s)
      seriesMapRef.current.clear()
    }
    try {
      const data = await fetchOptionsParams(inputSymbol.trim())
      if (!data?.expirations?.length) {
        setParamsError('No options data found. Check symbol and IBKR connection.')
      } else {
        const sym = inputSymbol.trim().toUpperCase()
        setParams(data)
        setSymbol(sym)
        setSelectedStrikes([])

        const restore = pendingRestoreRef.current
        pendingRestoreRef.current = null

        if (restore) {
          const validExp = data.expirations.includes(restore.expiration)
            ? restore.expiration : data.expirations[0]
          setExpiration(validExp)
          setRight(restore.right ?? 'C')
          setSelectedStrikes(restore.selectedStrikes.filter(s => data.strikes.includes(s)))
          setPeriodIndex(restore.periodIndex)
          setNeighbors(restore.neighbors)
          setBeyondInterval(restore.beyondInterval)
          setMaxCount(restore.maxCount)
        } else {
          setExpiration(data.expirations[0])
        }

        // Auto-fetch price: live if market open, last close otherwise
        fetchLiveQuotes([sym]).then(async quotes => {
          const q = quotes[sym]
          const price = q?.last || q?.prevClose
          if (price) {
            setPriceRef(String(price))
          } else {
            const bars = await fetchStockHistory(sym, '1 day', '5 D')
            if (bars.length > 0) {
              const last = bars.sort((a, b) => b.timestamp - a.timestamp)[0]
              setPriceRef(String(last.close))
            }
          }
        }).catch(() => {})
      }
    } catch (err) {
      setParamsError(err.message || 'Failed to load options parameters')
    } finally {
      setParamsLoading(false)
    }
  }

  const handleLoadChart = async () => {
    if (!symbol || !expiration || selectedStrikes.length === 0) return
    setAdding(true)
    setAddError(null)

    // Clear chart
    setLines([])
    setUnderlyingLoaded(false)
    if (chartRef.current) {
      for (const [, s] of seriesMapRef.current) chartRef.current.removeSeries(s)
      seriesMapRef.current.clear()
    }

    const period = PERIODS[periodIndex]
    barSizeRef.current = period.barSize

    const newLines = []
    let errors = 0

    for (let i = 0; i < selectedStrikes.length; i++) {
      const s = selectedStrikes[i]
      const label = `$${s} ${right}`
      setLoadStatus(`(${i + 1}/${selectedStrikes.length}) Fetching ${label}...`)
      const t0 = Date.now()
      try {
        const data = await fetchOptionHistory(
          symbol, expiration, parseFloat(s), right,
          period.barSize, period.duration,
          params?.exchange, params?.multiplier
        )
        const elapsed = ((Date.now() - t0) / 1000).toFixed(1)
        if (data.length > 0) {
          console.log(`[Options] ${label}: ${data.length} bars in ${elapsed}s`)
          const id = `${symbol}-${expiration}-${s}-${right}`
          const color = OPTION_COLORS[i % OPTION_COLORS.length]
          const lineLabel = `${symbol} ${formatExpiration(expiration)} $${s}${right}`
          const chartData = toChartData(data)
          newLines.push({ id, label: lineLabel, color, data, chartData, strike: parseFloat(s), right })
        } else {
          console.log(`[Options] ${label}: no data (${elapsed}s)`)
          errors++
        }
      } catch (err) {
        const elapsed = ((Date.now() - t0) / 1000).toFixed(1)
        console.log(`[Options] ${label}: error after ${elapsed}s —`, err.message)
        errors++
      }
    }

    setLoadStatus('')

    if (newLines.length === 0) {
      setAddError(`No historical data returned for any of the selected contracts.`)
    } else if (errors > 0) {
      setAddError(`${errors} of ${selectedStrikes.length} contract${errors > 1 ? 's' : ''} had no data and were skipped.`)
    }

    setLines(newLines)

    if (newLines.length > 0) {
      fetchStockHistory(symbol, period.barSize, period.duration).then(data => {
        if (data.length > 0) addUnderlyingSeries(data)
      })
    }

    setAdding(false)
  }

  const handleRemoveLine = (id) => {
    setLines(prev => {
      const next = prev.filter(l => l.id !== id)
      if (next.length === 0 && underlyingLoaded && chartRef.current) {
        const s = seriesMapRef.current.get(UNDERLYING_SERIES_ID)
        if (s) chartRef.current.removeSeries(s)
        seriesMapRef.current.delete(UNDERLYING_SERIES_ID)
        setUnderlyingLoaded(false)
      }
      return next
    })
  }

  const handleStrikeToggle = (strike) => {
    setSelectedStrikes(prev =>
      prev.includes(strike) ? prev.filter(s => s !== strike) : [...prev, strike]
    )
  }

  const handleSelectAll = () => setSelectedStrikes([...visibleStrikes])
  const handleClearStrikes = () => setSelectedStrikes([])

  const savePresets = (updated) => {
    setPresets(updated)
    localStorage.setItem('options_presets', JSON.stringify(updated))
  }

  const handleSavePreset = () => {
    const name = presetName.trim()
    if (!name || !symbol) return
    const entry = {
      name,
      symbol,
      expiration,
      right,
      selectedStrikes,
      periodIndex,
      neighbors,
      beyondInterval,
      maxCount,
      savedAt: Date.now()
    }
    savePresets([...presets.filter(p => p.name !== name), entry])
    setPresetName('')
  }

  const handleLoadPreset = (name) => {
    const preset = presets.find(p => p.name === name)
    if (!preset) return
    pendingRestoreRef.current = preset
    setInputSymbol(preset.symbol)
    // Trigger load params — reuse handleLoadParams but with preset symbol
    const syntheticLoad = async () => {
      setParamsLoading(true)
      setParamsError(null)
      setParams(null)
      setExpiration('')
      setSelectedStrikes([])
      setLines([])
      setUnderlyingLoaded(false)
      if (seriesMapRef.current.size > 0 && chartRef.current) {
        for (const [, s] of seriesMapRef.current) chartRef.current.removeSeries(s)
        seriesMapRef.current.clear()
      }
      try {
        const data = await fetchOptionsParams(preset.symbol)
        if (!data?.expirations?.length) {
          setParamsError('No options data found. Check symbol and IBKR connection.')
          pendingRestoreRef.current = null
        } else {
          setParams(data)
          setSymbol(preset.symbol)
          const validExp = data.expirations.includes(preset.expiration)
            ? preset.expiration : data.expirations[0]
          setExpiration(validExp)
          setRight(preset.right ?? 'C')
          setSelectedStrikes(preset.selectedStrikes.filter(s => data.strikes.includes(s)))
          setPeriodIndex(preset.periodIndex)
          setNeighbors(preset.neighbors)
          setBeyondInterval(preset.beyondInterval)
          setMaxCount(preset.maxCount)
          pendingRestoreRef.current = null
          fetchLiveQuotes([preset.symbol]).then(async quotes => {
            const q = quotes[preset.symbol]
            const price = q?.last || q?.prevClose
            if (price) {
              setPriceRef(String(price))
            } else {
              const bars = await fetchStockHistory(preset.symbol, '1 day', '5 D')
              if (bars.length > 0) {
                const last = bars.sort((a, b) => b.timestamp - a.timestamp)[0]
                setPriceRef(String(last.close))
              }
            }
          }).catch(() => {})
        }
      } catch (err) {
        setParamsError(err.message || 'Failed to load options parameters')
        pendingRestoreRef.current = null
      } finally {
        setParamsLoading(false)
      }
    }
    syntheticLoad()
  }

  const handleDeletePreset = (name) => {
    savePresets(presets.filter(p => p.name !== name))
  }

  return (
    <VStack align="stretch" spacing={4}>
      {/* Symbol loader */}
      <HStack spacing={3} wrap="wrap" align="flex-end">
        <FormControl w="auto">
          <FormLabel fontSize="sm" color="gray.400" mb={1}>Underlying Symbol</FormLabel>
          <HStack>
            <Input
              value={inputSymbol}
              onChange={e => setInputSymbol(e.target.value.toUpperCase())}
              onKeyDown={e => e.key === 'Enter' && handleLoadParams()}
              placeholder="e.g. SPY"
              w="110px"
              bg="gray.700"
              border="1px solid"
              borderColor="gray.600"
              _focus={{ borderColor: 'blue.400' }}
              fontSize="sm"
            />
            <Button
              onClick={handleLoadParams}
              isLoading={paramsLoading}
              loadingText="Loading..."
              isDisabled={!inputSymbol.trim() || !isConnected}
              colorScheme="blue"
              size="sm"
            >
              Load
            </Button>
            <Popover placement="bottom-start" isLazy>
              <PopoverTrigger>
                <IconButton
                  icon={<StarIcon />}
                  aria-label="Presets"
                  size="sm"
                  variant="outline"
                  colorScheme={presets.length > 0 ? 'yellow' : 'gray'}
                />
              </PopoverTrigger>
              <PopoverContent bg="gray.800" borderColor="gray.600" w="260px">
                <PopoverArrow bg="gray.800" />
                <PopoverBody>
                  <VStack align="stretch" spacing={3}>
                    <VStack align="stretch" spacing={1}>
                      <Text fontSize="xs" color="gray.400" fontWeight="bold">SAVE PRESET</Text>
                      <HStack>
                        <Input
                          size="sm"
                          placeholder="Name..."
                          value={presetName}
                          onChange={e => setPresetName(e.target.value)}
                          onKeyDown={e => e.key === 'Enter' && handleSavePreset()}
                          bg="gray.700"
                          borderColor="gray.600"
                          _focus={{ borderColor: 'blue.400' }}
                          fontSize="sm"
                        />
                        <Button
                          size="sm"
                          colorScheme="purple"
                          isDisabled={!presetName.trim() || !symbol}
                          onClick={handleSavePreset}
                          flexShrink={0}
                        >
                          Save
                        </Button>
                      </HStack>
                    </VStack>
                    {presets.length > 0 && (
                      <VStack align="stretch" spacing={1}>
                        <Text fontSize="xs" color="gray.400" fontWeight="bold">PRESETS</Text>
                        {presets.map(p => (
                          <HStack key={p.name} justify="space-between">
                            <Text
                              fontSize="sm"
                              color="gray.200"
                              cursor="pointer"
                              _hover={{ color: 'blue.300' }}
                              onClick={() => handleLoadPreset(p.name)}
                              flex={1}
                              noOfLines={1}
                            >
                              {p.name}
                            </Text>
                            <Text fontSize="xs" color="gray.500" flexShrink={0}>{p.symbol}</Text>
                            <Button
                              size="xs"
                              variant="ghost"
                              color="red.400"
                              _hover={{ color: 'red.200' }}
                              onClick={() => handleDeletePreset(p.name)}
                              flexShrink={0}
                              minW="auto"
                              px={1}
                            >✕</Button>
                          </HStack>
                        ))}
                      </VStack>
                    )}
                    {presets.length === 0 && (
                      <Text fontSize="xs" color="gray.500">No presets saved yet.</Text>
                    )}
                  </VStack>
                </PopoverBody>
              </PopoverContent>
            </Popover>
          </HStack>
        </FormControl>

        {!isConnected && (
          <Alert status="warning" borderRadius="md" py={2} px={3} w="auto">
            <AlertIcon />
            <Text fontSize="sm">Not connected to IBKR</Text>
          </Alert>
        )}

        {params && (
          <Text fontSize="sm" color="gray.400" alignSelf="flex-end" pb={1}>
            {params.expirations.length} expirations · {params.strikes.length} strikes · {params.exchange}
          </Text>
        )}
      </HStack>

      {paramsError && (
        <Alert status="error" borderRadius="md">
          <AlertIcon />
          <Text fontSize="sm">{paramsError}</Text>
        </Alert>
      )}

      {/* Contract selectors */}
      {params && (
        <>
          <Divider borderColor="gray.600" />
          <HStack spacing={4} wrap="wrap" align="flex-start">
            {/* Left column: expiration + type + period + button */}
            <VStack align="stretch" spacing={3}>
              <HStack spacing={3} align="flex-end">
                <FormControl w="auto">
                  <FormLabel fontSize="sm" color="gray.400" mb={1}>Expiration</FormLabel>
                  <Select
                    value={expiration}
                    onChange={e => { setExpiration(e.target.value); setSelectedStrikes(prev => prev.filter(s => params.strikes.includes(s))) }}
                    bg="gray.700"
                    borderColor={expiration && (new Date(expiration.replace(/(\d{4})(\d{2})(\d{2})/, '$1-$2-$3')) - new Date()) / 86400000 < 7 ? 'orange.400' : 'gray.600'}
                    fontSize="sm"
                    w="155px"
                  >
                    {params.expirations.map(exp => (
                      <option key={exp} value={exp}>{formatExpiration(exp)}</option>
                    ))}
                  </Select>
                  {expiration && (new Date(expiration.replace(/(\d{4})(\d{2})(\d{2})/, '$1-$2-$3')) - new Date()) / 86400000 < 7 && (
                    <Text fontSize="xs" color="orange.400" mt={1}>Expiring soon — IBKR may have no data</Text>
                  )}
                </FormControl>

                <FormControl w="auto">
                  <FormLabel fontSize="sm" color="gray.400" mb={1}>Type</FormLabel>
                  <ButtonGroup size="sm" isAttached>
                    <Button
                      onClick={() => { setRight('C'); setSelectedStrikes([]) }}
                      colorScheme={right === 'C' ? 'blue' : 'gray'}
                      variant={right === 'C' ? 'solid' : 'outline'}
                    >Call</Button>
                    <Button
                      onClick={() => { setRight('P'); setSelectedStrikes([]) }}
                      colorScheme={right === 'P' ? 'blue' : 'gray'}
                      variant={right === 'P' ? 'solid' : 'outline'}
                    >Put</Button>
                  </ButtonGroup>
                </FormControl>

                <FormControl w="auto">
                  <FormLabel fontSize="sm" color="gray.400" mb={1}>Period</FormLabel>
                  <Select
                    value={periodIndex}
                    onChange={e => setPeriodIndex(Number(e.target.value))}
                    bg="gray.700"
                    borderColor="gray.600"
                    fontSize="sm"
                    w="165px"
                  >
                    {PERIODS.map((p, i) => (
                      <option key={i} value={i}>{p.label}</option>
                    ))}
                  </Select>
                </FormControl>
              </HStack>

              <HStack spacing={2} alignSelf="flex-start">
                <Button
                  onClick={handleLoadChart}
                  isLoading={adding}
                  loadingText={loadStatus || 'Loading...'}
                  isDisabled={selectedStrikes.length === 0 || !expiration}
                  colorScheme="green"
                  size="sm"
                >
                  Load Chart
                </Button>
                <Popover placement="right-start" isLazy>
                  <PopoverTrigger>
                    <IconButton
                      icon={<InfoOutlineIcon />}
                      aria-label="Help"
                      size="sm"
                      variant="ghost"
                      color="gray.400"
                      _hover={{ color: 'blue.300' }}
                    />
                  </PopoverTrigger>
                  <PopoverContent bg="gray.800" borderColor="gray.600" w="340px" maxH="480px" overflowY="auto">
                    <PopoverArrow bg="gray.800" />
                    <PopoverBody p={4}>
                      <VStack align="stretch" spacing={4} fontSize="sm">
                        <Box>
                          <Text fontWeight="bold" color="blue.300" mb={2}>Chart Lines</Text>
                          <VStack align="stretch" spacing={1}>
                            <Text color="gray.300"><Text as="span" color="white" fontWeight="600">White line</Text> — underlying stock price (left axis)</Text>
                            <Text color="gray.300"><Text as="span" color="white" fontWeight="600">Colored lines</Text> — option premium for each selected strike (right axis)</Text>
                          </VStack>
                        </Box>
                        <Box>
                          <Text fontWeight="bold" color="blue.300" mb={2}>Crosshair Tooltip Columns</Text>
                          <VStack align="stretch" spacing={2}>
                            <Box>
                              <Text color="white" fontWeight="600">Prem</Text>
                              <Text color="gray.400">Option premium — the market price of the contract at that moment. Multiply by 100 for cost of one contract.</Text>
                            </Box>
                            <Box>
                              <Text color="white" fontWeight="600">Δ prev</Text>
                              <Text color="gray.400">Difference in premium vs the strike directly below. Shows how premium changes per strike increment.</Text>
                            </Box>
                            <Box>
                              <Text color="white" fontWeight="600">Overpay</Text>
                              <Text color="gray.400">How much more you would spend to get 100 shares via option exercise vs buying at the current stock price. Calculated as (strike + premium − stock price) × 100. Includes the premium cost so it reflects the true total outlay.</Text>
                            </Box>
                            <Box>
                              <Text color="white" fontWeight="600">Prem%</Text>
                              <Text color="gray.400">Premium as a percentage of Overpay. High % means most of the extra cost is time value; low % means the strike is deeply OTM and the premium is a small portion of the gap.</Text>
                            </Box>
                          </VStack>
                        </Box>
                        <Box>
                          <Text fontWeight="bold" color="blue.300" mb={2}>Strike Picker Controls</Text>
                          <VStack align="stretch" spacing={2}>
                            <Box>
                              <Text color="white" fontWeight="600">Neighbors</Text>
                              <Text color="gray.400">Number of strikes shown on each side of the ATM strike.</Text>
                            </Box>
                            <Box>
                              <Text color="white" fontWeight="600">Step beyond</Text>
                              <Text color="gray.400">After the neighbors range, show every Nth strike further out.</Text>
                            </Box>
                            <Box>
                              <Text color="white" fontWeight="600">Max</Text>
                              <Text color="gray.400">Maximum total strikes shown in the picker.</Text>
                            </Box>
                          </VStack>
                        </Box>
                      </VStack>
                    </PopoverBody>
                  </PopoverContent>
                </Popover>
              </HStack>
            </VStack>

            {/* Right column: strike picker */}
            <VStack align="stretch" spacing={2} flex={1} minW="220px">
              {/* Settings row */}
              <HStack spacing={3} wrap="wrap">
                <HStack spacing={1} align="center">
                  <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Neighbors</Text>
                  <Input
                    type="number"
                    size="xs"
                    w="48px"
                    value={neighbors}
                    onChange={e => setNeighbors(Math.max(0, parseInt(e.target.value) || 0))}
                    bg="gray.700"
                    borderColor="gray.600"
                    textAlign="center"
                    fontSize="xs"
                  />
                </HStack>
                <HStack spacing={1} align="center">
                  <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Step beyond</Text>
                  <Input
                    type="number"
                    size="xs"
                    w="48px"
                    value={beyondInterval}
                    onChange={e => setBeyondInterval(Math.max(1, parseInt(e.target.value) || 1))}
                    bg="gray.700"
                    borderColor="gray.600"
                    textAlign="center"
                    fontSize="xs"
                  />
                </HStack>
                <HStack spacing={1} align="center">
                  <Text fontSize="xs" color="gray.400" whiteSpace="nowrap">Max</Text>
                  <Input
                    type="number"
                    size="xs"
                    w="48px"
                    value={maxCount}
                    onChange={e => setMaxCount(Math.max(1, parseInt(e.target.value) || 1))}
                    bg="gray.700"
                    borderColor="gray.600"
                    textAlign="center"
                    fontSize="xs"
                  />
                </HStack>
              </HStack>

              <HStack justify="space-between">
                <HStack spacing={2}>
                  <Text fontSize="xs" color="gray.400">
                    Strikes {visibleStrikes.length < params.strikes.length && `(${visibleStrikes.length} of ${params.strikes.length})`}
                  </Text>
                  {priceRef && (
                    <Badge colorScheme="blue" fontSize="xs">@ {parseFloat(priceRef).toFixed(2)}</Badge>
                  )}
                </HStack>
                <HStack spacing={2}>
                  <Button size="xs" variant="ghost" color="blue.300" onClick={handleSelectAll}>All</Button>
                  <Button size="xs" variant="ghost" color="gray.400" onClick={handleClearStrikes}>Clear</Button>
                </HStack>
              </HStack>
              <Box
                bg="gray.800"
                border="1px solid"
                borderColor="gray.600"
                borderRadius="md"
                maxH="140px"
                overflowY="auto"
                p={2}
              >
                {visibleStrikes.length === 0 ? (
                  <Text fontSize="xs" color="gray.500">No strikes match filter</Text>
                ) : (
                  <Wrap spacing={1}>
                    {visibleStrikes.map(s => {
                      const isAtm = s === atmStrike
                      return (
                        <WrapItem key={s}>
                          <Checkbox
                            size="sm"
                            isChecked={selectedStrikes.includes(s)}
                            onChange={() => handleStrikeToggle(s)}
                            colorScheme={isAtm ? 'yellow' : 'blue'}
                          >
                            <Text
                              fontSize="xs"
                              fontWeight={isAtm ? 'bold' : 'normal'}
                              color={isAtm ? 'yellow.300' : 'inherit'}
                            >
                              {s}
                            </Text>
                          </Checkbox>
                        </WrapItem>
                      )
                    })}
                  </Wrap>
                )}
              </Box>
              <Text fontSize="xs" color="gray.500">
                {selectedStrikes.length} selected
              </Text>
            </VStack>
          </HStack>
        </>
      )}

      {addError && (
        <Alert status="warning" borderRadius="md">
          <AlertIcon />
          <Text fontSize="sm">{addError}</Text>
        </Alert>
      )}

      {/* Legend */}
      {(lines.length > 0 || underlyingLoaded) && (
        <Wrap spacing={2}>
          {underlyingLoaded && (
            <WrapItem>
              <Tag size="sm" borderRadius="full" bg="#FFFFFF22" border="1px solid" borderColor={UNDERLYING_COLOR} color={UNDERLYING_COLOR}>
                <Box w="8px" h="8px" borderRadius="full" bg={UNDERLYING_COLOR} mr={1} flexShrink={0} />
                <TagLabel>{symbol} (underlying)</TagLabel>
              </Tag>
            </WrapItem>
          )}
          {lines.map(line => (
            <WrapItem key={line.id}>
              <Tag size="sm" borderRadius="full" bg={line.color + '22'} border="1px solid" borderColor={line.color} color={line.color}>
                <Box w="8px" h="8px" borderRadius="full" bg={line.color} mr={1} flexShrink={0} />
                <TagLabel>{line.label}</TagLabel>
                <TagCloseButton onClick={() => handleRemoveLine(line.id)} />
              </Tag>
            </WrapItem>
          ))}
        </Wrap>
      )}

      {/* Chart */}
      <Box position="relative" w="100%" h="420px">
        <Box
          ref={chartContainerRef}
          w="100%"
          h="100%"
          borderRadius="md"
          overflow="hidden"
          border="1px solid"
          borderColor="gray.700"
        />
        <div
          ref={tooltipRef}
          style={{
            display: 'none',
            position: 'absolute',
            pointerEvents: 'none',
            zIndex: 10,
            background: 'rgba(26,32,44,0.95)',
            border: '1px solid #4A5568',
            borderRadius: '6px',
            padding: '8px 10px',
            minWidth: '200px',
            maxWidth: '470px',
            fontSize: '13px',
            color: '#E2E8F0',
            boxShadow: '0 4px 12px rgba(0,0,0,0.4)'
          }}
        />
        {lines.length === 0 && (
          <Box
            position="absolute"
            inset={0}
            display="flex"
            alignItems="center"
            justifyContent="center"
            pointerEvents="none"
          >
            <Text color="gray.600" fontSize="sm">
              {params
                ? 'Select strikes and click "Add to Chart"'
                : 'Load a symbol to see available strikes'}
            </Text>
          </Box>
        )}
      </Box>
    </VStack>
  )
}
