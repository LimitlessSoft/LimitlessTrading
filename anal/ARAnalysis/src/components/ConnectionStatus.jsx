import {
  Box,
  HStack,
  Badge,
  IconButton,
  Tooltip,
  Text
} from '@chakra-ui/react'
import { RepeatIcon } from '@chakra-ui/icons'

function ConnectionStatus({ status, onRefresh }) {
  const isConnected = status?.connected
  const port = status?.port

  return (
    <HStack spacing={2}>
      <Badge
        colorScheme={isConnected ? 'green' : 'red'}
        variant="solid"
        px={3}
        py={1}
        borderRadius="full"
      >
        <HStack spacing={2}>
          <Box
            w={2}
            h={2}
            borderRadius="full"
            bg={isConnected ? 'green.200' : 'red.200'}
            animation={isConnected ? 'pulse 2s infinite' : 'none'}
          />
          <Text>
            {isConnected ? `Connected (${port})` : 'Disconnected'}
          </Text>
        </HStack>
      </Badge>
      <Tooltip label="Refresh status">
        <IconButton
          aria-label="Refresh connection status"
          icon={<RepeatIcon />}
          size="sm"
          variant="ghost"
          onClick={onRefresh}
          color="gray.400"
          _hover={{ color: 'white' }}
        />
      </Tooltip>
    </HStack>
  )
}

export default ConnectionStatus
