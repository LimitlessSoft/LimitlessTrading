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

function TemplatePageLabel({ label, onDelete, onResize, onUpdate, isEditing }) {
  const [isDragging, setIsDragging] = useState(false)
  const [previewWidth, setPreviewWidth] = useState(null)
  const [previewHeight, setPreviewHeight] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const width = label.width || 4
  const height = label.height || DEFAULT_HEIGHT
  const style = { ...DEFAULT_STYLE, ...label.style }
  const displayWidth = previewWidth !== null ? previewWidth : width
  const displayHeight = previewHeight !== null ? previewHeight : height

  // Clear preview once the label dimensions catch up
  useEffect(() => {
    if (previewWidth !== null && label.width === previewWidth) {
      setPreviewWidth(null)
    }
    if (previewHeight !== null && label.height === previewHeight) {
      setPreviewHeight(null)
    }
  }, [label.width, label.height, previewWidth, previewHeight])

  const handleOpenEdit = () => {
    setEditStyle({ ...DEFAULT_STYLE, ...label.style })
    onOpen()
  }

  const handleSaveStyle = () => {
    onUpdate(label.id, { style: editStyle })
    onClose()
  }

  const handleHorizontalResize = (e) => {
    if (!isEditing || !boxRef.current) return
    e.preventDefault()

    const container = boxRef.current.parentElement
    if (!container) return
    const containerWidth = container.getBoundingClientRect().width
    if (!containerWidth) return

    const segmentSize = containerWidth / TOTAL_SEGMENTS
    const startX = e.clientX
    const startWidth = width
    let currentWidth = width

    setIsDragging(true)

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startX
      const deltaSegments = Math.round(deltaX / segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS, startWidth + deltaSegments))
      currentWidth = newWidth
      setPreviewWidth(newWidth)
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsDragging(false)
      if (currentWidth !== width) {
        onResize(label.id, { width: currentWidth })
      } else {
        setPreviewWidth(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleVerticalResize = (e) => {
    if (!isEditing || !boxRef.current) return
    e.preventDefault()

    const startY = e.clientY
    const startHeight = height
    let currentHeight = height

    setIsDragging(true)

    const handleMouseMove = (moveEvent) => {
      const deltaY = moveEvent.clientY - startY
      const newHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
      currentHeight = newHeight
      setPreviewHeight(newHeight)
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsDragging(false)
      if (currentHeight !== height) {
        onResize(label.id, { height: currentHeight })
      } else {
        setPreviewHeight(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleCornerResize = (e) => {
    if (!isEditing || !boxRef.current) return
    e.preventDefault()

    const container = boxRef.current.parentElement
    if (!container) return
    const containerWidth = container.getBoundingClientRect().width
    if (!containerWidth) return

    const segmentSize = containerWidth / TOTAL_SEGMENTS
    const startX = e.clientX
    const startY = e.clientY
    const startWidth = width
    const startHeight = height
    let currentWidth = width
    let currentHeight = height

    setIsDragging(true)

    const handleMouseMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startX
      const deltaY = moveEvent.clientY - startY
      const deltaSegments = Math.round(deltaX / segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS, startWidth + deltaSegments))
      const newHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
      currentWidth = newWidth
      currentHeight = newHeight
      setPreviewWidth(newWidth)
      setPreviewHeight(newHeight)
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsDragging(false)
      const changes = {}
      if (currentWidth !== width) changes.width = currentWidth
      if (currentHeight !== height) changes.height = currentHeight
      if (Object.keys(changes).length > 0) {
        onResize(label.id, changes)
      } else {
        setPreviewWidth(null)
        setPreviewHeight(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  return (
    <>
      <Box
        ref={boxRef}
        bg={style.bgColor}
        px={4}
        py={2}
        borderRadius="md"
        display="flex"
        alignItems="flex-start"
        justifyContent="space-between"
        position="relative"
        width={`${(displayWidth / TOTAL_SEGMENTS) * 100}%`}
        height={`${displayHeight}px`}
        overflow="auto"
        transition={isDragging ? 'none' : 'width 0.15s ease, height 0.15s ease'}
        borderWidth={isEditing ? '1px' : `${style.borderWidth}px`}
        borderColor={isEditing ? (isDragging ? 'blue.400' : 'gray.600') : style.borderColor}
        borderStyle={isEditing ? 'dashed' : 'solid'}
        flexShrink={0}
      >
        <Text
          flex={1}
          whiteSpace="pre-wrap"
          color={style.textColor}
          fontWeight={style.fontWeight}
          fontStyle={style.fontStyle}
          fontSize={`${style.fontSize}rem`}
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
              onClick={handleOpenEdit}
              ml={2}
              flexShrink={0}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete label"
              onClick={() => onDelete(label.id)}
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
