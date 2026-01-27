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
  Select,
  FormControl,
  FormLabel,
  VStack,
  useDisclosure,
  useToast
} from '@chakra-ui/react'
import { EditIcon, AddIcon } from '@chakra-ui/icons'
import { fetchRoutes, updateRoute } from '../modules/routesService'
import PageNavMenu from '../components/PageNavMenu'
import TemplatePageLabel from '../components/TemplatePageLabel'
import TemplatePagePanel from '../components/TemplatePagePanel'

function TemplatePage() {
  const location = useLocation()
  const [pageInfo, setPageInfo] = useState(null)
  const [labelText, setLabelText] = useState('')
  const [controlType, setControlType] = useState('label')
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

  const handleAddControl = async () => {
    if (controlType === 'label' && !labelText.trim()) return
    if (!pageInfo) return

    setIsSaving(true)
    try {
      const newControl = {
        id: crypto.randomUUID(),
        type: controlType,
        text: controlType === 'label' ? labelText.trim() : '',
        width: controlType === 'panel' ? 8 : 4,
        height: controlType === 'panel' ? 120 : 60,
        createdAt: Date.now(),
        children: controlType === 'panel' ? [] : undefined
      }
      const currentLabels = pageInfo.config?.labels || []
      const updatedConfig = {
        ...pageInfo.config,
        labels: [...currentLabels, newControl]
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      setLabelText('')
      setControlType('label')
      onClose()
      toast({
        title: `${controlType === 'panel' ? 'Panel' : 'Label'} added`,
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to add control',
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
          aria-label="Add control"
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
                label.type === 'panel' ? (
                  <TemplatePagePanel
                    key={label.id}
                    label={label}
                    onDelete={handleDeleteLabel}
                    onResize={handleUpdateLabel}
                    onUpdate={handleUpdateLabel}
                    isEditing={isEditing}
                  />
                ) : (
                  <TemplatePageLabel
                    key={label.id}
                    label={label}
                    onDelete={handleDeleteLabel}
                    onResize={handleUpdateLabel}
                    onUpdate={handleUpdateLabel}
                    isEditing={isEditing}
                  />
                )
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
          <ModalHeader>Add Control</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel fontSize="sm">Control Type</FormLabel>
                <Select
                  value={controlType}
                  onChange={(e) => setControlType(e.target.value)}
                >
                  <option value="label">Label</option>
                  <option value="panel">Panel</option>
                </Select>
              </FormControl>
              {controlType === 'label' && (
                <FormControl>
                  <FormLabel fontSize="sm">Label Text</FormLabel>
                  <Input
                    placeholder="Enter label text"
                    value={labelText}
                    onChange={(e) => setLabelText(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && labelText.trim()) {
                        handleAddControl()
                      }
                    }}
                  />
                </FormControl>
              )}
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>
              Cancel
            </Button>
            <Button
              colorScheme="blue"
              onClick={handleAddControl}
              isLoading={isSaving}
              isDisabled={controlType === 'label' && !labelText.trim()}
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
