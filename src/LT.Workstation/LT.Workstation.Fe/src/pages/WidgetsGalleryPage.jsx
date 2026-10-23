import { useState, useEffect, useRef } from 'react'
import {
  Box, Flex, Heading, Text, Input, SimpleGrid, Spinner, Center, Badge, HStack,
  ButtonGroup, Button, IconButton, useDisclosure, useToast,
  Drawer, DrawerBody, DrawerCloseButton, DrawerContent, DrawerHeader, DrawerOverlay, DrawerFooter,
  FormControl, FormLabel, Textarea, AlertDialog, AlertDialogBody, AlertDialogFooter,
  AlertDialogHeader, AlertDialogContent, AlertDialogOverlay, Divider,
} from '@chakra-ui/react'
import { DeleteIcon, DownloadIcon, AttachmentIcon } from '@chakra-ui/icons'
import PageNavMenu from '../components/PageNavMenu'
import { useLicense } from '../modules/LicenseContext'
import { renderWidget } from '../components/widgetRenderer'

function WidgetPreview({ config, onUpdate, isEditing }) {
  const containerRef = useRef(null)
  const [openSettingsForId, setOpenSettingsForId] = useState(null)

  if (!config) return null

  const previewLabel = { ...config, x: 0, y: 0, width: 20 }
  const height = config.height || 80

  return (
    <Box
      ref={containerRef}
      position="relative"
      height={`${height}px`}
      overflow="visible"
    >
      {renderWidget(config, {
        key: config.id,
        label: previewLabel,
        layoutY: 0,
        onUpdate: (id, updates) => onUpdate?.(id, updates),
        onDelete: () => {},
        onDuplicate: () => {},
        onExport: () => {},
        onShare: () => {},
        onDragPreview: () => {},
        onDragStateChange: () => {},
        onMoveWidget: () => {},
        onLinkSync: () => {},
        activeDrag: null,
        parentPanelId: null,
        isEditing,
        galleryMode: true,
        containerRef,
        openSettingsForId,
        onSettingsOpened: setOpenSettingsForId,
      })}
    </Box>
  )
}

function WidgetsGalleryPage() {
  const [widgets, setWidgets] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [search, setSearch] = useState('')
  const [myOnly, setMyOnly] = useState(false)

  // Detail drawer state
  const [detailWidget, setDetailWidget] = useState(null)
  const [detailName, setDetailName] = useState('')
  const [detailDescription, setDetailDescription] = useState('')
  const [detailConfig, setDetailConfig] = useState(null)
  const [isDirty, setIsDirty] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const uploadRef = useRef()

  // Delete dialog state
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const { isOpen: isDrawerOpen, onOpen: onDrawerOpen, onClose: onDrawerClose } = useDisclosure()
  const { isOpen: isDeleteOpen, onOpen: onDeleteOpen, onClose: onDeleteClose } = useDisclosure()
  const cancelRef = useRef()
  const toast = useToast()
  const { customerEmail, status: licenseStatus, refetch: refetchLicense } = useLicense()

  useEffect(() => {
    if (licenseStatus === 'error' || (licenseStatus === 'ready' && !customerEmail)) {
      refetchLicense?.()
    }
  }, [licenseStatus])

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await fetch('http://localhost:8744/api/gallery')
      const data = await res.json()
      if (!res.ok || !Array.isArray(data)) throw new Error(data?.error)
      setWidgets(data)
    } catch (err) {
      setError(err.message || 'Could not load gallery')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const filtered = widgets.filter(w => {
    if (myOnly && w.authorId !== customerEmail) return false
    if (!search.trim()) return true
    const q = search.toLowerCase()
    return (
      w.name.toLowerCase().includes(q) ||
      w.description?.toLowerCase().includes(q) ||
      w.widgetType?.toLowerCase().includes(q)
    )
  })

  const openDetail = (w) => {
    setDetailWidget(w)
    setDetailName(w.name)
    setDetailDescription(w.description ?? '')
    setDetailConfig(w.config)
    setIsDirty(false)
    onDrawerOpen()
  }

  const handlePreviewUpdate = (_id, updates) => {
    setDetailConfig(prev => ({ ...prev, ...updates }))
    setIsDirty(true)
  }

  const handleDownloadConfig = () => {
    if (!detailWidget) return
    const config = isDirty ? detailConfig : detailWidget.config
    const blob = new Blob(
      [JSON.stringify({ version: 1, control: config }, null, 2)],
      { type: 'application/json' }
    )
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${detailName.replace(/\s+/g, '_')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleUploadConfig = (e) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const parsed = JSON.parse(ev.target.result)
        const config = parsed?.control ?? parsed
        setDetailConfig(config)
        setIsDirty(true)
        toast({ title: 'Config loaded', description: file.name, status: 'success', duration: 2000 })
      } catch {
        toast({ title: 'Invalid JSON file', status: 'error', duration: 3000 })
      }
      e.target.value = ''
    }
    reader.readAsText(file)
  }

  const handleSave = async () => {
    if (!detailName.trim() || !detailWidget) return
    setIsSaving(true)
    try {
      const body = {
        name: detailName.trim(),
        description: detailDescription.trim(),
        ...(isDirty ? { config: detailConfig } : {}),
      }
      const res = await fetch(`http://localhost:8744/api/gallery/${detailWidget.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({}))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      const updated = {
        ...detailWidget,
        name: detailName.trim(),
        description: detailDescription.trim(),
        ...(isDirty ? { config: detailConfig } : {}),
      }
      setWidgets(prev => prev.map(w => w.id === detailWidget.id ? updated : w))
      setDetailWidget(updated)
      setIsDirty(false)
      toast({ title: 'Widget updated', status: 'success', duration: 2000 })
    } catch (err) {
      toast({ title: 'Failed to update', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const openDelete = (w, e) => {
    e?.stopPropagation()
    setDeleteTarget(w)
    onDeleteOpen()
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    setIsDeleting(true)
    try {
      const res = await fetch(`http://localhost:8744/api/gallery/${deleteTarget.id}`, { method: 'DELETE' })
      if (!res.ok && res.status !== 204) {
        const err = await res.json().catch(() => ({}))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      setWidgets(prev => prev.filter(w => w.id !== deleteTarget.id))
      toast({ title: 'Widget removed', status: 'success', duration: 2000 })
      onDeleteClose()
      if (detailWidget?.id === deleteTarget.id) onDrawerClose()
    } catch (err) {
      toast({ title: 'Failed to delete', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsDeleting(false)
    }
  }

  const isOwner = customerEmail && detailWidget?.authorId === customerEmail

  return (
    <Box minH="100vh" bg="gray.900" color="white">
      <PageNavMenu />
      <Box maxW="1200px" mx="auto" px={6} py={8}>
        <Flex align="center" mb={6} gap={4}>
          <Heading size="lg" fontWeight="semibold">Widgets Gallery</Heading>
          <Text color="gray.500" fontSize="sm" mt={1}>
            {!loading && !error ? `${filtered.length} widget${filtered.length !== 1 ? 's' : ''}` : ''}
          </Text>
        </Flex>

        <Flex mb={6} gap={3} align="center" wrap="wrap">
          <Input
            placeholder="Search widgets…"
            value={search}
            onChange={e => setSearch(e.target.value)}
            maxW="360px"
            bg="gray.800"
            borderColor="gray.600"
            _hover={{ borderColor: 'gray.500' }}
            _focus={{ borderColor: 'blue.400', boxShadow: 'none' }}
          />
          {customerEmail && (
            <ButtonGroup size="sm" isAttached variant="outline">
              <Button
                borderColor="gray.600"
                bg={!myOnly ? 'gray.700' : 'transparent'}
                color={!myOnly ? 'white' : 'gray.400'}
                _hover={{ bg: 'gray.700' }}
                onClick={() => setMyOnly(false)}
              >
                All
              </Button>
              <Button
                borderColor="gray.600"
                bg={myOnly ? 'blue.700' : 'transparent'}
                color={myOnly ? 'white' : 'gray.400'}
                _hover={{ bg: myOnly ? 'blue.600' : 'gray.700' }}
                onClick={() => setMyOnly(true)}
              >
                My Widgets
              </Button>
            </ButtonGroup>
          )}
        </Flex>

        {loading && <Center py={20}><Spinner size="lg" color="blue.400" /></Center>}

        {error && (
          <Box p={8} textAlign="center" borderWidth="1px" borderColor="gray.700" borderRadius="md" borderStyle="dashed">
            <Text color="gray.400">{error}</Text>
          </Box>
        )}

        {!loading && !error && filtered.length === 0 && (
          <Box p={8} textAlign="center" borderWidth="1px" borderColor="gray.700" borderRadius="md" borderStyle="dashed">
            <Text color="gray.400">
              {widgets.length === 0
                ? 'No widgets in the gallery yet. Configure a widget and share it!'
                : myOnly
                ? "You haven't shared any widgets yet."
                : 'No widgets match your search.'}
            </Text>
          </Box>
        )}

        {!loading && !error && filtered.length > 0 && (
          <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} spacing={4}>
            {filtered.map(w => {
              const isMine = customerEmail && w.authorId === customerEmail
              return (
                <Box
                  key={w.id}
                  p={4}
                  borderWidth="1px"
                  borderRadius="md"
                  borderColor="gray.700"
                  bg="gray.800"
                  cursor="pointer"
                  _hover={{ borderColor: 'gray.500' }}
                  transition="border-color 0.15s"
                  position="relative"
                  role="group"
                  onClick={() => openDetail(w)}
                >
                  {isMine && (
                    <HStack
                      position="absolute"
                      bottom={2}
                      right={2}
                      spacing={1}
                      opacity={0}
                      _groupHover={{ opacity: 1 }}
                      transition="opacity 0.15s"
                    >
                      <IconButton
                        icon={<DeleteIcon />}
                        size="xs"
                        variant="ghost"
                        colorScheme="red"
                        aria-label="Delete widget"
                        onClick={(e) => openDelete(w, e)}
                      />
                    </HStack>
                  )}
                  <HStack mb={1} align="flex-start">
                    <Text fontWeight="semibold" fontSize="sm" noOfLines={1} flex={1}>{w.name}</Text>
                    {w.widgetType && <Badge colorScheme="blue" fontSize="9px" flexShrink={0}>{w.widgetType}</Badge>}
                  </HStack>
                  {w.description && (
                    <Text fontSize="xs" color="gray.400" noOfLines={3} lineHeight="short" mb={2}>
                      {w.description}
                    </Text>
                  )}
                  <Text fontSize="10px" color="gray.600">
                    {w.authorId !== 'anonymous' ? w.authorId : 'Anonymous'}
                  </Text>
                </Box>
              )
            })}
          </SimpleGrid>
        )}
      </Box>

      {/* Detail drawer */}
      <Drawer isOpen={isDrawerOpen} onClose={onDrawerClose} placement="right" size="md">
        <DrawerOverlay />
        <DrawerContent bg="gray.800">
          <DrawerCloseButton />
          <DrawerHeader borderBottomWidth="1px" borderColor="gray.700" pb={3}>
            <HStack align="center" spacing={2} pr={8}>
              <Text fontSize="md" fontWeight="semibold" noOfLines={1} flex={1}>
                {detailWidget?.name}
              </Text>
              {detailWidget?.widgetType && (
                <Badge colorScheme="blue" fontSize="9px" flexShrink={0}>{detailWidget.widgetType}</Badge>
              )}
            </HStack>
            <Text fontSize="11px" color="gray.500" mt={1} fontWeight="normal">
              {detailWidget?.authorId !== 'anonymous' ? detailWidget?.authorId : 'Anonymous'}
            </Text>
          </DrawerHeader>

          <DrawerBody overflowY="auto" px={4} py={4}>
            {/* Widget preview */}
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" letterSpacing="wider" mb={3}>
              Preview
            </Text>
            <Box
              bg="gray.900"
              borderRadius="md"
              borderWidth="1px"
              borderColor="gray.700"
              px={4}
              pt={isOwner ? 8 : 4}
              pb={4}
              mb={6}
              overflow="visible"
              position="relative"
            >
              {detailConfig ? (
                <WidgetPreview
                  config={detailConfig}
                  onUpdate={isOwner ? handlePreviewUpdate : undefined}
                  isEditing={!!isOwner}
                />
              ) : (
                <Center py={6}><Text color="gray.500" fontSize="sm">No config available</Text></Center>
              )}
            </Box>

            {isOwner && (
              <>
                <Divider borderColor="gray.700" mb={4} />
                <Text fontSize="xs" color="gray.500" textTransform="uppercase" letterSpacing="wider" mb={3}>
                  Details
                </Text>
                <FormControl mb={4} isRequired>
                  <FormLabel fontSize="sm">Name</FormLabel>
                  <Input
                    value={detailName}
                    onChange={e => { setDetailName(e.target.value); setIsDirty(true) }}
                  />
                </FormControl>
                <FormControl mb={4}>
                  <FormLabel fontSize="sm">Description</FormLabel>
                  <Textarea
                    value={detailDescription}
                    onChange={e => { setDetailDescription(e.target.value); setIsDirty(true) }}
                    rows={3}
                  />
                </FormControl>
                <Divider borderColor="gray.700" mb={4} />
                <Text fontSize="xs" color="gray.500" textTransform="uppercase" letterSpacing="wider" mb={3}>
                  Config File
                </Text>
                <HStack spacing={2}>
                  <Button
                    size="sm"
                    variant="outline"
                    borderColor="gray.600"
                    leftIcon={<DownloadIcon />}
                    onClick={handleDownloadConfig}
                  >
                    Download
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    borderColor={isDirty && detailConfig !== detailWidget?.config ? 'green.500' : 'gray.600'}
                    color={isDirty && detailConfig !== detailWidget?.config ? 'green.300' : undefined}
                    leftIcon={<AttachmentIcon />}
                    onClick={() => uploadRef.current?.click()}
                  >
                    Upload
                  </Button>
                  <input
                    ref={uploadRef}
                    type="file"
                    accept=".json"
                    style={{ display: 'none' }}
                    onChange={handleUploadConfig}
                  />
                </HStack>
              </>
            )}

            {!isOwner && detailConfig && (
              <>
                <Divider borderColor="gray.700" mb={4} />
                <HStack spacing={2}>
                  <Button
                    size="sm"
                    variant="outline"
                    borderColor="gray.600"
                    leftIcon={<DownloadIcon />}
                    onClick={handleDownloadConfig}
                  >
                    Download Config
                  </Button>
                </HStack>
              </>
            )}
          </DrawerBody>

          {isOwner && (
            <DrawerFooter borderTopWidth="1px" borderColor="gray.700" gap={2}>
              <Button
                size="sm"
                colorScheme="red"
                variant="ghost"
                mr="auto"
                onClick={(e) => openDelete(detailWidget, e)}
              >
                Delete
              </Button>
              <Button size="sm" variant="ghost" onClick={onDrawerClose}>Cancel</Button>
              <Button
                size="sm"
                colorScheme="blue"
                isDisabled={!isDirty || !detailName.trim()}
                isLoading={isSaving}
                onClick={handleSave}
              >
                Save
              </Button>
            </DrawerFooter>
          )}
        </DrawerContent>
      </Drawer>

      {/* Delete confirmation */}
      <AlertDialog isOpen={isDeleteOpen} leastDestructiveRef={cancelRef} onClose={onDeleteClose} isCentered>
        <AlertDialogOverlay>
          <AlertDialogContent bg="gray.800">
            <AlertDialogHeader fontSize="lg" fontWeight="bold">Remove Widget</AlertDialogHeader>
            <AlertDialogBody>
              Remove <strong>"{deleteTarget?.name}"</strong> from the gallery? This cannot be undone.
            </AlertDialogBody>
            <AlertDialogFooter>
              <Button ref={cancelRef} variant="ghost" onClick={onDeleteClose}>Cancel</Button>
              <Button colorScheme="red" ml={3} isLoading={isDeleting} onClick={handleDelete}>
                Remove
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialogOverlay>
      </AlertDialog>
    </Box>
  )
}

export default WidgetsGalleryPage
