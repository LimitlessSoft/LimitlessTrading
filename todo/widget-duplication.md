# Widget Duplication

Add a duplicate button to each widget in edit mode so the user can clone a widget (and all its children) without recreating it from scratch.

## Scope

- Top-level page widgets (`config.labels`)
- Panel children
- Header controls (`config.headerControls`)

## Implementation

### 1. Add `onDuplicate` prop to all widget components

All widget components (`TemplatePageLabel`, `TemplatePagePanel`, `TemplatePageBadge`, `TemplatePageChart`, `TemplatePageGauge`, `TemplatePageGraph`) already receive `onDelete`. Add `onDuplicate(id)` alongside it.

### 2. Show a duplicate button in edit mode

Place the duplicate button next to the existing delete button, gated behind `isEditing`. Use the same styling pattern as the delete button.

### 3. Wire up handlers in `TemplatePage.jsx`

`regenerateIds` (line 295) already deep-clones a widget tree with fresh UUIDs. Use it to implement:

```js
const handleDuplicateLabel = async (labelId) => {
  const original = pageInfo.config.labels.find(l => l.id === labelId)
  if (!original) return
  const clone = { ...regenerateIds(original), y: original.y + 0.5 }
  const updatedLabels = [...pageInfo.config.labels, clone]
  // persist via updateRoute, same pattern as handleAddWidget
}
```

The `y: original.y + 0.5` places the clone immediately after the original in the flow sort without shifting other widgets.

Similarly add `handleDuplicateHeaderWidget` for header controls.

For panel children, add `onDuplicate` to `TemplatePagePanel` and handle the clone internally (same `regenerateIds` + append to `children`, then call the parent `onUpdate`).

### 4. Widget Tree Panel

The tree panel's row hover menu currently shows a settings icon. Add a duplicate icon alongside it that calls `onDuplicate`.

## Files to Touch

| File | Change |
|------|--------|
| `TemplatePage.jsx` | `handleDuplicateLabel`, `handleDuplicateHeaderWidget`, pass `onDuplicate` to widget render |
| `TemplatePageLabel.jsx` | Add duplicate button in edit mode |
| `TemplatePagePanel.jsx` | Add duplicate button + `handleDuplicateChild` for children |
| `TemplatePageBadge.jsx` | Add duplicate button in edit mode |
| `TemplatePageChart.jsx` | Add duplicate button in edit mode |
| `TemplatePageGauge.jsx` | Add duplicate button in edit mode |
| `TemplatePageGraph.jsx` | Add duplicate button in edit mode |
| `WidgetTreePanel.jsx` | Add duplicate icon to row hover actions |
</content>
</invoke>