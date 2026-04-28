import { useState, useEffect } from 'react'
import { HStack, Text, Badge, Spinner } from '@chakra-ui/react'
import { API_BASE } from '../api'

function TreasuryYields() {
  const [yields, setYields] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const fetchYields = async () => {
    try {
      const response = await fetch(`${API_BASE}/treasury/yields`)

      if (!response.ok) {
        throw new Error('Failed to fetch Treasury yields')
      }

      const data = await response.json()
      setYields(data)
      setError(null)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchYields()
    // Refresh every 15 minutes (data updates daily anyway)
    const interval = setInterval(fetchYields, 15 * 60 * 1000)
    return () => clearInterval(interval)
  }, [])

  if (loading) {
    return (
      <HStack spacing={2}>
        <Text color="gray.400" fontSize="sm">Yields</Text>
        <Spinner size="xs" color="gray.400" />
      </HStack>
    )
  }

  if (error || !yields) {
    return (
      <HStack spacing={2}>
        <Badge colorScheme="gray" variant="outline" px={2} py={1} fontSize="xs">
          Yields --
        </Badge>
      </HStack>
    )
  }

  const maturities = ['2y', '5y', '10y', '20y', '30y']

  const formatChange = (change) => {
    if (change === null || change === undefined) return null
    const sign = change >= 0 ? '+' : ''
    // Convert to basis points for display
    const bps = Math.round(change * 100)
    return `${sign}${bps}`
  }

  return (
    <HStack spacing={3}>
      {maturities.map((m) => {
        const data = yields[m]
        const value = data?.value ?? data
        const change = data?.change
        const changeDisplay = formatChange(change)

        return (
          <HStack key={m} spacing={1}>
            <Text color="gray.500" fontSize="xs" fontWeight="medium">
              {m.toUpperCase()}
            </Text>
            <Text
              color="cyan.300"
              fontFamily="mono"
              fontSize="sm"
              fontWeight="semibold"
            >
              {value !== null ? value.toFixed(2) : '--'}
            </Text>
            {changeDisplay !== null && (
              <Text
                color={change >= 0 ? 'red.400' : 'green.400'}
                fontFamily="mono"
                fontSize="xs"
              >
                {changeDisplay}
              </Text>
            )}
          </HStack>
        )
      })}
    </HStack>
  )
}

export default TreasuryYields
