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
  Select,
  HStack,
  VStack,
  Checkbox,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, InfoOutlineIcon } from '@chakra-ui/icons'
import { useNavigate } from 'react-router-dom'
import { getConnectionStatus } from '../modules/dataService'
import { useSymbolQuotes, hasSymbolPlaceholders } from '../hooks/useSymbolQuotes'
import { getExchangeTime, getExchangeStatus } from '../modules/exchangeService'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2
const MIN_HEIGHT = 40
const DEFAULT_HEIGHT = 60

const DEFAULT_STYLE = {
  bgColor: '#4A5568',
  borderColor: '#718096',
  borderWidth: 0,
  textColor: '#FFFFFF',
  fontWeight: 'normal',
  fontStyle: 'normal',
  fontSize: 1,
  textAlign: 'left',
  verticalAlign: 'top'
}

// Placeholder pattern for template variables: ${name} or ${name:interval}
const PLACEHOLDER_REGEX = /\$\{(\w+)(?::(\d+))?\}/g

function TemplatePageLabel({ label, layoutY, onDelete, onUpdate, onDragPreview, onDragStateChange, onMoveControl, activeDrag, parentPanelId, isEditing, containerRef, isHeaderMode }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [editText, setEditText] = useState('')
  const [placeholderValues, setPlaceholderValues] = useState({})
  const boxRef = useRef(null)
  const navigate = useNavigate()
  const { isOpen, onOpen, onClose } = useDisclosure()

  // Symbol quote placeholders (@{SYMBOL:property:seconds})
  const { resolveText: resolveSymbolText } = useSymbolQuotes(label.text)

  // Check if text contains system placeholders (${...}) and extract refresh interval
  const hasSystemPlaceholders = label.text && label.text.includes('${')

  // Parse system placeholders to get the minimum refresh interval
  const getRefreshInterval = useCallback(() => {
    if (!label.text) return null
    let minInterval = Infinity

    for (const match of label.text.matchAll(PLACEHOLDER_REGEX)) {
      const interval = match[2] ? parseInt(match[2], 10) : 5
      if (interval < minInterval) minInterval = interval
    }

    for (const match of label.text.matchAll(/\$\{[A-Za-z0-9]+:(?:time|status)(?::(\d+))?\}/g)) {
      const interval = match[1] ? parseInt(match[1], 10) : 5
      if (interval < minInterval) minInterval = interval
    }

    return minInterval === Infinity ? null : minInterval
  }, [label.text])

  // Fetch system placeholder values
  const fetchPlaceholderValues = useCallback(async () => {
    if (!label.text) return

    const values = {}

    if (label.text.includes('${tws_connection_status')) {
      try {
        const status = await getConnectionStatus()
        values.tws_connection_status = status.connected
          ? `Connected (${status.port})`
          : 'Disconnected'
      } catch (err) {
        values.tws_connection_status = 'Unknown'
      }
    }

    for (const match of label.text.matchAll(/\$\{([A-Za-z0-9]+):(time|status)(?::(\d+))?\}/g)) {
      const exchange = match[1].toUpperCase()
      const property = match[2]
      values[`${exchange}_${property}`] = property === 'time'
        ? getExchangeTime(exchange)
        : getExchangeStatus(exchange)
    }

    setPlaceholderValues(values)
  }, [label.text])

  // Poll for system placeholder values when text contains them
  useEffect(() => {
    if (!hasSystemPlaceholders) return

    const refreshInterval = getRefreshInterval()
    if (!refreshInterval) return

    fetchPlaceholderValues()
    const interval = setInterval(fetchPlaceholderValues, refreshInterval * 1000)
    return () => clearInterval(interval)
  }, [hasSystemPlaceholders, fetchPlaceholderValues, getRefreshInterval])

  // Resolve system placeholders in text (${name} or ${name:interval})
  const resolveSystemText = (text) => {
    if (!text) return ''
    let resolved = text.replace(PLACEHOLDER_REGEX, (match, key) => {
      return placeholderValues[key] !== undefined ? placeholderValues[key] : match
    })
    resolved = resolved.replace(/\$\{([A-Za-z0-9]+):(time|status)(?::(\d+))?\}/g, (match, exchange, property) => {
      const key = `${exchange.toUpperCase()}_${property}`
      return placeholderValues[key] !== undefined ? placeholderValues[key] : match
    })
    return resolved
  }

  // Resolve all placeholders: first system (${...}), then symbol (@{...})
  const resolveText = (text) => {
    const systemResolved = resolveSystemText(text)
    return resolveSymbolText(systemResolved)
  }

  const width = label.width || 4
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0  // Used as order hint for layout calculation
  const style = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0  // Show preview during drag, otherwise use calculated layout

  // Clear preview once the label catches up
  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  // Clear position preview when label catches up (but not during active move)
  useEffect(() => {
    if (previewPos && !isMoving) {
      // Clear when x has caught up (y is order hint, will be recalculated by layout)
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
    setEditStyle({ ...DEFAULT_STYLE, ...label.style })
    setEditText(label.text || '')
    onOpen()
  }

  const handleSaveStyle = () => {
    onUpdate(label.id, { style: editStyle, text: editText })
    onClose()
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

  // Move handler (drag to reposition)
  const handleMoveStart = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const info = getContainerInfo()
    if (!info) return

    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startX = x
    const startVisualY = layoutY ?? 0  // Start from visual position
    let currentX = x
    let currentY = y
    let lastMouseX = e.clientX
    let lastMouseY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startMouseX
      const deltaY = moveEvent.clientY - startMouseY

      // Snap X to segments
      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - width, startX + deltaSegments))
      // Visual Y follows mouse, order hint updated proportionally
      const newVisualY = Math.max(0, startVisualY + deltaY)

      currentX = newX
      currentY = newVisualY  // Use visual position as new order hint
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
        // Move to the target (panel or header)
        onMoveControl?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
        // Check if dropped outside any panel/header - move to root
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
        // Normal position update within current container
        if (currentX !== x || currentY !== y) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
      } else if (currentX !== x || currentY !== y) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        // No change, clear previews immediately
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleHorizontalResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const info = getContainerInfo()
    if (!info) return

    const startX = e.clientX
    const startWidth = width
    let currentWidth = width

    setIsResizing(true)

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startX
      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      currentWidth = newWidth
      setPreviewSize({ width: newWidth, height })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsResizing(false)
      if (currentWidth !== width) {
        onUpdate(label.id, { width: currentWidth })
      }
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleVerticalResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const startY = e.clientY
    const startHeight = height
    let currentHeight = height

    setIsResizing(true)

    const handleMouseMove = (moveEvent) => {
      const deltaY = moveEvent.clientY - startY
      const newHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
      currentHeight = newHeight
      setPreviewSize({ width, height: newHeight })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsResizing(false)
      if (currentHeight !== height) {
        onUpdate(label.id, { height: currentHeight })
      }
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleCornerResize = (e) => {
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
      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      const newHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
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

  // Header mode: simplified inline rendering
  if (isHeaderMode) {
    return (
      <>
        <Box
          ref={boxRef}
          bg={style.bgColor}
          px={2}
          py={1}
          borderRadius="md"
          display="flex"
          alignItems="center"
          borderWidth={`${style.borderWidth}px`}
          borderColor={style.borderColor}
          borderStyle="solid"
          outline={isEditing ? '1px dashed' : 'none'}
          outlineColor={isEditing ? 'gray.500' : undefined}
          cursor={isEditing ? 'move' : 'default'}
          onMouseDown={isEditing ? handleMoveStart : undefined}
          opacity={isMoving ? 0.7 : 1}
        >
          <Text
            whiteSpace="nowrap"
            color={style.textColor}
            fontWeight={style.fontWeight}
            fontStyle={style.fontStyle}
            fontSize={`${(style.fontSize || 1) * 0.875}rem`}
            pointerEvents="none"
            userSelect="none"
          >
            {resolveText(label.text)}
          </Text>
          {isEditing && (
            <>
              <IconButton
                icon={<SettingsIcon />}
                size="xs"
                variant="ghost"
                aria-label="Edit label"
                onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={1}
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete label"
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </>
          )}
        </Box>
        {/* Edit Modal - same as normal mode */}
        <Modal isOpen={isOpen} onClose={onClose}>
          <ModalOverlay />
          <ModalContent bg="gray.800">
            <ModalHeader>Edit Label</ModalHeader>
            <ModalCloseButton />
            <ModalBody>
              {editStyle && (
                <VStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm" display="flex" alignItems="center" gap={1}>
                      Label Text
                      <Tooltip label="View placeholder API docs" placement="top" hasArrow>
                        <IconButton
                          icon={<InfoOutlineIcon />}
                          size="xs"
                          variant="ghost"
                          aria-label="Placeholder documentation"
                          onClick={() => window.open('/docs/placeholders', '_blank')}
                          minW="auto"
                          h="auto"
                          p={0}
                          color="gray.400"
                          _hover={{ color: 'blue.300' }}
                        />
                      </Tooltip>
                    </FormLabel>
                    <Input
                      value={editText}
                      onChange={(e) => setEditText(e.target.value)}
                      placeholder="Enter label text"
                    />
                  </FormControl>
                  <HStack width="100%" spacing={4}>
                    <FormControl>
                      <FormLabel fontSize="sm">Background</FormLabel>
                      <HStack>
                        <Input
                          type="color"
                          value={editStyle.bgColor === 'transparent' ? '#4A5568' : editStyle.bgColor}
                          onChange={(e) => setEditStyle({ ...editStyle, bgColor: e.target.value })}
                          height="40px"
                          p={1}
                          isDisabled={editStyle.bgColor === 'transparent'}
                          flex={1}
                        />
                        <Checkbox
                          isChecked={editStyle.bgColor === 'transparent'}
                          onChange={(e) => setEditStyle({
                            ...editStyle,
                            bgColor: e.target.checked ? 'transparent' : '#4A5568'
                          })}
                          size="sm"
                        >
                          None
                        </Checkbox>
                      </HStack>
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="sm">Text Color</FormLabel>
                      <Input
                        type="color"
                        value={editStyle.textColor}
                        onChange={(e) => setEditStyle({ ...editStyle, textColor: e.target.value })}
                        height="40px"
                        p={1}
                      />
                    </FormControl>
                  </HStack>
                </VStack>
              )}
            </ModalBody>
            <ModalFooter>
              <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
              <Button colorScheme="blue" onClick={handleSaveStyle}>Save</Button>
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
        px={4}
        py={2}
        borderRadius="md"
        display="flex"
        alignItems={style.verticalAlign === 'top' ? 'flex-start' : style.verticalAlign === 'bottom' ? 'flex-end' : 'center'}
        justifyContent="space-between"
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
        <Text
          flex={1}
          whiteSpace="pre-wrap"
          color={style.textColor}
          fontWeight={style.fontWeight}
          fontStyle={style.fontStyle}
          fontSize={`${style.fontSize}rem`}
          textAlign={style.textAlign}
          pointerEvents="none"
          userSelect="none"
        >
          {resolveText(label.text)}
        </Text>
        {isEditing && (
          <>
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit label style"
              onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
              onMouseDown={(e) => e.stopPropagation()}
              ml={2}
              flexShrink={0}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete label"
              onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
              onMouseDown={(e) => e.stopPropagation()}
              ml={1}
              flexShrink={0}
            />
            {/* Right edge - horizontal resize */}
            <Box
              position="absolute"
              right={0}
              top={0}
              bottom="12px"
              width="12px"
              cursor="ew-resize"
              display="flex"
              alignItems="center"
              justifyContent="center"
              bg="whiteAlpha.100"
              _hover={{ bg: 'whiteAlpha.300' }}
              onMouseDown={handleHorizontalResize}
            >
              <DragHandleIcon boxSize={3} transform="rotate(90deg)" />
            </Box>
            {/* Bottom edge - vertical resize */}
            <Box
              position="absolute"
              bottom={0}
              left={0}
              right="12px"
              height="12px"
              cursor="ns-resize"
              display="flex"
              alignItems="center"
              justifyContent="center"
              bg="whiteAlpha.100"
              _hover={{ bg: 'whiteAlpha.300' }}
              onMouseDown={handleVerticalResize}
            >
              <DragHandleIcon boxSize={3} />
            </Box>
            {/* Corner - both resize */}
            <Box
              position="absolute"
              right={0}
              bottom={0}
              width="12px"
              height="12px"
              cursor="nwse-resize"
              bg="whiteAlpha.200"
              _hover={{ bg: 'whiteAlpha.400' }}
              onMouseDown={handleCornerResize}
            />
          </>
        )}
      </Box>

      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Label</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editStyle && (
              <VStack spacing={4}>
                <FormControl>
                  <FormLabel fontSize="sm" display="flex" alignItems="center" gap={1}>
                    Label Text
                    <Tooltip label="View placeholder API docs" placement="top" hasArrow>
                      <IconButton
                        icon={<InfoOutlineIcon />}
                        size="xs"
                        variant="ghost"
                        aria-label="Placeholder documentation"
                        onClick={() => window.open('/docs/placeholders', '_blank')}
                        minW="auto"
                        h="auto"
                        p={0}
                        color="gray.400"
                        _hover={{ color: 'blue.300' }}
                      />
                    </Tooltip>
                  </FormLabel>
                  <Input
                    value={editText}
                    onChange={(e) => setEditText(e.target.value)}
                    placeholder="Enter label text (supports ${placeholder} syntax)"
                  />
                </FormControl>

                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background Color</FormLabel>
                    <HStack>
                      <Input
                        type="color"
                        value={editStyle.bgColor === 'transparent' ? '#4A5568' : editStyle.bgColor}
                        onChange={(e) => setEditStyle({ ...editStyle, bgColor: e.target.value })}
                        height="40px"
                        p={1}
                        isDisabled={editStyle.bgColor === 'transparent'}
                        flex={1}
                      />
                      <Checkbox
                        isChecked={editStyle.bgColor === 'transparent'}
                        onChange={(e) => setEditStyle({
                          ...editStyle,
                          bgColor: e.target.checked ? 'transparent' : '#4A5568'
                        })}
                        size="sm"
                      >
                        None
                      </Checkbox>
                    </HStack>
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Text Color</FormLabel>
                    <Input
                      type="color"
                      value={editStyle.textColor}
                      onChange={(e) => setEditStyle({ ...editStyle, textColor: e.target.value })}
                      height="40px"
                      p={1}
                    />
                  </FormControl>
                </HStack>

                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Border Color</FormLabel>
                    <Input
                      type="color"
                      value={editStyle.borderColor}
                      onChange={(e) => setEditStyle({ ...editStyle, borderColor: e.target.value })}
                      height="40px"
                      p={1}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Border Width (px)</FormLabel>
                    <Input
                      type="number"
                      min={0}
                      max={10}
                      value={editStyle.borderWidth}
                      onChange={(e) => setEditStyle({ ...editStyle, borderWidth: parseInt(e.target.value) || 0 })}
                    />
                  </FormControl>
                </HStack>

                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Font Weight</FormLabel>
                    <Select
                      value={editStyle.fontWeight}
                      onChange={(e) => setEditStyle({ ...editStyle, fontWeight: e.target.value })}
                    >
                      <option value="normal">Normal</option>
                      <option value="bold">Bold</option>
                      <option value="lighter">Light</option>
                    </Select>
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Font Style</FormLabel>
                    <Select
                      value={editStyle.fontStyle}
                      onChange={(e) => setEditStyle({ ...editStyle, fontStyle: e.target.value })}
                    >
                      <option value="normal">Normal</option>
                      <option value="italic">Italic</option>
                    </Select>
                  </FormControl>
                </HStack>

                <FormControl>
                  <FormLabel fontSize="sm">Font Size (rem)</FormLabel>
                  <Input
                    type="number"
                    min={0.5}
                    max={4}
                    step={0.1}
                    value={editStyle.fontSize}
                    onChange={(e) => setEditStyle({ ...editStyle, fontSize: parseFloat(e.target.value) || 1 })}
                  />
                </FormControl>

                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Horizontal Align</FormLabel>
                    <Select
                      value={editStyle.textAlign}
                      onChange={(e) => setEditStyle({ ...editStyle, textAlign: e.target.value })}
                    >
                      <option value="left">Left</option>
                      <option value="center">Center</option>
                      <option value="right">Right</option>
                    </Select>
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Vertical Align</FormLabel>
                    <Select
                      value={editStyle.verticalAlign}
                      onChange={(e) => setEditStyle({ ...editStyle, verticalAlign: e.target.value })}
                    >
                      <option value="top">Top</option>
                      <option value="center">Center</option>
                      <option value="bottom">Bottom</option>
                    </Select>
                  </FormControl>
                </HStack>
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleSaveStyle}>
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageLabel
