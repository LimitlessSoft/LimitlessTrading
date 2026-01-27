import { useRef, useState, useEffect, useCallback } from 'react'
import {
  Box,
  IconButton,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  ModalCloseButton,
  Button,
  FormControl,
  FormLabel,
  Input,
  HStack,
  VStack,
  Checkbox,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
import { getConnectionStatus } from '../modules/dataService'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 1
const MIN_SIZE = 20
const DEFAULT_SIZE = 40

const DEFAULT_STYLE = {
  bgColor: 'transparent',
  borderColor: '#718096',
  borderWidth: 0
}

// Placeholder pattern for template variables: ${name} or ${name:interval}
const PLACEHOLDER_REGEX = /\$\{(\w+)(?::(\d+))?\}/

function TemplatePageBadge({ label, layoutY, onDelete, onUpdate, onDragPreview, onDragStateChange, onMoveControl, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const [currentValue, setCurrentValue] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const width = label.width || 2
  const height = label.height || DEFAULT_SIZE
  const x = label.x || 0
  const y = label.y || 0
  const style = { ...DEFAULT_STYLE, ...label.style }
  const placeholder = label.placeholder || ''
  const colorMappings = label.colorMappings || []
  const defaultColor = label.defaultColor || '#718096'

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  // Parse placeholder to get refresh interval
  const getRefreshInterval = useCallback(() => {
    if (!placeholder) return null
    const match = placeholder.match(PLACEHOLDER_REGEX)
    if (!match) return null
    return match[2] ? parseInt(match[2], 10) : 5
  }, [placeholder])

  // Fetch placeholder value
  const fetchPlaceholderValue = useCallback(async () => {
    if (!placeholder) return

    const match = placeholder.match(PLACEHOLDER_REGEX)
    if (!match) return

    const placeholderName = match[1]

    if (placeholderName === 'tws_connection_status') {
      try {
        const status = await getConnectionStatus()
        setCurrentValue(status.connected ? `Connected (${status.port})` : 'Disconnected')
      } catch (err) {
        setCurrentValue('Unknown')
      }
    }
  }, [placeholder])

  // Poll for placeholder value
  useEffect(() => {
    if (!placeholder) return

    const refreshInterval = getRefreshInterval()
    if (!refreshInterval) return

    fetchPlaceholderValue()
    const interval = setInterval(fetchPlaceholderValue, refreshInterval * 1000)
    return () => clearInterval(interval)
  }, [placeholder, fetchPlaceholderValue, getRefreshInterval])

  // Determine badge color based on current value and mappings
  // Check exact matches first, then contains matches (order-independent)
  const getBadgeColor = () => {
    if (!currentValue) return defaultColor

    const valueLower = currentValue.toLowerCase()

    // First pass: exact matches take priority
    for (const mapping of colorMappings) {
      if (!mapping.contains && valueLower === mapping.value.toLowerCase()) {
        return mapping.color
      }
    }

    // Second pass: contains matches
    for (const mapping of colorMappings) {
      if (mapping.contains && valueLower.includes(mapping.value.toLowerCase())) {
        return mapping.color
      }
    }

    return defaultColor
  }

  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  useEffect(() => {
    if (previewPos && !isMoving) {
      if (label.x === previewPos.x) {
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }
  }, [label.x, previewPos, isMoving, onDragPreview])

  const getContainerInfo = () => {
    const container = containerRef?.current || boxRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    return { width: rect.width, segmentSize: rect.width / TOTAL_SEGMENTS, rect }
  }

  const handleOpenEdit = () => {
    setEditConfig({
      placeholder: placeholder,
      colorMappings: colorMappings.length > 0 ? [...colorMappings] : [{ value: '', color: '#48BB78', contains: true }],
      defaultColor: defaultColor,
      style: { ...DEFAULT_STYLE, ...label.style }
    })
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      placeholder: editConfig.placeholder,
      colorMappings: editConfig.colorMappings.filter(m => m.value.trim()),
      defaultColor: editConfig.defaultColor,
      style: editConfig.style
    })
    onClose()
  }

  const handleAddMapping = () => {
    setEditConfig({
      ...editConfig,
      colorMappings: [...editConfig.colorMappings, { value: '', color: '#718096', contains: true }]
    })
  }

  const handleRemoveMapping = (index) => {
    const newMappings = editConfig.colorMappings.filter((_, i) => i !== index)
    setEditConfig({ ...editConfig, colorMappings: newMappings })
  }

  const handleUpdateMapping = (index, field, value) => {
    const newMappings = [...editConfig.colorMappings]
    newMappings[index] = { ...newMappings[index], [field]: value }
    setEditConfig({ ...editConfig, colorMappings: newMappings })
  }

  // Find panel under mouse position
  const findPanelAtPosition = (mouseX, mouseY) => {
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const panelId = panel.getAttribute('data-panel-id')
      if (panelId === label.id) continue
      const rect = panel.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) {
        return panelId
      }
    }
    return null
  }

  // Move handler
  const handleMoveStart = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startX = x
    const startVisualY = layoutY ?? 0
    let currentX = x
    let currentY = y
    let lastMouseX = e.clientX
    let lastMouseY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startMouseX
      const deltaY = moveEvent.clientY - startMouseY

      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - width, startX + deltaSegments))
      const newVisualY = Math.max(0, startVisualY + deltaY)

      currentX = newX
      currentY = newVisualY
      lastMouseX = moveEvent.clientX
      lastMouseY = moveEvent.clientY

      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
      onDragStateChange?.(true, { x: moveEvent.clientX, y: moveEvent.clientY })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsMoving(false)
      onDragStateChange?.(false, null)

      // Check if dropped on a panel
      const targetPanelId = findPanelAtPosition(lastMouseX, lastMouseY)

      if (targetPanelId && targetPanelId !== parentPanelId) {
        onMoveControl?.(label.id, parentPanelId, targetPanelId)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (targetPanelId === null && parentPanelId !== null) {
        const mainContainer = containerRef?.current
        if (mainContainer) {
          const rect = mainContainer.getBoundingClientRect()
          if (lastMouseX >= rect.left && lastMouseX <= rect.right && lastMouseY >= rect.top && lastMouseY <= rect.bottom) {
            onMoveControl?.(label.id, parentPanelId, null)
            setPreviewPos(null)
            onDragPreview?.(null)
            return
          }
        }
        if (currentX !== x || currentY !== y) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
      } else if (currentX !== x || currentY !== y) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  // Resize handler (proportional)
  const handleResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startWidth = width
    const startHeight = height
    let currentWidth = width
    let currentHeight = height

    setIsResizing(true)

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startMouseX
      const deltaY = moveEvent.clientY - startMouseY
      const delta = Math.max(deltaX, deltaY)

      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      const newHeight = Math.max(MIN_SIZE, startHeight + deltaY)

      currentWidth = newWidth
      currentHeight = newHeight
      setPreviewSize({ width: newWidth, height: newHeight })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsResizing(false)
      const changes = {}
      if (currentWidth !== width) changes.width = currentWidth
      if (currentHeight !== height) changes.height = currentHeight
      if (Object.keys(changes).length > 0) {
        onUpdate(label.id, changes)
      }
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const badgeColor = getBadgeColor()

  return (
    <>
      <Box
        ref={boxRef}
        position="absolute"
        left={`${(displayX / TOTAL_SEGMENTS) * 100}%`}
        top={`${displayY}px`}
        width={`calc(${(displayWidth / TOTAL_SEGMENTS) * 100}% - 8px)`}
        height={`${displayHeight}px`}
        bg={style.bgColor}
        borderRadius="md"
        display="flex"
        alignItems="center"
        justifyContent="center"
        overflow="hidden"
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        borderWidth={`${style.borderWidth}px`}
        borderColor={style.borderColor}
        borderStyle="solid"
        outline={isEditing ? '2px dashed' : 'none'}
        outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : undefined}
        outlineOffset="-2px"
        opacity={isMoving ? 0.7 : 1}
        cursor={isEditing ? 'move' : 'default'}
        onMouseDown={isEditing ? handleMoveStart : undefined}
        zIndex={isMoving ? 100 : 1}
      >
        {/* Badge circle */}
        <Tooltip label={currentValue || 'No data'} placement="top" hasArrow>
          <Box
            width={`${Math.min(displayHeight - 8, 32)}px`}
            height={`${Math.min(displayHeight - 8, 32)}px`}
            borderRadius="full"
            bg={badgeColor}
            transition="background-color 0.3s ease"
          />
        </Tooltip>

        {isEditing && (
          <>
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit badge"
              onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
              onMouseDown={(e) => e.stopPropagation()}
              position="absolute"
              top={0}
              right={6}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete badge"
              onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
              onMouseDown={(e) => e.stopPropagation()}
              position="absolute"
              top={0}
              right={0}
            />
            {/* Corner resize */}
            <Box
              position="absolute"
              right={0}
              bottom={0}
              width="12px"
              height="12px"
              cursor="nwse-resize"
              bg="whiteAlpha.200"
              _hover={{ bg: 'whiteAlpha.400' }}
              onMouseDown={handleResize}
            />
          </>
        )}
      </Box>

      {/* Edit Modal */}
      <Modal isOpen={isOpen} onClose={onClose} size="lg">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Badge</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Placeholder</FormLabel>
                  <Input
                    value={editConfig.placeholder}
                    onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                    placeholder="${tws_connection_status:5}"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Default Color</FormLabel>
                  <Input
                    type="color"
                    value={editConfig.defaultColor}
                    onChange={(e) => setEditConfig({ ...editConfig, defaultColor: e.target.value })}
                    height="40px"
                    p={1}
                  />
                </FormControl>

                <Box>
                  <HStack justify="space-between" mb={2}>
                    <FormLabel fontSize="sm" mb={0}>Color Mappings</FormLabel>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddMapping}>
                      Add
                    </Button>
                  </HStack>

                  <VStack spacing={2} align="stretch">
                    {editConfig.colorMappings.map((mapping, index) => (
                      <HStack key={index} spacing={2}>
                        <Input
                          size="sm"
                          placeholder="Value to match"
                          value={mapping.value}
                          onChange={(e) => handleUpdateMapping(index, 'value', e.target.value)}
                          flex={1}
                        />
                        <Input
                          type="color"
                          value={mapping.color}
                          onChange={(e) => handleUpdateMapping(index, 'color', e.target.value)}
                          width="50px"
                          height="32px"
                          p={1}
                        />
                        <Checkbox
                          size="sm"
                          isChecked={mapping.contains}
                          onChange={(e) => handleUpdateMapping(index, 'contains', e.target.checked)}
                        >
                          Contains
                        </Checkbox>
                        <IconButton
                          icon={<DeleteIcon />}
                          size="xs"
                          variant="ghost"
                          colorScheme="red"
                          onClick={() => handleRemoveMapping(index)}
                          aria-label="Remove mapping"
                        />
                      </HStack>
                    ))}
                  </VStack>
                </Box>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background</FormLabel>
                    <HStack>
                      <Input
                        type="color"
                        value={editConfig.style.bgColor === 'transparent' ? '#4A5568' : editConfig.style.bgColor}
                        onChange={(e) => setEditConfig({
                          ...editConfig,
                          style: { ...editConfig.style, bgColor: e.target.value }
                        })}
                        height="40px"
                        p={1}
                        isDisabled={editConfig.style.bgColor === 'transparent'}
                        flex={1}
                      />
                      <Checkbox
                        isChecked={editConfig.style.bgColor === 'transparent'}
                        onChange={(e) => setEditConfig({
                          ...editConfig,
                          style: { ...editConfig.style, bgColor: e.target.checked ? 'transparent' : '#4A5568' }
                        })}
                        size="sm"
                      >
                        None
                      </Checkbox>
                    </HStack>
                  </FormControl>
                </HStack>
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleSaveConfig}>
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageBadge
