import { useState, useEffect, useCallback } from 'react'
import {
  Box,
  Text,
  CloseButton,
  VStack,
  Portal,
} from '@chakra-ui/react'
import { subscribeToMessages } from '../modules/alertService'

const AUTO_DISMISS_MS = 12_000

export default function AlertOverlay() {
  const [notifications, setNotifications] = useState([])

  const dismiss = useCallback((id) => {
    setNotifications(prev => prev.filter(n => n.id !== id))
  }, [])

  useEffect(() => {
    const unsub = subscribeToMessages((notification) => {
      setNotifications(prev => [...prev, notification])

      setTimeout(() => {
        dismiss(notification.id)
      }, AUTO_DISMISS_MS)
    })

    return unsub
  }, [dismiss])

  if (notifications.length === 0) return null

  return (
    <Portal>
      <VStack
        position="fixed"
        top={4}
        right={4}
        zIndex={9999}
        spacing={2}
        align="flex-end"
        pointerEvents="none"
      >
        {notifications.map(n => (
          <Box
            key={n.id}
            bg="gray.800"
            border="1px solid"
            borderColor="blue.500"
            borderRadius="md"
            px={4}
            py={3}
            minW="220px"
            maxW="360px"
            boxShadow="lg"
            pointerEvents="auto"
          >
            <Box display="flex" alignItems="flex-start" gap={3}>
              <Box flex={1}>
                {n.label && (
                  <Text fontSize="xs" color="blue.300" fontWeight="semibold" mb={0.5}>
                    {n.label}
                  </Text>
                )}
                <Text fontSize="sm" color="white">
                  {n.message}
                </Text>
                <Text fontSize="xs" color="gray.400" mt={1}>
                  {n.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                </Text>
              </Box>
              <CloseButton size="sm" color="gray.400" onClick={() => dismiss(n.id)} />
            </Box>
          </Box>
        ))}
      </VStack>
    </Portal>
  )
}
