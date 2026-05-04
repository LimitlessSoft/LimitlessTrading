import { createContext, useContext, useRef } from 'react'

const WidgetContext = createContext(null)

export function WidgetProvider({ children }) {
  const callbackRef = useRef(null)
  const pageInfoRef = useRef(null)

  function setOnCreateWidget(fn, pageInfo) {
    callbackRef.current = fn
    pageInfoRef.current = pageInfo ?? null
  }

  function onCreateWidget(widgetDef) {
    callbackRef.current?.(widgetDef)
  }

  function getPageContext() {
    const pi = pageInfoRef.current
    if (!pi) return { type: null }
    return {
      type: 'template',
      name: pi.name || pi.path || 'Template',
      widgets: (pi.config?.labels || []).map(w => ({
        type: w.type,
        ...(w.text != null ? { text: w.text } : {}),
        ...(w.symbol != null ? { symbol: w.symbol } : {}),
        ...(w.placeholder != null ? { placeholder: w.placeholder } : {})
      }))
    }
  }

  return (
    <WidgetContext.Provider value={{ onCreateWidget, setOnCreateWidget, getPageContext }}>
      {children}
    </WidgetContext.Provider>
  )
}

export const useWidgetCallback = () => useContext(WidgetContext)
