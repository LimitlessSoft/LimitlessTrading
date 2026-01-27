import { useState, useEffect } from 'react'
import { HStack, Box, Text, Tooltip } from '@chakra-ui/react'

// Market hours configuration
const MARKETS = {
  NYSE: {
    name: 'NYSE',
    timezone: 'America/New_York',
    preMarketOpen: { hours: 4, minutes: 0 },   // 4:00 AM ET
    marketOpen: { hours: 9, minutes: 30 },      // 9:30 AM ET
    marketClose: { hours: 16, minutes: 0 },     // 4:00 PM ET
    afterHoursClose: { hours: 20, minutes: 0 }, // 8:00 PM ET
    weekendDays: [0, 6] // Sunday, Saturday
  },
  LSE: {
    name: 'LSE',
    timezone: 'Europe/London',
    preMarketOpen: { hours: 5, minutes: 5 },    // 5:05 AM GMT/BST (auction)
    marketOpen: { hours: 8, minutes: 0 },       // 8:00 AM GMT/BST
    marketClose: { hours: 16, minutes: 30 },    // 4:30 PM GMT/BST
    afterHoursClose: { hours: 17, minutes: 15 }, // 5:15 PM GMT/BST (auction close)
    weekendDays: [0, 6]
  }
}

function getMarketStatus(market) {
  const now = new Date()

  // Get current time in the market's timezone
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: market.timezone,
    hour: 'numeric',
    minute: 'numeric',
    second: 'numeric',
    hour12: false,
    weekday: 'short'
  })

  const parts = formatter.formatToParts(now)
  const hours = parseInt(parts.find(p => p.type === 'hour').value)
  const minutes = parseInt(parts.find(p => p.type === 'minute').value)
  const weekday = parts.find(p => p.type === 'weekday').value

  const timeInMinutes = hours * 60 + minutes
  const preMarketMinutes = market.preMarketOpen.hours * 60 + market.preMarketOpen.minutes
  const openMinutes = market.marketOpen.hours * 60 + market.marketOpen.minutes
  const closeMinutes = market.marketClose.hours * 60 + market.marketClose.minutes
  const afterHoursMinutes = market.afterHoursClose.hours * 60 + market.afterHoursClose.minutes

  // Check for weekend
  const dayMap = { 'Sun': 0, 'Mon': 1, 'Tue': 2, 'Wed': 3, 'Thu': 4, 'Fri': 5, 'Sat': 6 }
  const dayNum = dayMap[weekday]

  if (market.weekendDays.includes(dayNum)) {
    return { status: 'Closed', colorScheme: 'gray', description: 'Weekend' }
  }

  // Check market status based on time
  if (timeInMinutes >= openMinutes && timeInMinutes < closeMinutes) {
    return { status: 'Open', colorScheme: 'green', description: 'Market Open' }
  }

  if (timeInMinutes >= preMarketMinutes && timeInMinutes < openMinutes) {
    return { status: 'Pre', colorScheme: 'yellow', description: 'Pre-Market' }
  }

  if (timeInMinutes >= closeMinutes && timeInMinutes < afterHoursMinutes) {
    return { status: 'AH', colorScheme: 'orange', description: 'After Hours' }
  }

  return { status: 'Closed', colorScheme: 'gray', description: 'Market Closed' }
}

function formatTime(timezone) {
  const now = new Date()
  return now.toLocaleTimeString('en-US', {
    timeZone: timezone,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false
  })
}

function MarketClock({ market }) {
  const [time, setTime] = useState('')
  const [status, setStatus] = useState({ status: 'Closed', colorScheme: 'gray', description: '' })

  useEffect(() => {
    const update = () => {
      setTime(formatTime(market.timezone))
      setStatus(getMarketStatus(market))
    }

    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [market])

  const colorMap = {
    green: { bg: 'green.900', border: 'green.500', text: 'green.300' },
    yellow: { bg: 'yellow.900', border: 'yellow.500', text: 'yellow.300' },
    orange: { bg: 'orange.900', border: 'orange.500', text: 'orange.300' },
    gray: { bg: 'gray.800', border: 'gray.600', text: 'gray.400' }
  }

  const colors = colorMap[status.colorScheme]

  return (
    <Tooltip label={status.description} fontSize="xs">
      <HStack
        spacing={2}
        bg={colors.bg}
        borderWidth="1px"
        borderColor={colors.border}
        borderRadius="md"
        px={3}
        py={1}
        cursor="default"
        align="baseline"
      >
        <Text fontSize="xs" fontWeight="bold" color={colors.text} lineHeight="1">
          {market.name}
        </Text>
        <Text fontSize="xs" fontFamily="mono" color={colors.text} lineHeight="1">
          {time}
        </Text>
        <Text fontSize="xs" color={colors.text} opacity={0.8} lineHeight="1">
          {status.status}
        </Text>
      </HStack>
    </Tooltip>
  )
}

function MarketClocks() {
  return (
    <Box
      bg="gray.850"
      borderBottom="1px solid"
      borderColor="gray.700"
      px={4}
      py={1}
    >
      <HStack spacing={4} justify="center">
        <MarketClock market={MARKETS.NYSE} />
        <MarketClock market={MARKETS.LSE} />
      </HStack>
    </Box>
  )
}

export default MarketClocks
