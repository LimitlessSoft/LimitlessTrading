import { useRef, useState, useEffect } from 'react'
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
  Select,
  HStack,
  VStack,
  Checkbox,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, AddIcon } from '@chakra-ui/icons'
import TemplatePageLabel from './TemplatePageLabel'
import TemplatePageBadge from './TemplatePageBadge'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2
const MIN_HEIGHT = 58
const DEFAULT_HEIGHT = 120

const DEFAULT_STYLE = {
  bgColor: '#2D3748',
  borderColor: '#4A5568',
  borderWidth: 1,
  verticalAlign: 'top'
}

function TemplatePagePanel({ label, layoutY, onDelete, onUpdate, onDragPreview, onDragStateChange, onMoveControl, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [addType, setAddType] = useState('label')
  const [addText, setAddText] = useState('')
  const boxRef = useRef(null)
  const childContainerRef = useRef(null)
  const { isOpen: isStyleOpen, onOpen: onStyleOpen, onClose: onStyleClose } = useDisclosure()
  const { isOpen: isAddOpen, onOpen: onAddOpen, onClose: onAddClose } = useDisclosure()

  const width = label.width || 8
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0  // Used as order hint for layout calculation
  const style = { ...DEFAULT_STYLE, ...label.style }
  const children = label.children || []

  // Calculate layout positions for children and total content height
  const calculateChildLayout = (controls, gap = 8) => {
    if (!controls.length) return { positions: new Map(), contentHeight: 0 }

    const sorted = [...controls].sort((a, b) => (a.y || 0) - (b.y || 0) || (a.x || 0) - (b.x || 0))
    const positions = new Map()
    const placed = []

    for (const control of sorted) {
      const cx = control.x || 0
      const cwidth = control.width || (control.type === 'panel' ? 8 : 4)
      const cheight = control.height || (control.type === 'panel' ? DEFAULT_HEIGHT : 60)

      let cy = 0
      for (const p of placed) {
        if (cx < p.x + p.width && cx + cwidth > p.x) {
          cy = Math.max(cy, p.y + p.height + gap)
        }
      }

      positions.set(control.id, cy)
      placed.push({ x: cx, width: cwidth, y: cy, height: cheight })
    }

    // Calculate total content height
    let contentHeight = 0
    for (const p of placed) {
      contentHeight = Math.max(contentHeight, p.y + p.height)
    }

    return { positions, contentHeight }
  }

  const { positions: childLayoutPositions, contentHeight } = calculateChildLayout(children)

  // Calculate vertical offset based on alignment
  // Note: children use absolute positioning, so top:0 is at the padding edge.
  // For middle/bottom, we add top padding to position within the visual content area.
  const PADDING = 8 // matches p={2} on children container
  const getVerticalOffset = () => {
    if (!childContainerRef.current || children.length === 0) return 0
    const availableHeight = childContainerRef.current.clientHeight - (PADDING * 2)
    if (contentHeight >= availableHeight) return 0

    switch (style.verticalAlign) {
      case 'middle':
        // Center within the padded content area (add padding to offset from edge)
        return PADDING + (availableHeight - contentHeight) / 2
      case 'bottom':
        // Align to bottom of content area (padding + available - content)
        return PADDING + availableHeight - contentHeight
      default:
        // Top: start at top padding edge
        return PADDING
    }
  }

  const [verticalOffset, setVerticalOffset] = useState(0)

  // Recalculate offset when relevant values change
  useEffect(() => {
    const updateOffset = () => {
      setVerticalOffset(getVerticalOffset())
    }
    updateOffset()
    // Also update on resize
    const observer = new ResizeObserver(updateOffset)
    if (childContainerRef.current) {
      observer.observe(childContainerRef.current)
    }
    return () => observer.disconnect()
  }, [children, style.verticalAlign, height, contentHeight])

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0  // Show preview during drag, otherwise use calculated layout

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

  const handleOpenStyleEdit = () => {
    setEditStyle({ ...DEFAULT_STYLE, ...label.style })
    onStyleOpen()
  }

  const handleSaveStyle = () => {
    onUpdate(label.id, { style: editStyle })
    onStyleClose()
  }

  const handleAddChild = () => {
    const newChild = {
      id: crypto.randomUUID(),
      type: addType,
      text: addType === 'label' ? addText.trim() : undefined,
      width: addType === 'panel' ? 8 : addType === 'badge' ? 2 : 4,
      height: addType === 'panel' ? DEFAULT_HEIGHT : addType === 'badge' ? 40 : 60,
      x: 0,
      y: 0,
      createdAt: Date.now(),
      children: addType === 'panel' ? [] : undefined
    }
    onUpdate(label.id, { children: [...children, newChild] })
    setAddText('')
    setAddType('label')
    onAddClose()
  }

  const handleDeleteChild = (childId) => {
    const updatedChildren = children.filter(c => c.id !== childId)
    onUpdate(label.id, { children: updatedChildren })
  }

  const handleUpdateChild = (childId, changes) => {
    const updatedChildren = children.map(c =>
      c.id === childId ? { ...c, ...changes } : c
    )
    onUpdate(label.id, { children: updatedChildren })
  }

  // Find panel under mouse position (excluding self and children)
  const findPanelAtPosition = (mouseX, mouseY) => {
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const panelId = panel.getAttribute('data-panel-id')
      // Don't allow dropping on self
      if (panelId === label.id) continue
      // Don't allow dropping on own children (would cause infinite loop)
      if (boxRef.current?.contains(panel)) continue
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

  // Check if something is being dragged over this panel
  const isDragTarget = activeDrag &&
    activeDrag.controlId !== label.id &&
    activeDrag.mousePos

  const checkIsOverPanel = () => {
    if (!isDragTarget || !boxRef.current) return false
    const rect = boxRef.current.getBoundingClientRect()
    const { x, y } = activeDrag.mousePos
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom
  }

  const isDropTarget = checkIsOverPanel()

  const renderChild = (child) => {
    const childCommonProps = {
      key: child.id,
      label: child,
      layoutY: (childLayoutPositions.get(child.id) || 0) + verticalOffset,
      onDelete: handleDeleteChild,
      onUpdate: handleUpdateChild,
      onDragStateChange: onDragStateChange,
      onMoveControl: onMoveControl,
      activeDrag: activeDrag,
      parentPanelId: label.id,
      isEditing: isEditing,
      containerRef: childContainerRef
    }

    if (child.type === 'panel') {
      return <TemplatePagePanel {...childCommonProps} />
    } else if (child.type === 'badge') {
      return <TemplatePageBadge {...childCommonProps} />
    }
    return <TemplatePageLabel {...childCommonProps} />
  }

  return (
    <>
      <Box
        ref={boxRef}
        data-panel-id={label.id}
        position="absolute"
        left={`${(displayX / TOTAL_SEGMENTS) * 100}%`}
        top={`${displayY}px`}
        width={`calc(${(displayWidth / TOTAL_SEGMENTS) * 100}% - 8px)`}
        height={`${displayHeight}px`}
        bg={isDropTarget ? 'blue.900' : style.bgColor}
        borderRadius="md"
        display="flex"
        flexDirection="column"
        overflow="hidden"
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        borderWidth={isDropTarget ? '2px' : `${style.borderWidth}px`}
        borderColor={isDropTarget ? 'blue.400' : style.borderColor}
        borderStyle="solid"
        outline={isEditing && !isDropTarget ? '2px dashed' : 'none'}
        outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.500') : undefined}
        outlineOffset="-2px"
        opacity={isMoving ? 0.7 : 1}
        zIndex={isMoving ? 100 : 1}
      >
        {/* Header with controls - drag handle area (only visible in edit mode) */}
        {isEditing && (
          <Box
            px={2}
            py={1}
            cursor="move"
            onMouseDown={handleMoveStart}
            bg="whiteAlpha.100"
            display="flex"
            justifyContent="flex-end"
            flexShrink={0}
          >
            <IconButton
              icon={<AddIcon />}
              size="xs"
              variant="ghost"
              aria-label="Add child control"
              onClick={(e) => { e.stopPropagation(); onAddOpen() }}
              onMouseDown={(e) => e.stopPropagation()}
            />
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit panel style"
              onClick={(e) => { e.stopPropagation(); handleOpenStyleEdit() }}
              onMouseDown={(e) => e.stopPropagation()}
              ml={1}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete panel"
              onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
              onMouseDown={(e) => e.stopPropagation()}
              ml={1}
            />
          </Box>
        )}

        {/* Children container - relative positioning for absolute children */}
        <Box
          ref={childContainerRef}
          position="relative"
          flex={1}
          overflow="auto"
          p={2}
        >
          {children.map(renderChild)}
        </Box>

        {/* Resize handles */}
        {isEditing && (
          <>
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

      {/* Style Edit Modal */}
      <Modal isOpen={isStyleOpen} onClose={onStyleClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Panel Style</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editStyle && (
              <VStack spacing={4}>
                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background Color</FormLabel>
                    <HStack>
                      <Input
                        type="color"
                        value={editStyle.bgColor === 'transparent' ? '#2D3748' : editStyle.bgColor}
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
                          bgColor: e.target.checked ? 'transparent' : '#2D3748'
                        })}
                        size="sm"
                      >
                        None
                      </Checkbox>
                    </HStack>
                  </FormControl>
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
                </HStack>
                <HStack width="100%" spacing={4}>
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
                  <FormControl>
                    <FormLabel fontSize="sm">Vertical Align</FormLabel>
                    <Select
                      value={editStyle.verticalAlign || 'top'}
                      onChange={(e) => setEditStyle({ ...editStyle, verticalAlign: e.target.value })}
                    >
                      <option value="top">Top</option>
                      <option value="middle">Middle</option>
                      <option value="bottom">Bottom</option>
                    </Select>
                  </FormControl>
                </HStack>
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onStyleClose}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleSaveStyle}>
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      {/* Add Child Modal */}
      <Modal isOpen={isAddOpen} onClose={onAddClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Add Control</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel fontSize="sm">Control Type</FormLabel>
                <Select
                  value={addType}
                  onChange={(e) => setAddType(e.target.value)}
                >
                  <option value="label">Label</option>
                  <option value="panel">Panel</option>
                  <option value="badge">Badge</option>
                </Select>
              </FormControl>
              {addType === 'label' && (
                <FormControl>
                  <FormLabel fontSize="sm">Label Text</FormLabel>
                  <Input
                    placeholder="Enter label text"
                    value={addText}
                    onChange={(e) => setAddText(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && addText.trim()) {
                        handleAddChild()
                      }
                    }}
                  />
                </FormControl>
              )}
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onAddClose}>
              Cancel
            </Button>
            <Button
              colorScheme="blue"
              onClick={handleAddChild}
              isDisabled={addType === 'label' && !addText.trim()}
            >
              Add
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePagePanel
