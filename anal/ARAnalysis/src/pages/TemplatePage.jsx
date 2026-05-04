import { useState, useEffect, useRef } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
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
import TemplatePageBadge from '../components/TemplatePageBadge'
import TemplatePageGauge from '../components/TemplatePageGauge'
import { WIDGET_REGISTRY, createWidget, renderWidget } from '../components/widgetRenderer'
import WidgetsGalleryModal from '../components/WidgetsGalleryModal'
import WidgetTreePanel from '../components/WidgetTreePanel'

function TemplatePage() {
  const location = useLocation()
  const navigate = useNavigate()
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
  const { isOpen: isInfoOpen, onOpen: onInfoOpen, onClose: onInfoClose } = useDisclosure()
  const [infoName, setInfoName] = useState('')
  const [infoPath, setInfoPath] = useState('')
  const [routesVersion, setRoutesVersion] = useState(0)
  const toast = useToast()
  const containerRef = useRef(null)
  const headerRef = useRef(null)
  const fileInputRef = useRef(null)
  const widgetImportRef = useRef(null)
  const [dragPreview, setDragPreview] = useState(null)  // { id, x, y } during drag
  const [activeDrag, setActiveDrag] = useState(null)  // { widgetId, mousePos } during drag
  const [fillHeight, setFillHeight] = useState(null)
  const [isTreeOpen, setIsTreeOpen] = useState(false)
  const [headerHeight, setHeaderHeight] = useState(0)

  useEffect(() => {
    const loadPageInfo = async () => {
      try {
        const routes = await fetchRoutes()
        const matchingRoute = routes.find(r => r.path === location.pathname)
        setPageInfo(matchingRoute || null)
      } catch {
        setPageInfo(null)
      }
    }
    loadPageInfo()
  }, [location.pathname])

  useEffect(() => {
    if (isOpen) setAddStage('pick')
  }, [isOpen])

  useEffect(() => {
    const compute = () => {
      const header = headerRef.current
      if (!header) return
      const headerH = header.getBoundingClientRect().height
      const padding = 12 // py={3} top + bottom
      setFillHeight(window.innerHeight - headerH - padding * 2)
      setHeaderHeight(headerH)
    }
    compute()
    window.addEventListener('resize', compute)
    return () => window.removeEventListener('resize', compute)
  }, [])

  const handleAddWidget = async () => {
    if (WIDGET_REGISTRY[widgetType]?.needsText && !labelText.trim()) return
    if (!pageInfo) return

    setIsSaving(true)
    try {
      const newControl = createWidget(widgetType, labelText.trim())
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

  // Sync a linked property across all widgets that share the same linkGroup.
  // linkedChanges: applied to every widget in the group (e.g. { symbol: 'AAPL' })
  // sourceId / sourceExtraChanges: non-linked per-source changes (e.g. { interval, outsideRTH })
  const handleLinkSync = async (groupId, linkedChanges, sourceId, sourceExtraChanges = {}) => {
    if (!pageInfo) return
    const applyToWidget = (w) => {
      if (w.linkGroup !== groupId) return w
      const changes = { ...linkedChanges }
      if (w.id === sourceId) Object.assign(changes, sourceExtraChanges)
      return { ...w, ...changes }
    }
    const updateWidgets = (widgets) => widgets.map(w => {
      const updated = applyToWidget(w)
      if (updated.children) return { ...updated, children: updateWidgets(updated.children) }
      return updated
    })
    try {
      const updatedLabels = updateWidgets(pageInfo.config?.labels || [])
      const updatedConfig = { ...pageInfo.config, labels: updatedLabels }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to sync linked widgets', description: err.message, status: 'error', duration: 3000 })
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

  const handleOpenInfo = () => {
    setInfoName(pageInfo?.name || '')
    setInfoPath(pageInfo?.path || '')
    onInfoOpen()
  }

  const handleSaveInfo = async () => {
    if (!pageInfo || !infoName.trim() || !infoPath.trim()) return
    setIsSaving(true)
    try {
      const updatedRoute = await updateRoute(pageInfo.id, { name: infoName.trim(), path: infoPath.trim() })
      setPageInfo(updatedRoute)
      setRoutesVersion(v => v + 1)
      onInfoClose()
      if (infoPath.trim() !== pageInfo.path) {
        navigate(infoPath.trim())
      }
      toast({ title: 'Template updated', status: 'success', duration: 2000 })
    } catch (err) {
      toast({ title: 'Failed to update template', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const labels = pageInfo?.config?.labels || []
  const headerControls = pageInfo?.config?.headerControls || []

  const handleReorderWidgets = async (newOrderedIds, parentPanelId) => {
    if (!pageInfo) return
    try {
      let currentLabels = [...(pageInfo.config?.labels || [])]
      let currentHeaderControls = [...(pageInfo.config?.headerControls || [])]

      if (parentPanelId === 'header') {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentHeaderControls = currentHeaderControls.map(w =>
          idxMap.has(w.id) ? { ...w, y: idxMap.get(w.id) } : w
        )
      } else if (parentPanelId === null) {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentLabels = currentLabels.map(w =>
          idxMap.has(w.id) ? { ...w, y: idxMap.get(w.id) } : w
        )
      } else {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentLabels = currentLabels.map(panel => {
          if (panel.id !== parentPanelId || !panel.children) return panel
          return {
            ...panel,
            children: panel.children.map(w =>
              idxMap.has(w.id) ? { ...w, y: idxMap.get(w.id) } : w
            )
          }
        })
      }

      const updatedConfig = { ...pageInfo.config, labels: currentLabels, headerControls: currentHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to reorder widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

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
      onLinkSync: handleLinkSync,
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
      const def = WIDGET_REGISTRY[widget.type] || WIDGET_REGISTRY.label
      const x = widget.x || 0
      const width = widget.width || def.width
      const height = widget.height || def.height

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
        refreshKey={routesVersion}
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
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={handleOpenInfo}>
              Template information
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => setIsEditing(!isEditing)}>
              {isEditing ? 'Stop editing' : 'Edit layout'}
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => setIsTreeOpen(v => !v)}>
              {isTreeOpen ? 'Hide widget tree' : 'Widget tree'}
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
      <Box pl={2} pr={0} py={3}>
        {pageInfo ? (
          <>
            <Box
              ref={containerRef}
              position="relative"
              width="100%"
              minH={isEditing ? '200px' : '100px'}
            >
              {labels.map((label) => {
                const effectiveLabel = label.dock === 'fill'
                  ? { ...label, x: 0, width: 20, ...(fillHeight != null ? { height: fillHeight } : {}) }
                  : label
                return renderWidget(label, {
                key: label.id,
                label: effectiveLabel,
                layoutY: layoutPositions.get(label.id),
                onDelete: handleDeleteLabel,
                onUpdate: handleUpdateLabel,
                onExport: handleExportWidget,
                onLinkSync: handleLinkSync,
                onDragPreview: (pos) => setDragPreview(pos ? { id: label.id, ...pos } : null),
                onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { widgetId: label.id, mousePos } : null),
                onMoveWidget: handleMoveWidget,
                activeDrag: activeDrag,
                parentPanelId: null,
                isEditing: isEditing,
                containerRef: containerRef,
              })
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
          <Button
            size="sm"
            colorScheme={isTreeOpen ? 'blue' : 'blue'}
            variant={isTreeOpen ? 'solid' : 'outline'}
            onClick={() => setIsTreeOpen(v => !v)}
          >
            {isTreeOpen ? 'Hide tree' : 'Widget tree'}
          </Button>
          <Button size="sm" colorScheme="blue" variant="solid" onClick={() => setIsEditing(false)}>
            Stop editing
          </Button>
        </Box>
      )}

      {isTreeOpen && (
        <WidgetTreePanel
          widgets={labels}
          headerWidgets={headerControls}
          topOffset={headerHeight}
          onClose={() => setIsTreeOpen(false)}
          onReorder={handleReorderWidgets}
        />
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
                      {Object.entries(WIDGET_REGISTRY).map(([type, def]) => (
                        <option key={type} value={type}>{def.displayName}</option>
                      ))}
                    </Select>
                  </FormControl>
                  {WIDGET_REGISTRY[widgetType]?.needsText && (
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
                  isDisabled={WIDGET_REGISTRY[widgetType]?.needsText && !labelText.trim()}
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
      <Modal isOpen={isInfoOpen} onClose={onInfoClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Template information</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel fontSize="sm">Name</FormLabel>
                <Input
                  value={infoName}
                  onChange={(e) => setInfoName(e.target.value)}
                  placeholder="Template name"
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize="sm">Path</FormLabel>
                <Input
                  value={infoPath}
                  onChange={(e) => setInfoPath(e.target.value)}
                  placeholder="/my-page"
                />
              </FormControl>
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onInfoClose}>Cancel</Button>
            <Button
              colorScheme="blue"
              onClick={handleSaveInfo}
              isLoading={isSaving}
              isDisabled={!infoName.trim() || !infoPath.trim()}
            >
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </Box>
  )
}

export default TemplatePage
