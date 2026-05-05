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
  HStack,
  VStack,
  Tooltip,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'
import FillIcon from './FillIcon'
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { useWidgetSizeEdit } from '../hooks/useWidgetSizeEdit'
import { useWidgetResolveText } from '../hooks/useWidgetResolveText'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)

const ShareIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M18 16c-.8 0-1.5.3-2 .8L8.9 12l7.1-4.8c.5.5 1.2.8 2 .8 1.7 0 3-1.3 3-3s-1.3-3-3-3-3 1.3-3 3c0 .2 0 .5.1.7L8 10.4C7.5 9.8 6.8 9.5 6 9.5c-1.7 0-3 1.3-3 3s1.3 3 3 3c.8 0 1.5-.3 2-.8l8.1 4.8c-.1.2-.1.4-.1.5 0 1.7 1.3 3 3 3s3-1.3 3-3-1.3-3-3-3z" />
  </svg>
)

const DuplicateIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M16 1H4C2.9 1 2 1.9 2 3v14h2V3h12V1zm3 4H8C6.9 5 6 5.9 6 7v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z" />
  </svg>
)

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 4
const MIN_HEIGHT = 80
const DEFAULT_HEIGHT = 200
const DEFAULT_WIDTH = 10

const DEFAULT_COLUMNS = [
  { id: 'col-key', name: 'Field', expression: '{key}' },
  { id: 'col-val', name: 'Value', expression: '{value}' },
]

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1,
  headerBgColor: '#2D3748',
  textColor: '#E2E8F0',
  headerTextColor: '#A0AEC0',
  rowAltBgColor: '#1E2633',
}

function evaluateExpression(expr, row) {
  if (!expr) return ''
  return expr.replace(/\{([^}]+)\}/g, (_, inner) => {
    const fields = inner.split('??').map(f => f.trim())
    for (const field of fields) {
      const val = row[field]
      if (val != null && val !== '' && val !== '--') return String(val)
    }
    return '--'
  })
}

function parseRows(resolved) {
  try {
    const data = JSON.parse(resolved)
    if (Array.isArray(data)) return data
    if (data && typeof data === 'object') {
      return Object.entries(data).map(([key, val]) =>
        val && typeof val === 'object' ? { key, ...val } : { key, value: val }
      )
    }
  } catch {}
  return null
}

function TemplatePageTable({
  label, layoutY, onDelete, onDuplicate, onUpdate, onExport, onShare,
  onDragPreview, onDragStateChange, onMoveWidget,
  activeDrag, parentPanelId, isEditing, containerRef,
  openSettingsForId, onSettingsOpened, galleryMode
}) {
  const [rows, setRows] = useState([])
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { requestDelete, confirmDeleteDialog } = useDeleteConfirm()
  const { initSize, sizeUpdates, sizeFields } = useWidgetSizeEdit(label, {
    defaultWidth: DEFAULT_WIDTH,
    defaultHeight: DEFAULT_HEIGHT,
    minHeight: MIN_HEIGHT,
  })

  const width = label.width || DEFAULT_WIDTH
  const height = label.height || DEFAULT_HEIGHT
  const x = label.x || 0
  const source = label.source || ''
  const columns = label.columns || DEFAULT_COLUMNS
  const style = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  const { resolveText } = useWidgetResolveText(source)

  useEffect(() => {
    const parsed = parseRows(resolveText(source))
    if (parsed) setRows(parsed)
  }, [resolveText, source])

  // Clear preview once label prop catches up
  useEffect(() => {
    if (previewPos && !isMoving && label.x === previewPos.x) setPreviewPos(null)
  }, [label.x, previewPos, isMoving])

  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  useEffect(() => {
    if (openSettingsForId === label.id) {
      handleOpenEdit()
      onSettingsOpened?.()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openSettingsForId])

  const getContainerInfo = () => {
    const container = containerRef?.current || boxRef.current?.parentElement
    if (!container) return null
    const rect = container.getBoundingClientRect()
    return { width: rect.width, segmentSize: rect.width / TOTAL_SEGMENTS, rect }
  }

  const findDropTarget = (mx, my) => {
    const header = document.querySelector('[data-header-dropzone]')
    if (header) {
      const r = header.getBoundingClientRect()
      if (mx >= r.left && mx <= r.right && my >= r.top && my <= r.bottom) return 'header'
    }
    const panels = document.querySelectorAll('[data-panel-id]')
    for (const panel of panels) {
      const pid = panel.getAttribute('data-panel-id')
      if (pid === label.id) continue
      const r = panel.getBoundingClientRect()
      if (mx >= r.left && mx <= r.right && my >= r.top && my <= r.bottom) return pid
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
    let currentY = label.y || 0
    let lastMX = e.clientX
    let lastMY = e.clientY

    setIsMoving(true)
    onDragStateChange?.(true, { x: e.clientX, y: e.clientY })

    const onMove = mv => {
      const dSeg = Math.round((mv.clientX - startMouseX) / info.segmentSize)
      const newX = Math.max(0, Math.min(TOTAL_SEGMENTS - displayWidth, startX + dSeg))
      const newVisualY = Math.max(0, startVisualY + (mv.clientY - startMouseY))
      currentX = newX
      currentY = newVisualY
      lastMX = mv.clientX
      lastMY = mv.clientY
      setPreviewPos({ x: newX, y: newVisualY })
      onDragPreview?.({ x: newX, y: newVisualY })
      onDragStateChange?.(true, { x: mv.clientX, y: mv.clientY })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsMoving(false)
      onDragStateChange?.(false, null)

      const dropTarget = findDropTarget(lastMX, lastMY)
      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveWidget?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
        const main = containerRef?.current
        if (main) {
          const r = main.getBoundingClientRect()
          if (lastMX >= r.left && lastMX <= r.right && lastMY >= r.top && lastMY <= r.bottom) {
            onMoveWidget?.(label.id, parentPanelId, null)
            setPreviewPos(null)
            onDragPreview?.(null)
            return
          }
        }
        if (currentX !== x || currentY !== (label.y || 0)) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
      } else if (currentX !== x || currentY !== (label.y || 0)) {
        onUpdate(label.id, { x: currentX, y: currentY })
      } else {
        setPreviewPos(null)
        onDragPreview?.(null)
      }
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleResize = (e) => {
    if (!isEditing) return
    e.preventDefault()
    e.stopPropagation()
    const info = getContainerInfo()
    if (!info) return

    const startMX = e.clientX
    const startMY = e.clientY
    const startW = width
    const startH = height
    let curW = width
    let curH = height

    setIsResizing(true)

    const onMove = mv => {
      const dSeg = Math.round((mv.clientX - startMX) / info.segmentSize)
      curW = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startW + dSeg))
      curH = Math.max(MIN_HEIGHT, startH + (mv.clientY - startMY))
      setPreviewSize({ width: curW, height: curH })
    }

    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      setIsResizing(false)
      const changes = {}
      if (curW !== width) changes.width = curW
      if (curH !== height) changes.height = curH
      if (Object.keys(changes).length > 0) onUpdate(label.id, changes)
      setPreviewSize(null)
    }

    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }

  const handleOpenEdit = () => {
    setEditConfig({
      source,
      columns: columns.map(c => ({ ...c })),
      title: label.title || '',
      style: { ...DEFAULT_STYLE, ...label.style },
    })
    initSize()
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      source: editConfig.source.trim(),
      columns: editConfig.columns,
      title: editConfig.title,
      style: editConfig.style,
      ...sizeUpdates,
    })
    onClose()
  }

  const handleAddColumn = () => {
    setEditConfig({
      ...editConfig,
      columns: [...editConfig.columns, { id: crypto.randomUUID(), name: '', expression: '' }],
    })
  }

  const updateColumn = (id, changes) =>
    setEditConfig({ ...editConfig, columns: editConfig.columns.map(c => c.id === id ? { ...c, ...changes } : c) })

  const deleteColumn = (id) =>
    setEditConfig({ ...editConfig, columns: editConfig.columns.filter(c => c.id !== id) })

  const ROW_H = 24
  const HEADER_H = 28

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
        borderRadius="md"
        overflow="hidden"
        transition={isResizing || isMoving ? 'none' : 'all 0.15s ease'}
        borderWidth={`${style.borderWidth}px`}
        borderColor={style.borderColor}
        borderStyle="solid"
        outline={isEditing ? '2px dashed' : 'none'}
        outlineColor={isEditing ? (isResizing || isMoving ? 'blue.400' : 'gray.600') : undefined}
        outlineOffset="-2px"
        opacity={isMoving ? 0.7 : 1}
        cursor={isEditing && !galleryMode ? 'move' : 'default'}
        onMouseDown={isEditing && !galleryMode ? handleMoveStart : undefined}
        zIndex={isMoving ? 100 : 1}
      >
        {/* Title bar */}
        {label.title && (
          <Box
            position="absolute" top={0} left={0} right={0}
            textAlign="center" pointerEvents="none" zIndex={5} pt="2px"
          >
            <Text fontSize="xs" color="gray.400" noOfLines={1} px={6}>{label.title}</Text>
          </Box>
        )}

        {/* Table */}
        <Box
          position="absolute"
          top={label.title ? '18px' : '0'}
          left={0} right={0} bottom={0}
          overflowY="auto"
          overflowX="hidden"
          onMouseDown={e => e.stopPropagation()}
          sx={{
            '&::-webkit-scrollbar': { width: '4px' },
            '&::-webkit-scrollbar-track': { bg: 'transparent' },
            '&::-webkit-scrollbar-thumb': { bg: 'whiteAlpha.300', borderRadius: '2px' },
          }}
        >
          <Box as="table" width="100%" style={{ tableLayout: 'fixed', borderCollapse: 'collapse' }}>
            <Box as="thead">
              <Box as="tr">
                {columns.map((col) => (
                  <Box
                    key={col.id}
                    as="th"
                    px={2}
                    py="4px"
                    textAlign="left"
                    fontSize="10px"
                    fontWeight="semibold"
                    color={style.headerTextColor}
                    bg={style.headerBgColor}
                    borderBottomWidth="1px"
                    borderColor={style.borderColor}
                    height={`${HEADER_H}px`}
                    whiteSpace="nowrap"
                    overflow="hidden"
                    style={{ textOverflow: 'ellipsis' }}
                  >
                    {col.name || col.expression || '—'}
                  </Box>
                ))}
              </Box>
            </Box>
            <Box as="tbody">
              {rows.length === 0 ? (
                <Box as="tr">
                  <Box
                    as="td"
                    colSpan={columns.length}
                    textAlign="center"
                    py={4}
                    fontSize="xs"
                    color="gray.500"
                  >
                    No positions
                  </Box>
                </Box>
              ) : (
                rows.map((row, idx) => (
                  <Box
                    as="tr"
                    key={row.symbol}
                    bg={idx % 2 === 1 ? style.rowAltBgColor : 'transparent'}
                    _hover={{ bg: 'whiteAlpha.100' }}
                  >
                    {columns.map((col) => (
                      <Box
                        key={col.id}
                        as="td"
                        px={2}
                        height={`${ROW_H}px`}
                        fontSize="xs"
                        color={style.textColor}
                        whiteSpace="nowrap"
                        overflow="hidden"
                        style={{ textOverflow: 'ellipsis' }}
                      >
                        {evaluateExpression(col.expression, row)}
                      </Box>
                    ))}
                  </Box>
                ))
              )}
            </Box>
          </Box>
        </Box>

        {/* Edit controls */}
        <HStack position="absolute" top="2px" right="2px" spacing={0} zIndex={10}>
          {isEditing && (
            <>
              {!galleryMode && (
                <Tooltip label="Export table" placement="top" hasArrow>
                  <IconButton icon={<DownloadIcon />} size="xs" variant="ghost" aria-label="Export table"
                    onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                    onMouseDown={(e) => e.stopPropagation()}
                  />
                </Tooltip>
              )}
              {!galleryMode && (
                <Tooltip label="Share to gallery" placement="top" hasArrow>
                  <IconButton icon={<ShareIcon />} size="xs" variant="ghost" aria-label="Share to gallery"
                    onClick={(e) => { e.stopPropagation(); onShare?.(label) }}
                    onMouseDown={(e) => e.stopPropagation()}
                  />
                </Tooltip>
              )}
            </>
          )}
          <IconButton icon={<SettingsIcon />} size="xs" variant="ghost" aria-label="Edit table"
            color="gray.400" _hover={{ color: 'white', bg: 'whiteAlpha.200' }}
            onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
            onMouseDown={(e) => e.stopPropagation()}
          />
          {isEditing && (
            <>
              <Tooltip label={label.dock === 'fill' ? 'Remove fill' : 'Fill parent'} placement="top" hasArrow>
                <IconButton icon={<FillIcon />} size="xs" variant="ghost" aria-label="Fill parent"
                  color={label.dock === 'fill' ? 'blue.300' : 'gray.400'}
                  _hover={{ color: label.dock === 'fill' ? 'blue.200' : 'white', bg: 'whiteAlpha.200' }}
                  onClick={(e) => { e.stopPropagation(); onUpdate(label.id, { dock: label.dock === 'fill' ? undefined : 'fill' }) }}
                  onMouseDown={(e) => e.stopPropagation()}
                />
              </Tooltip>
              {!galleryMode && (
                <Tooltip label="Duplicate table" placement="top" hasArrow>
                  <IconButton icon={<DuplicateIcon />} size="xs" variant="ghost" aria-label="Duplicate table"
                    onClick={(e) => { e.stopPropagation(); onDuplicate?.(label.id) }}
                    onMouseDown={(e) => e.stopPropagation()}
                  />
                </Tooltip>
              )}
              {!galleryMode && (
                <IconButton icon={<CloseIcon />} size="xs" variant="ghost" aria-label="Delete table"
                  onClick={(e) => { e.stopPropagation(); requestDelete(label.id, onDelete) }}
                  onMouseDown={(e) => e.stopPropagation()}
                />
              )}
            </>
          )}
        </HStack>

        {isEditing && !galleryMode && (
          <Box position="absolute" right={0} bottom={0} width="12px" height="12px"
            cursor="nwse-resize" bg="whiteAlpha.200" _hover={{ bg: 'whiteAlpha.400' }}
            onMouseDown={handleResize}
          />
        )}
      </Box>

      {/* Settings modal */}
      <Modal isOpen={isOpen} onClose={onClose} size="lg">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Table</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Title</FormLabel>
                  <Input size="sm" value={editConfig.title}
                    onChange={e => setEditConfig({ ...editConfig, title: e.target.value })}
                    placeholder="Positions"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Source expression</FormLabel>
                  <Input size="sm" value={editConfig.source}
                    onChange={e => setEditConfig({ ...editConfig, source: e.target.value })}
                    placeholder="Expression that resolves to an array or object"
                    fontFamily="mono"
                  />
                </FormControl>

                <Box borderTopWidth={1} borderColor="gray.600" pt={3}>
                  <HStack justify="space-between" mb={2}>
                    <Text fontSize="sm" fontWeight="medium">Columns</Text>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddColumn}>Add Column</Button>
                  </HStack>

                  {editConfig.columns.length === 0 && (
                    <Text fontSize="sm" color="gray.500" textAlign="center" py={4}>
                      No columns — click Add Column to begin.
                    </Text>
                  )}

                  {editConfig.columns.map((col, idx) => (
                    <Box key={col.id} p={3} mb={2} borderWidth={1} borderColor="gray.600" borderRadius="md">
                      <HStack justify="space-between" mb={2}>
                        <Text fontSize="xs" color="gray.400">Column {idx + 1}</Text>
                        <IconButton
                          icon={<DeleteIcon />} size="xs" variant="ghost" colorScheme="red"
                          aria-label="Remove column" onClick={() => deleteColumn(col.id)}
                        />
                      </HStack>
                      <HStack spacing={2}>
                        <FormControl maxW="140px">
                          <FormLabel fontSize="xs">Name</FormLabel>
                          <Input size="sm" value={col.name}
                            onChange={e => updateColumn(col.id, { name: e.target.value })}
                            placeholder="Symbol"
                          />
                        </FormControl>
                        <FormControl>
                          <FormLabel fontSize="xs">Expression</FormLabel>
                          <Input size="sm" value={col.expression}
                            onChange={e => updateColumn(col.id, { expression: e.target.value })}
                            placeholder="{symbol}"
                            fontFamily="mono"
                          />
                        </FormControl>
                      </HStack>
                    </Box>
                  ))}

                </Box>

                <Box borderTopWidth={1} borderColor="gray.600" pt={3}>
                  <Text fontSize="sm" fontWeight="medium" mb={2}>Style</Text>
                  <HStack flexWrap="wrap" spacing={3}>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Background</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.bgColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, bgColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Header bg</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.headerBgColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, headerBgColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Row alt bg</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.rowAltBgColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, rowAltBgColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Text</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.textColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, textColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Header text</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.headerTextColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, headerTextColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="80px">
                      <FormLabel fontSize="xs">Border</FormLabel>
                      <Input type="color" size="sm" value={editConfig.style.borderColor}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderColor: e.target.value } })}
                        height="32px" p={1}
                      />
                    </FormControl>
                    <FormControl maxW="72px">
                      <FormLabel fontSize="xs">Border px</FormLabel>
                      <Input size="sm" type="number" min={0} max={5}
                        value={editConfig.style.borderWidth}
                        onChange={e => setEditConfig({ ...editConfig, style: { ...editConfig.style, borderWidth: parseInt(e.target.value) || 0 } })}
                      />
                    </FormControl>
                  </HStack>
                </Box>

                {sizeFields}
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
            <Button colorScheme="blue" onClick={handleSaveConfig}>Save</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      {confirmDeleteDialog}
    </>
  )
}

export default TemplatePageTable
