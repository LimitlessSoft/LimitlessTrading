import {
  Box, Heading, Text, Code, Table, Thead, Tbody, Tr, Th, Td,
  VStack, HStack, IconButton, Tabs, TabList, TabPanels, Tab, TabPanel,
  Alert, AlertIcon, Badge,
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

function JsonBlock({ children }) {
  let display = children
  try { display = JSON.stringify(JSON.parse(children), null, 2) } catch {}
  return (
    <Box as="pre" bg="gray.800" border="1px solid" borderColor="gray.700" borderRadius="md"
      p={3} fontFamily="mono" fontSize="xs" color="green.300" overflowX="auto" whiteSpace="pre-wrap">
      {display}
    </Box>
  )
}

function WidgetDocsPage() {
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
        <Heading size="lg">Widget Reference</Heading>
      </HStack>

      <Box maxW="960px">
        <Tabs variant="enclosed" colorScheme="blue">
          <TabList>
            <Tab>Layout</Tab>
            <Tab>label &amp; badge</Tab>
            <Tab>gauge &amp; graph &amp; chart</Tab>
            <Tab>panel &amp; table</Tab>
            <Tab>Style</Tab>
          </TabList>

          <TabPanels>

            {/* ── LAYOUT ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={1}><strong>Width</strong> is in grid segments (1–20, where 20 = full page width). <strong>Height</strong> is in pixels.</Text>
                  <Text fontSize="sm" color="gray.400">When placing widgets side by side, their widths must sum to exactly 20.</Text>
                </Box>

                <SubHeading>Column Examples</SubHeading>
                <SectionTable
                  headers={['Layout', 'Widths']}
                  rows={[
                    ['2 equal columns', '10 / 10'],
                    ['3 equal columns', '7 / 7 / 6'],
                    ['4 equal columns', '5 / 5 / 5 / 5'],
                  ]}
                />

                <SubHeading>Fill Height</SubHeading>
                <Box>
                  <Text fontSize="sm" mb={2}>
                    Set <Code>dock: "fill-height"</Code> to make a widget fill all remaining viewport height after fixed-height rows. Omit <Code>height</Code> when using this.
                  </Text>
                </Box>
                <SectionTable
                  headers={['Case', 'How to configure']}
                  rows={[
                    ['Single fill-height row', <Text fontSize="sm"><Code>dock: "fill-height"</Code> — omit <Code>heightRows</Code></Text>],
                    ['Multiple equal fill-height rows', <Text fontSize="sm"><Code>dock: "fill-height"</Code> + <Code>heightRows: N</Code> (e.g. 2 rows → <Code>heightRows: 2</Code>)</Text>],
                  ]}
                />
                <Alert status="warning" bg="gray.800" borderRadius="md" fontSize="sm">
                  <AlertIcon />
                  <Code>dock: "fill-height"</Code> does not work inside panels. Use fixed pixel heights for panel children.
                </Alert>
              </VStack>
            </TabPanel>

            {/* ── LABEL & BADGE ── */}
            <TabPanel>
              <VStack align="stretch" spacing={6}>

                <Box>
                  <HStack mb={2}><Heading size="sm">label</Heading><Badge colorScheme="blue">display</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Displays text with optional placeholder expressions. Supports conditional styles.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>text</Code>, '✓', '—', 'String with placeholder expressions'],
                      [<Code>width</Code>, '', '4', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '60', 'Pixels'],
                      [<Code>title</Code>, '', '—', 'Optional label above the widget'],
                      [<Code>style</Code>, '', '—', 'Visual overrides — see Style tab'],
                    ]}
                  />
                  <SubHeading>Example</SubHeading>
                  <JsonBlock>{'{"type":"label","text":"@{SPY:last:5}","width":4,"height":60}'}</JsonBlock>
                </Box>

                <Box>
                  <HStack mb={2}><Heading size="sm">badge</Heading><Badge colorScheme="green">indicator</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Colored circle or full-width indicator. Color is driven by conditional styles.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>placeholder</Code>, '✓', '—', 'Expression to evaluate for conditional styling'],
                      [<Code>width</Code>, '', '2', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '40', 'Pixels'],
                      [<Code>displayMode</Code>, '', '"circle"', '"circle" or "full" (full-width bar)'],
                      [<Code>showValueAsText</Code>, '', 'false', 'Show the resolved placeholder value as text'],
                      [<Code>text</Code>, '', '—', 'Static or placeholder text to display'],
                      [<Code>style</Code>, '', '—', 'Visual overrides — see Style tab'],
                    ]}
                  />
                  <SubHeading>Example</SubHeading>
                  <JsonBlock>{'{"type":"badge","placeholder":"${tws_connection_status:5}","width":2,"height":40}'}</JsonBlock>
                </Box>

              </VStack>
            </TabPanel>

            {/* ── GAUGE & GRAPH & CHART ── */}
            <TabPanel>
              <VStack align="stretch" spacing={6}>

                <Box>
                  <HStack mb={2}><Heading size="sm">gauge</Heading><Badge colorScheme="orange">visualization</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Semicircle speedometer with colored arc bands and a needle.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>placeholder</Code>, '✓', '—', 'Expression that resolves to a numeric value'],
                      [<Code>min</Code>, '✓', '—', 'Minimum value on the gauge'],
                      [<Code>max</Code>, '✓', '—', 'Maximum value on the gauge'],
                      [<Code>segments</Code>, '✓', '—', 'Array of {from, to, color} colored arc bands'],
                      [<Code>title</Code>, '', '—', 'Label below the needle'],
                      [<Code>width</Code>, '', '6', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '160', 'Pixels'],
                      [<Code>style</Code>, '', '—', 'Visual overrides'],
                    ]}
                  />
                  <SubHeading>Example — VIX gauge</SubHeading>
                  <JsonBlock>{'{"type":"gauge","placeholder":"@{VIX:last:5}","min":0,"max":80,"title":"VIX","segments":[{"from":0,"to":20,"color":"#48BB78"},{"from":20,"to":40,"color":"#ECC94B"},{"from":40,"to":60,"color":"#ED8936"},{"from":60,"to":80,"color":"#F56565"}],"width":6,"height":160}'}</JsonBlock>
                </Box>

                <Box>
                  <HStack mb={2}><Heading size="sm">graph</Heading><Badge colorScheme="purple">visualization</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Multi-series rolling time-series chart (line or bars).</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>series</Code>, '✓', '—', 'Array of series objects — see below'],
                      [<Code>maxHistory</Code>, '', '200', 'Number of data points to retain'],
                      [<Code>snapshotInterval</Code>, '', '300', 'Seconds between snapshots'],
                      [<Code>title</Code>, '', '—', 'Chart title'],
                      [<Code>width</Code>, '', '10', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '220', 'Pixels'],
                      [<Code>style</Code>, '', '—', 'Visual overrides'],
                    ]}
                  />
                  <SubHeading>Series object fields</SubHeading>
                  <SectionTable
                    headers={['Field', 'Description']}
                    rows={[
                      [<Code>id</Code>, 'Unique series identifier'],
                      [<Code>name</Code>, 'Display name'],
                      [<Code>expression</Code>, 'Any placeholder expression, e.g. @{SPY:last:5}'],
                      [<Code>color</Code>, 'Hex color string'],
                      [<Code>viewType</Code>, '"line" or "bars"'],
                    ]}
                  />
                  <SubHeading>Example</SubHeading>
                  <JsonBlock>{'{"type":"graph","series":[{"id":"s1","name":"SPY","expression":"@{SPY:last:5}","color":"#4299E1","viewType":"line"}],"width":10,"height":220}'}</JsonBlock>
                </Box>

                <Box>
                  <HStack mb={2}><Heading size="sm">chart</Heading><Badge colorScheme="cyan">visualization</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Interactive candlestick chart with OHLCV data from IBKR.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>symbol</Code>, '✓', '—', 'Ticker symbol'],
                      [<Code>interval</Code>, '', '"5m"', '"1m" | "2m" | "3m" | "5m" | "15m" | "30m" | "1h" | "1d"'],
                      [<Code>width</Code>, '', '10', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '220', 'Pixels'],
                      [<Code>style</Code>, '', '—', 'Visual overrides'],
                    ]}
                  />
                  <SubHeading>Example</SubHeading>
                  <JsonBlock>{'{"type":"chart","symbol":"AAPL","interval":"5m","width":10,"height":220}'}</JsonBlock>
                </Box>

              </VStack>
            </TabPanel>

            {/* ── PANEL & TABLE ── */}
            <TabPanel>
              <VStack align="stretch" spacing={6}>

                <Box>
                  <HStack mb={2}><Heading size="sm">panel</Heading><Badge colorScheme="yellow">container</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Container that holds child widgets using one of three layout modes.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>children</Code>, '✓', '—', 'Array of widget definitions'],
                      [<Code>width</Code>, '', '20', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '200', 'Pixels'],
                      [<Code>style</Code>, '', '—', 'Visual overrides including layoutMode'],
                    ]}
                  />
                  <SubHeading>Layout modes (style.layoutMode)</SubHeading>
                  <SectionTable
                    headers={['Mode', 'Description']}
                    rows={[
                      [<Code>grid</Code>, <Text fontSize="sm">Best for N side-by-side widgets. Set <Code>style.gridAxis: "columns"</Code> and <Code>style.gridCount: N</Code>. Children fill cells automatically — their width/height are ignored.</Text>],
                      [<Code>flow</Code>, 'Children fill left-to-right by width, wrapping to the next row. Children need explicit width and height.'],
                      [<Code>absolute</Code>, 'Default. Children need explicit x (0–19) to sit side by side; without x they stack vertically.'],
                    ]}
                  />
                  <SubHeading>Example — 2-column grid</SubHeading>
                  <JsonBlock>{'{"type":"panel","width":20,"height":400,"style":{"layoutMode":"grid","gridAxis":"columns","gridCount":2},"children":[{"type":"chart","symbol":"AAPL"},{"type":"chart","symbol":"GOOGL"}]}'}</JsonBlock>
                </Box>

                <Box>
                  <HStack mb={2}><Heading size="sm">table</Heading><Badge colorScheme="red">container</Badge></HStack>
                  <Text fontSize="sm" color="gray.300" mb={3}>Tabular data widget. The source expression resolves to a JSON array or object; each element becomes a row.</Text>
                  <SectionTable
                    headers={['Field', 'Required', 'Default', 'Description']}
                    rows={[
                      [<Code>source</Code>, '✓', '—', 'Expression resolving to a JSON array or object'],
                      [<Code>columns</Code>, '✓', '—', 'Array of {id, name, expression} column definitions'],
                      [<Code>title</Code>, '', '—', 'Table title'],
                      [<Code>width</Code>, '', '10', 'Grid segments (1–20)'],
                      [<Code>height</Code>, '', '200', 'Pixels'],
                      [<Code>style</Code>, '', '—', 'Visual overrides'],
                    ]}
                  />
                  <SubHeading>Column expressions</SubHeading>
                  <Text fontSize="sm" color="gray.300" mb={2}>
                    Use <Code>{'{fieldName}'}</Code> tokens referencing fields on each row object.
                    Fallback syntax: <Code>{'{field1 ?? field2 ?? field3}'}</Code> — resolves to the first non-empty value.
                  </Text>
                  <SubHeading>Example — positions table</SubHeading>
                  <JsonBlock>{'{"type":"table","source":"&{positions:30}","columns":[{"id":"c1","name":"Instrument","expression":"{description ?? localSymbol ?? symbol}"},{"id":"c2","name":"Qty","expression":"{position}"},{"id":"c3","name":"Avg Cost","expression":"{avgCost}"}],"width":10,"height":200}'}</JsonBlock>
                </Box>

              </VStack>
            </TabPanel>

            {/* ── STYLE ── */}
            <TabPanel>
              <VStack align="stretch" spacing={4}>
                <Box>
                  <Text mb={3}>Applies to <Code>label</Code>, <Code>badge</Code>, and <Code>panel</Code>. All fields are optional.</Text>
                  <SubHeading>Default style</SubHeading>
                  <JsonBlock>{'{"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}'}</JsonBlock>
                </Box>

                <Box>
                  <SubHeading>Conditional styles</SubHeading>
                  <Text fontSize="sm" color="gray.300" mb={3}>
                    Available on <Code>label</Code> and <Code>badge</Code>. Add a <Code>conditionalStyles</Code> array to <Code>style</Code>.
                    The first matching rule overrides the specified color fields — <Code>null</Code> fields are not overridden.
                  </Text>
                  <SectionTable
                    headers={['Field', 'Description']}
                    rows={[
                      [<Code>id</Code>, 'Unique rule identifier'],
                      [<Code>placeholder</Code>, 'Expression to evaluate'],
                      [<Code>operator</Code>, '"equals" | "contains" | ">" | "<" | ">=" | "<=" | "!="'],
                      [<Code>value</Code>, 'String for equals/contains; number for comparison operators'],
                      [<Code>bgColor</Code>, 'Override background color, or null to leave unchanged'],
                      [<Code>textColor</Code>, 'Override text color, or null to leave unchanged'],
                      [<Code>borderColor</Code>, 'Override border color, or null to leave unchanged'],
                      [<Code>borderWidth</Code>, 'Override border width, or null to leave unchanged'],
                    ]}
                  />
                  <SubHeading>Example — green when NYSE open, red when closed</SubHeading>
                  <JsonBlock>{'{"conditionalStyles":[{"id":"r1","placeholder":"${NYSE:status:10}","operator":"equals","value":"Open","bgColor":"#276749","textColor":null,"borderColor":null,"borderWidth":null},{"id":"r2","placeholder":"${NYSE:status:10}","operator":"equals","value":"Closed","bgColor":"#742A2A","textColor":null,"borderColor":null,"borderWidth":null}]}'}</JsonBlock>
                </Box>
              </VStack>
            </TabPanel>

          </TabPanels>
        </Tabs>

        <Box mt={6} p={4} bg="gray.800" borderRadius="md" fontSize="sm" color="gray.400">
          <Text fontWeight="semibold" color="gray.300" mb={2}>General Notes</Text>
          <VStack align="stretch" spacing={1}>
            <Text>• All widgets live on template pages and are managed via the widget editor in edit mode.</Text>
            <Text>• Placeholder expressions can be used in <Code>text</Code>, <Code>placeholder</Code>, and <Code>expression</Code> fields.</Text>
            <Text>• <Code>width</Code> is always in grid segments (1–20); <Code>height</Code> is always in pixels.</Text>
          </VStack>
        </Box>
      </Box>
    </Box>
  )
}

export default WidgetDocsPage
