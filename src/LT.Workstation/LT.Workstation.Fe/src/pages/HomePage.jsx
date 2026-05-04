import { Box, Heading, Text, VStack } from '@chakra-ui/react'
import PageNavMenu from '../components/PageNavMenu'

function HomePage() {
  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu />
      <Box px={8} py={12}>
        <VStack spacing={4} align="flex-start" maxW="600px">
          <Heading size="xl" color="blue.300">Limitless Trading</Heading>
          <Text color="gray.300" fontSize="lg">
            Trading analysis toolkit for Interactive Brokers.
          </Text>
        </VStack>
      </Box>
    </Box>
  )
}

export default HomePage
