import { useRef, useState, useEffect, useCallback } from 'react'
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
  Tooltip,
  useDisclosure,
  Badge,
} from '@chakra-ui/react'
import { CloseIcon, SettingsIcon, AddIcon } from '@chakra-ui/icons'
import FillIcon from './FillIcon'
import { useDeleteConfirm } from '../hooks/useDeleteConfirm'
import { useWidgetSizeEdit } from '../hooks/useWidgetSizeEdit'

const DuplicateIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M16 1H4C2.9 1 2 1.9 2 3v14h2V3h12V1zm3 4H8C6.9 5 6 5.9 6 7v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z" />
  </svg>
)

const ShareIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M18 16c-.8 0-1.5.3-2 .8L8.9 12l7.1-4.8c.5.5 1.2.8 2 .8 1.7 0 3-1.3 3-3s-1.3-3-3-3-3 1.3-3 3c0 .2 0 .5.1.7L8 10.4C7.5 9.8 6.8 9.5 6 9.5c-1.7 0-3 1.3-3 3s1.3 3 3 3c.8 0 1.5-.3 2-.8l8.1 4.8c-.1.2-.1.4-.1.5 0 1.7 1.3 3 3 3s3-1.3 3-3-1.3-3-3-3z" />
  </svg>
)

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 4
const MIN_HEIGHT = 100
const DEFAULT_HEIGHT = 300
const DEFAULT_WIDTH = 8

const DEFAULT_STYLE = {
  bgColor: '#1a202c',
  borderColor: '#4A5568',
  borderWidth: 1,
}

const FEED_TYPES = [
  { value: 'yahoo', label: 'Yahoo Finance' },
  { value: 'ibkr',  label: 'IBKR' },
]

function relativeTime(isoStr) {
  const diff = Date.now() - new Date(isoStr).getTime()
  const mins  = Math.floor(diff / 60000)
  const hours = Math.floor(mins / 60)
  const days  = Math.floor(hours / 24)
  if (days  > 0)  return `${days}d ago`
  if (hours > 0)  return `${hours}h ago`
  if (mins  > 0)  return `${mins}m ago`
  return 'just now'
}

async function fetchFeed(feed, maxItems) {
  const { type, symbol } = feed
  const sym = symbol?.trim().toUpperCase()
  const params = new URLSearchParams({ limit: maxItems })
  if (sym) params.set('symbol', sym)
  const res = await fetch(`/api/news/${type}?${params}`)
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw Object.assign(new Error(body.error || res.statusText), { status: res.status })
  }
  return res.json()
}

function mergeAndSort(results, maxItems) {
  const all = results.flat()
  all.sort((a, b) => new Date(b.publishedAt) - new Date(a.publishedAt))
  return all.slice(0, maxItems)
}

function NewsItem({ item }) {
  const handleClick = () => {
    if (item.url) window.open(item.url, '_blank', 'noopener')
  }
  return (
    <Box
      px={3}
      py="6px"
      borderBottomWidth="1px"
      borderColor="whiteAlpha.100"
      _hover={{ bg: 'whiteAlpha.100' }}
      cursor={item.url ? 'pointer' : 'default'}
      onClick={handleClick}
      onMouseDown={e => e.stopPropagation()}
    >
      <Text fontSize="xs" color="gray.100" lineHeight="1.4" noOfLines={2}>
        {item.title}
      </Text>
      <HStack spacing={2} mt="3px">
        <Text fontSize="10px" color="gray.500" flexShrink={0}>
          {relativeTime(item.publishedAt)}
        </Text>
        <Text fontSize="10px" color="gray.600" noOfLines={1}>
          {item.source}
        </Text>
      </HStack>
    </Box>
  )
}

function FeedRow({ feed, onChange, onRemove }) {
  return (
    <HStack spacing={2} align="center">
      <Select
        size="sm"
        value={feed.type}
        onChange={e => onChange({ ...feed, type: e.target.value })}
        width="140px"
        flexShrink={0}
      >
        {FEED_TYPES.map(ft => (
          <option key={ft.value} value={ft.value}>{ft.label}</option>
        ))}
      </Select>
      <Input
        size="sm"
        value={feed.symbol || ''}
        onChange={e => onChange({ ...feed, symbol: e.target.value.toUpperCase() })}
        placeholder={feed.type === 'ibkr' ? 'Symbol (required)' : 'Symbol (optional)'}
        fontFamily="mono"
      />
      <IconButton
        icon={<CloseIcon />}
        size="xs"
        variant="ghost"
        aria-label="Remove feed"
        flexShrink={0}
        onClick={onRemove}
      />
    </HStack>
  )
}

export default function TemplatePageNews({
  label, layoutY, onDelete, onDuplicate, onUpdate, onShare,
  onDragPreview, onDragStateChange, onMoveWidget,
  activeDrag, parentPanelId, isEditing, containerRef,
  openSettingsForId, onSettingsOpened, galleryMode
}) {
  const [items, setItems]         = useState([])
  const [loading, setLoading]     = useState(true)
  const [feedErrors, setFeedErrors] = useState({})
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving]   = useState(false)
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

  const width           = label.width || DEFAULT_WIDTH
  const height          = label.height || DEFAULT_HEIGHT
  const x               = label.x || 0
  const feeds           = label.feeds || []
  const maxItems        = label.maxItems || 15
  const refreshInterval = (label.refreshInterval || 300) * 1000
  const style           = { ...DEFAULT_STYLE, ...label.style }

  const displayWidth  = previewSize?.width  ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX      = previewPos?.x       ?? x
  const displayY      = previewPos?.y       ?? layoutY ?? 0

  const loadNews = useCallback(async () => {
    if (!feeds.length) { setItems([]); setLoading(false); return }
    setLoading(prev => items.length === 0 ? true : prev)
    const errors = {}
    const results = await Promise.all(
      feeds.map(async (feed, i) => {
        try {
          return await fetchFeed(feed, maxItems)
        } catch (err) {
          errors[i] = err.status === 503 ? 'IBKR offline' : (err.message || 'Error')
          return []
        }
      })
    )
    setFeedErrors(errors)
    setItems(mergeAndSort(results, maxItems))
    setLoading(false)
  }, [feeds, maxItems])

  useEffect(() => {
    loadNews()
    const id = setInterval(loadNews, refreshInterval)
    return () => clearInterval(id)
  }, [loadNews, refreshInterval])

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
      feeds: feeds.map(f => ({ ...f })),
      maxItems: label.maxItems || 15,
      refreshInterval: label.refreshInterval || 300,
      title: label.title || '',
    })
    initSize()
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      feeds: editConfig.feeds,
      maxItems: editConfig.maxItems,
      refreshInterval: editConfig.refreshInterval,
      title: editConfig.title,
      ...sizeUpdates,
    })
    onClose()
  }

  const addFeed = () =>
    setEditConfig({ ...editConfig, feeds: [...editConfig.feeds, { id: crypto.randomUUID(), type: 'yahoo', symbol: '' }] })

  const updateFeed = (id, updated) =>
    setEditConfig({ ...editConfig, feeds: editConfig.feeds.map(f => f.id === id ? { ...updated, id } : f) })

  const removeFeed = (id) =>
    setEditConfig({ ...editConfig, feeds: editConfig.feeds.filter(f => f.id !== id) })

  const ibkrOfflineCount = Object.values(feedErrors).filter(e => e === 'IBKR offline').length

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
        {/* Header bar */}
        {(label.title || ibkrOfflineCount > 0) && (
          <Box
            position="absolute" top={0} left={0} right={0}
            textAlign="center" pointerEvents="none" zIndex={5} pt="2px"
          >
            <HStack justify="center" spacing={2}>
              {label.title && (
                <Text fontSize="xs" color="gray.400" noOfLines={1} px={6}>{label.title}</Text>
              )}
              {ibkrOfflineCount > 0 && (
                <Badge colorScheme="orange" fontSize="9px">IBKR offline</Badge>
              )}
            </HStack>
          </Box>
        )}

        {/* News list */}
        <Box
          position="absolute"
          top={(label.title || ibkrOfflineCount > 0) ? '18px' : '0'}
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
          {loading && items.length === 0 ? (
            <Box py={6} textAlign="center">
              <Text fontSize="xs" color="gray.500">Loading...</Text>
            </Box>
          ) : feeds.length === 0 ? (
            <Box py={6} textAlign="center">
              <Text fontSize="xs" color="gray.500">No feeds configured</Text>
            </Box>
          ) : items.length === 0 ? (
            <Box py={6} textAlign="center">
              <Text fontSize="xs" color="gray.500">No news available</Text>
            </Box>
          ) : (
            items.map((item, i) => <NewsItem key={`${item.publishedAt}-${i}`} item={item} />)
          )}
        </Box>

        {/* Edit controls */}
        <HStack position="absolute" top="2px" right="2px" spacing={0} zIndex={10}>
          {isEditing && !galleryMode && (
            <Tooltip label="Share to gallery" placement="top" hasArrow>
              <IconButton icon={<ShareIcon />} size="xs" variant="ghost" aria-label="Share to gallery"
                onClick={(e) => { e.stopPropagation(); onShare?.(label) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </Tooltip>
          )}
          <IconButton icon={<SettingsIcon />} size="xs" variant="ghost" aria-label="Edit news"
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
                <Tooltip label="Duplicate news" placement="top" hasArrow>
                  <IconButton icon={<DuplicateIcon />} size="xs" variant="ghost" aria-label="Duplicate news"
                    onClick={(e) => { e.stopPropagation(); onDuplicate?.(label.id) }}
                    onMouseDown={(e) => e.stopPropagation()}
                  />
                </Tooltip>
              )}
              {!galleryMode && (
                <IconButton icon={<CloseIcon />} size="xs" variant="ghost" aria-label="Delete news"
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
          <ModalHeader>Edit News</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Title</FormLabel>
                  <Input size="sm" value={editConfig.title}
                    onChange={e => setEditConfig({ ...editConfig, title: e.target.value })}
                    placeholder="News"
                  />
                </FormControl>

                <Box borderTopWidth={1} borderColor="gray.600" pt={3}>
                  <HStack justify="space-between" mb={2}>
                    <Text fontSize="sm" fontWeight="medium">Feeds</Text>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={addFeed}>Add Feed</Button>
                  </HStack>
                  <VStack spacing={2} align="stretch">
                    {editConfig.feeds.length === 0 && (
                      <Text fontSize="xs" color="gray.500">No feeds — add one above.</Text>
                    )}
                    {editConfig.feeds.map(feed => (
                      <FeedRow
                        key={feed.id}
                        feed={feed}
                        onChange={updated => updateFeed(feed.id, updated)}
                        onRemove={() => removeFeed(feed.id)}
                      />
                    ))}
                  </VStack>
                </Box>

                <HStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Max items</FormLabel>
                    <Input size="sm" type="number" min={5} max={50}
                      value={editConfig.maxItems}
                      onChange={e => setEditConfig({ ...editConfig, maxItems: Math.min(50, Math.max(5, parseInt(e.target.value) || 15)) })}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel fontSize="sm">Refresh (seconds)</FormLabel>
                    <Input size="sm" type="number" min={30}
                      value={editConfig.refreshInterval}
                      onChange={e => setEditConfig({ ...editConfig, refreshInterval: Math.max(30, parseInt(e.target.value) || 300) })}
                    />
                  </FormControl>
                </HStack>

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
