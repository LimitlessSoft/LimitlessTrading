import { useState, useEffect } from 'react'
import { HStack, Text, Badge, Tooltip } from '@chakra-ui/react'
import { WarningIcon } from '@chakra-ui/icons'
import { getLastFetchTime, checkMissingBars } from '../modules/dataService'

function DataStatus({ symbol, interval, data }) {
  const [lastFetch, setLastFetch] = useState(null)
  const [missingInfo, setMissingInfo] = useState({ hasMissing: false, missingCount: 0 })

  useEffect(() => {
    const loadStatus = async () => {
      if (symbol && interval) {
        const fetchTime = await getLastFetchTime(symbol, interval)
        setLastFetch(fetchTime)
      } else {
        setLastFetch(null)
      }
    }
    loadStatus()
  }, [symbol, interval, data])

  useEffect(() => {
    if (data && data.length > 0 && interval) {
      const info = checkMissingBars(data, interval)
      setMissingInfo(info)
    } else {
      setMissingInfo({ hasMissing: false, missingCount: 0 })
    }
  }, [data, interval])

  if (!symbol || !data || data.length === 0) {
    return null
  }

  const formatTime = (timestamp) => {
    if (!timestamp) return 'Never'
    const date = new Date(timestamp)
    const now = new Date()
    const diffMs = now - date
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMins / 60)
    const diffDays = Math.floor(diffHours / 24)

    if (diffMins < 1) return 'Just now'
    if (diffMins < 60) return `${diffMins}m ago`
    if (diffHours < 24) return `${diffHours}h ago`
    return `${diffDays}d ago`
  }

  const formatLatestBar = (timestamp) => {
    if (!timestamp) return 'N/A'
    const date = new Date(timestamp)
    if (interval === '1d') {
      return date.toLocaleDateString()
    }
    return date.toLocaleString()
  }

  const isStale = missingInfo.hasMissing || missingInfo.missingCount > 0

  return (
    <HStack spacing={3} fontSize="sm">
      <HStack spacing={1}>
        <Text color="gray.500">Last fetch:</Text>
        <Text color={lastFetch ? 'gray.300' : 'gray.500'}>
          {formatTime(lastFetch)}
        </Text>
      </HStack>

      <HStack spacing={1}>
        <Text color="gray.500">Latest bar:</Text>
        <Text color="gray.300">
          {formatLatestBar(missingInfo.latestBarTime)}
        </Text>
      </HStack>

      {isStale && (
        <Tooltip
          label={`Detected ${missingInfo.missingCount} potentially missing bar(s). Data may be incomplete.`}
        >
          <Badge colorScheme="red" variant="solid" display="flex" alignItems="center" gap={1}>
            <WarningIcon boxSize={3} />
            Missing data
          </Badge>
        </Tooltip>
      )}
    </HStack>
  )
}

export default DataStatus
