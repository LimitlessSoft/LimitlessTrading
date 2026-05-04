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
import FillIcon from './FillIcon'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
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

function TemplatePagePanel({ label, layoutY, onDelete, onUpdate, onExport, onLinkSync, onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [addType, setAddType] = useState('label')
  const [addText, setAddText] = useState('')
  const [addStage, setAddStage] = useState('pick')
  const [childContainerWidth, setChildContainerWidth] = useState(0)
  const [childContainerHeight, setChildContainerHeight] = useState(0)
  const boxRef = useRef(null)
  const childContainerRef = useRef(null)
  const [gridDragIdx, setGridDragIdx] = useState(null)
  const [gridDropIdx, setGridDropIdx] = useState(null)
  const gridDropIdxRef = useRef(null)
  const { isOpen: isStyleOpen, onOpen: onStyleOpen, onClose: onStyleClose } = useDisclosure()
  const { isOpen: isAddOpen, onOpen: onAddOpen, onClose: onAddClose } = useDisclosure()
  const { isOpen: isGalleryOpen, onOpen: onGalleryOpen, onClose: onGalleryClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()

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

  const gap = style.gap ?? 8
  const { positions: childLayoutPositions, contentHeight } = calculateChildLayout(children, gap)

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

  useEffect(() => {
    if (!childContainerRef.current) return
    const ro = new ResizeObserver(entries => {
      setChildContainerWidth(entries[0].contentRect.width)
      setChildContainerHeight(entries[0].contentRect.height)
    })
    ro.observe(childContainerRef.current)
    return () => ro.disconnect()
  }, [])

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
          curY += h + gap
          return entry
        })
      }
      let curX = 0, curY = 0, rowH = 0
      return children.map(child => {
        const def = WIDGET_REGISTRY[child.type] || WIDGET_REGISTRY.label
        const w = child.width ?? def.width
        const h = child.height ?? def.height
        if (curX + w > TOTAL_SEGMENTS && curX > 0) { curX = 0; curY += rowH + gap; rowH = 0 }
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

  // In flow mode, position changes reorder the children array; in grid mode strip them
  const handleUpdateChildInLayout = (childId, changes) => {
    const { x, y, width, height, ...rest } = changes

    if (layoutMode === 'flow' && (y !== undefined || x !== undefined)) {
      const dragY = y ?? 0
      const dragX = x ?? 0
      const dir = style.flowDirection || 'row'

      const others = children.filter(c => c.id !== childId).map(c => {
        const layout = childLayoutMap.get(c.id) || { layoutY: 0, labelOverride: null }
        const def = WIDGET_REGISTRY[c.type] || WIDGET_REGISTRY.label
        const h = c.height ?? def.height
        const w = c.width ?? def.width
        return {
          id: c.id,
          rowY: layout.layoutY,
          rowH: h,
          cy: layout.layoutY + h / 2,
          cx: (layout.labelOverride?.x ?? 0) + w / 2,
        }
      })

      let newIndex = others.length
      for (let i = 0; i < others.length; i++) {
        const { cy, cx, rowY, rowH } = others[i]
        if (dir === 'column') {
          if (dragY < cy) { newIndex = i; break }
        } else {
          // Row flow: items on the same row share layoutY; compare cx within a row, cy across rows
          const sameRow = dragY >= rowY && dragY < rowY + rowH
          if (sameRow) {
            if (dragX < cx) { newIndex = i; break }
          } else if (dragY < cy) {
            newIndex = i; break
          }
        }
      }

      const withoutDragged = children.filter(c => c.id !== childId)
      const dragged = children.find(c => c.id === childId)
      if (dragged) {
        const reordered = [...withoutDragged.slice(0, newIndex), dragged, ...withoutDragged.slice(newIndex)]
        if (reordered.some((c, i) => c.id !== children[i]?.id)) {
          onUpdate(label.id, { children: reordered })
          return
        }
      }
    }

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
    const fillOverride = child.dock === 'fill' && layoutMode === 'absolute'
      ? { x: 0, y: 0, width: TOTAL_SEGMENTS, height: displayHeight - 2 * PADDING }
      : null
    const effectiveOverride = fillOverride || layout.labelOverride
    return renderWidget(child, {
      key: child.id,
      label: effectiveOverride ? { ...child, ...effectiveOverride } : child,
      layoutY: layout.layoutY,
      onDelete: handleDeleteChild,
      onUpdate: layoutMode !== 'absolute' ? handleUpdateChildInLayout : handleUpdateChild,
      onExport: onExport,
      onLinkSync: onLinkSync,
      onDragStateChange: onDragStateChange,
      onMoveWidget: onMoveWidget,
      activeDrag: activeDrag,
      parentPanelId: label.id,
      isEditing: isEditing,
      containerRef: childContainerRef,
    })
  }

  const handleGridCellDrag = (e, srcIdx) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()

    const gridAxis = style.gridAxis || 'columns'
    const gridCount = Math.max(1, style.gridCount || 2)
    const cellGap = style.gap ?? 8

    setGridDragIdx(srcIdx)
    gridDropIdxRef.current = srcIdx
    setGridDropIdx(srcIdx)

    const handleMouseMove = (moveEvent) => {
      const container = childContainerRef.current
      if (!container) return
      const rect = container.getBoundingClientRect()
      const relX = moveEvent.clientX - rect.left
      const relY = moveEvent.clientY - rect.top

      let newIdx
      if (gridAxis === 'columns') {
        const cols = gridCount
        const rows = Math.max(1, Math.ceil(children.length / cols))
        const cellH = Math.max(20, (rect.height - (rows - 1) * cellGap) / rows)
        const col = Math.min(cols - 1, Math.max(0, Math.floor((relX / rect.width) * cols)))
        const row = Math.max(0, Math.floor(relY / (cellH + cellGap)))
        newIdx = Math.min(children.length - 1, row * cols + col)
      } else {
        const rows = gridCount
        const visibleCols = Math.max(1, Math.ceil(children.length / rows))
        const cellH = Math.max(20, (rect.height - (rows - 1) * cellGap) / rows)
        const cellW = Math.max(20, (rect.width - (visibleCols - 1) * cellGap) / visibleCols)
        const col = Math.max(0, Math.floor(relX / (cellW + cellGap)))
        const row = Math.min(rows - 1, Math.max(0, Math.floor((relY / rect.height) * rows)))
        newIdx = Math.min(children.length - 1, col * rows + row)
      }

      if (newIdx !== gridDropIdxRef.current) {
        gridDropIdxRef.current = newIdx
        setGridDropIdx(newIdx)
      }
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)

      const dropIdx = gridDropIdxRef.current
      if (dropIdx !== null && dropIdx !== srcIdx) {
        const reordered = [...children]
        const [moved] = reordered.splice(srcIdx, 1)
        reordered.splice(dropIdx, 0, moved)
        onUpdate(label.id, { children: reordered })
      }

      setGridDragIdx(null)
      setGridDropIdx(null)
      gridDropIdxRef.current = null
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const renderGridChildren = () => {
    const gridAxis = style.gridAxis || 'columns'
    const gridCount = Math.max(1, style.gridCount || 2)
    const contentW = childContainerWidth
    const contentH = childContainerHeight
    const cellGap = gap
    const childProps = {
      onDelete: handleDeleteChild,
      onUpdate: handleUpdateChildInLayout,
      onExport,
      onLinkSync,
      onDragStateChange,
      onMoveWidget,
      activeDrag,
      parentPanelId: label.id,
      isEditing,
      containerRef: childContainerRef,
    }

    const renderCell = (child, i, left, top, width, height) => {
      const isDragging = gridDragIdx === i
      const isDropTarget = gridDropIdx === i && gridDropIdx !== gridDragIdx
      return (
        <Box
          key={child.id}
          position="absolute"
          left={left}
          top={top}
          width={width}
          height={`${height}px`}
          overflow="hidden"
          opacity={isDragging ? 0.4 : 1}
          boxShadow={isDropTarget ? 'inset 0 0 0 2px var(--chakra-colors-blue-400)' : undefined}
          transition="opacity 0.1s"
        >
          {renderWidget(child, {
            key: child.id,
            label: { ...child, x: 0, y: 0, width: TOTAL_SEGMENTS, height },
            layoutY: 0,
            ...childProps,
          })}
          {isEditing && (
            <Box
              position="absolute"
              top={0}
              left={0}
              right={0}
              height="14px"
              zIndex={10}
              cursor="grab"
              display="flex"
              alignItems="center"
              justifyContent="center"
              bg="whiteAlpha.0"
              _hover={{ bg: 'whiteAlpha.200' }}
              onMouseDown={(e) => handleGridCellDrag(e, i)}
            >
              <DragHandleIcon boxSize={2} color="whiteAlpha.500" />
            </Box>
          )}
        </Box>
      )
    }

    if (gridAxis === 'columns') {
      const cols = gridCount
      const rows = Math.max(1, Math.ceil(children.length / cols))
      const cellW = Math.max(20, (contentW - (cols - 1) * cellGap) / cols)
      const cellH = Math.max(20, (contentH - (rows - 1) * cellGap) / rows)
      return children.map((child, i) => {
        const col = i % cols
        const row = Math.floor(i / cols)
        return renderCell(child, i,
          `${col * (cellW + cellGap)}px`,
          `${row * (cellH + cellGap)}px`,
          `${cellW + 8}px`,
          cellH
        )
      })
    } else {
      const rows = gridCount
      const visibleCols = Math.max(1, Math.ceil(children.length / rows))
      const cellH = Math.max(20, (contentH - (rows - 1) * cellGap) / rows)
      const cellW = Math.max(20, (contentW - (visibleCols - 1) * cellGap) / visibleCols)
      return children.map((child, i) => {
        const row = i % rows
        const col = Math.floor(i / rows)
        return renderCell(child, i,
          `${col * (cellW + cellGap)}px`,
          `${row * (Math.round(cellH) + cellGap)}px`,
          `${cellW + 8}px`,
          Math.round(cellH)
        )
      })
    }
  }

  return (
    <>
      {isEditing && (
        <HStack
          position="absolute"
          top={`${displayY - 26}px`}
          left={`calc(${((displayX + displayWidth) / TOTAL_SEGMENTS) * 100}% - 8px)`}
          style={{ transform: 'translateX(-100%)' }}
          spacing={0}
          zIndex={isMoving ? 1101 : 1001}
          bg="gray.700"
          borderRadius="md"
          px={1}
          onMouseDown={(e) => e.stopPropagation()}
        >
          <IconButton
            icon={<AddIcon />}
            size="xs"
            variant="ghost"
            aria-label="Add child widget"
            onClick={(e) => { e.stopPropagation(); onAddOpen() }}
          />
          <Tooltip label={label.dock === 'fill' ? 'Remove fill' : 'Fill parent'} placement="top" hasArrow>
            <IconButton
              icon={<FillIcon />}
              size="xs"
              variant="ghost"
              aria-label="Fill parent"
              color={label.dock === 'fill' ? 'blue.300' : 'gray.400'}
              _hover={{ color: label.dock === 'fill' ? 'blue.200' : 'white', bg: 'whiteAlpha.200' }}
              onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { dock: label.dock === 'fill' ? undefined : 'fill' }) }}
            />
          </Tooltip>
          <Tooltip label="Export panel" placement="top" hasArrow>
            <IconButton
              icon={<DownloadIcon />}
              size="xs"
              variant="ghost"
              aria-label="Export panel"
              onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
            />
          </Tooltip>
          <IconButton
            icon={<SettingsIcon />}
            size="xs"
            variant="ghost"
            aria-label="Edit panel style"
            onClick={(e) => { e.stopPropagation(); handleOpenStyleEdit() }}
          />
          <IconButton
            icon={<CloseIcon />}
            size="xs"
            variant="ghost"
            aria-label="Delete panel"
            onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
          />
        </HStack>
      )}
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

        {/* Children container - relative positioning for absolute children */}
        <Box
          ref={childContainerRef}
          position="relative"
          flex={1}
          overflowX={layoutMode === 'grid' && style.gridAxis === 'rows' ? 'auto' : 'hidden'}
          overflowY={layoutMode === 'grid' && style.gridAxis !== 'rows' ? 'auto' : layoutMode !== 'grid' ? 'auto' : 'hidden'}
          p={layoutMode === 'grid' ? 0 : 2}
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
                  <>
                    <HStack width="100%" spacing={4}>
                      <FormControl>
                        <FormLabel fontSize="sm">Fix axis</FormLabel>
                        <Select
                          value={editStyle.gridAxis || 'columns'}
                          onChange={(e) => setEditStyle({ ...editStyle, gridAxis: e.target.value })}
                        >
                          <option value="columns">Columns</option>
                          <option value="rows">Rows</option>
                        </Select>
                      </FormControl>
                      <FormControl>
                        <FormLabel fontSize="sm">{(editStyle.gridAxis || 'columns') === 'columns' ? 'Columns' : 'Rows'}</FormLabel>
                        <Input
                          type="number" min={1} max={20}
                          value={editStyle.gridCount || 2}
                          onChange={(e) => setEditStyle({ ...editStyle, gridCount: Math.max(1, parseInt(e.target.value) || 2) })}
                        />
                      </FormControl>
                    </HStack>
                  </>
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
                {(editStyle.layoutMode === 'grid' || editStyle.layoutMode === 'flow') && (
                  <FormControl>
                    <FormLabel fontSize="sm">Spacing (px)</FormLabel>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      value={editStyle.gap ?? 8}
                      onChange={(e) => setEditStyle({ ...editStyle, gap: Math.max(0, parseInt(e.target.value) || 0) })}
                    />
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
      { confirmDeleteDialog }
    </>
  )
}

export default TemplatePagePanel
