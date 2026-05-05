# Widget Hook Pattern

When adding shared behavior to all widget components, use a custom React hook rather than repeating logic in each component file. This is the JavaScript equivalent of a C# interface — the hook defines a contract, and any widget that calls it gets the behavior.

## When to use this pattern

Use it when:
- The same state, initialization, and render logic would otherwise be duplicated across multiple widget components (`TemplatePageLabel`, `TemplatePageBadge`, `TemplatePageGauge`, `TemplatePageGraph`, `TemplatePageChart`, `TemplatePagePanel`)
- The widget needs to opt in with per-widget configuration (e.g. different defaults per type)
- The behavior involves both state and JSX output

## Structure of a widget hook

A widget hook lives in `src/hooks/` and returns three things:

| Return value | Purpose |
|---|---|
| `initX()` | Call when the settings modal opens — resets edit state from the current widget config |
| `xUpdates` | Spread into the `onUpdate()` call in the save handler |
| `xFields` | JSX to drop into the modal body — renders the UI for editing |

Example — `useWidgetSizeEdit.jsx`:

```jsx
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
```

## Using the hook in a widget component

Three integration points per widget:

**1. Call the hook** — near the top of the component function, alongside other state:

```jsx
const { initSize, sizeUpdates, sizeFields } = useWidgetSizeEdit(label, {
  defaultWidth: DEFAULT_WIDTH,
  defaultHeight: DEFAULT_HEIGHT,
  minHeight: MIN_HEIGHT
})
```

**2. Initialize on modal open** — call `initSize()` inside the `handleOpenEdit` / `handleOpenSettings` function:

```jsx
const handleOpenEdit = () => {
  setEditConfig({ ... })
  initSize()
  onOpen()
}
```

**3. Save and render** — spread `xUpdates` into `onUpdate()`, and drop `{xFields}` into the modal body:

```jsx
// In the save handler:
onUpdate(label.id, { ...existingFields, ...sizeUpdates })

// In the modal body JSX:
<VStack spacing={4}>
  {/* ... existing fields ... */}
  {sizeFields}
</VStack>
```

## Shared UI components

If the hook renders JSX, extract the UI into a standalone component in `src/components/` and import it inside the hook. This keeps the hook focused on state management and lets the UI component be tested or restyled independently.

Example: `WidgetSizeFields.jsx` is the UI component; `useWidgetSizeEdit.jsx` is the hook that wires state to it.

## File naming

| File type | Location | Extension |
|---|---|---|
| Hook that returns only values/callbacks | `src/hooks/` | `.js` |
| Hook that returns JSX | `src/hooks/` | `.jsx` |
| Shared UI component | `src/components/` | `.jsx` |

## Existing widget hooks

| Hook | Returns | Used for |
|---|---|---|
| `useWidgetSizeEdit` | `initSize`, `sizeUpdates`, `sizeFields` | Width/height inputs in every widget settings modal |
| `useDeleteConfirm` | `requestDelete`, `confirmDeleteDialog` | Confirmation dialog before deleting a widget |
| `useWidgetResolveText` | `resolveText` | Evaluates placeholder expressions in label/badge text |
