import { createContext, useContext, useRef } from 'react'

const WidgetContext = createContext(null)

export function WidgetProvider({ children }) {
  const callbackRef = useRef(null)

  function setOnCreateWidget(fn) {
    callbackRef.current = fn
  }

  function onCreateWidget(widgetDef) {
    callbackRef.current?.(widgetDef)
  }

  return (
    <WidgetContext.Provider value={{ onCreateWidget, setOnCreateWidget }}>
      {children}
    </WidgetContext.Provider>
  )
}

export const useWidgetCallback = () => useContext(WidgetContext)
