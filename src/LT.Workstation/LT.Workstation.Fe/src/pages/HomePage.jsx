import { useState, useEffect } from 'react'
import { Box, Heading, Text, VStack, HStack, Badge, Spinner, Link } from '@chakra-ui/react'
import { ExternalLinkIcon } from '@chakra-ui/icons'
import PageNavMenu from '../components/PageNavMenu'

function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })
}

function HomePage() {
  const [entries, setEntries] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/whats-new?limit=5')
      .then(r => r.json())
      .then(data => setEntries(Array.isArray(data) ? data : []))
      .catch(() => setEntries([]))
      .finally(() => setLoading(false))
  }, [])

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu />
      <Box px={8} py={12} maxW="760px">
        <VStack spacing={4} align="flex-start" mb={12}>
          <Heading size="xl" color="blue.300">Limitless Trading</Heading>
          <Text color="gray.300" fontSize="lg">
            Trading analysis toolkit for Interactive Brokers.
          </Text>
        </VStack>

        <Heading size="md" color="gray.100" mb={6}>What's New</Heading>

        {loading ? (
          <Spinner color="blue.400" />
        ) : entries.length === 0 ? (
          <Text color="gray.500">No release notes available.</Text>
        ) : (
          <VStack spacing={0} align="stretch" divider={<Box borderBottom="1px solid" borderColor="gray.700" />}>
            {entries.map((entry, i) => (
              <Box key={entry.id} py={6}>
                <HStack mb={2} spacing={3} align="center">
                  <Text fontWeight="bold" color={i === 0 ? 'white' : 'whiteAlpha.500'} fontSize="lg">
                    v{entry.version}
                  </Text>
                  {i === 0 && (
                    <Badge colorScheme="blue" variant="subtle" fontSize="xs" letterSpacing="wider">
                      LATEST
                    </Badge>
                  )}
                  {entry.tags?.map(tag => (
                    <Badge key={tag} variant="outline" colorScheme="gray" fontSize="xs">
                      {tag}
                    </Badge>
                  ))}
                  <Text color="gray.500" fontSize="xs" letterSpacing="wider" ml="auto">
                    {formatDate(entry.publishedAt)}
                  </Text>
                </HStack>
                <Text fontWeight="semibold" color={i === 0 ? 'gray.200' : 'whiteAlpha.500'} mb={2}>
                  {entry.title}
                </Text>
                <Text
                  color={i === 0 ? 'gray.400' : 'whiteAlpha.400'}
                  fontSize="sm"
                  whiteSpace="pre-wrap"
                >
                  {entry.body}
                </Text>
              </Box>
            ))}
          </VStack>
        )}

        <Box mt={6}>
          <Link
            href="https://limitlessworkstation.com/whats-new"
            isExternal
            color="blue.400"
            fontSize="sm"
          >
            See all releases <ExternalLinkIcon mx="2px" />
          </Link>
        </Box>
      </Box>
    </Box>
  )
}

export default HomePage
