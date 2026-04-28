import { useState, useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import {
  Box,
  Heading,
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

function TemplatePage() {
  const location = useLocation()
  const [pageInfo, setPageInfo] = useState(null)
  const [labelText, setLabelText] = useState('')
  const [controlType, setControlType] = useState('label')
  const [isSaving, setIsSaving] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const [pendingImport, setPendingImport] = useState(null)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { isOpen: isImportOpen, onOpen: onImportOpen, onClose: onImportClose } = useDisclosure()
  const toast = useToast()
  const containerRef = useRef(null)
  const headerRef = useRef(null)
  const fileInputRef = useRef(null)
  const controlImportRef = useRef(null)
  const [dragPreview, setDragPreview] = useState(null)  // { id, x, y } during drag
  const [activeDrag, setActiveDrag] = useState(null)  // { controlId, mousePos } during drag

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
        text: controlType === 'label' ? labelText.trim() : undefined,
        width: controlType === 'panel' ? 8 : controlType === 'badge' ? 2 : controlType === 'chart' ? 10 : 4,
        height: controlType === 'panel' ? 120 : controlType === 'badge' ? 40 : controlType === 'chart' ? 220 : 60,
        x: 0,
        y: 0,
        createdAt: Date.now(),
        children: controlType === 'panel' ? [] : undefined,
        symbol: controlType === 'chart' ? '' : undefined,
        interval: controlType === 'chart' ? '5m' : undefined
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
        title: `${controlType.charAt(0).toUpperCase() + controlType.slice(1)} added`,
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

  // Move a control to a panel, root, or header
  // targetPanelId: null = root, 'header' = header bar, or panelId
  const handleMoveControl = async (controlId, sourcePanelId, targetPanelId) => {
    if (!pageInfo) return
    if (sourcePanelId === targetPanelId) return // No change
    if (controlId === targetPanelId) return // Can't move panel into itself

    try {
      let currentLabels = [...(pageInfo.config?.labels || [])]
      let currentHeaderControls = [...(pageInfo.config?.headerControls || [])]
      let controlToMove = null

      // Find and remove the control from its source
      if (sourcePanelId === 'header') {
        // Moving from header
        controlToMove = currentHeaderControls.find(c => c.id === controlId)
        if (!controlToMove) return
        currentHeaderControls = currentHeaderControls.filter(c => c.id !== controlId)
      } else if (sourcePanelId === null) {
        // Moving from root
        controlToMove = currentLabels.find(l => l.id === controlId)
        if (!controlToMove) return
        currentLabels = currentLabels.filter(l => l.id !== controlId)
      } else {
        // Moving from a panel - find the panel and remove the control from its children
        currentLabels = currentLabels.map(l => {
          if (l.id === sourcePanelId && l.children) {
            controlToMove = l.children.find(c => c.id === controlId)
            return { ...l, children: l.children.filter(c => c.id !== controlId) }
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
        title: 'Control moved',
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to move control',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const regenerateIds = (control) => {
    const result = { ...control, id: crypto.randomUUID() }
    if (result.children) result.children = result.children.map(regenerateIds)
    return result
  }

  const handleExportControl = (control) => {
    const exportData = { version: 1, control }
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const name = control.text || control.symbol || control.type || 'control'
    a.download = `${name.toLowerCase().replace(/\s+/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportControlFileChange = async (e) => {
    const file = e.target.files[0]
    if (!file || !pageInfo) return
    e.target.value = ''
    const reader = new FileReader()
    reader.onload = async (event) => {
      try {
        const data = JSON.parse(event.target.result)
        if (!data.control || typeof data.control !== 'object') {
          throw new Error('Invalid control file — missing control object')
        }
        const control = regenerateIds({ ...data.control, x: 0, y: 0 })
        const currentLabels = pageInfo.config?.labels || []
        const updatedConfig = { ...pageInfo.config, labels: [...currentLabels, control] }
        const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
        setPageInfo(updatedRoute)
        toast({ title: 'Control imported', status: 'success', duration: 2000 })
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

  // Header control handlers
  const handleDeleteHeaderControl = async (controlId) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.filter(c => c.id !== controlId)
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to delete control', status: 'error', duration: 3000 })
    }
  }

  const handleUpdateHeaderControl = async (controlId, changes) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.map(c =>
        c.id === controlId ? { ...c, ...changes } : c
      )
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update control', status: 'error', duration: 3000 })
    }
  }

  // Render a header control (simplified - inline, no absolute positioning)
  const renderHeaderControl = (control) => {
    const commonProps = {
      key: control.id,
      label: control,
      onDelete: handleDeleteHeaderControl,
      onUpdate: handleUpdateHeaderControl,
      onExport: handleExportControl,
      onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { controlId: control.id, mousePos } : null),
      onMoveControl: handleMoveControl,
      activeDrag: activeDrag,
      parentPanelId: 'header',
      isEditing: isEditing,
      isHeaderMode: true,
      containerRef: headerRef
    }

    if (control.type === 'badge') {
      return <TemplatePageBadge {...commonProps} />
    } else if (control.type === 'label') {
      return <TemplatePageLabel {...commonProps} />
    }
    // Panels not supported in header
    return null
  }

  // Calculate layout positions - prevents overlap, maintains consistent gaps
  const calculateLayout = (controls, preview, gap = 8) => {
    if (!controls.length) return new Map()

    // Apply preview position if dragging
    const withPreview = controls.map(c => {
      if (preview && c.id === preview.id) {
        return { ...c, x: preview.x, y: preview.y }
      }
      return c
    })

    // Sort by y (order hint), then by x
    const sorted = [...withPreview].sort((a, b) => (a.y || 0) - (b.y || 0) || (a.x || 0) - (b.x || 0))

    const positions = new Map()
    const placed = [] // {x, width, y, height}

    for (const control of sorted) {
      const x = control.x || 0
      const width = control.width || (control.type === 'panel' ? 8 : 4)
      const height = control.height || (control.type === 'panel' ? 120 : 60)

      // Find y position - must be below any horizontally overlapping controls
      let y = 0
      for (const p of placed) {
        // Check horizontal overlap (controls overlap if their x ranges intersect)
        if (x < p.x + p.width && x + width > p.x) {
          y = Math.max(y, p.y + p.height + gap)
        }
      }

      positions.set(control.id, y)
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
        onRenderControl={renderHeaderControl}
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
              Add control
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={handleExport}>
              Export template
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => fileInputRef.current?.click()}>
              Import template
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => controlImportRef.current?.click()}>
              Import control
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
          ref={controlImportRef}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={handleImportControlFileChange}
        />
      </PageNavMenu>
      <Box p={8}>
        {pageInfo ? (
          <>
            <Heading size="lg" mb={2}>{pageInfo.name}</Heading>
            <Text color="gray.500" mb={6}>{pageInfo.path}</Text>
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
                  onExport: handleExportControl,
                  onDragPreview: (pos) => setDragPreview(pos ? { id: label.id, ...pos } : null),
                  onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { controlId: label.id, mousePos } : null),
                  onMoveControl: handleMoveControl,
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
                  <option value="badge">Badge</option>
                  <option value="chart">Chart</option>
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
              This will replace all current controls on this page.
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
