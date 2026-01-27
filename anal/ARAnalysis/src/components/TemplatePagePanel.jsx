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
  Flex,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, AddIcon } from '@chakra-ui/icons'
import TemplatePageLabel from './TemplatePageLabel'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 2
const MIN_HEIGHT = 58
const DEFAULT_HEIGHT = 120

const DEFAULT_STYLE = {
  bgColor: '#2D3748',
  borderColor: '#4A5568',
  borderWidth: 1
}

function TemplatePagePanel({ label, onDelete, onResize, onUpdate, isEditing }) {
  const [isDragging, setIsDragging] = useState(false)
  const [previewWidth, setPreviewWidth] = useState(null)
  const [previewHeight, setPreviewHeight] = useState(null)
  const [editStyle, setEditStyle] = useState(null)
  const [addType, setAddType] = useState('label')
  const [addText, setAddText] = useState('')
  const boxRef = useRef(null)
  const { isOpen: isStyleOpen, onOpen: onStyleOpen, onClose: onStyleClose } = useDisclosure()
  const { isOpen: isAddOpen, onOpen: onAddOpen, onClose: onAddClose } = useDisclosure()

  const width = label.width || 8
  const height = label.height || DEFAULT_HEIGHT
  const style = { ...DEFAULT_STYLE, ...label.style }
  const children = label.children || []
  const displayWidth = previewWidth !== null ? previewWidth : width
  const displayHeight = previewHeight !== null ? previewHeight : height

  useEffect(() => {
    if (previewWidth !== null && label.width === previewWidth) {
      setPreviewWidth(null)
    }
    if (previewHeight !== null && label.height === previewHeight) {
      setPreviewHeight(null)
    }
  }, [label.width, label.height, previewWidth, previewHeight])

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
      text: addType === 'label' ? addText.trim() : '',
      width: addType === 'panel' ? 8 : 4,
      height: addType === 'panel' ? DEFAULT_HEIGHT : 60,
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

  const renderChild = (child) => {
    if (child.type === 'panel') {
      return (
        <TemplatePagePanel
          key={child.id}
          label={child}
          onDelete={handleDeleteChild}
          onResize={handleUpdateChild}
          onUpdate={handleUpdateChild}
          isEditing={isEditing}
        />
      )
    }
    return (
      <TemplatePageLabel
        key={child.id}
        label={child}
        onDelete={handleDeleteChild}
        onResize={handleUpdateChild}
        onUpdate={handleUpdateChild}
        isEditing={isEditing}
      />
    )
  }

  return (
    <>
      <Box
        ref={boxRef}
        bg={style.bgColor}
        p={2}
        borderRadius="md"
        display="flex"
        flexDirection="column"
        position="relative"
        width={`${(displayWidth / TOTAL_SEGMENTS) * 100}%`}
        height={`${displayHeight}px`}
        overflow="hidden"
        transition={isDragging ? 'none' : 'width 0.15s ease, height 0.15s ease'}
        borderWidth={`${style.borderWidth}px`}
        borderColor={isEditing ? (isDragging ? 'blue.400' : 'gray.500') : style.borderColor}
        borderStyle={isEditing ? 'dashed' : 'solid'}
        flexShrink={0}
      >
        {/* Header with controls */}
        {isEditing && (
          <Flex justify="flex-end" mb={1} flexShrink={0}>
            <IconButton
              icon={<AddIcon />}
              size="xs"
              variant="ghost"
              aria-label="Add child control"
              onClick={onAddOpen}
            />
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit panel style"
              onClick={handleOpenStyleEdit}
              ml={1}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete panel"
              onClick={() => onDelete(label.id)}
              ml={1}
            />
          </Flex>
        )}

        {/* Children container */}
        <Flex
          wrap="wrap"
          gap={2}
          flex={1}
          overflow="auto"
          alignContent="flex-start"
        >
          {children.map(renderChild)}
        </Flex>

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
                    <Input
                      type="color"
                      value={editStyle.bgColor}
                      onChange={(e) => setEditStyle({ ...editStyle, bgColor: e.target.value })}
                      height="40px"
                      p={1}
                    />
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
