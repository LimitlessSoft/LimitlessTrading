import { useState, useEffect } from 'react'
import { Box, HStack, Text, Badge } from '@chakra-ui/react'
import { getNextFomcDecision, formatFomcLabel, calculateCountdown } from '../modules/fomcService'

function FedCountdown() {
  const [nextDecision, setNextDecision] = useState(null)
  const [countdown, setCountdown] = useState(null)
  const [loading, setLoading] = useState(true)

  // Load next FOMC decision date
  useEffect(() => {
    async function loadNextDecision() {
      try {
        const date = await getNextFomcDecision()
        setNextDecision(date)
      } catch (error) {
        console.error('Failed to load FOMC date:', error)
      } finally {
        setLoading(false)
      }
    }
    loadNextDecision()
  }, [])

  // Update countdown every second
  useEffect(() => {
    if (!nextDecision) return

    const updateCountdown = () => {
      setCountdown(calculateCountdown(nextDecision))
    }

    updateCountdown()
    const interval = setInterval(updateCountdown, 1000)

    return () => clearInterval(interval)
  }, [nextDecision])

  if (loading || !nextDecision || !countdown) {
    return null
  }

  const isPast = countdown.totalMs <= 0
  if (isPast) {
    return null
  }

  // Urgency levels based on days remaining
  const daysRemaining = countdown.totalMs / (24 * 60 * 60 * 1000)
  const urgencyLevel =
    daysRemaining < 1 ? 'critical' :    // < 24 hours - red
    daysRemaining < 5 ? 'high' :        // < 5 days - orange
    daysRemaining < 10 ? 'medium' :     // < 10 days - yellow
    'normal'                            // 10+ days - blue

  const styles = {
    critical: {
      bg: 'red.900',
      borderColor: 'red.500',
      badgeScheme: 'red',
      textColor: 'red.200',
    },
    high: {
      bg: 'orange.900',
      borderColor: 'orange.500',
      badgeScheme: 'orange',
      textColor: 'orange.200',
    },
    medium: {
      bg: 'yellow.900',
      borderColor: 'yellow.500',
      badgeScheme: 'yellow',
      textColor: 'yellow.200',
    },
    normal: {
      bg: 'gray.800',
      borderColor: 'gray.700',
      badgeScheme: 'blue',
      textColor: 'gray.300',
    },
  }

  const style = styles[urgencyLevel]
  const formatNumber = (n) => String(n).padStart(2, '0')

  return (
    <Box
      position="fixed"
      top={0}
      left={0}
      right={0}
      zIndex={1000}
      bg={style.bg}
      borderBottom="2px solid"
      borderColor={style.borderColor}
      px={4}
      py={2}
    >
      <HStack justify="center" spacing={3}>
        <Badge
          colorScheme={style.badgeScheme}
          variant="solid"
          px={3}
          py={1}
          borderRadius="md"
          fontSize="sm"
        >
          {formatFomcLabel(nextDecision)}
        </Badge>
        <Text
          color={style.textColor}
          fontFamily="mono"
          fontSize="md"
          fontWeight="semibold"
        >
          {countdown.days > 0 && `${countdown.days}d `}
          {formatNumber(countdown.hours)}h {formatNumber(countdown.minutes)}m {formatNumber(countdown.seconds)}s
        </Text>
      </HStack>
    </Box>
  )
}

export default FedCountdown
