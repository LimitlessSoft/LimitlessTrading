import { useState, useEffect, useCallback, useRef } from 'react'
import {
  Box,
  Container,
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
import IntervalPicker from './components/IntervalPicker'
import ConnectionStatus from './components/ConnectionStatus'
import DataStatus from './components/DataStatus'
import ThresholdReport from './components/ThresholdReport'
import DataManager from './modules/DataManager'
import {
  getConnectionStatus,
  fetchFromCache,
  loadHistoricalData,
  getCachedSecurities,
  INTERVALS
} from './modules/dataService'

function App() {
  const [selectedSymbol, setSelectedSymbol] = useState('')
  const [selectedInterval, setSelectedInterval] = useState('1d')
  const [priceData, setPriceData] = useState([])
  const [loading, setLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(0)
  const [connectionState, setConnectionState] = useState({ connected: false })
  const [securities, setSecurities] = useState([])
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

  // Initial load and connection status polling
  useEffect(() => {
    refreshConnectionStatus()
    loadSecurities()
    const interval = setInterval(refreshConnectionStatus, 5000)
    return () => clearInterval(interval)
  }, [refreshConnectionStatus, loadSecurities])

  // Load from cache when symbol/interval changes
  useEffect(() => {
    cancelLoad()
    setPriceData([])
    if (selectedSymbol && selectedInterval) {
      loadFromCache(selectedSymbol, selectedInterval)
    }
  }, [selectedSymbol, selectedInterval, loadFromCache, cancelLoad])

  const handleSymbolChange = (symbol) => {
    setSelectedSymbol(symbol)
  }

  const handleIntervalChange = (interval) => {
    setSelectedInterval(interval)
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

  return (
    <Box minH="100vh" bg="gray.900">
      <Container maxW="container.xl" py={4}>
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
            <IntervalPicker
              value={selectedInterval}
              onChange={handleIntervalChange}
              intervals={INTERVALS}
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
              <Tab>Threshold Report</Tab>
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
                  securities={securities}
                />
              </TabPanel>
            </TabPanels>
          </Tabs>
        </VStack>
      </Container>
    </Box>
  )
}

export default App
