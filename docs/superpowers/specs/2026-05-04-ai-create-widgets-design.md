# AI Assistant — Create Template Widgets: Design

## Overview

Allow the AI assistant to add new widgets to the active template page in response to natural language requests (e.g. "add a label showing SPY last price", "create a VIX gauge"). Follows the same pattern as `create_alert`: the AI emits a structured widget definition which the frontend applies to the current page.

---

## 1. Context & Wiring

### Problem

`AIChatPanel` is mounted globally in `main.jsx` (outside any route), but widget creation must target the currently active `TemplatePage`. The callback must be registered and cleared as pages mount/unmount.

### Solution: React Context

A new `WidgetContext` module (`src/modules/WidgetContext.jsx`) provides:

- `WidgetProvider` — wraps the app in `main.jsx` (inside `LicenseProvider`)
- `useWidgetCallback()` — returns `{ onCreateWidget, setOnCreateWidget }`

**`TemplatePage`** calls `setOnCreateWidget(fn)` on mount, `setOnCreateWidget(null)` on unmount, where `fn` is `addWidgetFromAI`.

**`AIChatPanel`** reads `onCreateWidget` via `useWidgetCallback()`. When `null`, the page context sent to the server is `{ type: null }` and the AI responds that no template is active.

---

## 2. Backend Changes

### `POST /api/ai/chat` — new body field

```json
{
  "message": "...",
  "history": [...],
  "alerts": [...],
  "pageContext": {
    "type": "template",
    "name": "Dashboard",
    "widgets": [{ "type": "label", "text": "@{SPY:last:5}", "y": 0 }]
  }
}
```

`pageContext` is optional. When absent or `type` is `null`, no widget section is injected.

### `server/routes/ai.js`

Destructure `pageContext` from `req.body` and forward it to both provider `chat()` calls.

### `server/ai/providers/claude-cli.js`

`buildContext()` gains a `pageContext` parameter. When `pageContext.type === 'template'`, appends a `== CREATING WIDGETS ==` section to the system prompt containing:

- Full widget type schemas (all 6 types, required fields)
- Full placeholder syntax reference
- Current page name and widget list (so the AI knows what exists)
- Output format:

```
<create_widget>{"type":"label","text":"@{SPY:last:5}","width":4,"height":60,"style":{...}}</create_widget>
```

`parseCliResponse()` extracts `<create_widget>` blocks and returns them as `{ type: 'create_widget', widgetDef }` actions, exactly like `create_alert`.

### `server/ai/tools.js`

Adds a `create_widget` tool definition for the Anthropic provider. The `input_schema` is a JSON Schema object with a `type` discriminator and the union of all widget field sets. Client-side only — not executed server-side.

### `server/ai/providers/anthropic.js`

The `create_widget` tool call is collected as `{ type: 'create_widget', widgetDef: block.input }` in `pendingActions`, mirroring `create_alert` handling.

---

## 3. Frontend Action Handling

### `AIChatPanel`

- Imports `useWidgetCallback` from `WidgetContext`
- Sends `pageContext` in the `POST /api/ai/chat` body:
  - `{ type: 'template', name, widgets }` when `onCreateWidget` is non-null
  - `{ type: null }` when null
- `send()` gains a `create_widget` action case:

```js
if (action.type === 'create_widget') {
  if (onCreateWidget) {
    onCreateWidget(action.widgetDef)
    toast({ title: 'Widget added', status: 'success', duration: 3000, isClosable: true })
  }
}
```

### `TemplatePage`

Gains `addWidgetFromAI(widgetDef)`:

1. Generates `id` (crypto.randomUUID()) and `createdAt` (Date.now())
2. Computes `y` as `maxY + 1` from current labels
3. Appends to `pageInfo.config.labels` and calls `updateRoute()`, updating state on success

Registers this as the context callback on mount, clears on unmount.

---

## 4. Widget Schema in the System Prompt

The `== CREATING WIDGETS ==` section includes:

**Output format:**
```
<create_widget>
{"type":"label","text":"@{SPY:last:5}","width":4,"height":60,"style":{"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}}
</create_widget>
```

**Type-specific required fields:**

| Type | Required |
|------|----------|
| `label` | `text`, `width`, `height`, `style.*` |
| `panel` | `width`, `height`, `style.*`, `children: []` |
| `badge` | `placeholder`, `width`, `height`, `style.*` |
| `gauge` | `placeholder`, `min`, `max`, `width`, `height`, `segments`, `title` |
| `graph` | `series[]`, `width`, `height`, `maxHistory`, `snapshotInterval` |
| `chart` | `symbol`, `width`, `height` |

**Placeholder syntax:**
- `@{SYMBOL:property:seconds}` — live quote (last, bid, ask, change, changePercent, volume, etc.)
- `#{field:seconds}` — account summary (NetLiquidation, BuyingPower, etc.)
- `${name:seconds}` — system values (tws_connection_status, fomc_next, etc.)
- `%[expression:seconds]` — formula over live values

**Context injection:**
```
Current template page: "Dashboard"
Current widgets: [{"type":"label","text":"@{SPY:last:5}","y":0}, ...]
```

**Non-template guard:** When `pageContext.type` is null, the system prompt instructs the AI to respond: "I can only add widgets when a template page is open."

---

## 5. Files Changed

| File | Change |
|------|--------|
| `src/modules/WidgetContext.jsx` | New — context + provider + hook |
| `src/main.jsx` | Wrap app in `WidgetProvider` |
| `src/components/AIChatPanel.jsx` | Use `useWidgetCallback`; send `pageContext`; handle `create_widget` action |
| `src/pages/TemplatePage.jsx` | `addWidgetFromAI()`; register/clear context callback |
| `server/routes/ai.js` | Accept + forward `pageContext` |
| `server/ai/providers/claude-cli.js` | Widget schema section in system prompt; parse `<create_widget>` blocks |
| `server/ai/tools.js` | Add `create_widget` tool definition |
| `server/ai/providers/anthropic.js` | Handle `create_widget` tool call as pending action |
| `docs/ai-assistant.md` | Document `pageContext`, `create_widget` action, widget schema |
