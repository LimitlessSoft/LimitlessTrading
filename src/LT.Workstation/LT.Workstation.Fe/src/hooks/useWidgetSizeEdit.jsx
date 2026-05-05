import { useState } from 'react'
import WidgetSizeFields from '../components/WidgetSizeFields'

export function useWidgetSizeEdit(widget, { defaultWidth, defaultHeight, minHeight }) {
  const [editWidth, setEditWidth] = useState(widget?.width || defaultWidth)
  const [editHeight, setEditHeight] = useState(widget?.height || defaultHeight)

  const initSize = () => {
    setEditWidth(widget?.width || defaultWidth)
    setEditHeight(widget?.height || defaultHeight)
  }

  const sizeUpdates = { width: editWidth, height: editHeight }

  const sizeFields = (
    <WidgetSizeFields
      width={editWidth}
      height={editHeight}
      minHeight={minHeight}
      onChange={({ width: w, height: h }) => { setEditWidth(w); setEditHeight(h) }}
    />
  )

  return { editWidth, editHeight, initSize, sizeUpdates, sizeFields }
}
