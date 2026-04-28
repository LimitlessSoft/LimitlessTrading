import { useState, useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import {
  Box,
  Text,
  IconButton,
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
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  useDisclosure,
  useToast
} from '@chakra-ui/react'

const KebabIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <circle cx="12" cy="5" r="2" />
    <circle cx="12" cy="12" r="2" />
    <circle cx="12" cy="19" r="2" />
  </svg>
)
import { fetchRoutes, updateRoute } from '../modules/routesService'
import PageNavMenu from '../components/PageNavMenu'
import TemplatePageLabel from '../components/TemplatePageLabel'
import TemplatePagePanel from '../components/TemplatePagePanel'
import TemplatePageBadge from '../components/TemplatePageBadge'
import TemplatePageChart from '../components/TemplatePageChart'
import TemplatePageGauge from '../components/TemplatePageGauge'
import WidgetsGalleryModal from '../components/WidgetsGalleryModal'

function TemplatePage() {
  const location = useLocation()
  const [pageInfo, setPageInfo] = useState(null)
  const [labelText, setLabelText] = useState('')
  const [widgetType, setWidgetType] = useState('label')
  const [isSaving, setIsSaving] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const [pendingImport, setPendingImport] = useState(null)
  const [addStage, setAddStage] = useState('pick')
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { isOpen: isImportOpen, onOpen: onImportOpen, onClose: onImportClose } = useDisclosure()
  const { isOpen: isGalleryOpen, onOpen: onGalleryOpen, onClose: onGalleryClose } = useDisclosure()
  const toast = useToast()
  const containerRef = useRef(null)
  const headerRef = useRef(null)
  const fileInputRef = useRef(null)
  const widgetImportRef = useRef(null)
  const [dragPreview, setDragPreview] = useState(null)  // { id, x, y } during drag
  const [activeDrag, setActiveDrag] = useState(null)  // { widgetId, mousePos } during drag

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

  useEffect(() => {
    if (isOpen) setAddStage('pick')
  }, [isOpen])

  const handleAddWidget = async () => {
    if (widgetType === 'label' && !labelText.trim()) return
    if (!pageInfo) return

    setIsSaving(true)
    try {
      const newControl = {
        id: crypto.randomUUID(),
        type: widgetType,
        text: widgetType === 'label' ? labelText.trim() : undefined,
        width: widgetType === 'panel' ? 8 : widgetType === 'badge' ? 2 : widgetType === 'chart' ? 10 : widgetType === 'gauge' ? 6 : 4,
        height: widgetType === 'panel' ? 120 : widgetType === 'badge' ? 40 : widgetType === 'chart' ? 220 : widgetType === 'gauge' ? 160 : 60,
        x: 0,
        y: 0,
        createdAt: Date.now(),
        children: widgetType === 'panel' ? [] : undefined,
        symbol: widgetType === 'chart' ? '' : undefined,
        interval: widgetType === 'chart' ? '5m' : undefined,
        min: widgetType === 'gauge' ? 0 : undefined,
        max: widgetType === 'gauge' ? 100 : undefined,
        segments: widgetType === 'gauge' ? [
          { from: 0, to: 25, color: '#48BB78' },
          { from: 25, to: 50, color: '#ECC94B' },
          { from: 50, to: 75, color: '#ED8936' },
          { from: 75, to: 100, color: '#F56565' }
        ] : undefined
      }
      const currentLabels = pageInfo.config?.labels || []
      const updatedConfig = {
        ...pageInfo.config,
        labels: [...currentLabels, newControl]
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      setLabelText('')
      setWidgetType('label')
      onClose()
      toast({
        title: `${widgetType.charAt(0).toUpperCase() + widgetType.slice(1)} added`,
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to add widget',
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

  // Move a widget to a panel, root, or header
  // targetPanelId: null = root, 'header' = header bar, or panelId
  const handleMoveWidget = async (widgetId, sourcePanelId, targetPanelId) => {
    if (!pageInfo) return
    if (sourcePanelId === targetPanelId) return // No change
    if (widgetId === targetPanelId) return // Can't move panel into itself

    try {
      let currentLabels = [...(pageInfo.config?.labels || [])]
      let currentHeaderControls = [...(pageInfo.config?.headerControls || [])]
      let controlToMove = null

      // Find and remove the control from its source
      if (sourcePanelId === 'header') {
        // Moving from header
        controlToMove = currentHeaderControls.find(c => c.id === widgetId)
        if (!controlToMove) return
        currentHeaderControls = currentHeaderControls.filter(c => c.id !== widgetId)
      } else if (sourcePanelId === null) {
        // Moving from root
        controlToMove = currentLabels.find(l => l.id === widgetId)
        if (!controlToMove) return
        currentLabels = currentLabels.filter(l => l.id !== widgetId)
      } else {
        // Moving from a panel - find the panel and remove the control from its children
        currentLabels = currentLabels.map(l => {
          if (l.id === sourcePanelId && l.children) {
            controlToMove = l.children.find(c => c.id === widgetId)
            return { ...l, children: l.children.filter(c => c.id !== widgetId) }
          }
          return l
        })
      }

      if (!controlToMove) return

      // Reset position for the new container
      controlToMove = { ...controlToMove, x: 0, y: 0 }

      // Add the control to its target
      if (targetPanelId === 'header') {
        // Moving to header
        currentHeaderControls = [...currentHeaderControls, controlToMove]
      } else if (targetPanelId === null) {
        // Moving to root
        currentLabels = [...currentLabels, controlToMove]
      } else {
        // Moving to a panel
        currentLabels = currentLabels.map(l => {
          if (l.id === targetPanelId && l.type === 'panel') {
            return { ...l, children: [...(l.children || []), controlToMove] }
          }
          return l
        })
      }

      const updatedConfig = {
        ...pageInfo.config,
        labels: currentLabels,
        headerControls: currentHeaderControls
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)

      toast({
        title: 'Widget moved',
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to move widget',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const regenerateIds = (widget) => {
    const result = { ...widget, id: crypto.randomUUID() }
    if (result.children) result.children = result.children.map(regenerateIds)
    return result
  }

  const handleExportWidget = (widget) => {
    const exportData = { version: 1, control: widget }
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const name = widget.text || widget.symbol || widget.type || 'widget'
    a.download = `${name.toLowerCase().replace(/\s+/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportWidgetFileChange = async (e) => {
    const file = e.target.files[0]
    if (!file || !pageInfo) return
    e.target.value = ''
    const reader = new FileReader()
    reader.onload = async (event) => {
      try {
        const data = JSON.parse(event.target.result)
        if (!data.control || typeof data.control !== 'object') {
          throw new Error('Invalid widget file — missing widget object')
        }
        const widget = regenerateIds({ ...data.control, x: 0, y: 0 })
        const currentLabels = pageInfo.config?.labels || []
        const updatedConfig = { ...pageInfo.config, labels: [...currentLabels, widget] }
        const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
        setPageInfo(updatedRoute)
        toast({ title: 'Widget imported', status: 'success', duration: 2000 })
      } catch (err) {
        toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
      }
    }
    reader.readAsText(file)
  }

  const handleExport = () => {
    if (!pageInfo) return
    const exportData = {
      version: 1,
      name: pageInfo.name,
      path: pageInfo.path,
      config: pageInfo.config || {}
    }
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${pageInfo.name.toLowerCase().replace(/\s+/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportFileChange = (e) => {
    const file = e.target.files[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (event) => {
      try {
        const data = JSON.parse(event.target.result)
        if (!data.config || typeof data.config !== 'object') {
          throw new Error('Invalid template file — missing config')
        }
        setPendingImport(data)
        onImportOpen()
      } catch (err) {
        toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
      } finally {
        e.target.value = ''
      }
    }
    reader.readAsText(file)
  }

  const handleImportConfirm = async () => {
    if (!pendingImport || !pageInfo) return
    setIsSaving(true)
    try {
      const updatedRoute = await updateRoute(pageInfo.id, { config: pendingImport.config })
      setPageInfo(updatedRoute)
      onImportClose()
      setPendingImport(null)
      toast({ title: 'Template imported', status: 'success', duration: 2000 })
    } catch (err) {
      toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const labels = pageInfo?.config?.labels || []
  const headerControls = pageInfo?.config?.headerControls || []

  // Header widget handlers
  const handleDeleteHeaderWidget = async (widgetId) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.filter(c => c.id !== widgetId)
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to delete widget', status: 'error', duration: 3000 })
    }
  }

  const handleUpdateHeaderWidget = async (widgetId, changes) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.map(c =>
        c.id === widgetId ? { ...c, ...changes } : c
      )
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update widget', status: 'error', duration: 3000 })
    }
  }

  // Render a header widget (simplified - inline, no absolute positioning)
  const renderHeaderWidget = (widget) => {
    const commonProps = {
      key: widget.id,
      label: widget,
      onDelete: handleDeleteHeaderWidget,
      onUpdate: handleUpdateHeaderWidget,
      onExport: handleExportWidget,
      onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { widgetId: widget.id, mousePos } : null),
      onMoveWidget: handleMoveWidget,
      activeDrag: activeDrag,
      parentPanelId: 'header',
      isEditing: isEditing,
      isHeaderMode: true,
      containerRef: headerRef
    }

    if (widget.type === 'badge') {
      return <TemplatePageBadge {...commonProps} />
    } else if (widget.type === 'label') {
      return <TemplatePageLabel {...commonProps} />
    } else if (widget.type === 'gauge') {
      return <TemplatePageGauge {...commonProps} />
    }
    // Panels and charts not supported in header
    return null
  }

  // Calculate layout positions - prevents overlap, maintains consistent gaps
  const calculateLayout = (widgets, preview, gap = 8) => {
    if (!widgets.length) return new Map()

    // Apply preview position if dragging
    const withPreview = widgets.map(c => {
      if (preview && c.id === preview.id) {
        return { ...c, x: preview.x, y: preview.y }
      }
      return c
    })

    // Sort by y (order hint), then by x
    const sorted = [...withPreview].sort((a, b) => (a.y || 0) - (b.y || 0) || (a.x || 0) - (b.x || 0))

    const positions = new Map()
    const placed = []

    for (const widget of sorted) {
      const x = widget.x || 0
      const width = widget.width || (widget.type === 'panel' ? 8 : 4)
      const height = widget.height || (widget.type === 'panel' ? 120 : 60)

      let y = 0
      for (const p of placed) {
        if (x < p.x + p.width && x + width > p.x) {
          y = Math.max(y, p.y + p.height + gap)
        }
      }

      positions.set(widget.id, y)
      placed.push({ x, width, y, height })
    }

    return positions
  }

  const layoutPositions = calculateLayout(labels, dragPreview)

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu
        headerRef={headerRef}
        headerControls={headerControls}
        activeDrag={activeDrag}
        onRenderWidget={renderHeaderWidget}
      >
        <Menu>
          <MenuButton
            as={IconButton}
            icon={<KebabIcon />}
            variant="ghost"
            size="sm"
            aria-label="Page options"
            _hover={{ bg: 'whiteAlpha.200' }}
            ml={1}
            isDisabled={!pageInfo}
          />
          <MenuList bg="gray.800" borderColor="gray.700" minW="160px">
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => setIsEditing(!isEditing)}>
              {isEditing ? 'Stop editing' : 'Edit page'}
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={onOpen}>
              Add widget
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={handleExport}>
              Export template
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => fileInputRef.current?.click()}>
              Import template
            </MenuItem>
          </MenuList>
        </Menu>
        <input
          ref={fileInputRef}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={handleImportFileChange}
        />
        <input
          ref={widgetImportRef}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={handleImportWidgetFileChange}
        />
      </PageNavMenu>
      <Box p={8}>
        {pageInfo ? (
          <>
            <Box
              ref={containerRef}
              position="relative"
              width="100%"
              minH={isEditing ? '200px' : '100px'}
            >
              {labels.map((label) => {
                const commonProps = {
                  key: label.id,
                  label: label,
                  layoutY: layoutPositions.get(label.id),
                  onDelete: handleDeleteLabel,
                  onUpdate: handleUpdateLabel,
                  onExport: handleExportWidget,
                  onDragPreview: (pos) => setDragPreview(pos ? { id: label.id, ...pos } : null),
                  onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { widgetId: label.id, mousePos } : null),
                  onMoveWidget: handleMoveWidget,
                  activeDrag: activeDrag,
                  parentPanelId: null,
                  isEditing: isEditing,
                  containerRef: containerRef
                }

                if (label.type === 'panel') {
                  return <TemplatePagePanel {...commonProps} />
                } else if (label.type === 'badge') {
                  return <TemplatePageBadge {...commonProps} />
                } else if (label.type === 'chart') {
                  return <TemplatePageChart {...commonProps} />
                } else if (label.type === 'gauge') {
                  return <TemplatePageGauge {...commonProps} />
                } else {
                  return <TemplatePageLabel {...commonProps} />
                }
              })}
            </Box>
          </>
        ) : (
          <Text color="gray.500">{location.pathname}</Text>
        )}
      </Box>

      {isEditing && (
        <Box
          position="fixed"
          bottom={6}
          left="50%"
          transform="translateX(-50%)"
          bg="blue.900"
          borderWidth="1px"
          borderColor="blue.500"
          borderRadius="lg"
          px={5}
          py={3}
          display="flex"
          alignItems="center"
          gap={4}
          zIndex={1100}
          boxShadow="0 4px 24px rgba(0,0,0,0.5)"
        >
          <Text fontSize="sm" color="blue.200" fontWeight="medium">Edit mode</Text>
          <Button size="sm" colorScheme="blue" variant="solid" onClick={() => setIsEditing(false)}>
            Stop editing
          </Button>
        </Box>
      )}

      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Add Widget</ModalHeader>
          <ModalCloseButton />
          {addStage === 'pick' ? (
            <>
              <ModalBody>
                <VStack spacing={3}>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={() => setAddStage('new')}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">New</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Create a new widget from scratch</Text>
                    </VStack>
                  </Button>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={() => { onClose(); widgetImportRef.current?.click() }}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">Import from file</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Load a widget from a JSON file</Text>
                    </VStack>
                  </Button>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={onGalleryOpen}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">Widgets Gallery</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Browse and select from saved widgets</Text>
                    </VStack>
                  </Button>
                </VStack>
              </ModalBody>
              <ModalFooter>
                <Button variant="ghost" onClick={onClose}>Cancel</Button>
              </ModalFooter>
            </>
          ) : (
            <>
              <ModalBody>
                <VStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Widget Type</FormLabel>
                    <Select
                      value={widgetType}
                      onChange={(e) => setWidgetType(e.target.value)}
                    >
                      <option value="label">Label</option>
                      <option value="panel">Panel</option>
                      <option value="badge">Badge</option>
                      <option value="chart">Chart</option>
                      <option value="gauge">Gauge</option>
                    </Select>
                  </FormControl>
                  {widgetType === 'label' && (
                    <FormControl>
                      <FormLabel fontSize="sm">Label Text</FormLabel>
                      <Input
                        placeholder="Enter label text"
                        value={labelText}
                        onChange={(e) => setLabelText(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && labelText.trim()) {
                            handleAddWidget()
                          }
                        }}
                      />
                    </FormControl>
                  )}
                </VStack>
              </ModalBody>
              <ModalFooter>
                <Button variant="ghost" mr="auto" onClick={() => setAddStage('pick')}>Back</Button>
                <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
                <Button
                  colorScheme="blue"
                  onClick={handleAddWidget}
                  isLoading={isSaving}
                  isDisabled={widgetType === 'label' && !labelText.trim()}
                >
                  Add
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
      <WidgetsGalleryModal isOpen={isGalleryOpen} onClose={onGalleryClose} />
      <Modal isOpen={isImportOpen} onClose={onImportClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Import Template</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <Text>
              Import <strong>{pendingImport?.name || 'template'}</strong>?
            </Text>
            <Text mt={2} fontSize="sm" color="orange.300">
              This will replace all current widgets on this page.
            </Text>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={() => { onImportClose(); setPendingImport(null) }}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleImportConfirm} isLoading={isSaving}>
              Import
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </Box>
  )
}

export default TemplatePage
