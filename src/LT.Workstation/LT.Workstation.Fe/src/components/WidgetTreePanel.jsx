import { useState, useCallback } from 'react'
import { Box, Text, IconButton } from '@chakra-ui/react'

const sortByY = (arr) => [...arr].sort((a, b) => (a.y ?? 0) - (b.y ?? 0))
const sortByX = (arr) => [...arr].sort((a, b) => (a.x ?? 0) - (b.x ?? 0))

const CloseIcon = () => (
  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
    <line x1="18" y1="6" x2="6" y2="18" />
    <line x1="6" y1="6" x2="18" y2="18" />
  </svg>
)

const GripIcon = () => (
  <svg viewBox="0 0 10 14" width="11" height="14" fill="currentColor">
    <circle cx="3" cy="3"  r="1.1" />
    <circle cx="7" cy="3"  r="1.1" />
    <circle cx="3" cy="7"  r="1.1" />
    <circle cx="7" cy="7"  r="1.1" />
    <circle cx="3" cy="11" r="1.1" />
    <circle cx="7" cy="11" r="1.1" />
  </svg>
)

const TreeSettingsIcon = () => (
  <svg viewBox="0 0 14 14" width="16" height="16" fill="currentColor">
    <path fillRule="evenodd" d="M5.96 1.09A6 6 0 0 1 8.04 1.09L7.78 2.57A4.5 4.5 0 0 1 10.45 4.11L11.60 3.14A6 6 0 0 1 12.64 4.95L11.23 5.46A4.5 4.5 0 0 1 11.23 8.54L12.64 9.05A6 6 0 0 1 11.60 10.86L10.45 9.89A4.5 4.5 0 0 1 7.78 11.43L8.04 12.91A6 6 0 0 1 5.96 12.91L6.22 11.43A4.5 4.5 0 0 1 3.55 9.89L2.40 10.86A6 6 0 0 1 1.36 9.05L2.77 8.54A4.5 4.5 0 0 1 2.77 5.46L1.36 4.95A6 6 0 0 1 2.40 3.14L3.55 4.11A4.5 4.5 0 0 1 6.22 2.57ZM9.3 7A2.3 2.3 0 1 0 4.7 7A2.3 2.3 0 1 0 9.3 7Z" />
  </svg>
)

const TreeDuplicateIcon = () => (
  <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor">
    <path d="M16 1H4C2.9 1 2 1.9 2 3v14h2V3h12V1zm3 4H8C6.9 5 6 5.9 6 7v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z" />
  </svg>
)

const ChevronIcon = ({ collapsed }) => (
  <svg
    viewBox="0 0 8 8"
    width="10"
    height="10"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.8"
    strokeLinecap="round"
    strokeLinejoin="round"
    style={{ transform: collapsed ? 'rotate(-90deg)' : 'rotate(0deg)', transition: 'transform 0.15s', flexShrink: 0 }}
  >
    <polyline points="1,2.5 4,5.5 7,2.5" />
  </svg>
)

const ICONS = {
  label: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="currentColor">
      <text x="1" y="11" fontSize="10" fontFamily="Georgia, serif" fontWeight="bold">T</text>
    </svg>
  ),
  panel: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.4">
      <rect x="1.5" y="1.5" width="11" height="11" rx="1" />
      <line x1="1.5" y1="5" x2="12.5" y2="5" />
    </svg>
  ),
  badge: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="currentColor">
      <circle cx="7" cy="7" r="4.5" />
    </svg>
  ),
  chart: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="currentColor">
      <rect x="1" y="5" width="2.5" height="8" rx="0.5" />
      <rect x="5.5" y="2" width="2.5" height="11" rx="0.5" />
      <rect x="10" y="7" width="2.5" height="6" rx="0.5" />
    </svg>
  ),
  gauge: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round">
      <path d="M2.5 11 A5.5 5.5 0 0 1 11.5 11" />
      <line x1="7" y1="11" x2="10" y2="6.5" />
    </svg>
  ),
  graph: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="1,11 4.5,6.5 8,8.5 13,3" />
    </svg>
  ),
  table: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.3">
      <rect x="1" y="1" width="12" height="12" rx="1" />
      <line x1="1" y1="4.5" x2="13" y2="4.5" />
      <line x1="1" y1="8" x2="13" y2="8" />
      <line x1="5" y1="4.5" x2="5" y2="13" />
    </svg>
  ),
  button: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.3">
      <rect x="1" y="3.5" width="12" height="7" rx="1.5" />
      <line x1="4" y1="7" x2="10" y2="7" />
    </svg>
  ),
  news: () => (
    <svg viewBox="0 0 14 14" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.3">
      <rect x="1" y="1" width="12" height="12" rx="1" />
      <line x1="3" y1="4" x2="11" y2="4" />
      <line x1="3" y1="6.5" x2="11" y2="6.5" />
      <line x1="3" y1="9" x2="8" y2="9" />
    </svg>
  ),
}

const TYPE_COLORS = {
  label:  '#63B3ED',
  panel:  '#B794F4',
  badge:  '#68D391',
  chart:  '#F6AD55',
  gauge:  '#76E4F7',
  graph:  '#F687B3',
  table:  '#FBD38D',
  button: '#F6AD55',
  news:   '#68D391',
}

function snippet(widget) {
  switch (widget.type) {
    case 'label':
      return widget.text ? (widget.text.length > 30 ? widget.text.slice(0, 30) + '…' : widget.text) : null
    case 'panel':
      return `${widget.children?.length ?? 0} children`
    case 'badge':
      return widget.placeholder ? (widget.placeholder.length > 30 ? widget.placeholder.slice(0, 30) + '…' : widget.placeholder) : null
    case 'chart':
      return widget.symbol || null
    case 'gauge':
      return widget.placeholder ? (widget.placeholder.length > 30 ? widget.placeholder.slice(0, 30) + '…' : widget.placeholder) : null
    case 'graph':
      return widget.series?.length ? `${widget.series.length} series` : null
    case 'table':
      return widget.columns?.length ? `${widget.columns.length} columns` : null
    case 'button':
      return widget.text ? (widget.text.length > 30 ? widget.text.slice(0, 30) + '…' : widget.text) : null
    case 'news': {
      const feedCount = widget.feeds?.length ?? 0
      if (feedCount === 0) return 'No feeds'
      if (feedCount === 1) {
        const f = widget.feeds[0]
        const sym = f.symbol?.trim().toUpperCase()
        return sym ? `${f.type === 'ibkr' ? 'IBKR' : 'Yahoo'}: ${sym}` : 'Market news'
      }
      return `${feedCount} feeds`
    }
    default:
      return null
  }
}

function DropLine() {
  return (
    <Box
      h="2px"
      bg="blue.400"
      borderRadius="full"
      mx="6px"
      pointerEvents="none"
    />
  )
}

function TreeNode({ widget, siblings, parentId, depth, drag, onDragStart, onDragOver, onDrop, onDragEnd, selectedId, onSelect, collapsedIds, onToggleCollapse, onOpenSettings, onDuplicate }) {
  const Icon = ICONS[widget.type] ?? ICONS.label
  const color = TYPE_COLORS[widget.type] ?? '#A0AEC0'
  const isDragging = drag.id === widget.id
  const sameParent = drag.parentId === parentId
  const isOverBefore = sameParent && drag.overId === widget.id && drag.before
  const isOverAfter  = sameParent && drag.overId === widget.id && !drag.before
  const hasChildren  = widget.type === 'panel' && widget.children?.length > 0
  const isCollapsed  = collapsedIds.has(widget.id)
  const info = snippet(widget)

  const handleDragStart = (e) => {
    e.stopPropagation()
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', widget.id)
    onDragStart(widget.id, parentId)
  }

  const handleDragOver = (e) => {
    if (drag.id === null || drag.parentId !== parentId) return
    e.preventDefault()
    e.stopPropagation()
    e.dataTransfer.dropEffect = 'move'
    const rect = e.currentTarget.getBoundingClientRect()
    onDragOver(widget.id, e.clientY < rect.top + rect.height / 2)
  }

  const handleDrop = (e) => {
    e.preventDefault()
    e.stopPropagation()
    if (drag.id === null || drag.parentId !== parentId || drag.id === widget.id) return
    onDrop(siblings, parentId)
  }

  return (
    <Box>
      {isOverBefore && <DropLine />}

      <Box
        position="relative"
        role="group"
        display="flex"
        alignItems="center"
        gap="6px"
        pl={`${10 + depth * 16}px`}
        pr="8px"
        py="6px"
        cursor="grab"
        draggable
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        onDragEnd={onDragEnd}
        opacity={isDragging ? 0.35 : 1}
        bg={selectedId === widget.id ? 'whiteAlpha.100' : 'transparent'}
        _hover={{ bg: isDragging ? 'transparent' : 'whiteAlpha.50' }}
        borderRadius="4px"
        mx="2px"
        onClick={() => onSelect(widget.id === selectedId ? null : widget.id)}
        userSelect="none"
        transition="opacity 0.1s"
      >
        {hasChildren && (
          <Box
            color="gray.500"
            flexShrink={0}
            lineHeight={0}
            _hover={{ color: 'gray.300' }}
            onClick={e => { e.stopPropagation(); onToggleCollapse(widget.id) }}
          >
            <ChevronIcon collapsed={isCollapsed} />
          </Box>
        )}
        <Box color={color} flexShrink={0} lineHeight={0}>
          <Icon />
        </Box>
        <Text fontSize="13px" color="gray.300" flexShrink={0} lineHeight="1.4">
          {widget.type}
        </Text>
        {info && (
          <Text fontSize="12px" color="gray.500" flex={1} overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap" lineHeight="1.4">
            {info}
          </Text>
        )}
        <Box
          display="flex"
          alignItems="center"
          gap="3px"
          ml="auto"
          flexShrink={0}
          opacity={0}
          _groupHover={{ opacity: 1 }}
        >
          <Box
            as="button"
            color="gray.600"
            lineHeight={0}
            _hover={{ color: 'gray.300' }}
            onClick={(e) => { e.stopPropagation(); onDuplicate?.(widget.id) }}
          >
            <TreeDuplicateIcon />
          </Box>
          <Box
            as="button"
            color="gray.600"
            lineHeight={0}
            _hover={{ color: 'gray.300' }}
            onClick={(e) => { e.stopPropagation(); onOpenSettings?.(widget.id) }}
          >
            <TreeSettingsIcon />
          </Box>
          <Box color="gray.600" lineHeight={0} pointerEvents="none">
            <GripIcon />
          </Box>
        </Box>
      </Box>

      {isOverAfter && <DropLine />}

      {hasChildren && !isCollapsed && (() => { const sortedChildren = sortByY(widget.children); return (
        <Box position="relative">
          <Box
            position="absolute"
            left={`${10 + depth * 16 + 3}px`}
            top={0}
            bottom="4px"
            width="1px"
            bg="gray.700"
            pointerEvents="none"
          />
          {sortedChildren.map(child => (
            <TreeNode
              key={child.id}
              widget={child}
              siblings={sortedChildren}
              parentId={widget.id}
              depth={depth + 1}
              drag={drag}
              onDragStart={onDragStart}
              onDragOver={onDragOver}
              onDrop={onDrop}
              onDragEnd={onDragEnd}
              selectedId={selectedId}
              onSelect={onSelect}
              collapsedIds={collapsedIds}
              onToggleCollapse={onToggleCollapse}
              onOpenSettings={onOpenSettings}
              onDuplicate={onDuplicate}
            />
          ))}
        </Box>
      )})()}
    </Box>
  )
}

function WidgetTreePanel({ widgets = [], headerWidgets = [], topOffset = 0, onClose, onReorder, onOpenSettings, onDuplicate }) {
  const [selectedId, setSelectedId] = useState(null)
  const [drag, setDrag] = useState({ id: null, parentId: null, overId: null, before: true })
  const [collapsedIds, setCollapsedIds] = useState(new Set())

  const handleToggleCollapse = useCallback((id) => {
    setCollapsedIds(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }, [])

  const handleDragStart = useCallback((id, parentId) => {
    setDrag({ id, parentId, overId: null, before: true })
  }, [])

  const handleDragOver = useCallback((overId, before) => {
    setDrag(d => d.overId === overId && d.before === before ? d : { ...d, overId, before })
  }, [])

  const handleDrop = useCallback((siblings, parentId) => {
    setDrag(d => {
      if (!d.id || !d.overId || d.id === d.overId) return { id: null, parentId: null, overId: null, before: true }

      const withoutDragged = siblings.filter(w => w.id !== d.id)
      const targetIdx = withoutDragged.findIndex(w => w.id === d.overId)
      if (targetIdx === -1) return { id: null, parentId: null, overId: null, before: true }

      const insertAt = d.before ? targetIdx : targetIdx + 1
      const dragged = siblings.find(w => w.id === d.id)
      const newOrder = [...withoutDragged]
      newOrder.splice(insertAt, 0, dragged)

      onReorder(newOrder.map(w => w.id), parentId)
      return { id: null, parentId: null, overId: null, before: true }
    })
  }, [onReorder])

  const handleDragEnd = useCallback(() => {
    setDrag({ id: null, parentId: null, overId: null, before: true })
  }, [])

  const total = widgets.length + headerWidgets.length

  return (
    <Box
      position="fixed"
      top={`${topOffset}px`}
      right={0}
      bottom={0}
      width="300px"
      bg="gray.800"
      borderLeft="1px solid"
      borderColor="gray.700"
      zIndex={900}
      display="flex"
      flexDirection="column"
      boxShadow="-6px 0 20px rgba(0,0,0,0.5)"
    >
      <Box
        display="flex"
        alignItems="center"
        px={3}
        py="10px"
        borderBottom="1px solid"
        borderColor="gray.700"
        flexShrink={0}
        gap={2}
      >
        <Text fontSize="14px" fontWeight="semibold" color="gray.100" flex={1}>
          Widget Tree
        </Text>
        <Text fontSize="12px" color="gray.500">
          {total} {total === 1 ? 'widget' : 'widgets'}
        </Text>
        <IconButton
          icon={<CloseIcon />}
          size="xs"
          variant="ghost"
          aria-label="Close"
          color="gray.500"
          minW="26px"
          h="26px"
          _hover={{ color: 'gray.200', bg: 'whiteAlpha.100' }}
          onClick={onClose}
        />
      </Box>

      <Box
        flex={1}
        overflowY="auto"
        py="4px"
        sx={{
          '&::-webkit-scrollbar': { width: '4px' },
          '&::-webkit-scrollbar-thumb': { background: 'var(--chakra-colors-gray-600)', borderRadius: '2px' },
          '&::-webkit-scrollbar-track': { background: 'transparent' },
        }}
      >
        {total === 0 ? (
          <Text fontSize="12px" color="gray.600" textAlign="center" mt={6}>No widgets</Text>
        ) : (
          <>
            {widgets.length > 0 && (() => { const sorted = sortByY(widgets); return (
              <Box mb={2}>
                <Text fontSize="11px" fontWeight="600" color="gray.500" textTransform="uppercase" letterSpacing="0.08em" px={3} py="8px">
                  Page · {widgets.length}
                </Text>
                {sorted.map(w => (
                  <TreeNode
                    key={w.id}
                    widget={w}
                    siblings={sorted}
                    parentId={null}
                    depth={0}
                    drag={drag}
                    onDragStart={handleDragStart}
                    onDragOver={handleDragOver}
                    onDrop={handleDrop}
                    onDragEnd={handleDragEnd}
                    selectedId={selectedId}
                    onSelect={setSelectedId}
                    collapsedIds={collapsedIds}
                    onToggleCollapse={handleToggleCollapse}
                    onOpenSettings={onOpenSettings}
                    onDuplicate={onDuplicate}
                  />
                ))}
              </Box>
            )})()}

            {headerWidgets.length > 0 && (() => { const sorted = sortByX(headerWidgets); return (
              <Box>
                <Text
                  fontSize="11px" fontWeight="600" color="gray.500" textTransform="uppercase" letterSpacing="0.08em"
                  px={3} pt={widgets.length > 0 ? '12px' : '8px'} pb="8px"
                  borderTop={widgets.length > 0 ? '1px solid' : 'none'} borderColor="gray.700"
                >
                  Header · {headerWidgets.length}
                </Text>
                {sorted.map(w => (
                  <TreeNode
                    key={w.id}
                    widget={w}
                    siblings={sorted}
                    parentId="header"
                    depth={0}
                    drag={drag}
                    onDragStart={handleDragStart}
                    onDragOver={handleDragOver}
                    onDrop={handleDrop}
                    onDragEnd={handleDragEnd}
                    selectedId={selectedId}
                    onSelect={setSelectedId}
                    collapsedIds={collapsedIds}
                    onToggleCollapse={handleToggleCollapse}
                    onOpenSettings={onOpenSettings}
                    onDuplicate={onDuplicate}
                  />
                ))}
              </Box>
            )})()}
          </>
        )}
      </Box>
    </Box>
  )
}

export default WidgetTreePanel
