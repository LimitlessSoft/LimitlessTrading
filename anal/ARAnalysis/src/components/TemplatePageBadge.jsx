import { useRef, useState, useEffect, useCallback } from 'react'
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
const PLACEHOLDER_REGEX_GLOBAL = /\$\{(\w+)(?::(\d+))?\}/g

function TemplatePageBadge({ label, layoutY, onDelete, onUpdate, onDragPreview, onDragStateChange, onMoveControl, activeDrag, parentPanelId, isEditing, containerRef, isHeaderMode }) {
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
  const valueProvider = label.placeholder || ''
  const text = label.text || ''
  const colorMappings = label.colorMappings || []
  const defaultColor = label.defaultColor || '#718096'

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  // Check if text contains placeholders
  const hasTextPlaceholders = text && text.includes('${')

  // Parse value provider and text to get minimum refresh interval
  const getRefreshInterval = useCallback(() => {
    let minInterval = Infinity

    // Check value provider
    if (valueProvider) {
      const match = valueProvider.match(PLACEHOLDER_REGEX)
      if (match) {
        const interval = match[2] ? parseInt(match[2], 10) : 5
        if (interval < minInterval) minInterval = interval
      }
    }

    // Check text placeholders
    if (text) {
      const matches = [...text.matchAll(PLACEHOLDER_REGEX_GLOBAL)]
      for (const match of matches) {
        const interval = match[2] ? parseInt(match[2], 10) : 5
        if (interval < minInterval) minInterval = interval
      }
    }

    return minInterval === Infinity ? null : minInterval
  }, [valueProvider, text])

  // Fetch all placeholder values
  const fetchPlaceholderValues = useCallback(async () => {
    const needsTwsStatus = valueProvider.includes('tws_connection_status') || text.includes('tws_connection_status')

    if (needsTwsStatus) {
      try {
        const status = await getConnectionStatus()
        setCurrentValue(status.connected ? `Connected (${status.port})` : 'Disconnected')
      } catch (err) {
        setCurrentValue('Unknown')
      }
    }
  }, [valueProvider, text])

  // Resolve placeholders in text
  const resolveText = (inputText) => {
    if (!inputText) return ''
    return inputText.replace(PLACEHOLDER_REGEX_GLOBAL, (match, key) => {
      if (key === 'tws_connection_status') {
        return currentValue || match
      }
      return match
    })
  }

  // Poll for placeholder values
  useEffect(() => {
    if (!valueProvider && !hasTextPlaceholders) return

    const refreshInterval = getRefreshInterval()
    if (!refreshInterval) return

    fetchPlaceholderValues()
    const interval = setInterval(fetchPlaceholderValues, refreshInterval * 1000)
    return () => clearInterval(interval)
  }, [valueProvider, hasTextPlaceholders, fetchPlaceholderValues, getRefreshInterval])

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
      placeholder: valueProvider,
      text: text,
      colorMappings: colorMappings.length > 0 ? [...colorMappings] : [{ value: '', color: '#48BB78', contains: true }],
      defaultColor: defaultColor,
      style: { ...DEFAULT_STYLE, ...label.style }
    })
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      placeholder: editConfig.placeholder,
      text: editConfig.text,
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

  // Find drop target under mouse position (panel or header)
  const findDropTarget = (mouseX, mouseY) => {
    // Check header first
    const header = document.querySelector('[data-header-dropzone]')
    if (header) {
      const rect = header.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) {
        return 'header'
      }
    }

    // Check panels
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

      // Check if dropped on a panel or header
      const dropTarget = findDropTarget(lastMouseX, lastMouseY)

      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveControl?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
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

  // Header mode: simplified inline rendering
  if (isHeaderMode) {
    return (
      <>
        <Box
          ref={boxRef}
          display="flex"
          alignItems="center"
          outline={isEditing ? '1px dashed' : 'none'}
          outlineColor={isEditing ? 'gray.500' : undefined}
          cursor={isEditing ? 'move' : 'default'}
          onMouseDown={isEditing ? handleMoveStart : undefined}
          opacity={isMoving ? 0.7 : 1}
          px={1}
        >
          <Tooltip label={currentValue || 'No data'} placement="bottom" hasArrow>
            <Box
              width="16px"
              height="16px"
              borderRadius="full"
              bg={badgeColor}
              transition="background-color 0.3s ease"
              flexShrink={0}
            />
          </Tooltip>
          {text && (
            <Text
              ml={1}
              fontSize="sm"
              whiteSpace="nowrap"
              color="white"
            >
              {resolveText(text)}
            </Text>
          )}
          {isEditing && (
            <>
              <IconButton
                icon={<SettingsIcon />}
                size="xs"
                variant="ghost"
                aria-label="Edit badge"
                onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={1}
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete badge"
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
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
                    <FormLabel fontSize="sm">Value Provider</FormLabel>
                    <Input
                      value={editConfig.placeholder}
                      onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                      placeholder="${tws_connection_status:5}"
                    />
                  </FormControl>

                  <FormControl>
                    <FormLabel fontSize="sm">Text</FormLabel>
                    <Input
                      value={editConfig.text || ''}
                      onChange={(e) => setEditConfig({ ...editConfig, text: e.target.value })}
                      placeholder="Optional text (supports ${placeholder} syntax)"
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
        justifyContent={text ? 'flex-start' : 'center'}
        px={text ? 2 : 0}
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
            flexShrink={0}
          />
        </Tooltip>
        {text && (
          <Text
            ml={2}
            fontSize="sm"
            whiteSpace="nowrap"
            color="white"
            pointerEvents="none"
            userSelect="none"
          >
            {resolveText(text)}
          </Text>
        )}

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
                  <FormLabel fontSize="sm">Value Provider</FormLabel>
                  <Input
                    value={editConfig.placeholder}
                    onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                    placeholder="${tws_connection_status:5}"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Text</FormLabel>
                  <Input
                    value={editConfig.text || ''}
                    onChange={(e) => setEditConfig({ ...editConfig, text: e.target.value })}
                    placeholder="Optional text (supports ${placeholder} syntax)"
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
