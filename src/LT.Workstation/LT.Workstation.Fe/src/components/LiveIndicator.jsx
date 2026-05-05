import { useState, useEffect, useMemo } from 'react'
import { HStack, Box, Text } from '@chakra-ui/react'
import { keyframes } from '@emotion/react'

const pulse = keyframes`
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
`

/**
 * LiveIndicator - Shows a glowing dot for real-time streaming status
 *
 * @param {boolean} isConnected - Whether connected to data source
 * @param {boolean} isLoading - Whether currently fetching initial data
 * @param {boolean} isStreaming - Whether SSE stream is active
 * @param {number} lastFetchTime - Timestamp of last update (Date.now())
 * @param {number} refreshInterval - Not used in streaming mode, kept for compatibility
 */
function LiveIndicator({ isConnected, isLoading, isStreaming, lastFetchTime, refreshInterval = 15 }) {
  const [timeSinceUpdate, setTimeSinceUpdate] = useState(0)

  useEffect(() => {
    if (!lastFetchTime) return

    const tick = () => {
      const elapsed = Math.floor((Date.now() - lastFetchTime) / 1000)
      setTimeSinceUpdate(elapsed)
    }

    tick()
    const timer = setInterval(tick, 1000)
    return () => clearInterval(timer)
  }, [lastFetchTime])

  if (!isConnected) {
    return (
      <HStack spacing={1}>
        <Box
          w="6px"
          h="6px"
          borderRadius="full"
          bg="gray.500"
        />
        <Text color="gray.500" fontSize="xs" fontFamily="mono">
          offline
        </Text>
      </HStack>
    )
  }

  // Determine status based on streaming state and time since last update
  let status, color, showPulse
  if (isLoading) {
    status = 'connecting'
    color = 'blue.400'
    showPulse = true
  } else if (isStreaming) {
    if (timeSinceUpdate < 5) {
      status = 'live'
      color = 'green.400'
      showPulse = true
    } else if (timeSinceUpdate < 30) {
      status = 'live'
      color = 'green.400'
      showPulse = false
    } else {
      status = 'stale'
      color = 'yellow.400'
      showPulse = false
    }
  } else {
    status = 'disconnected'
    color = 'orange.400'
    showPulse = false
  }

  const boxShadow = useMemo(
    () => `0 0 6px var(--chakra-colors-${color.replace('.', '-')})`,
    [color]
  )

  return (
    <HStack spacing={1}>
      <Box
        w="6px"
        h="6px"
        borderRadius="full"
        bg={color}
        boxShadow={boxShadow}
        animation={showPulse ? `${pulse} 1.5s ease-in-out infinite` : undefined}
        transition="all 0.2s"
      />
      <Text color="gray.500" fontSize="xs" fontFamily="mono">
        {status}
      </Text>
    </HStack>
  )
}

export default LiveIndicator
