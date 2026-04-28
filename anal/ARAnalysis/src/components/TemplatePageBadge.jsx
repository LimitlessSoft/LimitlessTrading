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
  Checkbox,
  Tooltip,
  Code,
  useDisclosure
} from '@chakra-ui/react'
import { CloseIcon, DragHandleIcon, SettingsIcon, AddIcon, DeleteIcon } from '@chakra-ui/icons'

const DownloadIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
  </svg>
)
import { getConnectionStatus } from '../modules/dataService'
import { useSymbolQuotes, hasSymbolPlaceholders } from '../hooks/useSymbolQuotes'
import { parseSymbolPlaceholders, formatQuoteValue } from '../modules/symbolQuoteService'
import { getExchangeTime, getExchangeStatus } from '../modules/exchangeService'

const TOTAL_SEGMENTS = 20
const MIN_SEGMENTS = 1
const MIN_SIZE = 20
const DEFAULT_SIZE = 40

const DEFAULT_STYLE = {
  bgColor: 'transparent',
  borderColor: '#718096',
  borderWidth: 0
}

// Placeholder pattern for system template variables: ${name} or ${name:interval}
const PLACEHOLDER_REGEX = /\$\{(\w+)(?::(\d+))?\}/
const PLACEHOLDER_REGEX_GLOBAL = /\$\{(\w+)(?::(\d+))?\}/g

// Symbol quote placeholder pattern: @{SYMBOL:property:seconds}
const SYMBOL_QUOTE_REGEX = /@\{([A-Za-z0-9.^]+):(\w+)(?::(\d+))?\}/

function TemplatePageBadge({ label, layoutY, onDelete, onUpdate, onExport, onDragPreview, onDragStateChange, onMoveControl, activeDrag, parentPanelId, isEditing, containerRef, isHeaderMode }) {
  const [isResizing, setIsResizing] = useState(false)
  const [isMoving, setIsMoving] = useState(false)
  const [previewPos, setPreviewPos] = useState(null)
  const [previewSize, setPreviewSize] = useState(null)
  const [editConfig, setEditConfig] = useState(null)
  const [currentValue, setCurrentValue] = useState(null)
  const boxRef = useRef(null)
  const { isOpen, onOpen, onClose } = useDisclosure()

  const width = label.width || 2
  const height = label.height || DEFAULT_SIZE
  const x = label.x || 0
  const y = label.y || 0
  const style = { ...DEFAULT_STYLE, ...label.style }
  const valueProvider = label.placeholder || ''
  const text = label.text || ''
  const showValueAsText = label.showValueAsText || false
  const displayMode = label.displayMode || 'circle'  // 'circle' or 'full'
  const colorMappings = label.colorMappings || []
  const defaultColor = label.defaultColor || '#718096'

  const displayWidth = previewSize?.width ?? width
  const displayHeight = previewSize?.height ?? height
  const displayX = previewPos?.x ?? x
  const displayY = previewPos?.y ?? layoutY ?? 0

  // Check for placeholder types
  const hasSystemPlaceholders = (valueProvider && valueProvider.includes('${')) ||
                                (text && text.includes('${'))
  const hasSymbolQuotePlaceholders = (valueProvider && valueProvider.includes('@{')) ||
                                     (text && text.includes('@{'))

  // Parse symbol quote from value provider
  const symbolQuoteMatch = valueProvider ? valueProvider.match(SYMBOL_QUOTE_REGEX) : null
  const symbolQuoteInfo = symbolQuoteMatch ? {
    symbol: symbolQuoteMatch[1].toUpperCase(),
    property: symbolQuoteMatch[2].toLowerCase(),
    interval: symbolQuoteMatch[3] ? parseInt(symbolQuoteMatch[3], 10) : 5
  } : null

  // Subscribe to symbol quotes (for both value provider and text)
  const combinedTextForQuotes = [valueProvider, text].filter(Boolean).join(' ')
  const { quotes: symbolQuotes, resolveText: resolveSymbolText, getValue: getQuoteValue } = useSymbolQuotes(
    hasSymbolQuotePlaceholders ? combinedTextForQuotes : null
  )

  // Get value from symbol quote if applicable
  const symbolQuoteValue = symbolQuoteInfo
    ? getQuoteValue(symbolQuoteInfo.symbol, symbolQuoteInfo.property)
    : null
  const formattedSymbolQuoteValue = symbolQuoteInfo && symbolQuoteValue !== null
    ? formatQuoteValue(symbolQuoteInfo.property, symbolQuoteValue)
    : null

  // Parse system placeholders to get minimum refresh interval
  const getSystemRefreshInterval = useCallback(() => {
    let minInterval = Infinity
    const allText = [valueProvider, text].filter(Boolean).join(' ')

    for (const match of allText.matchAll(PLACEHOLDER_REGEX_GLOBAL)) {
      const interval = match[2] ? parseInt(match[2], 10) : 5
      if (interval < minInterval) minInterval = interval
    }

    for (const match of allText.matchAll(/\$\{[A-Za-z0-9]+:(?:time|status)(?::(\d+))?\}/g)) {
      const interval = match[1] ? parseInt(match[1], 10) : 5
      if (interval < minInterval) minInterval = interval
    }

    return minInterval === Infinity ? null : minInterval
  }, [valueProvider, text])

  // Fetch system placeholder values (like tws_connection_status and exchange placeholders)
  const fetchSystemPlaceholderValues = useCallback(async () => {
    const needsTwsStatus = (valueProvider && valueProvider.includes('tws_connection_status')) ||
                           (text && text.includes('tws_connection_status'))

    if (needsTwsStatus) {
      try {
        const status = await getConnectionStatus()
        setCurrentValue(status.connected ? `Connected (${status.port})` : 'Disconnected')
      } catch (err) {
        setCurrentValue('Unknown')
      }
      return
    }

    // Set currentValue from exchange placeholder in value provider (used for badge color mapping)
    if (valueProvider) {
      const match = valueProvider.match(/^\s*\$\{([A-Za-z0-9]+):(time|status)/)
      if (match) {
        const exchange = match[1].toUpperCase()
        const property = match[2]
        setCurrentValue(property === 'time' ? getExchangeTime(exchange) : getExchangeStatus(exchange))
      }
    }
  }, [valueProvider, text])

  // Resolve system placeholders in text (${name} syntax)
  const resolveSystemText = (inputText) => {
    if (!inputText) return ''
    let resolved = inputText.replace(PLACEHOLDER_REGEX_GLOBAL, (match, key) => {
      if (key === 'tws_connection_status') return currentValue || match
      return match
    })
    resolved = resolved.replace(/\$\{([A-Za-z0-9]+):(time|status)(?::(\d+))?\}/g, (match, exchange, property) => {
      return property === 'time'
        ? getExchangeTime(exchange.toUpperCase())
        : getExchangeStatus(exchange.toUpperCase())
    })
    return resolved
  }

  // Resolve all placeholders: first system (${...}), then symbol (@{...})
  const resolveText = (inputText) => {
    const systemResolved = resolveSystemText(inputText)
    return resolveSymbolText(systemResolved)
  }

  // Determine the current value for color mapping
  // Priority: symbol quote value > system placeholder value
  const effectiveValue = formattedSymbolQuoteValue !== null
    ? formattedSymbolQuoteValue
    : currentValue

  // For numeric comparisons in color mappings, use raw value
  const rawValue = symbolQuoteValue !== null ? symbolQuoteValue : currentValue

  // Get the display text - either from value provider or custom text
  const displayText = showValueAsText
    ? effectiveValue
    : (text ? resolveText(text) : null)

  // Poll for system placeholder values
  useEffect(() => {
    if (!hasSystemPlaceholders) return

    const refreshInterval = getSystemRefreshInterval()
    if (!refreshInterval) return

    fetchSystemPlaceholderValues()
    const interval = setInterval(fetchSystemPlaceholderValues, refreshInterval * 1000)
    return () => clearInterval(interval)
  }, [hasSystemPlaceholders, fetchSystemPlaceholderValues, getSystemRefreshInterval])

  // Determine badge color based on current value and mappings
  // Supports both string matching and numeric comparisons
  const getBadgeColor = () => {
    if (effectiveValue === null && rawValue === null) return defaultColor

    const stringValue = effectiveValue !== null ? String(effectiveValue) : ''
    const valueLower = stringValue.toLowerCase()
    const numericValue = typeof rawValue === 'number' ? rawValue : parseFloat(stringValue)
    const isNumeric = !isNaN(numericValue)

    // First pass: exact matches take priority
    for (const mapping of colorMappings) {
      if (!mapping.contains) {
        // Check for numeric comparison operators
        const mappingValue = mapping.value.trim()
        if (isNumeric && /^[<>=!]/.test(mappingValue)) {
          if (evaluateNumericCondition(numericValue, mappingValue)) {
            return mapping.color
          }
        } else if (valueLower === mappingValue.toLowerCase()) {
          return mapping.color
        }
      }
    }

    // Second pass: contains matches
    for (const mapping of colorMappings) {
      if (mapping.contains && valueLower.includes(mapping.value.toLowerCase())) {
        return mapping.color
      }
    }

    return defaultColor
  }

  // Evaluate numeric conditions like ">20", "<=15", ">=30"
  function evaluateNumericCondition(value, condition) {
    const match = condition.match(/^([<>=!]+)\s*(-?\d+\.?\d*)$/)
    if (!match) return false

    const operator = match[1]
    const threshold = parseFloat(match[2])

    switch (operator) {
      case '>': return value > threshold
      case '>=': return value >= threshold
      case '<': return value < threshold
      case '<=': return value <= threshold
      case '=':
      case '==': return value === threshold
      case '!=':
      case '<>': return value !== threshold
      default: return false
    }
  }

  useEffect(() => {
    if (previewSize && label.width === previewSize.width && label.height === previewSize.height) {
      setPreviewSize(null)
    }
  }, [label.width, label.height, previewSize])

  useEffect(() => {
    if (previewPos && !isMoving) {
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
    setEditConfig({
      placeholder: valueProvider,
      text: text,
      showValueAsText: showValueAsText,
      displayMode: displayMode,
      colorMappings: colorMappings.length > 0 ? [...colorMappings] : [{ value: '', color: '#48BB78', contains: true }],
      defaultColor: defaultColor,
      style: { ...DEFAULT_STYLE, ...label.style }
    })
    onOpen()
  }

  const handleSaveConfig = () => {
    onUpdate(label.id, {
      placeholder: editConfig.placeholder,
      text: editConfig.text,
      showValueAsText: editConfig.showValueAsText,
      displayMode: editConfig.displayMode,
      colorMappings: editConfig.colorMappings.filter(m => m.value.trim()),
      defaultColor: editConfig.defaultColor,
      style: editConfig.style
    })
    onClose()
  }

  const handleAddMapping = () => {
    setEditConfig({
      ...editConfig,
      colorMappings: [...editConfig.colorMappings, { value: '', color: '#718096', contains: true }]
    })
  }

  const handleRemoveMapping = (index) => {
    const newMappings = editConfig.colorMappings.filter((_, i) => i !== index)
    setEditConfig({ ...editConfig, colorMappings: newMappings })
  }

  const handleUpdateMapping = (index, field, value) => {
    const newMappings = [...editConfig.colorMappings]
    newMappings[index] = { ...newMappings[index], [field]: value }
    setEditConfig({ ...editConfig, colorMappings: newMappings })
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

  // Move handler
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

      // Check if dropped on a panel or header
      const dropTarget = findDropTarget(lastMouseX, lastMouseY)

      if (dropTarget && dropTarget !== parentPanelId) {
        onMoveControl?.(label.id, parentPanelId, dropTarget)
        setPreviewPos(null)
        onDragPreview?.(null)
      } else if (dropTarget === null && parentPanelId !== null && parentPanelId !== 'header') {
        const mainContainer = containerRef?.current
        if (mainContainer) {
          const rect = mainContainer.getBoundingClientRect()
          if (lastMouseX >= rect.left && lastMouseX <= rect.right && lastMouseY >= rect.top && lastMouseY <= rect.bottom) {
            onMoveControl?.(label.id, parentPanelId, null)
            setPreviewPos(null)
            onDragPreview?.(null)
            return
          }
        }
        if (currentX !== x || currentY !== y) {
          onUpdate(label.id, { x: currentX, y: currentY })
        } else {
          setPreviewPos(null)
          onDragPreview?.(null)
        }
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

  // Resize handler (proportional)
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
      const delta = Math.max(deltaX, deltaY)

      const deltaSegments = Math.round(deltaX / info.segmentSize)
      const newWidth = Math.max(MIN_SEGMENTS, Math.min(TOTAL_SEGMENTS - x, startWidth + deltaSegments))
      const newHeight = Math.max(MIN_SIZE, startHeight + deltaY)

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

  const badgeColor = getBadgeColor()

  // Header mode: simplified inline rendering
  if (isHeaderMode) {
    return (
      <>
        <Box
          ref={boxRef}
          display="flex"
          alignItems="center"
          outline={isEditing ? '1px dashed' : 'none'}
          outlineColor={isEditing ? (isMoving ? 'blue.400' : 'gray.500') : undefined}
          cursor={isEditing ? (isMoving ? 'grabbing' : 'grab') : 'default'}
          onMouseDown={isEditing ? handleMoveStart : undefined}
          opacity={isMoving ? 0.85 : 1}
          transform={isMoving ? 'scale(1.08)' : 'scale(1)'}
          boxShadow={isMoving ? '0 6px 20px rgba(0,0,0,0.6)' : 'none'}
          transition={isMoving ? 'none' : 'transform 0.15s, box-shadow 0.15s, opacity 0.15s'}
          zIndex={isMoving ? 100 : 'auto'}
          px={1}
        >
          {displayMode === 'full' ? (
            // Full background mode
            <Tooltip label={effectiveValue || 'No data'} placement="bottom" hasArrow>
              <Box
                bg={badgeColor}
                borderRadius="md"
                px={2}
                py={0.5}
                transition="background-color 0.3s ease"
              >
                <Text
                  fontSize="sm"
                  fontWeight="medium"
                  whiteSpace="nowrap"
                  color="white"
                >
                  {displayText || effectiveValue || '—'}
                </Text>
              </Box>
            </Tooltip>
          ) : (
            // Circle indicator mode
            <>
              <Tooltip label={effectiveValue || 'No data'} placement="bottom" hasArrow>
                <Box
                  width="16px"
                  height="16px"
                  borderRadius="full"
                  bg={badgeColor}
                  transition="background-color 0.3s ease"
                  flexShrink={0}
                />
              </Tooltip>
              {displayText && (
                <Text
                  ml={1}
                  fontSize="sm"
                  whiteSpace="nowrap"
                  color="white"
                >
                  {displayText}
                </Text>
              )}
            </>
          )}
          {isEditing && (
            <>
              <Tooltip label="Export badge" placement="top" hasArrow>
                <IconButton
                  icon={<DownloadIcon />}
                  size="xs"
                  variant="ghost"
                  aria-label="Export badge"
                  onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                  onMouseDown={(e) => e.stopPropagation()}
                  ml={1}
                />
              </Tooltip>
              <IconButton
                icon={<SettingsIcon />}
                size="xs"
                variant="ghost"
                aria-label="Edit badge"
                onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
                onMouseDown={(e) => e.stopPropagation()}
                ml={1}
              />
              <IconButton
                icon={<CloseIcon />}
                size="xs"
                variant="ghost"
                aria-label="Delete badge"
                onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
                onMouseDown={(e) => e.stopPropagation()}
              />
            </>
          )}
        </Box>

        {/* Edit Modal */}
        <Modal isOpen={isOpen} onClose={onClose} size="lg">
          <ModalOverlay />
          <ModalContent bg="gray.800">
            <ModalHeader>Edit Badge</ModalHeader>
            <ModalCloseButton />
            <ModalBody>
              {editConfig && (
                <VStack spacing={4} align="stretch">
                  <FormControl>
                    <FormLabel fontSize="sm">Value Provider</FormLabel>
                    <Input
                      value={editConfig.placeholder}
                      onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                      placeholder="@{AAPL:last:5} or ${tws_connection_status:5}"
                    />
                    <Text fontSize="xs" color="gray.500" mt={1}>
                      System: <Code fontSize="xs">${'{name:seconds}'}</Code> | Quote: <Code fontSize="xs">@{'{SYMBOL:property:seconds}'}</Code>
                    </Text>
                  </FormControl>

                  <FormControl>
                    <FormLabel fontSize="sm">Text</FormLabel>
                    <Input
                      value={editConfig.text || ''}
                      onChange={(e) => setEditConfig({ ...editConfig, text: e.target.value })}
                      placeholder="Optional text (supports placeholders)"
                      isDisabled={editConfig.showValueAsText}
                    />
                  </FormControl>

                  <HStack spacing={4}>
                    <Checkbox
                      isChecked={editConfig.showValueAsText}
                      onChange={(e) => setEditConfig({ ...editConfig, showValueAsText: e.target.checked })}
                      size="sm"
                    >
                      Show value as text
                    </Checkbox>
                    <FormControl flex={1}>
                      <Select
                        size="sm"
                        value={editConfig.displayMode}
                        onChange={(e) => setEditConfig({ ...editConfig, displayMode: e.target.value })}
                      >
                        <option value="circle">Circle indicator</option>
                        <option value="full">Full background</option>
                      </Select>
                    </FormControl>
                  </HStack>

                  <FormControl>
                    <FormLabel fontSize="sm">Default Color</FormLabel>
                    <Input
                      type="color"
                      value={editConfig.defaultColor}
                      onChange={(e) => setEditConfig({ ...editConfig, defaultColor: e.target.value })}
                      height="40px"
                      p={1}
                    />
                  </FormControl>

                  <Box>
                    <HStack justify="space-between" mb={2}>
                      <FormLabel fontSize="sm" mb={0}>Color Mappings</FormLabel>
                      <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddMapping}>
                        Add
                      </Button>
                    </HStack>
                    <Text fontSize="xs" color="gray.500" mb={2}>
                      For numeric values, use operators: <Code fontSize="xs">&gt;20</Code>, <Code fontSize="xs">&lt;=15</Code>, <Code fontSize="xs">&gt;=30</Code>
                    </Text>

                    <VStack spacing={2} align="stretch">
                      {editConfig.colorMappings.map((mapping, index) => (
                        <HStack key={index} spacing={2}>
                          <Input
                            size="sm"
                            placeholder="Value or >20, <=15"
                            value={mapping.value}
                            onChange={(e) => handleUpdateMapping(index, 'value', e.target.value)}
                            flex={1}
                          />
                          <Input
                            type="color"
                            value={mapping.color}
                            onChange={(e) => handleUpdateMapping(index, 'color', e.target.value)}
                            width="50px"
                            height="32px"
                            p={1}
                          />
                          <Checkbox
                            size="sm"
                            isChecked={mapping.contains}
                            onChange={(e) => handleUpdateMapping(index, 'contains', e.target.checked)}
                          >
                            Contains
                          </Checkbox>
                          <IconButton
                            icon={<DeleteIcon />}
                            size="xs"
                            variant="ghost"
                            colorScheme="red"
                            onClick={() => handleRemoveMapping(index)}
                            aria-label="Remove mapping"
                          />
                        </HStack>
                      ))}
                    </VStack>
                  </Box>
                </VStack>
              )}
            </ModalBody>
            <ModalFooter>
              <Button variant="ghost" mr={3} onClick={onClose}>
                Cancel
              </Button>
              <Button colorScheme="blue" onClick={handleSaveConfig}>
                Save
              </Button>
            </ModalFooter>
          </ModalContent>
        </Modal>
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
        bg={displayMode === 'full' ? badgeColor : style.bgColor}
        borderRadius="md"
        display="flex"
        alignItems="center"
        justifyContent={displayMode === 'full' ? 'center' : (displayText ? 'flex-start' : 'center')}
        px={displayMode === 'full' ? 2 : (displayText ? 2 : 0)}
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
        {displayMode === 'full' ? (
          // Full background mode - text inside colored box
          <Tooltip label={effectiveValue || 'No data'} placement="top" hasArrow>
            <Text
              fontSize="sm"
              fontWeight="medium"
              whiteSpace="nowrap"
              color="white"
              pointerEvents="none"
              userSelect="none"
            >
              {displayText || effectiveValue || '—'}
            </Text>
          </Tooltip>
        ) : (
          // Circle indicator mode
          <>
            <Tooltip label={effectiveValue || 'No data'} placement="top" hasArrow>
              <Box
                width={`${Math.min(displayHeight - 8, 32)}px`}
                height={`${Math.min(displayHeight - 8, 32)}px`}
                borderRadius="full"
                bg={badgeColor}
                transition="background-color 0.3s ease"
                flexShrink={0}
              />
            </Tooltip>
            {displayText && (
              <Text
                ml={2}
                fontSize="sm"
                whiteSpace="nowrap"
                color="white"
                pointerEvents="none"
                userSelect="none"
              >
                {displayText}
              </Text>
            )}
          </>
        )}

        {isEditing && (
          <>
            <Tooltip label="Export badge" placement="top" hasArrow>
              <IconButton
                icon={<DownloadIcon />}
                size="xs"
                variant="ghost"
                aria-label="Export badge"
                onClick={(e) => { e.stopPropagation(); onExport?.(label) }}
                onMouseDown={(e) => e.stopPropagation()}
                position="absolute"
                top={0}
                right={12}
              />
            </Tooltip>
            <IconButton
              icon={<SettingsIcon />}
              size="xs"
              variant="ghost"
              aria-label="Edit badge"
              onClick={(e) => { e.stopPropagation(); handleOpenEdit() }}
              onMouseDown={(e) => e.stopPropagation()}
              position="absolute"
              top={0}
              right={6}
            />
            <IconButton
              icon={<CloseIcon />}
              size="xs"
              variant="ghost"
              aria-label="Delete badge"
              onClick={(e) => { e.stopPropagation(); onDelete(label.id) }}
              onMouseDown={(e) => e.stopPropagation()}
              position="absolute"
              top={0}
              right={0}
            />
            {/* Corner resize */}
            <Box
              position="absolute"
              right={0}
              bottom={0}
              width="12px"
              height="12px"
              cursor="nwse-resize"
              bg="whiteAlpha.200"
              _hover={{ bg: 'whiteAlpha.400' }}
              onMouseDown={handleResize}
            />
          </>
        )}
      </Box>

      {/* Edit Modal */}
      <Modal isOpen={isOpen} onClose={onClose} size="lg">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Edit Badge</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            {editConfig && (
              <VStack spacing={4} align="stretch">
                <FormControl>
                  <FormLabel fontSize="sm">Value Provider</FormLabel>
                  <Input
                    value={editConfig.placeholder}
                    onChange={(e) => setEditConfig({ ...editConfig, placeholder: e.target.value })}
                    placeholder="${tws_connection_status:5}"
                  />
                </FormControl>

                <FormControl>
                  <FormLabel fontSize="sm">Text</FormLabel>
                  <Input
                    value={editConfig.text || ''}
                    onChange={(e) => setEditConfig({ ...editConfig, text: e.target.value })}
                    placeholder="Optional text (supports ${placeholder} syntax)"
                    isDisabled={editConfig.showValueAsText}
                  />
                </FormControl>

                <HStack spacing={4}>
                  <Checkbox
                    isChecked={editConfig.showValueAsText}
                    onChange={(e) => setEditConfig({ ...editConfig, showValueAsText: e.target.checked })}
                    size="sm"
                  >
                    Show value as text
                  </Checkbox>
                  <FormControl flex={1}>
                    <Select
                      size="sm"
                      value={editConfig.displayMode}
                      onChange={(e) => setEditConfig({ ...editConfig, displayMode: e.target.value })}
                    >
                      <option value="circle">Circle indicator</option>
                      <option value="full">Full background</option>
                    </Select>
                  </FormControl>
                </HStack>

                <FormControl>
                  <FormLabel fontSize="sm">Default Color</FormLabel>
                  <Input
                    type="color"
                    value={editConfig.defaultColor}
                    onChange={(e) => setEditConfig({ ...editConfig, defaultColor: e.target.value })}
                    height="40px"
                    p={1}
                  />
                </FormControl>

                <Box>
                  <HStack justify="space-between" mb={2}>
                    <FormLabel fontSize="sm" mb={0}>Color Mappings</FormLabel>
                    <Button size="xs" leftIcon={<AddIcon />} onClick={handleAddMapping}>
                      Add
                    </Button>
                  </HStack>

                  <VStack spacing={2} align="stretch">
                    {editConfig.colorMappings.map((mapping, index) => (
                      <HStack key={index} spacing={2}>
                        <Input
                          size="sm"
                          placeholder="Value to match"
                          value={mapping.value}
                          onChange={(e) => handleUpdateMapping(index, 'value', e.target.value)}
                          flex={1}
                        />
                        <Input
                          type="color"
                          value={mapping.color}
                          onChange={(e) => handleUpdateMapping(index, 'color', e.target.value)}
                          width="50px"
                          height="32px"
                          p={1}
                        />
                        <Checkbox
                          size="sm"
                          isChecked={mapping.contains}
                          onChange={(e) => handleUpdateMapping(index, 'contains', e.target.checked)}
                        >
                          Contains
                        </Checkbox>
                        <IconButton
                          icon={<DeleteIcon />}
                          size="xs"
                          variant="ghost"
                          colorScheme="red"
                          onClick={() => handleRemoveMapping(index)}
                          aria-label="Remove mapping"
                        />
                      </HStack>
                    ))}
                  </VStack>
                </Box>
              </VStack>
            )}
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onClose}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleSaveConfig}>
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </>
  )
}

export default TemplatePageBadge
