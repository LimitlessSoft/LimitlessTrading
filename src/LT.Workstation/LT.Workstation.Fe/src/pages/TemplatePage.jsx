import { useState, useEffect, useRef } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
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
  Input,
  Select,
  FormControl,
  FormLabel,
  VStack,
  Menu,
  MenuButton,
  MenuList,
  MenuItem,
  useDisclosure,
  useToast
} from '@chakra-ui/react'

const KebabIcon = () => (
  <svg viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
    <circle cx="12" cy="5" r="2" />
    <circle cx="12" cy="12" r="2" />
    <circle cx="12" cy="19" r="2" />
  </svg>
)
import { fetchRoutes, updateRoute, deleteRoute } from '../modules/routesService'
import { useLicense } from '../modules/LicenseContext'
import { useWidgetCallback } from '../modules/WidgetContext'
import { FREE_LIMITS } from '../modules/freeLimits'
import LicenseModal from '../components/LicenseModal'
import PageNavMenu from '../components/PageNavMenu'
import TemplatePageLabel from '../components/TemplatePageLabel'
import TemplatePageBadge from '../components/TemplatePageBadge'
import TemplatePageGauge from '../components/TemplatePageGauge'
import { WIDGET_REGISTRY, createWidget, renderWidget } from '../components/widgetRenderer'
import WidgetsGalleryModal from '../components/WidgetsGalleryModal'
import WidgetTreePanel from '../components/WidgetTreePanel'

// Returns the fillHeight available to dock:fill-height widgets after subtracting fixed-height rows.
// Without this, charts in a 2-row layout (charts + label bar) only get fillHeight/heightRows instead of fillHeight minus the label bar.
function computeAdjustedFillHeight(widgets, fillHeight, gap = 8) {
  if (!fillHeight) return fillHeight
  if (!widgets.some(w => w.dock === 'fill-height' || w.dock === 'fill')) return fillHeight

  const TOTAL_SEGMENTS = 20
  const sorted = [...widgets].sort((a, b) => (a.y || 0) - (b.y || 0))

  const rows = []
  let curX = 0, rowH = 0, rowHasFill = false

  for (const widget of sorted) {
    const def = WIDGET_REGISTRY[widget.type] || WIDGET_REGISTRY.label
    const w = widget.dock === 'fill' ? TOTAL_SEGMENTS : (widget.width || def.width)
    const isFill = widget.dock === 'fill' || widget.dock === 'fill-height'

    if (curX > 0 && curX + w > TOTAL_SEGMENTS) {
      rows.push({ maxH: rowH, hasFill: rowHasFill })
      curX = 0; rowH = 0; rowHasFill = false
    }

    curX += w
    if (isFill) rowHasFill = true
    else rowH = Math.max(rowH, widget.height || def.height)
  }
  rows.push({ maxH: rowH, hasFill: rowHasFill })

  const fixedRows = rows.filter(r => !r.hasFill)
  if (!fixedRows.length) return fillHeight

  // Each fixed row contributes its height + one inter-row gap
  const fixedTotal = fixedRows.reduce((sum, r) => sum + r.maxH + gap, 0)
  return Math.max(0, fillHeight - fixedTotal)
}

function TemplatePage() {
  const location = useLocation()
  const navigate = useNavigate()
  const [pageInfo, setPageInfo] = useState(null)
  const [labelText, setLabelText] = useState('')
  const [widgetType, setWidgetType] = useState('label')
  const [isSaving, setIsSaving] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const [pendingImport, setPendingImport] = useState(null)
  const [addStage, setAddStage] = useState('pick')
  const { isOpen, onOpen, onClose } = useDisclosure()
  const { isOpen: isImportOpen, onOpen: onImportOpen, onClose: onImportClose } = useDisclosure()
  const { isOpen: isGalleryOpen, onOpen: onGalleryOpen, onClose: onGalleryClose } = useDisclosure()
  const { isOpen: isInfoOpen, onOpen: onInfoOpen, onClose: onInfoClose } = useDisclosure()
  const { isOpen: isLicenseOpen, onOpen: onLicenseOpen, onClose: onLicenseClose } = useDisclosure()
  const { isOpen: isDeletePageOpen, onOpen: onDeletePageOpen, onClose: onDeletePageClose } = useDisclosure()
  const { isPro } = useLicense()
  const { setWidgetHandlers } = useWidgetCallback()
  const [infoName, setInfoName] = useState('')
  const [infoPath, setInfoPath] = useState('')
  const [routesVersion, setRoutesVersion] = useState(0)
  const toast = useToast()
  const containerRef = useRef(null)
  const headerRef = useRef(null)
  const fileInputRef = useRef(null)
  const widgetImportRef = useRef(null)
  const [dragPreview, setDragPreview] = useState(null)  // { id, x, y } during drag
  const [activeDrag, setActiveDrag] = useState(null)  // { widgetId, mousePos } during drag
  const [fillHeight, setFillHeight] = useState(null)
  const [isTreeOpen, setIsTreeOpen] = useState(false)
  const [headerHeight, setHeaderHeight] = useState(0)
  const [openSettingsForId, setOpenSettingsForId] = useState(null)

  useEffect(() => {
    const loadPageInfo = async () => {
      try {
        const routes = await fetchRoutes()
        const matchingRoute = routes.find(r => r.path === location.pathname)
        setPageInfo(matchingRoute || null)
      } catch {
        setPageInfo(null)
      }
    }
    loadPageInfo()
  }, [location.pathname])

  useEffect(() => {
    if (isOpen) setAddStage('pick')
  }, [isOpen])

  useEffect(() => {
    const compute = () => {
      const header = headerRef.current
      if (!header) return
      const headerH = header.getBoundingClientRect().height
      const padding = 12 // py={3} top + bottom
      setFillHeight(window.innerHeight - headerH - padding * 2)
      setHeaderHeight(headerH)
    }
    compute()
    window.addEventListener('resize', compute)
    return () => window.removeEventListener('resize', compute)
  }, [])

  useEffect(() => {
    if (!pageInfo) return
    setWidgetHandlers({ onCreate: addWidgetFromAI, onBatchCreate: batchCreateWidgetsFromAI, onUpdate: updateWidgetFromAI, onBatchUpdate: batchUpdateWidgetsFromAI, onDelete: deleteWidgetFromAI, onBatchDelete: batchDeleteWidgetsFromAI, onApplyActions: applyWidgetActionsFromAI }, pageInfo)
    return () => setWidgetHandlers({}, null)
  }, [pageInfo, isPro])

  const isAtWidgetLimit = () => {
    if (isPro) return false
    const count = (pageInfo?.config?.labels || []).length
    return count >= FREE_LIMITS.widgetsPerPage
  }

  const handleAddWidget = async () => {
    if (WIDGET_REGISTRY[widgetType]?.needsText && !labelText.trim()) return
    if (!pageInfo) return
    if (isAtWidgetLimit()) { onClose(); onLicenseOpen(); return }

    setIsSaving(true)
    try {
      const currentLabels = pageInfo.config?.labels || []
      const maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
      const newControl = { ...createWidget(widgetType, labelText.trim()), y: maxY + 1 }
      const updatedConfig = {
        ...pageInfo.config,
        labels: [...currentLabels, newControl]
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      setLabelText('')
      setWidgetType('label')
      onClose()
      toast({
        title: `${widgetType.charAt(0).toUpperCase() + widgetType.slice(1)} added`,
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to add widget',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    } finally {
      setIsSaving(false)
    }
  }

  const addWidgetFromAI = async (widgetDef) => {
    if (!pageInfo) return
    if (isAtWidgetLimit()) {
      toast({ title: 'Widget limit reached — upgrade to Pro to add more widgets', status: 'warning', duration: 4000, isClosable: true })
      return
    }
    try {
      const currentLabels = pageInfo.config?.labels || []
      const maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
      const regDef = WIDGET_REGISTRY[widgetDef.type] || {}
      const { displayName, Component, needsText, ...regDefaults } = regDef
      const newWidget = { ...regDefaults, ...widgetDef, id: crypto.randomUUID(), createdAt: Date.now(), x: 0, y: maxY + 1 }
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: [...currentLabels, newWidget] } })
      setPageInfo(updatedRoute)
      toast({ title: 'Widget added to page', status: 'success', duration: 3000, isClosable: true })
    } catch (err) {
      toast({ title: 'Failed to add widget', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const batchCreateWidgetsFromAI = async (widgetDefs) => {
    if (!pageInfo) return
    const currentLabels = pageInfo.config?.labels || []
    if (!isPro && currentLabels.length + widgetDefs.length > FREE_LIMITS.widgetsPerPage) {
      toast({ title: 'Widget limit reached — upgrade to Pro to add more widgets', status: 'warning', duration: 4000, isClosable: true })
      return
    }
    try {
      let maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
      const newWidgets = widgetDefs.map(widgetDef => {
        const regDef = WIDGET_REGISTRY[widgetDef.type] || {}
        const { displayName, Component, needsText, ...regDefaults } = regDef
        return { ...regDefaults, ...widgetDef, id: crypto.randomUUID(), createdAt: Date.now(), x: 0, y: ++maxY }
      })
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: [...currentLabels, ...newWidgets] } })
      setPageInfo(updatedRoute)
      toast({ title: `${newWidgets.length} widgets added`, status: 'success', duration: 3000, isClosable: true })
    } catch (err) {
      toast({ title: 'Failed to add widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  // Apply all widget actions from one AI response atomically in a single updateRoute call.
  // { creates: [widgetDef], updates: [{widgetId, changes}], deletes: [widgetId] }
  const applyWidgetActionsFromAI = async ({ creates = [], updates = [], deletes = [] }) => {
    if (!pageInfo) return
    const currentLabels = pageInfo.config?.labels || []
    if (!isPro && currentLabels.length + creates.length > FREE_LIMITS.widgetsPerPage) {
      toast({ title: 'Widget limit reached — upgrade to Pro to add more widgets', status: 'warning', duration: 4000, isClosable: true })
      return
    }
    try {
      const deleteSet = new Set(deletes)
      const changeMap = new Map(updates.map(u => [u.widgetId, u.changes]))
      let maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
      const newWidgets = creates.map(widgetDef => {
        const regDef = WIDGET_REGISTRY[widgetDef.type] || {}
        const { displayName, Component, needsText, ...regDefaults } = regDef
        return { ...regDefaults, ...widgetDef, id: crypto.randomUUID(), createdAt: Date.now(), x: 0, y: ++maxY }
      })
      const applyChanges = (widget, changes) => {
        const result = { ...widget, ...changes }
        // Allow AI to explicitly clear nullable layout properties
        if (changes.heightRows === null) delete result.heightRows
        if (changes.dock === null) delete result.dock
        return result
      }
      const updatedLabels = [
        ...currentLabels
          .filter(l => !deleteSet.has(l.id))
          .map(l => changeMap.has(l.id) ? applyChanges(l, changeMap.get(l.id)) : l),
        ...newWidgets
      ]
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: updatedLabels } })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const updateWidgetFromAI = async (widgetId, changes) => {
    if (!pageInfo) return
    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.map(l => {
        if (l.id !== widgetId) return l
        const result = { ...l, ...changes }
        if (changes.heightRows === null) delete result.heightRows
        if (changes.dock === null) delete result.dock
        return result
      })
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: updatedLabels } })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update widget', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const batchUpdateWidgetsFromAI = async (updates) => {
    if (!pageInfo) return
    try {
      const changeMap = new Map(updates.map(u => [u.widgetId, u.changes]))
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.map(l => changeMap.has(l.id) ? { ...l, ...changeMap.get(l.id) } : l)
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: updatedLabels } })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const deleteWidgetFromAI = async (widgetId) => {
    if (!pageInfo) return
    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.filter(l => l.id !== widgetId)
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: updatedLabels } })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to delete widget', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const batchDeleteWidgetsFromAI = async (widgetIds) => {
    if (!pageInfo) return
    try {
      const idSet = new Set(widgetIds)
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.filter(l => !idSet.has(l.id))
      const updatedRoute = await updateRoute(pageInfo.id, { config: { ...pageInfo.config, labels: updatedLabels } })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to delete widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const handleDeleteLabel = async (labelId) => {
    if (!pageInfo) return

    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.filter(l => l.id !== labelId)
      const updatedConfig = {
        ...pageInfo.config,
        labels: updatedLabels
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
      toast({
        title: 'Label deleted',
        status: 'info',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to delete label',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const handleDuplicateLabel = async (labelId) => {
    if (!pageInfo) return
    try {
      const currentLabels = pageInfo.config?.labels || []
      const original = currentLabels.find(l => l.id === labelId)
      if (!original) return
      const clone = { ...regenerateIds(original), y: (original.y || 0) + 0.5 }
      const updatedConfig = { ...pageInfo.config, labels: [...currentLabels, clone] }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to duplicate widget', status: 'error', duration: 3000 })
    }
  }

  // Sync a linked property across all widgets that share the same linkGroup.
  // linkedChanges: applied to every widget in the group (e.g. { symbol: 'AAPL' })
  // sourceId / sourceExtraChanges: non-linked per-source changes (e.g. { interval, outsideRTH })
  const handleLinkSync = async (groupId, linkedChanges, sourceId, sourceExtraChanges = {}) => {
    if (!pageInfo) return
    const applyToWidget = (w) => {
      if (w.linkGroup !== groupId) return w
      const changes = { ...linkedChanges }
      if (w.id === sourceId) Object.assign(changes, sourceExtraChanges)
      return { ...w, ...changes }
    }
    const updateWidgets = (widgets) => widgets.map(w => {
      const updated = applyToWidget(w)
      if (updated.children) return { ...updated, children: updateWidgets(updated.children) }
      return updated
    })
    try {
      const updatedLabels = updateWidgets(pageInfo.config?.labels || [])
      const updatedConfig = { ...pageInfo.config, labels: updatedLabels }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to sync linked widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  const handleUpdateLabel = async (labelId, changes) => {
    if (!pageInfo) return

    try {
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = currentLabels.map(l =>
        l.id === labelId ? { ...l, ...changes } : l
      )
      const updatedConfig = {
        ...pageInfo.config,
        labels: updatedLabels
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({
        title: 'Failed to update label',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  // Move a widget to a panel, root, or header
  // targetPanelId: null = root, 'header' = header bar, or panelId
  const handleMoveWidget = async (widgetId, sourcePanelId, targetPanelId) => {
    if (!pageInfo) return
    if (sourcePanelId === targetPanelId) return // No change
    if (widgetId === targetPanelId) return // Can't move panel into itself

    try {
      let currentLabels = [...(pageInfo.config?.labels || [])]
      let currentHeaderControls = [...(pageInfo.config?.headerControls || [])]
      let controlToMove = null

      // Find and remove the control from its source
      if (sourcePanelId === 'header') {
        // Moving from header
        controlToMove = currentHeaderControls.find(c => c.id === widgetId)
        if (!controlToMove) return
        currentHeaderControls = currentHeaderControls.filter(c => c.id !== widgetId)
      } else if (sourcePanelId === null) {
        // Moving from root
        controlToMove = currentLabels.find(l => l.id === widgetId)
        if (!controlToMove) return
        currentLabels = currentLabels.filter(l => l.id !== widgetId)
      } else {
        // Moving from a panel - find the panel and remove the control from its children
        currentLabels = currentLabels.map(l => {
          if (l.id === sourcePanelId && l.children) {
            controlToMove = l.children.find(c => c.id === widgetId)
            return { ...l, children: l.children.filter(c => c.id !== widgetId) }
          }
          return l
        })
      }

      if (!controlToMove) return

      // Reset position for the new container
      controlToMove = { ...controlToMove, x: 0, y: 0 }

      // Add the control to its target
      if (targetPanelId === 'header') {
        // Moving to header
        currentHeaderControls = [...currentHeaderControls, controlToMove]
      } else if (targetPanelId === null) {
        // Moving to root
        currentLabels = [...currentLabels, controlToMove]
      } else {
        // Moving to a panel
        currentLabels = currentLabels.map(l => {
          if (l.id === targetPanelId && l.type === 'panel') {
            return { ...l, children: [...(l.children || []), controlToMove] }
          }
          return l
        })
      }

      const updatedConfig = {
        ...pageInfo.config,
        labels: currentLabels,
        headerControls: currentHeaderControls
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)

      toast({
        title: 'Widget moved',
        status: 'success',
        duration: 2000
      })
    } catch (err) {
      toast({
        title: 'Failed to move widget',
        description: err.message,
        status: 'error',
        duration: 3000
      })
    }
  }

  const regenerateIds = (widget) => {
    const result = { ...widget, id: crypto.randomUUID() }
    if (result.children) result.children = result.children.map(regenerateIds)
    return result
  }

  const handleExportWidget = (widget) => {
    const exportData = { version: 1, control: widget }
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const name = widget.text || widget.symbol || widget.type || 'widget'
    a.download = `${name.toLowerCase().replace(/\s+/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportWidgetFileChange = async (e) => {
    const file = e.target.files[0]
    if (!file || !pageInfo) return
    e.target.value = ''
    if (isAtWidgetLimit()) { onLicenseOpen(); return }
    const reader = new FileReader()
    reader.onload = async (event) => {
      try {
        const data = JSON.parse(event.target.result)
        if (!data.control || typeof data.control !== 'object') {
          throw new Error('Invalid widget file — missing widget object')
        }
        const currentLabels = pageInfo.config?.labels || []
        const maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
        const widget = regenerateIds({ ...data.control, x: 0, y: maxY + 1 })
        const updatedConfig = { ...pageInfo.config, labels: [...currentLabels, widget] }
        const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
        setPageInfo(updatedRoute)
        toast({ title: 'Widget imported', status: 'success', duration: 2000 })
      } catch (err) {
        toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
      }
    }
    reader.readAsText(file)
  }

  const handleExport = () => {
    if (!pageInfo) return
    const exportData = {
      version: 1,
      name: pageInfo.name,
      path: pageInfo.path,
      config: pageInfo.config || {}
    }
    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${pageInfo.name.toLowerCase().replace(/\s+/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const handleImportFileChange = (e) => {
    const file = e.target.files[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (event) => {
      try {
        const data = JSON.parse(event.target.result)
        if (!data.config || typeof data.config !== 'object') {
          throw new Error('Invalid template file — missing config')
        }
        setPendingImport(data)
        onImportOpen()
      } catch (err) {
        toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
      } finally {
        e.target.value = ''
      }
    }
    reader.readAsText(file)
  }

  const handleImportConfirm = async () => {
    if (!pendingImport || !pageInfo) return
    setIsSaving(true)
    try {
      const updatedRoute = await updateRoute(pageInfo.id, { config: pendingImport.config })
      setPageInfo(updatedRoute)
      onImportClose()
      setPendingImport(null)
      toast({ title: 'Template imported', status: 'success', duration: 2000 })
    } catch (err) {
      toast({ title: 'Import failed', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const handleOpenInfo = () => {
    setInfoName(pageInfo?.name || '')
    setInfoPath(pageInfo?.path || '')
    onInfoOpen()
  }

  const handleSaveInfo = async () => {
    if (!pageInfo || !infoName.trim() || !infoPath.trim()) return
    setIsSaving(true)
    try {
      const updatedRoute = await updateRoute(pageInfo.id, { name: infoName.trim(), path: infoPath.trim() })
      setPageInfo(updatedRoute)
      setRoutesVersion(v => v + 1)
      onInfoClose()
      if (infoPath.trim() !== pageInfo.path) {
        navigate(infoPath.trim())
      }
      toast({ title: 'Template updated', status: 'success', duration: 2000 })
    } catch (err) {
      toast({ title: 'Failed to update template', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const handleDeletePage = async () => {
    if (!pageInfo) return
    setIsSaving(true)
    try {
      await deleteRoute(pageInfo.id)
      onDeletePageClose()
      navigate('/')
    } catch (err) {
      toast({ title: 'Failed to delete page', description: err.message, status: 'error', duration: 3000 })
    } finally {
      setIsSaving(false)
    }
  }

  const labels = pageInfo?.config?.labels || []
  const headerControls = pageInfo?.config?.headerControls || []

  const handleOpenSettingsFromTree = (id) => setOpenSettingsForId(id)
  const handleSettingsOpened = () => setOpenSettingsForId(null)

  const handleReorderWidgets = async (newOrderedIds, parentPanelId) => {
    if (!pageInfo) return
    try {
      let currentLabels = [...(pageInfo.config?.labels || [])]
      let currentHeaderControls = [...(pageInfo.config?.headerControls || [])]

      if (parentPanelId === 'header') {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentHeaderControls = currentHeaderControls.map(w =>
          idxMap.has(w.id) ? { ...w, x: idxMap.get(w.id) } : w
        )
      } else if (parentPanelId === null) {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentLabels = currentLabels.map(w =>
          idxMap.has(w.id) ? { ...w, y: idxMap.get(w.id) } : w
        )
      } else {
        const idxMap = new Map(newOrderedIds.map((id, i) => [id, i]))
        currentLabels = currentLabels.map(panel => {
          if (panel.id !== parentPanelId || !panel.children) return panel
          return {
            ...panel,
            children: panel.children.map(w =>
              idxMap.has(w.id) ? { ...w, y: idxMap.get(w.id) } : w
            )
          }
        })
      }

      const updatedConfig = { ...pageInfo.config, labels: currentLabels, headerControls: currentHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to reorder widgets', description: err.message, status: 'error', duration: 3000 })
    }
  }

  // Header widget handlers
  const handleDeleteHeaderWidget = async (widgetId) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.filter(c => c.id !== widgetId)
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to delete widget', status: 'error', duration: 3000 })
    }
  }

  const handleDuplicateHeaderWidget = async (widgetId) => {
    if (!pageInfo) return
    try {
      const original = headerControls.find(c => c.id === widgetId)
      if (!original) return
      const clone = { ...regenerateIds(original), x: (original.x || 0) + 0.5 }
      const updatedConfig = { ...pageInfo.config, headerControls: [...headerControls, clone] }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to duplicate widget', status: 'error', duration: 3000 })
    }
  }

  const handleDuplicateFromTree = async (widgetId) => {
    if (!pageInfo) return
    try {
      if (headerControls.some(c => c.id === widgetId)) {
        await handleDuplicateHeaderWidget(widgetId)
        return
      }
      const dupeInTree = (widgets) => {
        for (const w of widgets) {
          if (w.id === widgetId) {
            const clone = { ...regenerateIds(w), y: (w.y || 0) + 0.5 }
            return [...widgets, clone]
          }
          if (w.children) {
            const updatedChildren = dupeInTree(w.children)
            if (updatedChildren !== w.children) {
              return widgets.map(item => item.id === w.id ? { ...w, children: updatedChildren } : item)
            }
          }
        }
        return widgets
      }
      const currentLabels = pageInfo.config?.labels || []
      const updatedLabels = dupeInTree(currentLabels)
      if (updatedLabels !== currentLabels) {
        const updatedConfig = { ...pageInfo.config, labels: updatedLabels }
        const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
        setPageInfo(updatedRoute)
      }
    } catch (err) {
      toast({ title: 'Failed to duplicate widget', status: 'error', duration: 3000 })
    }
  }

  const handleUpdateHeaderWidget = async (widgetId, changes) => {
    if (!pageInfo) return
    try {
      const updatedHeaderControls = headerControls.map(c =>
        c.id === widgetId ? { ...c, ...changes } : c
      )
      const updatedConfig = { ...pageInfo.config, headerControls: updatedHeaderControls }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to update widget', status: 'error', duration: 3000 })
    }
  }

  // Render a header widget (simplified - inline, no absolute positioning)
  const renderHeaderWidget = (widget) => {
    const commonProps = {
      key: widget.id,
      label: widget,
      onDelete: handleDeleteHeaderWidget,
      onDuplicate: handleDuplicateHeaderWidget,
      onUpdate: handleUpdateHeaderWidget,
      onExport: handleExportWidget,
      onLinkSync: handleLinkSync,
      onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { widgetId: widget.id, mousePos } : null),
      onMoveWidget: handleMoveWidget,
      activeDrag: activeDrag,
      parentPanelId: 'header',
      isEditing: isEditing,
      isHeaderMode: true,
      containerRef: headerRef,
      openSettingsForId,
      onSettingsOpened: handleSettingsOpened,
    }

    const { key, ...restProps } = commonProps
    if (widget.type === 'badge') {
      return <TemplatePageBadge key={key} {...restProps} />
    } else if (widget.type === 'label') {
      return <TemplatePageLabel key={key} {...restProps} />
    } else if (widget.type === 'gauge') {
      return <TemplatePageGauge key={key} {...restProps} />
    }
    // Panels and charts not supported in header
    return null
  }

  // Flow layout: widgets fill left-to-right, wrapping to the next row when they overflow
  const calculateLayout = (widgets, preview, effectiveFillHeight, gap = 8) => {
    if (!widgets.length) return new Map()

    const withPreview = widgets.map(c =>
      preview && c.id === preview.id ? { ...c, y: preview.y } : c
    )
    const sorted = [...withPreview].sort((a, b) => (a.y || 0) - (b.y || 0))

    const positions = new Map()
    const TOTAL_SEGMENTS = 20
    let curX = 0, curY = 0, rowH = 0

    for (const widget of sorted) {
      const def = WIDGET_REGISTRY[widget.type] || WIDGET_REGISTRY.label
      const w = widget.dock === 'fill' ? TOTAL_SEGMENTS : (widget.width || def.width)
      const h = (widget.dock === 'fill' || widget.dock === 'fill-height') && effectiveFillHeight != null
        ? Math.floor(effectiveFillHeight / (widget.heightRows || 1))
        : (widget.height || def.height)

      if (curX > 0 && curX + w > TOTAL_SEGMENTS) {
        curY += rowH + gap
        curX = 0
        rowH = 0
      }

      positions.set(widget.id, { x: curX, y: curY })
      curX += w
      rowH = Math.max(rowH, h)
    }

    return positions
  }

  const adjustedFillHeight = computeAdjustedFillHeight(labels, fillHeight)
  const layoutPositions = calculateLayout(labels, dragPreview, adjustedFillHeight)

  return (
    <Box minH="100vh" bg="gray.900">
      <PageNavMenu
        headerRef={headerRef}
        headerControls={headerControls}
        activeDrag={activeDrag}
        onRenderWidget={renderHeaderWidget}
        refreshKey={routesVersion}
      >
        <Menu>
          <MenuButton
            as={IconButton}
            icon={<KebabIcon />}
            variant="ghost"
            size="sm"
            aria-label="Page options"
            _hover={{ bg: 'whiteAlpha.200' }}
            ml={1}
            isDisabled={!pageInfo}
          />
          <MenuList bg="gray.800" borderColor="gray.700" minW="160px">
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={handleOpenInfo}>
              Template information
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => setIsEditing(!isEditing)}>
              {isEditing ? 'Stop editing' : 'Edit layout'}
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => setIsTreeOpen(v => !v)}>
              {isTreeOpen ? 'Hide widget tree' : 'Widget tree'}
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={onOpen}>
              Add widget
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={handleExport}>
              Export template
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'gray.700' }} onClick={() => fileInputRef.current?.click()}>
              Import template
            </MenuItem>
            <MenuItem bg="gray.800" _hover={{ bg: 'red.900' }} color="red.400" onClick={onDeletePageOpen}>
              Delete page
            </MenuItem>
          </MenuList>
        </Menu>
        <input
          ref={fileInputRef}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={handleImportFileChange}
        />
        <input
          ref={widgetImportRef}
          type="file"
          accept=".json"
          style={{ display: 'none' }}
          onChange={handleImportWidgetFileChange}
        />
      </PageNavMenu>
      <Box pl={2} pr={0} py={3}>
        {pageInfo ? (
          <>
            <Box
              ref={containerRef}
              position="relative"
              width="100%"
              minH={isEditing ? '200px' : '100px'}
            >
              {labels.map((label) => {
                const flowPos = layoutPositions.get(label.id) || { x: 0, y: 0 }
                const effectiveLabel = label.dock === 'fill'
                  ? { ...label, x: 0, width: 20, ...(adjustedFillHeight != null ? { height: adjustedFillHeight } : {}) }
                  : label.dock === 'fill-height' && adjustedFillHeight != null
                  ? { ...label, x: flowPos.x, height: Math.floor(adjustedFillHeight / (label.heightRows || 1)) }
                  : { ...label, x: flowPos.x }
                return renderWidget(label, {
                key: label.id,
                label: effectiveLabel,
                layoutY: flowPos.y,
                onDelete: handleDeleteLabel,
                onDuplicate: handleDuplicateLabel,
                onUpdate: handleUpdateLabel,
                onExport: handleExportWidget,
                onLinkSync: handleLinkSync,
                onDragPreview: (pos) => setDragPreview(pos ? { id: label.id, ...pos } : null),
                onDragStateChange: (dragging, mousePos) => setActiveDrag(dragging ? { widgetId: label.id, mousePos } : null),
                onMoveWidget: handleMoveWidget,
                activeDrag: activeDrag,
                parentPanelId: null,
                isEditing: isEditing,
                containerRef: containerRef,
                openSettingsForId,
                onSettingsOpened: handleSettingsOpened,
              })
              })}
            </Box>
          </>
        ) : (
          <Text color="gray.500">{location.pathname}</Text>
        )}
      </Box>

      {isEditing && (
        <Box
          position="fixed"
          bottom={6}
          left="50%"
          transform="translateX(-50%)"
          bg="blue.900"
          borderWidth="1px"
          borderColor="blue.500"
          borderRadius="lg"
          px={5}
          py={3}
          display="flex"
          alignItems="center"
          gap={4}
          zIndex={1100}
          boxShadow="0 4px 24px rgba(0,0,0,0.5)"
        >
          <Text fontSize="sm" color="blue.200" fontWeight="medium">Edit mode</Text>
          <Button
            size="sm"
            colorScheme={isTreeOpen ? 'blue' : 'blue'}
            variant={isTreeOpen ? 'solid' : 'outline'}
            onClick={() => setIsTreeOpen(v => !v)}
          >
            {isTreeOpen ? 'Hide tree' : 'Widget tree'}
          </Button>
          <Button size="sm" colorScheme="blue" variant="solid" onClick={() => setIsEditing(false)}>
            Stop editing
          </Button>
        </Box>
      )}

      {isTreeOpen && (
        <WidgetTreePanel
          widgets={labels}
          headerWidgets={headerControls}
          topOffset={headerHeight}
          onClose={() => setIsTreeOpen(false)}
          onReorder={handleReorderWidgets}
          onOpenSettings={handleOpenSettingsFromTree}
          onDuplicate={handleDuplicateFromTree}
        />
      )}

      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Add Widget</ModalHeader>
          <ModalCloseButton />
          {addStage === 'pick' ? (
            <>
              <ModalBody>
                <VStack spacing={3}>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={() => setAddStage('new')}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">New</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Create a new widget from scratch</Text>
                    </VStack>
                  </Button>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={() => { onClose(); widgetImportRef.current?.click() }}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">Import from file</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Load a widget from a JSON file</Text>
                    </VStack>
                  </Button>
                  <Button
                    w="full"
                    variant="outline"
                    h="auto"
                    py={3}
                    px={5}
                    justifyContent="flex-start"
                    onClick={onGalleryOpen}
                  >
                    <VStack align="flex-start" spacing={0}>
                      <Text fontWeight="semibold">Widgets Gallery</Text>
                      <Text fontSize="xs" color="gray.400" fontWeight="normal">Browse and select from saved widgets</Text>
                    </VStack>
                  </Button>
                </VStack>
              </ModalBody>
              <ModalFooter>
                <Button variant="ghost" onClick={onClose}>Cancel</Button>
              </ModalFooter>
            </>
          ) : (
            <>
              <ModalBody>
                <VStack spacing={4}>
                  <FormControl>
                    <FormLabel fontSize="sm">Widget Type</FormLabel>
                    <Select
                      value={widgetType}
                      onChange={(e) => setWidgetType(e.target.value)}
                    >
                      {Object.entries(WIDGET_REGISTRY).map(([type, def]) => (
                        <option key={type} value={type}>{def.displayName}</option>
                      ))}
                    </Select>
                  </FormControl>
                  {WIDGET_REGISTRY[widgetType]?.needsText && (
                    <FormControl>
                      <FormLabel fontSize="sm">Label Text</FormLabel>
                      <Input
                        placeholder="Enter label text"
                        value={labelText}
                        onChange={(e) => setLabelText(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && labelText.trim()) {
                            handleAddWidget()
                          }
                        }}
                      />
                    </FormControl>
                  )}
                </VStack>
              </ModalBody>
              <ModalFooter>
                <Button variant="ghost" mr="auto" onClick={() => setAddStage('pick')}>Back</Button>
                <Button variant="ghost" mr={3} onClick={onClose}>Cancel</Button>
                <Button
                  colorScheme="blue"
                  onClick={handleAddWidget}
                  isLoading={isSaving}
                  isDisabled={WIDGET_REGISTRY[widgetType]?.needsText && !labelText.trim()}
                >
                  Add
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
      <WidgetsGalleryModal isOpen={isGalleryOpen} onClose={onGalleryClose} />
      <LicenseModal isOpen={isLicenseOpen} onClose={onLicenseClose} />
      <Modal isOpen={isImportOpen} onClose={onImportClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Import Template</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <Text>
              Import <strong>{pendingImport?.name || 'template'}</strong>?
            </Text>
            <Text mt={2} fontSize="sm" color="orange.300">
              This will replace all current widgets on this page.
            </Text>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={() => { onImportClose(); setPendingImport(null) }}>
              Cancel
            </Button>
            <Button colorScheme="blue" onClick={handleImportConfirm} isLoading={isSaving}>
              Import
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
      <Modal isOpen={isDeletePageOpen} onClose={onDeletePageClose} isCentered size="sm">
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Delete page</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <Text>Delete <strong>{pageInfo?.name}</strong>?</Text>
            <Text mt={2} fontSize="sm" color="red.300">This will permanently remove the page and all its widgets.</Text>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onDeletePageClose}>Cancel</Button>
            <Button colorScheme="red" onClick={handleDeletePage} isLoading={isSaving}>Delete</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
      <Modal isOpen={isInfoOpen} onClose={onInfoClose}>
        <ModalOverlay />
        <ModalContent bg="gray.800">
          <ModalHeader>Template information</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel fontSize="sm">Name</FormLabel>
                <Input
                  value={infoName}
                  onChange={(e) => setInfoName(e.target.value)}
                  placeholder="Template name"
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize="sm">Path</FormLabel>
                <Input
                  value={infoPath}
                  onChange={(e) => setInfoPath(e.target.value)}
                  placeholder="/my-page"
                />
              </FormControl>
            </VStack>
          </ModalBody>
          <ModalFooter>
            <Button variant="ghost" mr={3} onClick={onInfoClose}>Cancel</Button>
            <Button
              colorScheme="blue"
              onClick={handleSaveInfo}
              isLoading={isSaving}
              isDisabled={!infoName.trim() || !infoPath.trim()}
            >
              Save
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </Box>
  )
}

export default TemplatePage
