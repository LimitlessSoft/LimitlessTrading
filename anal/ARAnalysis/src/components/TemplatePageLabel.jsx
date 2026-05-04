import { useRef, useState, useEffect } from 'react'
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
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { CloseIcon, DragHandleIcon, SettingsIcon, InfoOutlineIcon } from '@chakra-ui/icons'
import FillIcon from './FillIcon'
import { applyConditionalStyles } from '../modules/conditionalStyles'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'
import ConditionalStylesSection from './ConditionalStylesSection'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)
import { useNavigate } from 'react-router-dom'

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

function TemplatePageLabel({ label, layoutY, onDelete, onUpdate, onExport, onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId, isEditing, containerRef, isHeaderMode, openSettingsForId, onSettingsOpened }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [editText, setEditText] = useState('')
  const boxRef = useRef(null)
  const navigate = useNavigate()
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()

  const conditionalPlaceholders = (label.style?.conditionalStyles || []).map(r => r.placeholder)
  const { resolveText } = useWidgetResolveText([label.text, ...conditionalPlaceholders])

  const width = label.width || 4
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0  // Used as order hint for layout calculation
  const baseStyle = { ...DEFAULT_STYLE, ...label.style }
  const style = applyConditionalStyles(baseStyle, label.style?.conditionalStyles, resolveText)

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

  // When layout position changes externally (e.g. a flow reorder resolved), clear stale preview
  useEffect(() => {
    if (previewPos && !isMoving) {
      setPreviewPos(null)
      onDragPreview?.(null)
    }
  }, [layoutY])

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

  useEffect(() => {
    if (openSettingsForId === label.id) {
      handleOpenEdit()
      onSettingsOpened?.()
    }
  }, [openSettingsForId])

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
        onMoveWidget?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
        // Check if dropped outside any panel/header - move to root
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

  const renderEditModal = () => (
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

              <ConditionalStylesSection
                rules={editStyle.conditionalStyles || []}
                onChange={(rules) => setEditStyle({ ...editStyle, conditionalStyles: rules })}
              />
            </VStack>
          )}
        </ModalBody>
        <ModalFooter>
          <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
          <Button colorScheme="blue" onClick={handleSaveStyle}>Save</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  )

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
          outlineColor={isEditing ? (isMoving ? 'blue.400' : 'gray.500') : undefined}
          cursor={isEditing ? (isMoving ? 'grabbing' : 'grab') : 'default'}
          onMouseDown={isEditing ? handleMoveStart : undefined}
          opacity={isMoving ? 0.85 : 1}
          transform={isMoving ? 'scale(1.08)' : 'scale(1)'}
          boxShadow={isMoving ? '0 6px 20px rgba(0,0,0,0.6)' : 'none'}
          transition={isMoving ? 'none' : 'transform 0.15s, box-shadow 0.15s, opacity 0.15s'}
          zIndex={isMoving ? 100 : 'auto'}
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
              <Tooltip label="Export label" placement="top" hasArrow>
                <IconButton
                  icon={<DownloadIcon />}
                  size="xs"
                  variant="ghost"
                  aria-label="Export label"
                  onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                  onMouseDown={(e) => e.stopPropagation()}
                  ml={1}
                />
              </Tooltip>
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
                onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </>
          )}
        </Box>
        {renderEditModal()}
        { confirmDeleteDialog }
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
            <Tooltip label={label.dock === 'fill' ? 'Remove fill' : 'Fill parent'} placement="top" hasArrow>
              <IconButton
                icon={<FillIcon />}
                size="xs"
                variant="ghost"
                aria-label="Fill parent"
                color={label.dock === 'fill' ? 'blue.300' : 'gray.400'}
                _hover={{ color: label.dock === 'fill' ? 'blue.200' : 'white', bg: 'whiteAlpha.200' }}
                onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { dock: label.dock === 'fill' ? undefined : 'fill' }) }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={1}
                flexShrink={0}
              />
            </Tooltip>
            <Tooltip label="Export label" placement="top" hasArrow>
              <IconButton
                icon={<DownloadIcon />}
                size="xs"
                variant="ghost"
                aria-label="Export label"
                onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={2}
                flexShrink={0}
              />
            </Tooltip>
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit label style"
              onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
              onMouseDown={(e) => e.stopPropagation()}
              ml={1}
              flexShrink={0}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete label"
              onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
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

      {renderEditModal()}
      { confirmDeleteDialog }
    </>
  )
}

export default TemplatePageLabel
