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
  Text,
  Checkbox,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, AddIcon } from '@chakra-ui/icons'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)
import WidgetsGalleryModal from './WidgetsGalleryModal'
import { WIDGET_REGISTRY, createWidget, renderWidget } from './widgetRenderer'
import ConditionalStylesSection from './ConditionalStylesSection'
import { applyConditionalStyles } from '../modules/conditionalStyles'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'

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

function TemplatePagePanel({ label, layoutY, onDelete, onUpdate, onExport, onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [addType, setAddType] = useState('label')
  const [addText, setAddText] = useState('')
  const [addStage, setAddStage] = useState('pick')
  const boxRef = useRef(null)
  const childContainerRef = useRef(null)
  const { isOpen: isStyleOpen, onOpen: onStyleOpen, onClose: onStyleClose } = useDisclosure()
  const { isOpen: isAddOpen, onOpen: onAddOpen, onClose: onAddClose } = useDisclosure()
  const { isOpen: isGalleryOpen, onOpen: onGalleryOpen, onClose: onGalleryClose } = useDisclosure()

  const width = label.width || 8
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0  // Used as order hint for layout calculation
  const children = label.children || []

  const conditionalPlaceholders = (label.style?.conditionalStyles || []).map(r => r.placeholder)
  const { resolveText } = useWidgetResolveText(conditionalPlaceholders)
  const baseStyle = { ...DEFAULT_STYLE, ...label.style }
  const style = applyConditionalStyles(baseStyle, label.style?.conditionalStyles, resolveText)

  // Calculate layout positions for children and total content height
  const calculateChildLayout = (widgets, gap = 8) => {
    if (!widgets.length) return { positions: new Map(), contentHeight: 0 }

    const sorted = [...widgets].sort((a, b) => (a.y || 0) - (b.y || 0) || (a.x || 0) - (b.x || 0))
    const positions = new Map()
    const placed = []

    for (const widget of sorted) {
      const cx = widget.x || 0
      const def = WIDGET_REGISTRY[widget.type] || WIDGET_REGISTRY.label
      const cwidth = widget.width || def.width
      const cheight = widget.height || def.height

      let cy = 0
      for (const p of placed) {
        if (cx < p.x + p.width && cx + cwidth > p.x) {
          cy = Math.max(cy, p.y + p.height + gap)
        }
      }

      positions.set(widget.id, cy)
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
  const displayY = previewPos?.y ?? layoutY ?? 0

  const layoutMode = style.layoutMode || 'absolute'

  const childLayouts = (() => {
    if (layoutMode === 'flow') {
      const dir = style.flowDirection || 'row'
      if (dir === 'column') {
        let curY = 0
        return children.map(child => {
          const def = WIDGET_REGISTRY[child.type] || WIDGET_REGISTRY.label
          const h = child.height ?? def.height
          const entry = { id: child.id, layoutY: curY, labelOverride: { x: 0, y: 0 } }
          curY += h + 8
          return entry
        })
      }
      let curX = 0, curY = 0, rowH = 0
      return children.map(child => {
        const def = WIDGET_REGISTRY[child.type] || WIDGET_REGISTRY.label
        const w = child.width ?? def.width
        const h = child.height ?? def.height
        if (curX + w > TOTAL_SEGMENTS && curX > 0) { curX = 0; curY += rowH + 8; rowH = 0 }
        const entry = { id: child.id, layoutY: curY, labelOverride: { x: curX, y: 0 } }
        curX += w
        rowH = Math.max(rowH, h)
        return entry
      })
    }
    // absolute — use already-computed positions + vertical offset
    return children.map(child => ({
      id: child.id,
      layoutY: (childLayoutPositions.get(child.id) || 0) + verticalOffset,
      labelOverride: null,
    }))
  })()
  const childLayoutMap = new Map(childLayouts.map(e => [e.id, e]))

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

  useEffect(() => {
    if (isAddOpen) setAddStage('pick')
  }, [isAddOpen])

  const handleAddChild = () => {
    const newChild = createWidget(addType, addText.trim())
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

  // In grid/flow modes, strip position/size changes so the layout algorithm stays in control
  const handleUpdateChildInLayout = (childId, changes) => {
    const { x, y, width, height, ...rest } = changes
    if (Object.keys(rest).length > 0) handleUpdateChild(childId, rest)
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
        onMoveWidget?.(label.id, parentPanelId, targetPanelId)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (targetPanelId === null && parentPanelId !== null) {
        const mainContainer = containerRef?.current
        if (mainContainer) {
          const rect = mainContainer.getBoundingClientRect()
          if (lastMouseX >= rect.left && lastMouseX <= rect.right && lastMouseY >= rect.top && lastMouseY <= rect.bottom) {
            onMoveWidget?.(label.id, parentPanelId, null)
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
    activeDrag.widgetId !== label.id &&
    activeDrag.mousePos

  const checkIsOverPanel = () => {
    if (!isDragTarget || !boxRef.current) return false
    const rect = boxRef.current.getBoundingClientRect()
    const { x, y } = activeDrag.mousePos
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom
  }

  const isDropTarget = checkIsOverPanel()

  const renderChild = (child) => {
    const layout = childLayoutMap.get(child.id) || { layoutY: 0, labelOverride: null }
    return renderWidget(child, {
      key: child.id,
      label: layout.labelOverride ? { ...child, ...layout.labelOverride } : child,
      layoutY: layout.layoutY,
      onDelete: handleDeleteChild,
      onUpdate: layoutMode !== 'absolute' ? handleUpdateChildInLayout : handleUpdateChild,
      onExport: onExport,
      onDragStateChange: onDragStateChange,
      onMoveWidget: onMoveWidget,
      activeDrag: activeDrag,
      parentPanelId: label.id,
      isEditing: isEditing,
      containerRef: childContainerRef,
    })
  }

  // Grid uses wrapper Boxes so cell gaps are symmetric (4px edges, 8px between).
  // The widget -8px width convention is absorbed by making each wrapper 8px wider than
  // the visual cell, so: wrapper left = col/N*100% + 4px, wrapper width = 100%/N.
  // Child renders at x=0,w=TOTAL_SEGMENTS → width = 100%-8px = visual cell width.
  const renderGridChildren = () => {
    const cols = Math.max(1, style.gridCols || 2)
    const rows = Math.max(1, style.gridRows || 2)
    const contentH = displayHeight - 2 * PADDING
    const cellH = Math.max(40, (contentH - (rows - 1) * 8) / rows)
    const childProps = {
      onDelete: handleDeleteChild,
      onUpdate: handleUpdateChildInLayout,
      onExport,
      onDragStateChange,
      onMoveWidget,
      activeDrag,
      parentPanelId: label.id,
      isEditing,
      containerRef: childContainerRef,
    }
    return children.map((child, i) => {
      const col = i % cols
      const row = Math.floor(i / cols)
      return (
        <Box
          key={child.id}
          position="absolute"
          left={`calc(${(col / cols) * 100}% + 4px)`}
          top={`${row * (Math.round(cellH) + 8)}px`}
          width={`${(1 / cols) * 100}%`}
          height={`${Math.round(cellH)}px`}
          overflow="hidden"
        >
          {renderWidget(child, {
            key: child.id,
            label: { ...child, x: 0, y: 0, width: TOTAL_SEGMENTS, height: Math.round(cellH) },
            layoutY: 0,
            ...childProps,
          })}
        </Box>
      )
    })
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
        cursor={isEditing ? 'move' : 'default'}
        onMouseDown={isEditing ? handleMoveStart : undefined}
        zIndex={isMoving ? 100 : 1}
      >
        {/* Edit mode controls - absolutely positioned to not affect layout */}
        {isEditing && (
          <Box
            position="absolute"
            top={1}
            right={4}
            zIndex={10}
            display="flex"
          >
            <IconButton
              icon={<AddIcon />}
              size="xs"
              variant="ghost"
              aria-label="Add child widget"
              onClick={(e) => { e.stopPropagation(); onAddOpen() }}
              onMouseDown={(e) => e.stopPropagation()}
            />
            <Tooltip label="Export panel" placement="top" hasArrow>
              <IconButton
                icon={<DownloadIcon />}
                size="xs"
                variant="ghost"
                aria-label="Export panel"
                onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={1}
              />
            </Tooltip>
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
          {layoutMode === 'grid' ? renderGridChildren() : children.map(renderChild)}
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
                <FormControl>
                  <FormLabel fontSize="sm">Layout</FormLabel>
                  <Select
                    value={editStyle.layoutMode || 'absolute'}
                    onChange={(e) => setEditStyle({ ...editStyle, layoutMode: e.target.value })}
                  >
                    <option value="absolute">Absolute</option>
                    <option value="grid">Grid</option>
                    <option value="flow">Flow</option>
                  </Select>
                </FormControl>
                {editStyle.layoutMode === 'grid' && (
                  <HStack width="100%" spacing={4}>
                    <FormControl>
                      <FormLabel fontSize="sm">Columns</FormLabel>
                      <Input
                        type="number" min={1} max={20}
                        value={editStyle.gridCols || 2}
                        onChange={(e) => setEditStyle({ ...editStyle, gridCols: Math.max(1, parseInt(e.target.value) || 2) })}
                      />
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="sm">Rows</FormLabel>
                      <Input
                        type="number" min={1} max={20}
                        value={editStyle.gridRows || 2}
                        onChange={(e) => setEditStyle({ ...editStyle, gridRows: Math.max(1, parseInt(e.target.value) || 2) })}
                      />
                    </FormControl>
                  </HStack>
                )}
                {editStyle.layoutMode === 'flow' && (
                  <FormControl>
                    <FormLabel fontSize="sm">Direction</FormLabel>
                    <Select
                      value={editStyle.flowDirection || 'row'}
                      onChange={(e) => setEditStyle({ ...editStyle, flowDirection: e.target.value })}
                    >
                      <option value="row">Row — left to right, wraps</option>
                      <option value="column">Column — top to bottom</option>
                    </Select>
                  </FormControl>
                )}
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
                  {(!editStyle.layoutMode || editStyle.layoutMode === 'absolute') && (
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
                  )}
                </HStack>
                <ConditionalStylesSection
                  rules={editStyle.conditionalStyles || []}
                  onChange={(rules) => setEditStyle({ ...editStyle, conditionalStyles: rules })}
                  fields={['bgColor', 'borderColor', 'borderWidth']}
                />
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

      <WidgetsGalleryModal isOpen={isGalleryOpen} onClose={onGalleryClose} />
      {/* Add Child Modal */}
      <Modal isOpen={isAddOpen} onClose={onAddClose}>
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
                <Button variant="ghost" onClick={onAddClose}>Cancel</Button>
              </ModalFooter>
            </>
          ) : (
            <>
              <ModalBody>
                <VStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Widget Type</FormLabel>
                    <Select
                      value={addType}
                      onChange={(e) => setAddType(e.target.value)}
                    >
                      {Object.entries(WIDGET_REGISTRY).map(([type, def]) => (
                        <option key={type} value={type}>{def.displayName}</option>
                      ))}
                    </Select>
                  </FormControl>
                  {WIDGET_REGISTRY[addType]?.needsText && (
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
                <Button variant="ghost" mr="auto" onClick={() => setAddStage('pick')}>Back</Button>
                <Button variant="ghost" mr={3} onClick={onAddClose}>Cancel</Button>
                <Button
                  colorScheme="blue"
                  onClick={handleAddChild}
                  isDisabled={WIDGET_REGISTRY[addType]?.needsText && !addText.trim()}
                >
                  Add
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePagePanel
