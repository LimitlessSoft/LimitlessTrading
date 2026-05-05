import { useState, useEffect } from 'react'
import { Box, Heading, Text, HStack, IconButton, Spinner } from '@chakra-ui/react'
import { ArrowBackIcon } from '@chakra-ui/icons'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'

function PlaceholderDocsPage() {
  const navigate = useNavigate()
  const [docs, setDocs] = useState(null)

  useEffect(() => {
    axios.get('/api/placeholder-docs').then(r => setDocs(r.data)).catch(() => setDocs('Failed to load placeholder reference.'))
  }, [])

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
        {docs == null
          ? <Spinner color="blue.400" />
          : <Text as="pre" fontFamily="mono" fontSize="sm" whiteSpace="pre-wrap" color="gray.200">{docs}</Text>
        }
      </Box>
    </Box>
  )
}

export default PlaceholderDocsPage
