import { useState, useEffect } from 'react'
import { HStack, Text, Badge, Spinner } from '@chakra-ui/react'

const API_BASE = 'http://localhost:3001/api'

function VixDisplay({ isConnected }) {
  const [vixPrice, setVixPrice] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const fetchVix = async () => {
    try {
      const response = await fetch(`${API_BASE}/current-prices`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ symbols: ['VIX'] })
      })

      if (!response.ok) {
        throw new Error('Failed to fetch VIX')
      }

      const data = await response.json()
      if (data.VIX !== undefined && data.VIX !== null) {
        setVixPrice(data.VIX)
        setError(null)
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  // Only fetch when connected, refresh every 30 seconds
  useEffect(() => {
    if (!isConnected) {
      setLoading(false)
      return
    }

    fetchVix()
    const interval = setInterval(fetchVix, 30000)
    return () => clearInterval(interval)
  }, [isConnected])

  // Determine VIX level styling
  const getVixStyle = (price) => {
    if (price === null || price === undefined) return { colorScheme: 'gray', label: 'Unknown' }

    if (price >= 30) {
      return { colorScheme: 'red', label: 'Extreme Fear' }
    } else if (price >= 20) {
      return { colorScheme: 'orange', label: 'Elevated' }
    } else if (price >= 15) {
      return { colorScheme: 'yellow', label: 'Normal' }
    } else {
      return { colorScheme: 'green', label: 'Low' }
    }
  }

  const style = getVixStyle(vixPrice)

  if (!isConnected) {
    return (
      <HStack spacing={2}>
        <Badge colorScheme="gray" variant="outline" px={2} py={1}>
          VIX --
        </Badge>
      </HStack>
    )
  }

  if (loading) {
    return (
      <HStack spacing={2}>
        <Text color="gray.400" fontSize="sm">VIX</Text>
        <Spinner size="xs" color="gray.400" />
      </HStack>
    )
  }

  if (error || vixPrice === null) {
    return (
      <HStack spacing={2}>
        <Badge colorScheme="gray" variant="outline" px={2} py={1}>
          VIX --
        </Badge>
      </HStack>
    )
  }

  return (
    <HStack spacing={2}>
      <Badge
        colorScheme={style.colorScheme}
        variant="solid"
        px={3}
        py={1}
        borderRadius="md"
        fontSize="sm"
      >
        VIX
      </Badge>
      <Text
        color={`${style.colorScheme}.200`}
        fontFamily="mono"
        fontSize="md"
        fontWeight="semibold"
      >
        {vixPrice.toFixed(2)}
      </Text>
    </HStack>
  )
}

export default VixDisplay
