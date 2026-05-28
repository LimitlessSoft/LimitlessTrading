import {
  Box, Heading, Text, Code, Table, Thead, Tbody, Tr, Th, Td,
  VStack, HStack, IconButton, Tabs, TabList, TabPanels, Tab, TabPanel,
  Alert, AlertIcon,
} from '@chakra-ui/react'
import { ArrowBackIcon } from '@chakra-ui/icons'
import { useNavigate } from 'react-router-dom'

function SectionTable({ headers, rows }) {
  return (
    <Table variant="simple" size="sm" bg="gray.800" borderRadius="md">
      <Thead>
        <Tr>
          {headers.map(h => <Th key={h} color="gray.400">{h}</Th>)}
        </Tr>
      </Thead>
      <Tbody>
        {rows.map((row, i) => (
          <Tr key={i}>
            {row.map((cell, j) => (
              <Td key={j}>{typeof cell === 'string' ? cell : cell}</Td>
            ))}
          </Tr>
        ))}
      </Tbody>
    </Table>
  )
}

function SubHeading({ children }) {
  return <Heading size="sm" mb={3} mt={5}>{children}</Heading>
}

function PlaceholderDocsPage() {
  const navigate = useNavigate()

  return (
    <Box minH="100vh" bg="gray.900" color="white" p={8}>
      <HStack mb={6} spacing={3}>
        <IconButton
          icon={<ArrowBackIcon />}
          aria-label="Back to Documentation"
          variant="ghost"
          size="sm"
          onClick={() => navigate('/docs')}
          _hover={{ bg: 'whiteAlpha.200' }}
        />
        <Heading size="lg">Placeholder Reference</Heading>
      </HStack>

      <Box maxW="960px">
        <Tabs variant="enclosed" colorScheme="blue">
          <TabList>
            <Tab>System</Tab>
            <Tab>Exchange</Tab>
            <Tab>Symbol Quotes</Tab>
            <Tab>Formulas</Tab>
            <Tab>Positions</Tab>
            <Tab>Actions</Tab>
          </TabList>

          <TabPanels>

            {/* ── SYSTEM ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}>Syntax: <Code>${'{name:seconds}'}</Code> — system-level status values.</Text>
                  <Text fontSize="sm" color="gray.400">The <Code>:seconds</Code> interval controls the refresh rate.</Text>
                </Box>
                <SectionTable
                  headers={['Placeholder', 'Description', 'Example Output']}
                  rows={[
                    [<Code>${'{tws_connection_status:5}'}</Code>, 'TWS/IBKR connection status', 'Connected (7496) / Disconnected'],
                    [<Code>${'{fomc_next:3600}'}</Code>, 'Next FOMC decision date', 'Jun 17 2026'],
                    [<Code>${'{fomc_countdown:60}'}</Code>, 'Countdown to next FOMC decision', '49d 3h 22m / 2h 15m 30s'],
                  ]}
                />
                <Alert status="info" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  Mix system placeholders with other types in the same widget text. The smallest interval among all placeholders drives the poll rate.
                </Alert>
              </VStack>
            </TabPanel>

            {/* ── EXCHANGE ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}>Syntax: <Code>${'{EXCHANGE:property:seconds}'}</Code> — exchange time or market status.</Text>
                  <Text fontSize="sm" color="gray.400">The <Code>:seconds</Code> interval is optional and defaults to 5 s.</Text>
                </Box>

                <SubHeading>Properties</SubHeading>
                <SectionTable
                  headers={['Property', 'Description', 'Example output']}
                  rows={[
                    [<Code>time</Code>, 'Current local time at the exchange', '09:32 ET'],
                    [<Code>status</Code>, 'Current market session', 'Open / Pre-Market / After-Hours / Closed'],
                  ]}
                />

                <SubHeading>Examples</SubHeading>
                <SectionTable
                  headers={['Placeholder', 'Description']}
                  rows={[
                    [<Code>${'{NYSE:time:1}'}</Code>, 'NYSE local time, updates every second'],
                    [<Code>${'{NYSE:status:10}'}</Code>, 'NYSE market status, updates every 10 s'],
                    [<Code>${'{LSE:time:30}'}</Code>, 'LSE local time, updates every 30 s'],
                    [<Code>${'{JPX:status}'}</Code>, 'JPX status (default 5 s). Returns "Closed" during lunch break.'],
                  ]}
                />

                <SubHeading>Supported Exchanges</SubHeading>
                <SectionTable
                  headers={['Key', 'Exchange', 'Timezone', 'Regular hours']}
                  rows={[
                    [<Code>NYSE</Code>, 'New York Stock Exchange', 'America/New_York', '09:30–16:00 ET'],
                    [<Code>NASDAQ</Code>, 'NASDAQ', 'America/New_York', '09:30–16:00 ET'],
                    [<Code>LSE</Code>, 'London Stock Exchange', 'Europe/London', '08:00–16:30 GMT/BST'],
                    [<Code>XETRA</Code>, 'Deutsche Börse XETRA', 'Europe/Berlin', '09:00–17:30 CET/CEST'],
                    [<Code>TSX</Code>, 'Toronto Stock Exchange', 'America/Toronto', '09:30–16:00 ET'],
                    [<Code>JPX</Code>, 'Japan Exchange Group', 'Asia/Tokyo', '09:00–15:30 JST (lunch 11:30–12:30)'],
                    [<Code>HKEX</Code>, 'Hong Kong Exchange', 'Asia/Hong_Kong', '09:30–16:00 HKT (lunch 12:00–13:00)'],
                    [<Code>ASX</Code>, 'Australian Securities Exchange', 'Australia/Sydney', '10:00–16:00 AEST/AEDT'],
                    [<Code>SGX</Code>, 'Singapore Exchange', 'Asia/Singapore', '09:00–17:00 SGT'],
                  ]}
                />
              </VStack>
            </TabPanel>

            {/* ── SYMBOL QUOTES ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={3}>Syntax: <Code>{'@{SYMBOL:property-arg1-arg2:seconds}'}</Code></Text>
                  <SectionTable
                    headers={['Part', 'Description']}
                    rows={[
                      [<Code>SYMBOL</Code>, 'Ticker symbol, e.g. AAPL, SPY, VIX. Normalized to uppercase.'],
                      [<Code>property</Code>, 'Data field to retrieve — see All Properties below.'],
                      [<Code>-arg1-arg2</Code>, 'Optional dash-separated arguments for properties that accept them, e.g. close-20.'],
                      [<Code>:seconds</Code>, 'Optional refresh interval. Defaults to 5. Values ≤5 stream via SSE; >5 use HTTP polling.'],
                    ]}
                  />
                </Box>

                <SubHeading>Quick Examples</SubHeading>
                <SectionTable
                  headers={['Example', 'Description']}
                  rows={[
                    [<Code>@{'{AAPL:last:5}'}</Code>, 'AAPL last price, every 5 s'],
                    [<Code>@{'{SPY:change:3}'}</Code>, 'SPY price change (+/- format), every 3 s'],
                    [<Code>@{'{TSLA:changePercent:5}'}</Code>, 'TSLA % change (+X.XX%)'],
                    [<Code>@{'{MSFT:volume:10}'}</Code>, 'MSFT volume (K/M/B), polled every 10 s'],
                    [<Code>@{'{VIX:last}'}</Code>, 'VIX value (default 5 s refresh)'],
                    [<Code>@{'{SPY:callVol:300}'}</Code>, 'SPY total call volume (near-money sample), refreshed every 5 min'],
                    [<Code>@{'{SPY:putVol:300}'}</Code>, 'SPY total put volume'],
                    [<Code>@{'{SPY:pcRatio:300}'}</Code>, 'SPY put/call ratio (>1 = more put activity)'],
                    [<Code fontSize="xs">{'%[@{SPY:putVol} / (@{SPY:callVol} + @{SPY:putVol}) * 100:300]'}</Code>, 'SPY put volume as % of total — useful as a graph series'],
                  ]}
                />

                <SubHeading>All Properties</SubHeading>
                <SectionTable
                  headers={['Property', 'Arguments', 'Description', 'Format']}
                  rows={[
                    [<Code>last</Code>, '—', 'Last traded price', '2 decimals'],
                    [<Code>bid</Code>, '—', 'Current bid price', '2 decimals'],
                    [<Code>ask</Code>, '—', 'Current ask price', '2 decimals'],
                    [<Code>open</Code>, '—', 'Opening price', '2 decimals'],
                    [<Code>high</Code>, '—', 'Day high', '2 decimals'],
                    [<Code>low</Code>, '—', 'Day low', '2 decimals'],
                    [<Code>close</Code>, '—', 'Closing price', '2 decimals'],
                    [<HStack pl={4} spacing={1}><Text color="gray.500">└</Text><Code>close-N</Code></HStack>, <Text fontSize="xs"><Code>N</Code> — trading days back</Text>, 'Historical closing price N trading days ago. Cached server-side for 1 hour.', '2 decimals'],
                    [<Code>prevClose</Code>, '—', 'Previous day close', '2 decimals'],
                    [<Code>vwap</Code>, '—', 'Volume-weighted average price', '2 decimals'],
                    [<Code>change</Code>, '—', 'Price change from prev close', '+/- 2 decimals'],
                    [<Code>changePercent</Code>, '—', 'Percentage change from prev close', '+X.XX%'],
                    [<Code>volume</Code>, '—', 'Trading volume', 'K/M/B suffix'],
                    [<Code>avgVolume</Code>, '—', 'Average volume', 'K/M/B suffix'],
                    [<Code>bidSize</Code>, '—', 'Size at bid', 'K/M/B suffix'],
                    [<Code>askSize</Code>, '—', 'Size at ask', 'K/M/B suffix'],
                    [<Code>marketCap</Code>, '—', 'Market capitalization', '$X.XXM/B/T'],
                    [<Code>week52High</Code>, '—', '52-week high', '2 decimals'],
                    [<Code>week52Low</Code>, '—', '52-week low', '2 decimals'],
                    [<Code>callVol</Code>, '—', 'Total call volume across near-money options (2 nearest expirations, 15 closest strikes). Cached 5 min.', 'K/M suffix'],
                    [<Code>putVol</Code>, '—', 'Total put volume across near-money options (same sample as callVol).', 'K/M suffix'],
                    [<Code>pcRatio</Code>, '—', 'Put/call volume ratio (putVol ÷ callVol). Values >1 = more put activity; <1 = more call activity.', '2 decimals'],
                  ]}
                />
              </VStack>
            </TabPanel>

            {/* ── FORMULAS ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}>Syntax: <Code>%[expression:seconds]</Code> — arithmetic over live quote data.</Text>
                  <Text fontSize="sm" color="gray.300" mb={1}>
                    Reference ticker values as <Code>{'@{SYMBOL:property}'}</Code> (no per-ref interval — the formula's <Code>:seconds</Code> applies to all). Result is formatted to 2 decimal places. Shows <Code>--</Code> if any value is unavailable.
                  </Text>
                  <Text fontSize="sm" color="gray.400">
                    Operators: <Code>+</Code> <Code>-</Code> <Code>*</Code> <Code>/</Code> <Code>**</Code> and grouping with <Code>()</Code>.
                  </Text>
                </Box>

                <SubHeading>Examples</SubHeading>
                <SectionTable
                  headers={['Formula', 'Description']}
                  rows={[
                    [<Code>{'%[(@{AAPL:ask} + @{AAPL:bid}) / 2:3]'}</Code>, 'AAPL bid-ask midpoint'],
                    [<Code>{'%[(@{SPY:last} / @{SPY:vwap} - 1) * 100:5]'}</Code>, 'SPY deviation from VWAP (%)'],
                    [<Code>{'%[@{HYG:last} - @{LQD:last}:30]'}</Code>, 'Price spread: HYG minus LQD'],
                    [<Code>{'%[(@{HYG:last} / @{LQD:last} - 1) * 100:30]'}</Code>, 'HYG vs LQD relative performance (%)'],
                    [<Code>{'%[@{HYG:changePercent} - @{LQD:changePercent}:10]'}</Code>, 'Day % change spread: HYG vs LQD'],
                    [<Code>{'%[@{AAPL:last} * 150 + @{MSFT:last} * 80:10]'}</Code>, 'Weighted position value (150 AAPL + 80 MSFT)'],
                    [<Code>{'%[(@{HYG:last} / @{HYG:close-20} - 1) * 100:3600]'}</Code>, 'HYG 20-day return (%)'],
                    [<Code>{'%[@{VIX:last} - @{VIX:close-20}:3600]'}</Code>, 'VIX change vs 20 days ago'],
                    [<Code fontSize="xs">{'%[(@{HYG:last}/@{HYG:close-20}-1)*100 - (@{LQD:last}/@{LQD:close-20}-1)*100:3600]'}</Code>, 'HYG vs LQD 20-day relative performance'],
                  ]}
                />

                <Alert status="warning" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  When mixing <Code>close-N</Code> with real-time properties, use a slow interval (e.g. <Code>:3600</Code>) — historical closes only update once per trading day.
                </Alert>
              </VStack>
            </TabPanel>

            {/* ── POSITIONS ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}>Syntax: <Code>{'&{SYMBOL:property:seconds}'}</Code> — portfolio position data from IBKR.</Text>
                  <Text fontSize="sm" color="gray.400">Polled from <Code>/api/account/positions</Code>. Default refresh interval is 30 s. Requires an active IBKR connection.</Text>
                </Box>

                <SubHeading>Properties</SubHeading>
                <SectionTable
                  headers={['Property', 'Description', 'Format']}
                  rows={[
                    [<Code>position</Code>, 'Number of shares/contracts held (negative = short)', 'Integer'],
                    [<Code>avgCost</Code>, 'Average cost per share/contract', '2 decimals'],
                  ]}
                />

                <SubHeading>Examples</SubHeading>
                <SectionTable
                  headers={['Placeholder', 'Description']}
                  rows={[
                    [<Code>{'&{AAPL:position:30}'}</Code>, 'Shares of AAPL held, refreshed every 30 s'],
                    [<Code>{'&{AAPL:avgCost:30}'}</Code>, 'Average cost basis for AAPL'],
                    [<Code fontSize="xs">{'%[&{AAPL:position} * @{AAPL:last}:10]'}</Code>, 'Live market value of AAPL position'],
                    [<Code fontSize="xs">{'%[(@{AAPL:last} - &{AAPL:avgCost}) * &{AAPL:position}:10]'}</Code>, 'Unrealized P&L for AAPL'],
                    [<Code fontSize="xs">{'%[(&{AAPL:position} * @{AAPL:last}) / #{NetLiquidation} * 100:10]'}</Code>, 'AAPL position as % of portfolio'],
                  ]}
                />

                <Alert status="info" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  Position values are substituted as plain numbers before formula evaluation, so <Code>{'&{...}'}</Code> refs work freely inside <Code>{'%[...]'}</Code> formulas.
                </Alert>

                <Alert status="warning" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  Returns <Code>--</Code> for symbols not currently held or when IBKR is disconnected.
                </Alert>

                <SubHeading>All Positions</SubHeading>
                <Box>
                  <Text mb={1}>Syntax: <Code>{'&{positions:seconds}'}</Code> — returns all open positions as a JSON array.</Text>
                  <Text fontSize="sm" color="gray.400" mb={3}>Use as the <Code>source</Code> expression for a table widget. Default refresh 30 s.</Text>
                </Box>
                <SectionTable
                  headers={['Field', 'Description']}
                  rows={[
                    [<Code>description</Code>, 'Human-readable label, e.g. "SPY May05\'26 722 PUT"'],
                    [<Code>symbol</Code>, 'Ticker symbol'],
                    [<Code>localSymbol</Code>, 'Exchange-specific symbol'],
                    [<Code>position</Code>, 'Quantity held (negative = short)'],
                    [<Code>avgCost</Code>, 'Average cost per share/contract'],
                    [<Code>secType</Code>, 'Security type, e.g. STK, OPT'],
                    [<Code>expiry</Code>, 'Expiration date (options)'],
                    [<Code>strike</Code>, 'Strike price (options)'],
                    [<Code>right</Code>, 'C or P (options)'],
                    [<Code>multiplier</Code>, 'Contract multiplier (options)'],
                  ]}
                />
                <Alert status="info" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  Set a table widget's <Code>source</Code> to <Code>{'&{positions:30}'}</Code> and use <Code>{'{description}'}</Code>, <Code>{'{position}'}</Code>, <Code>{'{avgCost}'}</Code> as column expressions.
                </Alert>
              </VStack>
            </TabPanel>

            {/* ── ACTIONS ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}>Syntax: <Code>{'![kind:param1:param2:...]'}</Code></Text>
                  <Text fontSize="sm" color="gray.400">
                    Action expressions fire an operation when triggered by a widget. Unlike placeholders, they produce no display value — they are stored in widget config and executed on demand (e.g. button click). The first segment is the <Code>kind</Code>, which determines the available parameters.
                  </Text>
                </Box>

                <Alert status="info" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  Action expressions can only be used in widgets that support them (button, order-ticket). They cannot appear in label text or any read-only field.
                </Alert>

                <SubHeading>Live value references</SubHeading>
                <Text fontSize="sm" color="gray.300">
                  Any parameter value can use the same <Code>{'@{...}'}</Code> quote syntax from the Symbol Quotes tab — resolved once at the moment the action fires, not on a timer. For example, <Code>{'@{AAPL:ask}'}</Code> as a price parameter will use the last known ask price at the time the action is triggered.
                </Text>

                <Heading size="xs" mt={4} mb={2} color="gray.300">order</Heading>
                <Text fontSize="sm" color="gray.400" mb={2}>
                  Places an IBKR order. Syntax: <Code>{'![order:action:symbol:quantity:orderType:limitPrice?:stopPrice?]'}</Code>
                </Text>
                <SectionTable
                  headers={['Parameter', 'Values', 'Notes']}
                  rows={[
                    [<Code>action</Code>, 'BUY | SELL', ''],
                    [<Code>symbol</Code>, 'Ticker string', 'Normalized to uppercase'],
                    [<Code>quantity</Code>, 'Integer or @{...} placeholder', 'Resolved at fire time'],
                    [<Code>orderType</Code>, 'MKT | LMT | STP | STP_LMT', ''],
                    [<Code>limitPrice</Code>, 'Number or @{...} placeholder', 'Required for LMT and STP_LMT'],
                    [<Code>stopPrice</Code>, 'Number or @{...} placeholder', 'Required for STP and STP_LMT'],
                  ]}
                />
                <SectionTable
                  headers={['Expression', 'Description']}
                  rows={[
                    [<Code>{'![order:BUY:AAPL:100:MKT]'}</Code>, 'Buy 100 AAPL at market price'],
                    [<Code>{'![order:SELL:AAPL:100:LMT:185.50]'}</Code>, 'Sell 100 AAPL at limit $185.50'],
                    [<Code>{'![order:BUY:SPY:50:LMT:@{SPY:ask}]'}</Code>, 'Buy 50 SPY at limit = live ask price'],
                    [<Code>{'![order:SELL:TSLA:10:STP:220.00]'}</Code>, 'Sell 10 TSLA with stop at $220.00'],
                    [<Code>{'![order:BUY:QQQ:20:STP_LMT:410.00:408.50]'}</Code>, 'Buy 20 QQQ stop-limit: stop $410, limit $408.50'],
                  ]}
                />
              </VStack>
            </TabPanel>

          </TabPanels>
        </Tabs>

        <Box mt={6} p={4} bg="gray.800" borderRadius="md" fontSize="sm" color="gray.400">
          <Text fontWeight="semibold" color="gray.300" mb={2}>General Notes</Text>
          <VStack align="stretch" spacing={1}>
            <Text>• Placeholder types can be mixed in the same widget: <Code fontSize="xs">${'{tws_connection_status:5}'} | @{'{SPY:last:3}'}</Code></Text>
            <Text>• Symbols are automatically normalized to uppercase.</Text>
            <Text>• SSE connections are shared across all components using real-time quotes.</Text>
          </VStack>
        </Box>
      </Box>
    </Box>
  )
}

export default PlaceholderDocsPage
