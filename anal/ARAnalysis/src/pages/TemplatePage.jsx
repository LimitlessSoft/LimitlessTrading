import { useState, useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import {
  Box,
  Heading,
  Text,
  IconButton,
  Flex,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  Button,
  Input,
  useDisclosure,
  useToast
} from '@chakra-ui/react'
import { EditIcon, AddIcon } from '@chakra-ui/icons'
import { fetchRoutes, updateRoute } from '../modules/routesService'
import PageNavMenu from '../components/PageNavMenu'
import TemplatePageLabel from '../components/TemplatePageLabel'

function TemplatePage() {
  const location = useLocation()
  const [pageInfo, setPageInfo] = useState(null)
  const [labelText, setLabelText] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const toast = useToast()

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

  const handleAddLabel = async () => {
    if (!labelText.trim() || !pageInfo) return

    setIsSaving(true)
    try {
      const newLabel = {
        id: crypto.randomUUID(),
        text: labelText.trim(),
        width: 4,
        createdAt: Date.now()
      }
      const currentLabels = pageInfo.config?.labels || []
      const updatedConfig = {
        ...pageInfo.config,
        labels: [...currentLabels, newLabel]
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      setLabelText('')
      onClose()
      toast({
        title: 'Label added',
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to add label',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    } finally {
      setIsSaving(false)
    }
  }

  const handleDeleteLabel = async (labelId) => {
    if (!pageInfo) return

    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.filter(l => l.id !== labelId)
      const updatedConfig = {
        ...pageInfo.config,
        labels: updatedLabels
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      toast({
        title: 'Label deleted',
        status: 'info',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to delete label',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const handleUpdateLabel = async (labelId, changes) => {
    if (!pageInfo) return

    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.map(l =>
        l.id === labelId ? { ...l, ...changes } : l
      )
      const updatedConfig = {
        ...pageInfo.config,
        labels: updatedLabels
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({
        title: 'Failed to update label',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const labels = pageInfo?.config?.labels || []

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu>
        <IconButton
          icon={<EditIcon />}
          variant="ghost"
          size="sm"
          aria-label="Edit page"
          _hover={{ bg: 'whiteAlpha.200' }}
          bg={isEditing ? 'whiteAlpha.200' : undefined}
          ml={1}
          onClick={() => setIsEditing(!isEditing)}
          isDisabled={!pageInfo}
        />
        <IconButton
          icon={<AddIcon />}
          variant="ghost"
          size="sm"
          aria-label="Add label"
          _hover={{ bg: 'whiteAlpha.200' }}
          ml={1}
          onClick={onOpen}
          isDisabled={!pageInfo}
        />
      </PageNavMenu>
      <Box p={8}>
        {pageInfo ? (
          <>
            <Heading size="lg" mb={2}>{pageInfo.name}</Heading>
            <Text color="gray.500" mb={6}>{pageInfo.path}</Text>
            <Flex wrap="wrap" gap={2} width="100%">
              {labels.map(label => (
                <TemplatePageLabel
                  key={label.id}
                  label={label}
                  onDelete={handleDeleteLabel}
                  onResize={handleUpdateLabel}
                  onUpdate={handleUpdateLabel}
                  isEditing={isEditing}
                />
              ))}
            </Flex>
          </>
        ) : (
          <Text color="gray.500">{location.pathname}</Text>
        )}
      </Box>

      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Add Label</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <Input
              placeholder="Enter label text"
              value={labelText}
              onChange={(e) => setLabelText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && labelText.trim()) {
                  handleAddLabel()
                }
              }}
              autoFocus
            />
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>
              Cancel
            </Button>
            <Button
              colorScheme="blue"
              onClick={handleAddLabel}
              isLoading={isSaving}
              isDisabled={!labelText.trim()}
            >
              Add
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </Box>
  )
}

export default TemplatePage
