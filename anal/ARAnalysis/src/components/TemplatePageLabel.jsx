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
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon } from '@chakra-ui/icons'

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
  fontSize: 1
}

function TemplatePageLabel({ label, layoutY, onDelete, onUpdate, onDragPreview, isEditing, containerRef }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

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
    onOpen()
  }

  const handleSaveStyle = () => {
    onUpdate(label.id, { style: editStyle })
    onClose()
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

    setIsMoving(true)

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
      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsMoving(false)
      // Don't clear previews here - let useEffect clear them when data catches up

      if (currentX !== x || currentY !== y) {
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
        alignItems="flex-start"
        justifyContent="space-between"
        overflow="auto"
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        borderWidth={isEditing ? '2px' : `${style.borderWidth}px`}
        borderColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : style.borderColor}
        borderStyle={isEditing ? 'dashed' : 'solid'}
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
          pointerEvents="none"
          userSelect="none"
        >
          {label.text}
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
          <ModalHeader>Label Style</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editStyle && (
              <VStack spacing={4}>
                <HStack width="100%" spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background Color</FormLabel>
                    <Input
                      type="color"
                      value={editStyle.bgColor}
                      onChange={(e) => setEditStyle({ ...editStyle, bgColor: e.target.value })}
                      height="40px"
                      p={1}
                    />
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
