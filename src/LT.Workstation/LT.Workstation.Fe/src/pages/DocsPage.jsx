import { Box, Heading, Text, SimpleGrid, VStack } from '@chakra-ui/react'
import { useNavigate } from 'react-router-dom'
import PageNavMenu from '../components/PageNavMenu'

function DocCard({ title, description, onClick }) {
  return (
    <Box
      as="button"
      onClick={onClick}
      textAlign="left"
      bg="gray.800"
      border="1px solid"
      borderColor="gray.600"
      borderRadius="md"
      p={5}
      _hover={{ borderColor: 'blue.500', bg: 'gray.750' }}
      transition="border-color 0.15s, background 0.15s"
      w="100%"
    >
      <Text fontWeight="semibold" color="white" mb={1}>{title}</Text>
      <Text fontSize="sm" color="gray.400">{description}</Text>
    </Box>
  )
}

function DocsPage() {
  const navigate = useNavigate()

  return (
    <Box minH="100vh" bg="gray.900" color="white">
      <PageNavMenu />
      <Box p={8}>
        <Heading size="lg" mb={2}>Documentation</Heading>
        <Text color="gray.400" mb={8}>Reference guides for Limitless Trading features.</Text>
        <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={4} maxW="960px">
          <DocCard
            title="Placeholder Reference"
            description="System, exchange, symbol quote, formula, position, and account placeholders for use in widget labels."
            onClick={() => navigate('/docs/placeholders')}
          />
          <DocCard
            title="Widget Reference"
            description="All widget types — label, badge, gauge, graph, chart, panel, table — with required fields, layout options, and examples."
            onClick={() => navigate('/docs/widgets')}
          />
        </SimpleGrid>
      </Box>
    </Box>
  )
}

export default DocsPage
