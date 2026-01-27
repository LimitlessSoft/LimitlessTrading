import { useRef, useState, useEffect } from 'react'
import { Box, Text, IconButton } from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon } from '@chakra-ui/icons'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2
const MIN_HEIGHT = 40
const DEFAULT_HEIGHT = 60

function TemplatePageLabel({ label, onDelete, onResize, isEditing }) {
  const [isDragging, setIsDragging] = useState(false)
  const [previewWidth, setPreviewWidth] = useState(null)
  const [previewHeight, setPreviewHeight] = useState(null)
  const boxRef = useRef(null)

  const width = label.width || 4
  const height = label.height || DEFAULT_HEIGHT
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
    <Box
      ref={boxRef}
      bg="gray.700"
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
      borderWidth={isEditing ? '1px' : '0'}
      borderColor={isDragging ? 'blue.400' : 'gray.600'}
      borderStyle="dashed"
      _hover={{ bg: 'gray.600' }}
      flexShrink={0}
    >
      <Text flex={1} whiteSpace="pre-wrap">{label.text}</Text>
      {isEditing && (
        <>
          <IconButton
            icon={<CloseIcon />}
            size="xs"
            variant="ghost"
            aria-label="Delete label"
            onClick={() => onDelete(label.id)}
            ml={2}
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
  )
}

export default TemplatePageLabel
