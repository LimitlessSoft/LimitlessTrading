import { useState, useEffect } from 'react'
import { HStack, Box, Text, Tooltip } from '@chakra-ui/react'
import { EXCHANGES, getExchangeStatus, getExchangeTime } from '../modules/exchangeService'

const STATUS_COLORS = {
  'Open':        { bg: 'green.900',  border: 'green.500',  text: 'green.300'  },
  'Pre-Market':  { bg: 'yellow.900', border: 'yellow.500', text: 'yellow.300' },
  'After-Hours': { bg: 'orange.900', border: 'orange.500', text: 'orange.300' },
  'Closed':      { bg: 'gray.800',   border: 'gray.600',   text: 'gray.400'   },
}

const STATUS_SHORT = {
  'Open':        'Open',
  'Pre-Market':  'Pre',
  'After-Hours': 'AH',
  'Closed':      'Closed',
}

const STATUS_DESC = {
  'Open':        'Market Open',
  'Pre-Market':  'Pre-Market',
  'After-Hours': 'After Hours',
  'Closed':      'Market Closed',
}

function MarketClock({ exchangeKey }) {
  const market = EXCHANGES[exchangeKey]
  const [time, setTime]     = useState('')
  const [status, setStatus] = useState('Closed')

  useEffect(() => {
    const update = () => {
      setTime(getExchangeTime(exchangeKey))
      setStatus(getExchangeStatus(exchangeKey))
    }
    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [exchangeKey])

  const colors = STATUS_COLORS[status] ?? STATUS_COLORS['Closed']

  return (
    <Tooltip label={STATUS_DESC[status] ?? status} fontSize="xs">
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
          {STATUS_SHORT[status] ?? status}
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
        <MarketClock exchangeKey="NYSE" />
        <MarketClock exchangeKey="LSE" />
      </HStack>
    </Box>
  )
}

export default MarketClocks
