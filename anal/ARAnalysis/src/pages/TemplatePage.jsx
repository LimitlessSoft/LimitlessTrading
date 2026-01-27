import { useState, useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { Box, Heading, Text, IconButton } from '@chakra-ui/react'
import { EditIcon, AddIcon } from '@chakra-ui/icons'
import { fetchRoutes } from '../modules/routesService'
import PageNavMenu from '../components/PageNavMenu'

function TemplatePage() {
  const location = useLocation()
  const [pageInfo, setPageInfo] = useState(null)

  useEffect(() => {
    const loadPageInfo = async () => {
      try {
        const routes = await fetchRoutes()
        const matchingRoute = routes.find(r => r.path === location.pathname)
        setPageInfo(matchingRoute || null)
      } catch (err) {
        console.error('Failed to load page info:', err)
        setPageInfo(null)
      }
    }
    loadPageInfo()
  }, [location.pathname])

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu>
        <IconButton
          icon={<EditIcon />}
          variant="ghost"
          size="sm"
          aria-label="Edit page"
          _hover={{ bg: 'whiteAlpha.200' }}
          ml={1}
        />
        <IconButton
          icon={<AddIcon />}
          variant="ghost"
          size="sm"
          aria-label="Add widget"
          _hover={{ bg: 'whiteAlpha.200' }}
          ml={1}
        />
      </PageNavMenu>
      <Box p={8}>
        {pageInfo ? (
          <>
            <Heading size="lg" mb={2}>{pageInfo.name}</Heading>
            <Text color="gray.500">{pageInfo.path}</Text>
          </>
        ) : (
          <Text color="gray.500">{location.pathname}</Text>
        )}
      </Box>
    </Box>
  )
}

export default TemplatePage
