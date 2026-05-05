import { useState } from 'react'
import WidgetSizeFields from '../components/WidgetSizeFields'

export function useWidgetSizeEdit(widget, { defaultWidth, defaultHeight, minHeight }) {
  const [editWidth, setEditWidth] = useState(widget?.width || defaultWidth)
  const [editHeight, setEditHeight] = useState(widget?.height || defaultHeight)
  const [editDock, setEditDock] = useState(widget?.dock)

  const initSize = () => {
    setEditWidth(widget?.width || defaultWidth)
    setEditHeight(widget?.height || defaultHeight)
    setEditDock(widget?.dock)
  }

  const sizeUpdates = { width: editWidth, height: editHeight, dock: editDock }

  const sizeFields = (
    <WidgetSizeFields
      width={editWidth}
      height={editHeight}
      minHeight={minHeight}
      dock={editDock}
      onChange={({ width: w, height: h, dock: d }) => { setEditWidth(w); setEditHeight(h); setEditDock(d) }}
    />
  )

  return { editWidth, editHeight, editDock, initSize, sizeUpdates, sizeFields }
}
