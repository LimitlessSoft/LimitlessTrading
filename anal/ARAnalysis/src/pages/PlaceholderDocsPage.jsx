import { Box, Heading, Text, Code, Table, Thead, Tbody, Tr, Th, Td, VStack, HStack, IconButton } from '@chakra-ui/react'
import { ArrowBackIcon } from '@chakra-ui/icons'
import { useNavigate } from 'react-router-dom'

function PlaceholderDocsPage() {
  const navigate = useNavigate()

  return (
    <Box minH="100vh" bg="gray.900" color="white" p={8}>
      <HStack mb={6}>
        <IconButton
          icon={<ArrowBackIcon />}
          aria-label="Go back"
          variant="ghost"
          onClick={() => navigate(-1)}
        />
        <Heading size="lg">Placeholder API Documentation</Heading>
      </HStack>

      <VStack align="stretch" spacing={8} maxW="900px">
        {/* System Placeholders Section */}
        <Box>
          <Heading size="md" mb={4}>System Placeholders</Heading>
          <Text mb={3}>
            Syntax: <Code>${'{name:seconds}'}</Code> where <Code>seconds</Code> is the refresh interval.
          </Text>

          <Table variant="simple" size="sm" bg="gray.800" borderRadius="md">
            <Thead>
              <Tr>
                <Th color="gray.400">Placeholder</Th>
                <Th color="gray.400">Description</Th>
                <Th color="gray.400">Example Output</Th>
              </Tr>
            </Thead>
            <Tbody>
              <Tr>
                <Td><Code>${'{tws_connection_status:5}'}</Code></Td>
                <Td>TWS/IBKR connection status</Td>
                <Td>Connected (7496) / Disconnected</Td>
              </Tr>
            </Tbody>
          </Table>
        </Box>

        {/* Symbol Quote Placeholders Section */}
        <Box>
          <Heading size="md" mb={4}>Symbol Quote Placeholders</Heading>
          <Text mb={2}>
            Syntax: <Code>@{'{SYMBOL:property:seconds}'}</Code> for real-time market data.
          </Text>
          <Text fontSize="sm" color="gray.400" mb={4}>
            Intervals &le;5s use real-time streaming (SSE), &gt;5s use HTTP polling.
          </Text>

          <Table variant="simple" size="sm" bg="gray.800" borderRadius="md" mb={6}>
            <Thead>
              <Tr>
                <Th color="gray.400">Example</Th>
                <Th color="gray.400">Description</Th>
              </Tr>
            </Thead>
            <Tbody>
              <Tr>
                <Td><Code>@{'{AAPL:last:5}'}</Code></Td>
                <Td>AAPL last price, refreshing every 5 seconds</Td>
              </Tr>
              <Tr>
                <Td><Code>@{'{SPY:change:3}'}</Code></Td>
                <Td>SPY price change (+/- format), refreshing every 3 seconds</Td>
              </Tr>
              <Tr>
                <Td><Code>@{'{VIX:last}'}</Code></Td>
                <Td>VIX value (default 5s refresh)</Td>
              </Tr>
              <Tr>
                <Td><Code>@{'{MSFT:volume:10}'}</Code></Td>
                <Td>MSFT volume (formatted as K/M/B), polling every 10 seconds</Td>
              </Tr>
              <Tr>
                <Td><Code>@{'{TSLA:changePercent:5}'}</Code></Td>
                <Td>TSLA percentage change (+X.XX% format)</Td>
              </Tr>
            </Tbody>
          </Table>

          <Heading size="sm" mb={3}>Available Properties</Heading>
          <Table variant="simple" size="sm" bg="gray.800" borderRadius="md">
            <Thead>
              <Tr>
                <Th color="gray.400">Property</Th>
                <Th color="gray.400">Description</Th>
                <Th color="gray.400">Format</Th>
              </Tr>
            </Thead>
            <Tbody>
              <Tr>
                <Td><Code>last</Code></Td>
                <Td>Last traded price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>bid</Code></Td>
                <Td>Current bid price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>ask</Code></Td>
                <Td>Current ask price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>open</Code></Td>
                <Td>Opening price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>high</Code></Td>
                <Td>Day high</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>low</Code></Td>
                <Td>Day low</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>close</Code></Td>
                <Td>Closing price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>prevClose</Code></Td>
                <Td>Previous day close</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>vwap</Code></Td>
                <Td>Volume-weighted average price</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>change</Code></Td>
                <Td>Price change from previous close</Td>
                <Td>+/- with 2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>changePercent</Code></Td>
                <Td>Percentage change from previous close</Td>
                <Td>+X.XX%</Td>
              </Tr>
              <Tr>
                <Td><Code>volume</Code></Td>
                <Td>Trading volume</Td>
                <Td>K/M/B suffix</Td>
              </Tr>
              <Tr>
                <Td><Code>avgVolume</Code></Td>
                <Td>Average volume</Td>
                <Td>K/M/B suffix</Td>
              </Tr>
              <Tr>
                <Td><Code>bidSize</Code></Td>
                <Td>Size at bid</Td>
                <Td>K/M/B suffix</Td>
              </Tr>
              <Tr>
                <Td><Code>askSize</Code></Td>
                <Td>Size at ask</Td>
                <Td>K/M/B suffix</Td>
              </Tr>
              <Tr>
                <Td><Code>marketCap</Code></Td>
                <Td>Market capitalization</Td>
                <Td>$X.XXM/B/T</Td>
              </Tr>
              <Tr>
                <Td><Code>week52High</Code></Td>
                <Td>52-week high</Td>
                <Td>2 decimals</Td>
              </Tr>
              <Tr>
                <Td><Code>week52Low</Code></Td>
                <Td>52-week low</Td>
                <Td>2 decimals</Td>
              </Tr>
            </Tbody>
          </Table>
        </Box>

        {/* Badge Color Mappings Section */}
        <Box>
          <Heading size="md" mb={4}>Badge Color Mappings</Heading>
          <Text mb={3}>
            For badges, you can configure color mappings based on the value. Supports both string matching and numeric comparisons.
          </Text>

          <Heading size="sm" mb={3}>String Matching</Heading>
          <Table variant="simple" size="sm" bg="gray.800" borderRadius="md" mb={4}>
            <Thead>
              <Tr>
                <Th color="gray.400">Mode</Th>
                <Th color="gray.400">Example</Th>
                <Th color="gray.400">Matches</Th>
              </Tr>
            </Thead>
            <Tbody>
              <Tr>
                <Td>Exact</Td>
                <Td><Code>Disconnected</Code></Td>
                <Td>Only "Disconnected" (case-insensitive)</Td>
              </Tr>
              <Tr>
                <Td>Contains</Td>
                <Td><Code>Connected</Code></Td>
                <Td>"Connected (7496)", "Connected", etc.</Td>
              </Tr>
            </Tbody>
          </Table>

          <Heading size="sm" mb={3}>Numeric Comparisons</Heading>
          <Text fontSize="sm" color="gray.400" mb={3}>
            For numeric values (like quote data), use comparison operators. Exact match mode must be enabled (Contains unchecked).
          </Text>
          <Table variant="simple" size="sm" bg="gray.800" borderRadius="md">
            <Thead>
              <Tr>
                <Th color="gray.400">Operator</Th>
                <Th color="gray.400">Example</Th>
                <Th color="gray.400">Description</Th>
              </Tr>
            </Thead>
            <Tbody>
              <Tr>
                <Td><Code>&gt;</Code></Td>
                <Td><Code>&gt;20</Code></Td>
                <Td>Greater than 20</Td>
              </Tr>
              <Tr>
                <Td><Code>&gt;=</Code></Td>
                <Td><Code>&gt;=30</Code></Td>
                <Td>Greater than or equal to 30</Td>
              </Tr>
              <Tr>
                <Td><Code>&lt;</Code></Td>
                <Td><Code>&lt;15</Code></Td>
                <Td>Less than 15</Td>
              </Tr>
              <Tr>
                <Td><Code>&lt;=</Code></Td>
                <Td><Code>&lt;=10</Code></Td>
                <Td>Less than or equal to 10</Td>
              </Tr>
              <Tr>
                <Td><Code>=</Code></Td>
                <Td><Code>=25</Code></Td>
                <Td>Exactly 25</Td>
              </Tr>
              <Tr>
                <Td><Code>!=</Code></Td>
                <Td><Code>!=0</Code></Td>
                <Td>Not equal to 0</Td>
              </Tr>
            </Tbody>
          </Table>
        </Box>

        {/* Usage Notes Section */}
        <Box>
          <Heading size="md" mb={4}>Usage Notes</Heading>
          <VStack align="stretch" spacing={2} fontSize="sm" color="gray.300">
            <Text>
              • Both placeholder types can be mixed in the same label: <Code fontSize="xs">${'{tws_connection_status:5}'} | @{'{SPY:last:3}'}</Code>
            </Text>
            <Text>
              • If multiple placeholders exist, the smallest interval determines the polling rate for system placeholders.
            </Text>
            <Text>
              • Symbol quotes use the same backend as the Live Watchlist component.
            </Text>
            <Text>
              • SSE connections are shared across all components using real-time quotes.
            </Text>
            <Text>
              • Symbols are automatically normalized to uppercase.
            </Text>
          </VStack>
        </Box>
      </VStack>
    </Box>
  )
}

export default PlaceholderDocsPage
