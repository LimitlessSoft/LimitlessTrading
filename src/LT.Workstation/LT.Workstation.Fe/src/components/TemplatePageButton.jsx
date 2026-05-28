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
  Switch,
  Tooltip,
  useDisclosure,
  useToast,
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon } from '@chakra-ui/icons'
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { useWidgetSizeEdit } from '../hooks/useWidgetSizeEdit'
import { executeActionExpression, describeActionExpression } from '../utils/actionExpressions'
import { getCachedQuote } from '../modules/symbolQuoteService'

const DuplicateIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M16 1H4C2.9 1 2 1.9 2 3v14h2V3h12V1zm3 4H8C6.9 5 6 5.9 6 7v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z" />
  </svg>
)

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 1
const MIN_HEIGHT = 30
const DEFAULT_WIDTH = 3
const DEFAULT_HEIGHT = 50

const DEFAULT_STYLE = {
  bgColor: '#276749',
  textColor: '#FFFFFF',
  borderColor: '#38A169',
  borderWidth: 1,
  fontSize: 0.9,
  fontWeight: 'bold',
}

function resolveQuoteValue(placeholder) {
  const m = /@\{([A-Za-z0-9.^]+):([A-Za-z]\w*(?:-[A-Za-z0-9]+)*)\}/.exec(placeholder)
  if (!m) return null
  const quote = getCachedQuote(m[1])
  if (!quote) return null
  return quote[m[2]] ?? null
}

function TemplatePageButton({
  label, layoutY, onDelete, onDuplicate, onUpdate, onExport, onShare,
  onDragPreview, onDragStateChange, onMoveWidget, activeDrag, parentPanelId,
  isEditing, containerRef, isHeaderMode, openSettingsForId, onSettingsOpened, galleryMode,
}) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const [executing, setExecuting] = useState(false)
  const { initSize, sizeUpdates, sizeFields } = useWidgetSizeEdit(label, { defaultWidth: DEFAULT_WIDTH, defaultHeight: DEFAULT_HEIGHT, minHeight: MIN_HEIGHT })
  const boxRef = useRef(null)
  const { isOpen: isSettingsOpen, onOpen: onSettingsOpen, onClose: onSettingsClose } = useDisclosure()
  const { isOpen: isConfirmOpen, onOpen: onConfirmOpen, onClose: onConfirmClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()
  const toast = useToast()

  const width = label.width || DEFAULT_WIDTH
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const y = label.y || 0
  const buttonText = label.text || 'Button'
  const actionExpression = label.actionExpression || ''
  const confirmBeforeExecute = label.confirmBeforeExecute !== false
  const style = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  useEffect(() => {
    if (previewPos && !isMoving && label.x === previewPos.x) {
      setPreviewPos(null)
      onDragPreview?.(null)
    }
  }, [label.x, previewPos, isMoving, onDragPreview])

  useEffect(() => {
    if (openSettingsForId === label.id) {
      handleOpenSettings()
      onSettingsOpened?.()
    }
  }, [openSettingsForId])

  const getContainerInfo = () => {
    const container = containerRef?.current || boxRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    return { width: rect.width, segmentSize: rect.width / TOTAL_SEGMENTS, rect }
  }

  const findDropTarget = (mouseX, mouseY) => {
    const header = document.querySelector('[data-header-dropzone]')
    if (header) {
      const rect = header.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) return 'header'
    }
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const panelId = panel.getAttribute('data-panel-id')
      if (panelId === label.id) continue
      const rect = panel.getBoundingClientRect()
      if (mouseX >= rect.left && mouseX <= rect.right && mouseY >= rect.top && mouseY <= rect.bottom) return panelId
    }
    return null
  }

  const handleMoveStart = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return
    const startMouseX = e.clientX
    const startMouseY = e.clientY
    const startX = x
    const startVisualY = layoutY ?? 0
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
      const newVisualY = Math.max(0, startVisualY + deltaY)
      currentX = newX
      currentY = newVisualY
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
      const dropTarget = findDropTarget(lastMouseX, lastMouseY)
      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveWidget?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
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
        if (currentX !== x || currentY !== y) onUpdate(label.id, { x: currentX, y: currentY })
        else { setPreviewPos(null); onDragPreview?.(null) }
      } else if (currentX !== x || currentY !== y) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleResize = (e) => {
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
      currentWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      currentHeight = Math.max(MIN_HEIGHT, startHeight + deltaY)
      setPreviewSize({ width: currentWidth, height: currentHeight })
    }

    const handleMouseUp = () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      setIsResizing(false)
      const changes = {}
      if (currentWidth !== width) changes.width = currentWidth
      if (currentHeight !== height) changes.height = currentHeight
      if (Object.keys(changes).length > 0) onUpdate(label.id, changes)
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)
  }

  const handleOpenSettings = () => {
    setEditConfig({
      text: buttonText,
      actionExpression,
      confirmBeforeExecute,
      style: { ...DEFAULT_STYLE, ...label.style },
    })
    initSize()
    onSettingsOpen()
  }

  const handleSaveSettings = () => {
    onUpdate(label.id, {
      text: editConfig.text,
      actionExpression: editConfig.actionExpression,
      confirmBeforeExecute: editConfig.confirmBeforeExecute,
      style: editConfig.style,
      ...sizeUpdates,
    })
    onSettingsClose()
  }

  const fireAction = async () => {
    if (!actionExpression) return
    setExecuting(true)
    try {
      await executeActionExpression(actionExpression, resolveQuoteValue)
      toast({ title: 'Action executed', status: 'success', duration: 2000, isClosable: true })
    } catch (err) {
      toast({ title: err.message || 'Action failed', status: 'error', duration: 4000, isClosable: true })
    } finally {
      setExecuting(false)
    }
  }

  const handleClick = (e) => {
    if (isEditing) {
      e.stopPropagation()
      handleOpenSettings()
      return
    }
    if (!actionExpression) return
    if (confirmBeforeExecute) {
      onConfirmOpen()
    } else {
      fireAction()
    }
  }

  const confirmDescription = actionExpression
    ? describeActionExpression(actionExpression, resolveQuoteValue)
    : ''

  const buttonContent = (
    <Box
      ref={boxRef}
      position={isHeaderMode ? undefined : 'absolute'}
      left={isHeaderMode ? undefined : `${(displayX / TOTAL_SEGMENTS) * 100}%`}
      top={isHeaderMode ? undefined : `${displayY}px`}
      width={isHeaderMode ? undefined : `calc(${(displayWidth / TOTAL_SEGMENTS) * 100}% - 8px)`}
      height={isHeaderMode ? '32px' : `${displayHeight}px`}
      outline={isEditing ? '2px dashed' : 'none'}
      outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : undefined}
      outlineOffset="-2px"
      transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
      opacity={isMoving ? 0.7 : 1}
      cursor={isEditing && !galleryMode ? 'move' : 'pointer'}
      onMouseDown={isEditing && !galleryMode ? handleMoveStart : undefined}
      zIndex={isMoving ? 100 : 1}
      borderRadius="md"
      overflow="hidden"
    >
      {/* Button face */}
      <Box
        as="button"
        display="flex"
        alignItems="center"
        justifyContent="center"
        width="100%"
        height="100%"
        bg={style.bgColor}
        borderWidth={`${style.borderWidth}px`}
        borderColor={style.borderColor}
        borderStyle="solid"
        borderRadius="md"
        color={style.textColor}
        fontSize={`${style.fontSize}rem`}
        fontWeight={style.fontWeight}
        cursor={isEditing ? 'move' : executing ? 'wait' : 'pointer'}
        opacity={executing ? 0.7 : 1}
        _hover={!isEditing ? { filter: 'brightness(1.15)' } : {}}
        _active={!isEditing ? { filter: 'brightness(0.9)' } : {}}
        onClick={handleClick}
        onMouseDown={isEditing && !galleryMode ? (e) => e.stopPropagation() : undefined}
        transition="filter 0.1s"
        px={2}
        pointerEvents={executing ? 'none' : 'auto'}
      >
        <Text noOfLines={2} textAlign="center">
          {buttonText}
        </Text>
      </Box>

      {/* Edit mode controls */}
      {isEditing && (
        <HStack
          position="absolute"
          top={0}
          right={0}
          spacing={0}
          bg="blackAlpha.600"
          borderBottomLeftRadius="md"
          px={1}
          py="1px"
        >
          <IconButton icon={<SettingsIcon />} size="xs" variant="ghost" aria-label="Edit button"
            onClick={(e) => { e.stopPropagation(); handleOpenSettings() }}
            onMouseDown={(e) => e.stopPropagation()} />
          {!galleryMode && (
            <IconButton icon={<DuplicateIcon />} size="xs" variant="ghost" aria-label="Duplicate button"
              onClick={(e) => { e.stopPropagation(); onDuplicate?.(label.id) }}
              onMouseDown={(e) => e.stopPropagation()} />
          )}
          {!galleryMode && (
            <IconButton icon={<CloseIcon />} size="xs" variant="ghost" aria-label="Delete button"
              onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
              onMouseDown={(e) => e.stopPropagation()} />
          )}
        </HStack>
      )}

      {/* Resize handle */}
      {isEditing && !galleryMode && (
        <Box
          position="absolute"
          bottom={0}
          right={0}
          width="14px"
          height="14px"
          cursor="se-resize"
          onMouseDown={handleResize}
          _before={{
            content: '""',
            position: 'absolute',
            bottom: '3px',
            right: '3px',
            width: '6px',
            height: '6px',
            borderRight: '2px solid',
            borderBottom: '2px solid',
            borderColor: 'whiteAlpha.600',
          }}
        />
      )}
    </Box>
  )

  return (
    <>
      {buttonContent}
      {confirmDeleteDialog}

      {/* Confirm action modal */}
      <Modal isOpen={isConfirmOpen} onClose={onConfirmClose} isCentered size="sm">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader fontSize="md">Confirm action</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <Text fontSize="sm" color="gray.300">{confirmDescription}</Text>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onConfirmClose} size="sm">Cancel</Button>
            <Button colorScheme="blue" size="sm" onClick={() => { onConfirmClose(); fireAction() }}>
              Confirm
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      {/* Settings modal */}
      <Modal isOpen={isSettingsOpen} onClose={onSettingsClose} size="md">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Button</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Label</FormLabel>
                  <Input
                    value={editConfig.text}
                    onChange={(e) => setEditConfig({ ...editConfig, text: e.target.value })}
                    placeholder="Button label"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Action expression</FormLabel>
                  <Input
                    value={editConfig.actionExpression}
                    onChange={(e) => setEditConfig({ ...editConfig, actionExpression: e.target.value })}
                    placeholder="![order:BUY:AAPL:100:MKT]"
                    fontFamily="monospace"
                    fontSize="sm"
                  />
                </FormControl>

                <FormControl>
                  <HStack justify="space-between">
                    <FormLabel fontSize="sm" mb={0}>Confirm before execute</FormLabel>
                    <Switch
                      isChecked={editConfig.confirmBeforeExecute}
                      onChange={(e) => setEditConfig({ ...editConfig, confirmBeforeExecute: e.target.checked })}
                    />
                  </HStack>
                </FormControl>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Background</FormLabel>
                    <Input type="color" value={editConfig.style.bgColor}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, bgColor: e.target.value } })}
                      height="40px" p={1} />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Text color</FormLabel>
                    <Input type="color" value={editConfig.style.textColor}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, textColor: e.target.value } })}
                      height="40px" p={1} />
                  </FormControl>
                </HStack>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Border color</FormLabel>
                    <Input type="color" value={editConfig.style.borderColor}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderColor: e.target.value } })}
                      height="40px" p={1} />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Border width</FormLabel>
                    <Input type="number" min={0} max={4} value={editConfig.style.borderWidth}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderWidth: parseInt(e.target.value) || 0 } })} />
                  </FormControl>
                </HStack>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Font size (rem)</FormLabel>
                    <Input type="number" step="0.05" min={0.5} max={3} value={editConfig.style.fontSize}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, fontSize: parseFloat(e.target.value) || 0.9 } })} />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Font weight</FormLabel>
                    <Select value={editConfig.style.fontWeight}
                      onChange={(e) => setEditConfig({ ...editConfig, style: { ...editConfig.style, fontWeight: e.target.value } })}>
                      <option value="normal">Normal</option>
                      <option value="bold">Bold</option>
                      <option value="lighter">Lighter</option>
                    </Select>
                  </FormControl>
                </HStack>

                {sizeFields}
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onSettingsClose}>Cancel</Button>
            <Button colorScheme="blue" onClick={handleSaveSettings}>Save</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageButton
