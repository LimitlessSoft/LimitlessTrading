import { createContext, useContext, useRef } from 'react'

const WidgetContext = createContext(null)

export function WidgetProvider({ children }) {
  const createRef = useRef(null)
  const updateRef = useRef(null)
  const deleteRef = useRef(null)
  const pageInfoRef = useRef(null)

  function setOnCreateWidget(fn, pageInfo) {
    createRef.current = fn
    updateRef.current = null
    deleteRef.current = null
    pageInfoRef.current = pageInfo ?? null
  }

  const batchCreateRef = useRef(null)
  const batchUpdateRef = useRef(null)
  const batchDeleteRef = useRef(null)
  const applyActionsRef = useRef(null)

  function setWidgetHandlers({ onCreate, onBatchCreate, onUpdate, onBatchUpdate, onDelete, onBatchDelete, onApplyActions }, pageInfo) {
    createRef.current = onCreate ?? null
    batchCreateRef.current = onBatchCreate ?? null
    updateRef.current = onUpdate ?? null
    batchUpdateRef.current = onBatchUpdate ?? null
    deleteRef.current = onDelete ?? null
    batchDeleteRef.current = onBatchDelete ?? null
    applyActionsRef.current = onApplyActions ?? null
    pageInfoRef.current = pageInfo ?? null
  }

  function onCreateWidget(widgetDef) { createRef.current?.(widgetDef) }
  function onBatchCreateWidgets(defs) { batchCreateRef.current?.(defs) }
  function onUpdateWidget(widgetId, changes) { updateRef.current?.(widgetId, changes) }
  function onBatchUpdateWidgets(updates) { batchUpdateRef.current?.(updates) }
  function onDeleteWidget(widgetId) { deleteRef.current?.(widgetId) }
  function onBatchDeleteWidgets(ids) { batchDeleteRef.current?.(ids) }
  function onApplyWidgetActions(actions) { applyActionsRef.current?.(actions) }

  function getPageContext() {
    const pi = pageInfoRef.current
    if (!pi) return { type: null }
    return {
      type: 'template',
      name: pi.name || pi.path || 'Template',
      widgets: (pi.config?.labels || []).map(w => ({
        id: w.id,
        type: w.type,
        width: w.width,
        height: w.height,
        ...(w.dock != null ? { dock: w.dock } : {}),
        ...(w.heightRows != null ? { heightRows: w.heightRows } : {}),
        ...(w.text != null ? { text: w.text } : {}),
        ...(w.symbol != null ? { symbol: w.symbol } : {}),
        ...(w.placeholder != null ? { placeholder: w.placeholder } : {})
      }))
    }
  }

  return (
    <WidgetContext.Provider value={{ onCreateWidget, onBatchCreateWidgets, onUpdateWidget, onBatchUpdateWidgets, onDeleteWidget, onBatchDeleteWidgets, onApplyWidgetActions, setOnCreateWidget, setWidgetHandlers, getPageContext }}>
      {children}
    </WidgetContext.Provider>
  )
}

export const useWidgetCallback = () => useContext(WidgetContext)
