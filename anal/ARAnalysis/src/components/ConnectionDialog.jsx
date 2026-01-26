import { useState } from 'react'
import {
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  Button,
  Text,
  VStack,
  HStack,
  Select,
  Alert,
  AlertIcon,
  Spinner
} from '@chakra-ui/react'

const PORTS = [
  { value: 7496, label: '7496 - TWS Live' },
  { value: 7497, label: '7497 - TWS Paper' },
  { value: 4001, label: '4001 - Gateway Live' },
  { value: 4002, label: '4002 - Gateway Paper' }
]

export default function ConnectionDialog({
  isOpen,
  error,
  lastPort,
  onRetry,
  onOffline
}) {
  const [selectedPort, setSelectedPort] = useState(lastPort || 7497)
  const [retrying, setRetrying] = useState(false)

  const handleRetry = async () => {
    setRetrying(true)
    await onRetry(selectedPort)
    setRetrying(false)
  }

  return (
    <Modal isOpen={isOpen} onClose={() => {}} isCentered closeOnOverlayClick={false}>
      <ModalOverlay bg="blackAlpha.800" />
      <ModalContent bg="gray.800" color="white">
        <ModalHeader>IBKR Connection Failed</ModalHeader>
        <ModalBody>
          <VStack spacing={4} align="stretch">
            <Alert status="error" variant="subtle" borderRadius="md">
              <AlertIcon />
              <Text fontSize="sm">
                {error || 'Could not connect to Interactive Brokers'}
              </Text>
            </Alert>

            <Text fontSize="sm" color="gray.400">
              Make sure TWS or IB Gateway is running and API connections are enabled.
            </Text>

            <HStack>
              <Text fontSize="sm" whiteSpace="nowrap">Port:</Text>
              <Select
                size="sm"
                value={selectedPort}
                onChange={(e) => setSelectedPort(Number(e.target.value))}
                bg="gray.700"
                isDisabled={retrying}
              >
                {PORTS.map(p => (
                  <option key={p.value} value={p.value}>{p.label}</option>
                ))}
              </Select>
            </HStack>
          </VStack>
        </ModalBody>

        <ModalFooter>
          <HStack spacing={3}>
            <Button
              variant="outline"
              onClick={onOffline}
              isDisabled={retrying}
            >
              Continue Offline
            </Button>
            <Button
              colorScheme="blue"
              onClick={handleRetry}
              isDisabled={retrying}
              leftIcon={retrying ? <Spinner size="sm" /> : null}
            >
              {retrying ? 'Connecting...' : 'Retry'}
            </Button>
          </HStack>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )
}
