# AI Assistant — Create Template Widgets

Allow the AI assistant to add new widgets to the current template page in response to natural language requests (e.g. "add a label showing SPY last price", "create a VIX gauge", "add a graph tracking HYG and LQD").

## How it works

Follows the same pattern as `create_alert`: the AI emits a structured widget definition which the frontend applies to the current page.

### Claude CLI provider

Extend the system prompt so the model emits a block when asked to add a widget:

```
<create_widget>{"type":"label","text":"@{SPY:last:5}","width":4,"height":60,"style":{...}}</create_widget>
```

`parseCliResponse()` strips and collects `<create_widget>` blocks alongside the existing `<create_alert>` logic.

### Anthropic API provider

Add a `create_widget` tool to `server/ai/tools.js`. It is client-side only — not executed server-side. Config is collected as a pending action, identical to `create_alert`.

## Widget payload schema

A valid widget object minus `id` and `createdAt` (frontend generates those). All six types supported:

| Type | Required fields |
|------|----------------|
| `label` | `text`, `width`, `height`, `style.*` |
| `panel` | `width`, `height`, `style.*`, `children: []` |
| `badge` | `placeholder`, `width`, `height`, `style.*` |
| `gauge` | `placeholder`, `min`, `max`, `width`, `height`, `segments`, `title` |
| `graph` | `series[]`, `width`, `height`, `maxHistory`, `snapshotInterval` |
| `chart` | `symbol`, `width`, `height` |

The system prompt / tool description must include the full placeholder syntax (`@{SYMBOL:property:seconds}`, `#{field:seconds}`, `${name:seconds}`, `%[expr:seconds]`) so the model builds valid configs.

## Context injection

The frontend sends page context with every `/api/ai/chat` request so the AI knows what's on the page:

```json
{
  "message": "...",
  "history": [...],
  "pageContext": {
    "type": "template",
    "name": "Dashboard",
    "widgets": [{ "type": "label", "text": "@{SPY:last:5}", "y": 0 }]
  }
}
```

When the panel is open on a non-template page, `pageContext.type` is `null` and the AI responds that no template is active.

## Frontend wiring

`AIChatPanel` is mounted globally in `main.jsx`. Pass an `onCreateWidget(widgetDef)` callback prop from the active `TemplatePage` down — when on a non-template page the prop is `null`. The callback calls the existing `addWidget()` / `updateRoute()` path.

A React context or ref may be needed to thread the callback from `TemplatePage` up to the global panel mount.

## Files

| File | Change |
|------|--------|
| `src/components/AIChatPanel.jsx` | Accept `onCreateWidget` prop; send `pageContext` in POST body; handle `create_widget` actions |
| `src/pages/TemplatePage.jsx` | Pass `onCreateWidget` callback; supply current widget list |
| `src/main.jsx` | Thread callback from active page to `AIChatPanel` |
| `server/routes/ai.js` | Accept `pageContext` in body; inject into system prompt |
| `server/ai/providers/claude-cli.js` | Extend system prompt with widget schema + placeholder ref; parse `<create_widget>` blocks |
| `server/ai/providers/anthropic.js` | Add `create_widget` tool |
| `server/ai/tools.js` | Add provider-agnostic widget tool definition |
| `docs/ai-assistant.md` | Document the feature, pageContext fields, and create_widget schema |
