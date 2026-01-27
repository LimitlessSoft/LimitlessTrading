import { useState, useEffect, useRef } from 'react'
import {
  Box,
  Text,
  HStack,
  VStack,
  Tooltip,
  Spinner,
  Popover,
  PopoverTrigger,
  PopoverContent,
  PopoverBody,
  PopoverArrow,
  useDisclosure
} from '@chakra-ui/react'

const API_BASE = 'http://localhost:3001/api'

// SVG Speedometer/Gauge component
function SpeedometerSVG({ score, size = 80 }) {
  // Gauge spans from -135 degrees (fear) to +135 degrees (greed)
  // Score 0 = -135deg, Score 100 = +135deg
  const startAngle = -135
  const endAngle = 135
  const totalAngle = endAngle - startAngle // 270 degrees

  // Calculate needle angle based on score
  const needleAngle = startAngle + (score / 100) * totalAngle

  // SVG dimensions
  const cx = size / 2
  const cy = size / 2
  const radius = size / 2 - 8
  const innerRadius = radius - 10

  // Create arc path for gauge background segments
  const createArc = (startDeg, endDeg, r) => {
    const startRad = (startDeg - 90) * Math.PI / 180
    const endRad = (endDeg - 90) * Math.PI / 180
    const x1 = cx + r * Math.cos(startRad)
    const y1 = cy + r * Math.sin(startRad)
    const x2 = cx + r * Math.cos(endRad)
    const y2 = cy + r * Math.sin(endRad)
    const largeArc = endDeg - startDeg > 180 ? 1 : 0
    return `M ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2}`
  }

  // Gauge segments (fear to greed gradient)
  const segments = [
    { start: -135, end: -81, color: '#C53030' },    // Extreme Fear (red)
    { start: -81, end: -27, color: '#DD6B20' },     // Fear (orange)
    { start: -27, end: 27, color: '#718096' },      // Neutral (gray)
    { start: 27, end: 81, color: '#38A169' },       // Greed (green)
    { start: 81, end: 135, color: '#319795' }       // Extreme Greed (teal)
  ]

  // Needle endpoint
  const needleLength = radius - 15
  const needleRad = (needleAngle - 90) * Math.PI / 180
  const needleX = cx + needleLength * Math.cos(needleRad)
  const needleY = cy + needleLength * Math.sin(needleRad)

  return (
    <svg width={size} height={size * 0.65} viewBox={`0 0 ${size} ${size * 0.65}`}>
      {/* Gauge arc segments */}
      {segments.map((seg, i) => (
        <path
          key={i}
          d={createArc(seg.start, seg.end, radius)}
          fill="none"
          stroke={seg.color}
          strokeWidth="8"
          strokeLinecap="round"
        />
      ))}

      {/* Tick marks */}
      {[0, 25, 50, 75, 100].map((tick) => {
        const tickAngle = startAngle + (tick / 100) * totalAngle
        const tickRad = (tickAngle - 90) * Math.PI / 180
        const outerX = cx + (radius + 2) * Math.cos(tickRad)
        const outerY = cy + (radius + 2) * Math.sin(tickRad)
        const innerX = cx + (radius - 12) * Math.cos(tickRad)
        const innerY = cy + (radius - 12) * Math.sin(tickRad)
        return (
          <line
            key={tick}
            x1={innerX}
            y1={innerY}
            x2={outerX}
            y2={outerY}
            stroke="#4A5568"
            strokeWidth="2"
          />
        )
      })}

      {/* Needle */}
      <line
        x1={cx}
        y1={cy}
        x2={needleX}
        y2={needleY}
        stroke="white"
        strokeWidth="2.5"
        strokeLinecap="round"
      />

      {/* Center dot */}
      <circle cx={cx} cy={cy} r="4" fill="white" />

      {/* Score text */}
      <text
        x={cx}
        y={cy + 18}
        textAnchor="middle"
        fill="white"
        fontSize="14"
        fontWeight="bold"
        fontFamily="monospace"
      >
        {score}
      </text>
    </svg>
  )
}

function FearGreedGauge({ isConnected }) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const fetchData = async () => {
    try {
      const response = await fetch(`${API_BASE}/fear-greed`)
      if (!response.ok) throw new Error('Failed to fetch')
      const result = await response.json()
      if (result.error) {
        setError(result.error)
      } else {
        setData(result)
        setError(null)
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!isConnected) {
      setLoading(false)
      return
    }

    fetchData()
    // Refresh every 5 minutes
    const interval = setInterval(fetchData, 5 * 60 * 1000)
    return () => clearInterval(interval)
  }, [isConnected])

  if (!isConnected) {
    return (
      <HStack spacing={1}>
        <Box opacity={0.5}>
          <SpeedometerSVG score={50} size={60} />
        </Box>
        <Text color="gray.500" fontSize="xs">F&G</Text>
      </HStack>
    )
  }

  if (loading) {
    return (
      <HStack spacing={2}>
        <Spinner size="sm" color="gray.400" />
        <Text color="gray.400" fontSize="xs">F&G</Text>
      </HStack>
    )
  }

  if (error || !data) {
    return (
      <HStack spacing={1}>
        <Box opacity={0.5}>
          <SpeedometerSVG score={50} size={60} />
        </Box>
        <Text color="gray.500" fontSize="xs">--</Text>
      </HStack>
    )
  }

  const getColorScheme = (sentiment) => {
    switch (sentiment) {
      case 'Extreme Fear': return 'red'
      case 'Fear': return 'orange'
      case 'Neutral': return 'gray'
      case 'Greed': return 'green'
      case 'Extreme Greed': return 'teal'
      default: return 'gray'
    }
  }

  const colorScheme = getColorScheme(data.sentiment)

  return (
    <Popover
      isOpen={isOpen}
      onOpen={onOpen}
      onClose={onClose}
      placement="bottom-end"
      trigger="hover"
    >
      <PopoverTrigger>
        <HStack
          spacing={1}
          cursor="pointer"
          _hover={{ opacity: 0.8 }}
          transition="opacity 0.2s"
        >
          <SpeedometerSVG score={data.score} size={60} />
          <VStack spacing={0} align="start">
            <Text
              color={`${colorScheme}.300`}
              fontSize="xs"
              fontWeight="bold"
              lineHeight="1"
            >
              {data.sentiment}
            </Text>
          </VStack>
        </HStack>
      </PopoverTrigger>
      <PopoverContent
        bg="gray.800"
        borderColor="gray.600"
        maxW="280px"
      >
        <PopoverArrow bg="gray.800" />
        <PopoverBody p={3}>
          <VStack spacing={2} align="stretch">
            <HStack justify="space-between">
              <Text color="white" fontWeight="bold" fontSize="sm">
                Fear & Greed Index
              </Text>
              <Text color={`${colorScheme}.300`} fontWeight="bold" fontSize="lg">
                {data.score}
              </Text>
            </HStack>

            {data.components.map((comp, i) => (
              <Box
                key={i}
                p={2}
                bg="gray.700"
                borderRadius="md"
                borderLeft="3px solid"
                borderLeftColor={
                  comp.score <= 25 ? 'red.400' :
                  comp.score <= 45 ? 'orange.400' :
                  comp.score <= 55 ? 'gray.400' :
                  comp.score <= 75 ? 'green.400' : 'teal.400'
                }
              >
                <HStack justify="space-between">
                  <VStack spacing={0} align="start">
                    <Text color="gray.300" fontSize="xs" fontWeight="medium">
                      {comp.name}
                    </Text>
                    <Text color="gray.500" fontSize="xs">
                      {comp.indicator}: {comp.value}
                    </Text>
                  </VStack>
                  <Text
                    color={
                      comp.score <= 25 ? 'red.300' :
                      comp.score <= 45 ? 'orange.300' :
                      comp.score <= 55 ? 'gray.300' :
                      comp.score <= 75 ? 'green.300' : 'teal.300'
                    }
                    fontWeight="bold"
                    fontSize="sm"
                  >
                    {comp.score}
                  </Text>
                </HStack>
              </Box>
            ))}

            <Text color="gray.500" fontSize="xs" textAlign="center">
              Updated: {new Date(data.timestamp).toLocaleTimeString()}
            </Text>
          </VStack>
        </PopoverBody>
      </PopoverContent>
    </Popover>
  )
}

export default FearGreedGauge
