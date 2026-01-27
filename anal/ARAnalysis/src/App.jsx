import { useState, useEffect, useCallback, useRef } from 'react'
import {
  Box,
  Heading,
  HStack,
  VStack,
  Flex,
  Spacer,
  IconButton,
  Tooltip,
  Progress,
  Text,
  Tabs,
  TabList,
  TabPanels,
  Tab,
  TabPanel,
  useToast
} from '@chakra-ui/react'
import { RepeatIcon, CloseIcon } from '@chakra-ui/icons'
import SecuritySpreadsheet from './components/SecuritySpreadsheet'
import SecurityPicker from './components/SecurityPicker'
import ConnectionStatus from './components/ConnectionStatus'
import DataStatus from './components/DataStatus'
import ThresholdReport from './components/ThresholdReport'
import TwoUpThenBigMove from './components/TwoUpThenBigMove'
import Screener from './components/Screener'
import FedCountdown from './components/FedCountdown'
import MarketClocks from './components/MarketClocks'
import LiveWatchlist from './components/LiveWatchlist'
import DataManager from './modules/DataManager'
import {
  getConnectionStatus,
  connectToIBKR,
  getLastPort,
  fetchFromCache,
  loadHistoricalData,
  getCachedSecurities,
  updateSecurityAccess
} from './modules/dataService'
import ConnectionDialog from './components/ConnectionDialog'

function App() {
  const [selectedSymbol, setSelectedSymbol] = useState('')
  const selectedInterval = '1d'
  const [priceData, setPriceData] = useState([])
  const [loading, setLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(0)
  const [connectionState, setConnectionState] = useState({ connected: false })
  const [securities, setSecurities] = useState([])
  const [showConnectionDialog, setShowConnectionDialog] = useState(false)
  const [connectionError, setConnectionError] = useState(null)
  const [lastUsedPort, setLastUsedPort] = useState(7496)
  const [initialConnectionAttempted, setInitialConnectionAttempted] = useState(false)
  const [liveWatchlistInterval, setLiveWatchlistInterval] = useState(15)
  const cancelRef = useRef(null)
  const toast = useToast()

  const refreshConnectionStatus = useCallback(async () => {
    try {
      const status = await getConnectionStatus()
      setConnectionState(status)
    } catch (error) {
      setConnectionState({ connected: false, error: error.message })
    }
  }, [])

  const loadSecurities = useCallback(async () => {
    const cached = await getCachedSecurities()
    setSecurities(cached)
  }, [])

  // Load data from cache (instant)
  const loadFromCache = useCallback(async (symbol, interval) => {
    if (!symbol) return
    const cached = await fetchFromCache(symbol, interval)
    setPriceData(cached)
  }, [])

  // Cancel any ongoing load
  const cancelLoad = useCallback(() => {
    if (cancelRef.current) {
      cancelRef.current()
      cancelRef.current = null
    }
    setLoading(false)
    setLoadProgress(0)
  }, [])

  // Load fresh data from IBKR with progress
  const loadFromIBKR = useCallback((symbol, interval) => {
    if (!symbol || !connectionState.connected) {
      if (!connectionState.connected) {
        toast({
          title: 'Not Connected',
          description: 'Connect to IBKR to load data',
          status: 'warning',
          duration: 3000
        })
      }
      return
    }

    // Cancel any existing load
    cancelLoad()

    setLoading(true)
    setLoadProgress(0)

    const { cancel } = loadHistoricalData(
      symbol,
      interval,
      // onProgress
      (data, progress) => {
        setPriceData(data)
        setLoadProgress(progress)
      },
      // onComplete
      (finalData) => {
        setPriceData(finalData)
        setLoading(false)
        setLoadProgress(100)
        cancelRef.current = null
        loadSecurities()
        toast({
          title: 'Data Loaded',
          description: `Loaded ${finalData.length} bars`,
          status: 'success',
          duration: 2000
        })
      },
      // onError
      (error) => {
        setLoading(false)
        setLoadProgress(0)
        cancelRef.current = null
        toast({
          title: 'Load Failed',
          description: error.message,
          status: 'error',
          duration: 5000
        })
      }
    )

    cancelRef.current = cancel
  }, [connectionState.connected, cancelLoad, loadSecurities, toast])

  // Auto-connect on startup
  useEffect(() => {
    const autoConnect = async () => {
      if (initialConnectionAttempted) return
      setInitialConnectionAttempted(true)

      // Get last used port from storage
      const savedPort = await getLastPort()
      const port = savedPort || 7496
      setLastUsedPort(port)

      try {
        const status = await connectToIBKR(port)
        setConnectionState(status)
        if (!status.connected) {
          setConnectionError(status.error || 'Connection failed')
          setShowConnectionDialog(true)
        }
      } catch (error) {
        setConnectionError(error.message)
        setShowConnectionDialog(true)
        setConnectionState({ connected: false, error: error.message })
      }
    }

    autoConnect()
    loadSecurities()
  }, [initialConnectionAttempted, loadSecurities])

  // Connection status polling (only after initial connection attempt)
  useEffect(() => {
    if (!initialConnectionAttempted) return
    const interval = setInterval(refreshConnectionStatus, 5000)
    return () => clearInterval(interval)
  }, [refreshConnectionStatus, initialConnectionAttempted])

  // Load from cache when symbol/interval changes
  useEffect(() => {
    cancelLoad()
    setPriceData([])
    if (selectedSymbol && selectedInterval) {
      loadFromCache(selectedSymbol, selectedInterval)
    }
  }, [selectedSymbol, selectedInterval, loadFromCache, cancelLoad])

  const handleSymbolChange = async (symbol) => {
    setSelectedSymbol(symbol)
    if (symbol) {
      await updateSecurityAccess(symbol)
      loadSecurities() // Refresh recent list
    }
  }

  const handleConnectionChange = (status) => {
    setConnectionState(status)
    if (status.connected) {
      loadSecurities()
    }
  }

  const handleDataLoaded = (symbol) => {
    loadSecurities()
    if (symbol === selectedSymbol) {
      loadFromCache(selectedSymbol, selectedInterval)
    }
  }

  const handleRefresh = () => {
    if (!selectedSymbol) return
    loadFromIBKR(selectedSymbol, selectedInterval)
  }

  const handleCancel = () => {
    cancelLoad()
    toast({
      title: 'Cancelled',
      description: 'Data loading cancelled',
      status: 'info',
      duration: 2000
    })
  }

  const handleConnectionRetry = async (port) => {
    setLastUsedPort(port)
    try {
      const status = await connectToIBKR(port)
      setConnectionState(status)
      if (status.connected) {
        setShowConnectionDialog(false)
        setConnectionError(null)
        toast({
          title: 'Connected',
          description: `Connected to IBKR on port ${port}`,
          status: 'success',
          duration: 3000
        })
        loadSecurities()
      } else {
        setConnectionError(status.error || 'Connection failed')
      }
    } catch (error) {
      setConnectionError(error.message)
    }
  }

  const handleGoOffline = () => {
    setShowConnectionDialog(false)
    toast({
      title: 'Offline Mode',
      description: 'Using cached data only',
      status: 'info',
      duration: 3000
    })
  }

  return (
    <Box minH="100vh" bg="gray.900">
      <ConnectionDialog
        isOpen={showConnectionDialog}
        error={connectionError}
        lastPort={lastUsedPort}
        onRetry={handleConnectionRetry}
        onOffline={handleGoOffline}
      />
      <FedCountdown isConnected={connectionState.connected} />
      <MarketClocks />
      <LiveWatchlist isConnected={connectionState.connected} refreshInterval={liveWatchlistInterval} />
      <Box px={4} py={4}>
        <VStack spacing={4} align="stretch">
          {/* Header */}
          <Flex align="center" wrap="wrap" gap={4}>
            <Heading size="lg" color="blue.300">
              ARAnalysis
            </Heading>
            <Spacer />
            <ConnectionStatus
              status={connectionState}
              onRefresh={refreshConnectionStatus}
            />
          </Flex>

          {/* Controls */}
          <HStack spacing={4} wrap="wrap">
            <SecurityPicker
              value={selectedSymbol}
              onChange={handleSymbolChange}
              securities={securities}
            />
            <Tooltip label={loading ? "Loading..." : "Load data from IBKR"}>
              <IconButton
                aria-label="Load data"
                icon={<RepeatIcon />}
                onClick={handleRefresh}
                isLoading={loading}
                isDisabled={!selectedSymbol || !connectionState.connected}
                colorScheme="blue"
                variant="outline"
              />
            </Tooltip>
            {loading && (
              <Tooltip label="Cancel loading">
                <IconButton
                  aria-label="Cancel"
                  icon={<CloseIcon />}
                  onClick={handleCancel}
                  colorScheme="red"
                  variant="outline"
                  size="sm"
                />
              </Tooltip>
            )}
            <Spacer />
            <DataManager
              connectionStatus={connectionState}
              onConnectionChange={handleConnectionChange}
              onDataLoaded={handleDataLoaded}
              onSettingsChange={(settings) => {
                if (settings.liveWatchlistInterval) {
                  setLiveWatchlistInterval(settings.liveWatchlistInterval)
                }
              }}
            />
          </HStack>

          {/* Loading Progress */}
          {loading && (
            <Box>
              <Progress value={loadProgress} size="sm" colorScheme="blue" hasStripe isAnimated />
              <Text fontSize="xs" color="gray.400" mt={1}>
                Loading... {loadProgress}% ({priceData.length} bars)
              </Text>
            </Box>
          )}

          {/* Data Status */}
          <DataStatus
            symbol={selectedSymbol}
            interval={selectedInterval}
            data={priceData}
          />

          {/* Tab Navigation */}
          <Tabs colorScheme="blue" variant="enclosed">
            <TabList>
              <Tab>Spreadsheet</Tab>
              <Tab
                _selected={{ color: 'orange.300', borderColor: 'orange.300', borderBottomColor: 'gray.800' }}
                color="orange.400"
              >
                High Over Prev Close
              </Tab>
              <Tab
                _selected={{ color: 'orange.300', borderColor: 'orange.300', borderBottomColor: 'gray.800' }}
                color="orange.400"
              >
                Two Up + Big Move
              </Tab>
              <Tab>Screener</Tab>
            </TabList>
            <TabPanels>
              <TabPanel px={0}>
                <SecuritySpreadsheet
                  data={priceData}
                  symbol={selectedSymbol}
                  interval={selectedInterval}
                  loading={false}
                  connectionState={connectionState}
                />
              </TabPanel>
              <TabPanel px={0}>
                <ThresholdReport
                  symbol={selectedSymbol}
                  data={priceData}
                />
              </TabPanel>
              <TabPanel px={0}>
                <TwoUpThenBigMove
                  symbol={selectedSymbol}
                  data={priceData}
                />
              </TabPanel>
              <TabPanel px={0}>
                <Screener />
              </TabPanel>
            </TabPanels>
          </Tabs>
        </VStack>
      </Box>
    </Box>
  )
}

export default App
