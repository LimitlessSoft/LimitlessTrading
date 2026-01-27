import { useState, useEffect } from 'react'
import { HStack, Box, Text } from '@chakra-ui/react'

/**
 * LiveIndicator - Shows a glowing dot and countdown timer for live data refresh
 *
 * @param {boolean} isConnected - Whether connected to data source
 * @param {boolean} isLoading - Whether currently fetching data
 * @param {number} lastFetchTime - Timestamp of last successful fetch (Date.now())
 * @param {number} refreshInterval - Refresh interval in seconds (default: 15)
 */
function LiveIndicator({ isConnected, isLoading, lastFetchTime, refreshInterval = 15 }) {
  const [nextRefreshIn, setNextRefreshIn] = useState(refreshInterval)

  useEffect(() => {
    if (!isConnected || !lastFetchTime) {
      setNextRefreshIn(refreshInterval)
      return
    }

    const tick = () => {
      const elapsed = Math.floor((Date.now() - lastFetchTime) / 1000)
      const remaining = Math.max(0, refreshInterval - elapsed)
      setNextRefreshIn(remaining)
    }

    tick()
    const timer = setInterval(tick, 1000)
    return () => clearInterval(timer)
  }, [isConnected, lastFetchTime, refreshInterval])

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

  return (
    <HStack spacing={1}>
      <Box
        w="6px"
        h="6px"
        borderRadius="full"
        bg={isLoading ? 'blue.400' : 'green.400'}
        boxShadow={isLoading ? '0 0 6px var(--chakra-colors-blue-400)' : '0 0 6px var(--chakra-colors-green-400)'}
        transition="all 0.2s"
      />
      <Text color="gray.500" fontSize="xs" fontFamily="mono">
        {isLoading ? 'updating' : `${nextRefreshIn}s`}
      </Text>
    </HStack>
  )
}

export default LiveIndicator
