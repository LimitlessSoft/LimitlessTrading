import { useState, useEffect } from 'react'
import {
  Box,
  Button,
  HStack,
  VStack,
  Select,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  useDisclosure,
  useToast,
  Text,
  Divider,
  FormControl,
  FormLabel,
  IconButton,
  Tooltip
} from '@chakra-ui/react'
import { SettingsIcon, DeleteIcon } from '@chakra-ui/icons'
import {
  connectToIBKR,
  disconnectFromIBKR,
  getLastPort,
  clearAllCache
} from './dataService'
import {
  getLiveWatchlistInterval,
  setLiveWatchlistInterval
} from '../db/database'

const IBKR_PORTS = [
  { value: 7496, label: '7496 (TWS Live)' },
  { value: 4001, label: '4001 (Gateway Live)' }
]

const REFRESH_INTERVALS = [
  { value: 1, label: '1 second' },
  { value: 5, label: '5 seconds' },
  { value: 10, label: '10 seconds' },
  { value: 15, label: '15 seconds' },
  { value: 30, label: '30 seconds' },
  { value: 60, label: '1 minute' }
]

function DataManager({ connectionStatus, onConnectionChange, onDataLoaded, onSettingsChange }) {
  const { isOpen, onOpen, onClose } = useDisclosure()
  const toast = useToast()

  const [selectedPort, setSelectedPort] = useState(7496)
  const [connecting, setConnecting] = useState(false)
  const [refreshInterval, setRefreshInterval] = useState(15)

  useEffect(() => {
    const loadSettings = async () => {
      const lastPort = await getLastPort()
      if (lastPort) {
        setSelectedPort(lastPort)
      }
      const interval = await getLiveWatchlistInterval()
      setRefreshInterval(interval)
    }
    loadSettings()
  }, [])

  const handleIntervalChange = async (value) => {
    const interval = Number(value)
    setRefreshInterval(interval)
    await setLiveWatchlistInterval(interval)
    if (onSettingsChange) {
      onSettingsChange({ liveWatchlistInterval: interval })
    }
    toast({
      title: 'Setting Saved',
      description: `Refresh interval set to ${interval} seconds`,
      status: 'success',
      duration: 2000
    })
  }

  const handleConnect = async () => {
    setConnecting(true)
    try {
      const status = await connectToIBKR(selectedPort)
      onConnectionChange(status)
      toast({
        title: 'Connected',
        description: `Connected to IBKR on port ${selectedPort}`,
        status: 'success',
        duration: 3000
      })
    } catch (error) {
      toast({
        title: 'Connection Failed',
        description: error.message,
        status: 'error',
        duration: 5000
      })
    } finally {
      setConnecting(false)
    }
  }

  const handleDisconnect = async () => {
    try {
      const status = await disconnectFromIBKR()
      onConnectionChange(status)
      toast({
        title: 'Disconnected',
        description: 'Disconnected from IBKR',
        status: 'info',
        duration: 2000
      })
    } catch (error) {
      toast({
        title: 'Error',
        description: error.message,
        status: 'error',
        duration: 5000
      })
    }
  }

  const handleClearData = async () => {
    if (confirm('Are you sure you want to clear all cached data?')) {
      await clearAllCache()
      toast({
        title: 'Cache Cleared',
        description: 'All local data has been cleared',
        status: 'info',
        duration: 2000
      })
      onDataLoaded(null)
    }
  }

  return (
    <>
      <Tooltip label="Data Manager">
        <IconButton
          aria-label="Open Data Manager"
          icon={<SettingsIcon />}
          onClick={onOpen}
          variant="outline"
          colorScheme="blue"
        />
      </Tooltip>

      <Modal isOpen={isOpen} onClose={onClose} size="md">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader color="blue.300">Data Manager</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={6} align="stretch">
              {/* Connection Section */}
              <Box>
                <Text fontWeight="bold" mb={3} color="gray.200">
                  IBKR Connection
                </Text>
                <VStack spacing={3} align="stretch">
                  <FormControl>
                    <FormLabel color="gray.400" fontSize="sm">Port</FormLabel>
                    <Select
                      value={selectedPort}
                      onChange={(e) => setSelectedPort(Number(e.target.value))}
                      bg="gray.700"
                      borderColor="gray.600"
                      isDisabled={connectionStatus?.connected}
                    >
                      {IBKR_PORTS.map(port => (
                        <option key={port.value} value={port.value}>
                          {port.label}
                        </option>
                      ))}
                    </Select>
                  </FormControl>

                  <HStack>
                    {!connectionStatus?.connected ? (
                      <Button
                        colorScheme="green"
                        onClick={handleConnect}
                        isLoading={connecting}
                        flex={1}
                      >
                        Connect
                      </Button>
                    ) : (
                      <Button
                        colorScheme="red"
                        onClick={handleDisconnect}
                        flex={1}
                      >
                        Disconnect
                      </Button>
                    )}
                  </HStack>
                </VStack>
              </Box>

              <Divider borderColor="gray.600" />

              {/* Live Watchlist Settings */}
              <Box>
                <Text fontWeight="bold" mb={3} color="gray.200">
                  Live Watchlist
                </Text>
                <FormControl>
                  <FormLabel color="gray.400" fontSize="sm">Refresh Interval</FormLabel>
                  <Select
                    value={refreshInterval}
                    onChange={(e) => handleIntervalChange(e.target.value)}
                    bg="gray.700"
                    borderColor="gray.600"
                  >
                    {REFRESH_INTERVALS.map(opt => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </Select>
                </FormControl>
              </Box>

              <Divider borderColor="gray.600" />

              {/* Clear Data Section */}
              <Box>
                <Text fontWeight="bold" mb={3} color="gray.200">
                  Local Storage
                </Text>
                <Button
                  leftIcon={<DeleteIcon />}
                  colorScheme="red"
                  variant="outline"
                  onClick={handleClearData}
                  size="sm"
                >
                  Clear All Cached Data
                </Button>
              </Box>
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" onClick={onClose}>
              Close
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default DataManager
