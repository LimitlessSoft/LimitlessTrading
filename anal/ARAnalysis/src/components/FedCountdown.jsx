import { useState, useEffect } from 'react'
import { HStack, Stack, Spacer, Text, Badge } from '@chakra-ui/react'
import { getNextFomcDecision, formatFomcLabel, calculateCountdown } from '../modules/fomcService'
import VixDisplay from './VixDisplay'
import TreasuryYields from './TreasuryYields'
import FearGreedGauge from './FearGreedGauge'
import PageNavMenu from './PageNavMenu'

function FedCountdown({ isConnected }) {
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

  // Determine if we have a valid FOMC countdown to show
  const showFomc = !loading && nextDecision && countdown && countdown.totalMs > 0

  // Urgency levels based on days remaining (only calculated if showing FOMC)
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

  let style = styles.normal
  let urgencyLevel = 'normal'

  if (showFomc) {
    const daysRemaining = countdown.totalMs / (24 * 60 * 60 * 1000)
    urgencyLevel =
      daysRemaining < 1 ? 'critical' :    // < 24 hours - red
      daysRemaining < 5 ? 'high' :        // < 5 days - orange
      daysRemaining < 10 ? 'medium' :     // < 10 days - yellow
      'normal'                            // 10+ days - blue
    style = styles[urgencyLevel]
  }

  const formatNumber = (n) => String(n).padStart(2, '0')

  return (
    <PageNavMenu bg={style.bg} borderColor={style.borderColor}>
      {showFomc && (
        <Stack direction={{ base: 'column', md: 'row' }} spacing={{ base: 1, md: 3 }} ml={2} align={{ base: 'flex-start', md: 'center' }}>
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
            whiteSpace="nowrap"
          >
            {countdown.days > 0 && `${countdown.days}d `}
            {formatNumber(countdown.hours)}h {formatNumber(countdown.minutes)}m {formatNumber(countdown.seconds)}s
          </Text>
        </Stack>
      )}
      <Spacer />
      <HStack spacing={6}>
        <FearGreedGauge isConnected={isConnected} />
        <TreasuryYields />
        <VixDisplay isConnected={isConnected} />
      </HStack>
    </PageNavMenu>
  )
}

export default FedCountdown
