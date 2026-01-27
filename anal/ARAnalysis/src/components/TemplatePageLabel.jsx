import { useRef, useState, useEffect } from 'react'
import { Box, Text, IconButton } from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon } from '@chakra-ui/icons'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2

function TemplatePageLabel({ label, onDelete, onResize, isEditing }) {
  const [isDragging, setIsDragging] = useState(false)
  const [previewWidth, setPreviewWidth] = useState(null)
  const boxRef = useRef(null)

  const width = label.width || 4
  const displayWidth = previewWidth !== null ? previewWidth : width

  // Clear preview once the label width catches up
  useEffect(() => {
    if (previewWidth !== null && label.width === previewWidth) {
      setPreviewWidth(null)
    }
  }, [label.width, previewWidth])

  const handleMouseDown = (e) => {
    if (!isEditing || !boxRef.current) return
    e.preventDefault()

    // Get container width from parent at drag start
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
        onResize(label.id, currentWidth)
      } else {
        setPreviewWidth(null)
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
      alignItems="center"
      justifyContent="space-between"
      position="relative"
      width={`${(displayWidth / TOTAL_SEGMENTS) * 100}%`}
      minH="40px"
      overflow="hidden"
      transition={isDragging ? 'none' : 'width 0.15s ease'}
      borderWidth={isEditing ? '1px' : '0'}
      borderColor={isDragging ? 'blue.400' : 'gray.600'}
      borderStyle="dashed"
      _hover={{ bg: 'gray.600' }}
      flexShrink={0}
    >
      <Text noOfLines={2} flex={1}>{label.text}</Text>
      {isEditing && (
        <>
          <IconButton
            icon={<CloseIcon />}
            size="xs"
            variant="ghost"
            aria-label="Delete label"
            onClick={() => onDelete(label.id)}
            ml={2}
          />
          <Box
            position="absolute"
            right={0}
            top={0}
            bottom={0}
            width="12px"
            cursor="ew-resize"
            display="flex"
            alignItems="center"
            justifyContent="center"
            bg="whiteAlpha.100"
            _hover={{ bg: 'whiteAlpha.300' }}
            onMouseDown={handleMouseDown}
          >
            <DragHandleIcon boxSize={3} transform="rotate(90deg)" />
          </Box>
        </>
      )}
    </Box>
  )
}

export default TemplatePageLabel
