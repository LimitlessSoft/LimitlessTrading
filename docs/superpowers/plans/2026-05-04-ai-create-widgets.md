# AI Create Widgets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the AI assistant to add new widgets to the active template page via natural language requests, using a React Context to thread the callback from `TemplatePage` to the globally-mounted `AIChatPanel`.

**Architecture:** A new `WidgetContext` lets `TemplatePage` register an `addWidgetFromAI` callback that `AIChatPanel` calls when the AI emits a `create_widget` action. The server accepts an optional `pageContext` field in `POST /api/ai/chat` and injects widget schema + current page state into the system prompt (CLI provider) or adds a `create_widget` tool (Anthropic provider). Both providers return `{ type: 'create_widget', widgetDef }` actions.

**Tech Stack:** React 18, Chakra UI, Express, Anthropic SDK, Claude CLI subprocess. No test runner exists — verification steps use the dev server.

---

## File Map

| File | Action |
|------|--------|
| `src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx` | **Create** — context, provider, hook |
| `src/LT.Workstation/LT.Workstation.Fe/src/main.jsx` | **Modify** — wrap app in `WidgetProvider` |
| `src/LT.Workstation/LT.Workstation.Fe/src/pages/TemplatePage.jsx` | **Modify** — `addWidgetFromAI`, register/clear context callback |
| `src/LT.Workstation/LT.Workstation.Fe/src/components/AIChatPanel.jsx` | **Modify** — use context, send `pageContext`, handle `create_widget` action |
| `src/LT.Workstation/LT.Workstation.Server/routes/ai.js` | **Modify** — accept + forward `pageContext` |
| `src/LT.Workstation/LT.Workstation.Server/ai/tools.js` | **Modify** — add `create_widget` tool definition |
| `src/LT.Workstation/LT.Workstation.Server/ai/providers/anthropic.js` | **Modify** — handle `create_widget` tool call as pending action |
| `src/LT.Workstation/LT.Workstation.Server/ai/providers/claude-cli.js` | **Modify** — widget schema section + parse `<create_widget>` blocks |
| `docs/ai-assistant.md` | **Modify** — document `pageContext`, `create_widget` action, widget schema |

---

## Task 1: Create WidgetContext

**Files:**
- Create: `src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx`

- [ ] **Step 1: Create the context module**

Create `src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx`:

```jsx
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
```

Note: using a ref (not state) so registering/clearing the callback never triggers re-renders.

- [ ] **Step 2: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx
git commit -m "Add WidgetContext for AI widget callback"
```

---

## Task 2: Mount WidgetProvider in main.jsx

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Fe/src/main.jsx`

- [ ] **Step 1: Add the import**

In `src/LT.Workstation/LT.Workstation.Fe/src/main.jsx`, add after the `LicenseProvider` import line:

```jsx
import { WidgetProvider } from './modules/WidgetContext'
```

- [ ] **Step 2: Wrap the app**

Replace:
```jsx
        <LicenseProvider>
```
with:
```jsx
        <LicenseProvider>
          <WidgetProvider>
```

And replace the closing `</LicenseProvider>` with:
```jsx
          </WidgetProvider>
        </LicenseProvider>
```

The full render call should now look like:

```jsx
ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ChakraProvider theme={theme}>
      <LicenseProvider>
        <WidgetProvider>
          <BrowserRouter>
            <AutoConnect />
            <AlertOverlay />
            <AlertExpressionWatcher />
            <AIChatPanel />
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/docs/placeholders" element={<PlaceholderDocsPage />} />
              <Route path="*" element={<TemplatePage />} />
            </Routes>
          </BrowserRouter>
        </WidgetProvider>
      </LicenseProvider>
    </ChakraProvider>
  </React.StrictMode>
)
```

- [ ] **Step 3: Verify app still loads**

Run `npm run dev` in `src/LT.Workstation/LT.Workstation.Fe/` and confirm no console errors.

- [ ] **Step 4: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/main.jsx
git commit -m "Mount WidgetProvider in app root"
```

---

## Task 3: Update server route to accept pageContext

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Server/routes/ai.js`

- [ ] **Step 1: Destructure pageContext from request body and forward it**

In `router.post('/chat', ...)`, change the destructure line:

```js
const { message, history = [], alerts = [] } = req.body
```
to:
```js
const { message, history = [], alerts = [], pageContext = null } = req.body
```

Then update both provider calls to pass it through.

Change:
```js
    if (provider === 'claude_subscription') {
      result = await cliChat({ model, messages, alerts })
    } else if (provider === 'anthropic') {
      const apiKey = settings['ai.anthropic.apiKey']
      if (!apiKey) return res.status(400).json({ error: 'No API key configured. Open AI settings to add one.' })
      result = await anthropicChat({ apiKey, model, messages, alerts })
    }
```
to:
```js
    if (provider === 'claude_subscription') {
      result = await cliChat({ model, messages, alerts, pageContext })
    } else if (provider === 'anthropic') {
      const apiKey = settings['ai.anthropic.apiKey']
      if (!apiKey) return res.status(400).json({ error: 'No API key configured. Open AI settings to add one.' })
      result = await anthropicChat({ apiKey, model, messages, alerts, pageContext })
    }
```

- [ ] **Step 2: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Server/routes/ai.js
git commit -m "Forward pageContext through AI chat route"
```

---

## Task 4: Add create_widget tool definition

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Server/ai/tools.js`

- [ ] **Step 1: Append create_widget to TOOL_DEFINITIONS**

In `src/LT.Workstation/LT.Workstation.Server/ai/tools.js`, add to the `TOOL_DEFINITIONS` array after the `create_alert` entry:

```js
  {
    name: 'create_widget',
    description:
      'Add a new widget to the current template page. Only call this when the user explicitly ' +
      'asks to add, create, or place a widget. The widget appears immediately on the page. ' +
      'Use get_live_quote first to verify a ticker exists before using it in a widget.',
    parameters: {
      type: 'object',
      properties: {
        type: {
          type: 'string',
          enum: ['label', 'panel', 'badge', 'gauge', 'graph', 'chart'],
          description: 'Widget type'
        },
        text: {
          type: 'string',
          description: 'Label text with optional placeholders. Required for type=label. Example: "@{SPY:last:5}"'
        },
        symbol: {
          type: 'string',
          description: 'Ticker symbol. Required for type=chart. Example: "AAPL"'
        },
        placeholder: {
          type: 'string',
          description: 'Placeholder expression. Required for type=badge and type=gauge. Example: "@{VIX:last:5}"'
        },
        title: {
          type: 'string',
          description: 'Optional label shown below a gauge arc'
        },
        min: { type: 'number', description: 'Gauge minimum value. Required for type=gauge' },
        max: { type: 'number', description: 'Gauge maximum value. Required for type=gauge' },
        segments: {
          type: 'array',
          description: 'Gauge colored arc bands. Required for type=gauge',
          items: {
            type: 'object',
            properties: {
              from: { type: 'number' },
              to: { type: 'number' },
              color: { type: 'string', description: 'Hex color e.g. "#48BB78"' }
            },
            required: ['from', 'to', 'color']
          }
        },
        series: {
          type: 'array',
          description: 'Data series. Required for type=graph',
          items: {
            type: 'object',
            properties: {
              name: { type: 'string' },
              expression: { type: 'string', description: 'Placeholder expression, e.g. "@{SPY:last:5}"' },
              color: { type: 'string', description: 'Hex color' },
              viewType: { type: 'string', enum: ['line', 'bars'] }
            },
            required: ['name', 'expression', 'color']
          }
        },
        maxHistory: { type: 'number', description: 'Max data points for type=graph. Default 200' },
        snapshotInterval: { type: 'number', description: 'Fetch interval in seconds for snapshot series. Default 300' },
        width: { type: 'number', description: 'Width in grid segments (1-20). Default varies by type' },
        height: { type: 'number', description: 'Height in pixels. Default varies by type' },
        style: {
          type: 'object',
          description: 'Visual style overrides',
          properties: {
            bgColor: { type: 'string' },
            borderColor: { type: 'string' },
            borderWidth: { type: 'number' },
            textColor: { type: 'string' },
            fontWeight: { type: 'string', enum: ['normal', 'bold', 'lighter'] },
            fontStyle: { type: 'string', enum: ['normal', 'italic'] },
            fontSize: { type: 'number', description: 'Font size in rem' },
            textAlign: { type: 'string', enum: ['left', 'center', 'right'] },
            verticalAlign: { type: 'string', enum: ['top', 'center', 'bottom'] }
          }
        }
      },
      required: ['type']
    }
  }
```

- [ ] **Step 2: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Server/ai/tools.js
git commit -m "Add create_widget tool definition"
```

---

## Task 5: Handle create_widget in Anthropic provider

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Server/ai/providers/anthropic.js`

- [ ] **Step 1: Add pageContext parameter to chat() signature**

Change:
```js
export async function chat({ apiKey, model, messages, alerts = [] }) {
```
to:
```js
export async function chat({ apiKey, model, messages, alerts = [], pageContext = null }) {
```

- [ ] **Step 2: Update buildSystemPrompt to include page context**

Change:
```js
function buildSystemPrompt(alerts = []) {
  const alertSection = alerts.length > 0
    ? `\n\nActive Alerts (${alerts.length}):\n` + alerts.map(a => {
        const cond = formatAlertCondition(a.schedule)
        const status = a.enabled === false ? 'disabled' : 'enabled'
        return `  ${a.id} | ${a.label}${cond ? ` | ${cond}` : ''} | ${status}`
      }).join('\n')
    : '\n\nActive Alerts: none'
  return SYSTEM_PROMPT_BASE + alertSection
}
```
to:
```js
function buildSystemPrompt(alerts = [], pageContext = null) {
  const alertSection = alerts.length > 0
    ? `\n\nActive Alerts (${alerts.length}):\n` + alerts.map(a => {
        const cond = formatAlertCondition(a.schedule)
        const status = a.enabled === false ? 'disabled' : 'enabled'
        return `  ${a.id} | ${a.label}${cond ? ` | ${cond}` : ''} | ${status}`
      }).join('\n')
    : '\n\nActive Alerts: none'

  let pageSection = ''
  if (pageContext?.type === 'template') {
    const widgetSummary = (pageContext.widgets || [])
      .map(w => `  ${w.type}${w.text ? `: "${w.text}"` : w.symbol ? `: ${w.symbol}` : w.placeholder ? `: ${w.placeholder}` : ''}`)
      .join('\n')
    pageSection = `\n\nCurrent template page: "${pageContext.name}"\nExisting widgets:\n${widgetSummary || '  (none)'}`
  } else {
    pageSection = '\n\nCurrent page: not a template page — the create_widget tool is unavailable here. If the user asks to add a widget, tell them to navigate to a template page first.'
  }

  return SYSTEM_PROMPT_BASE + alertSection + pageSection
}
```

- [ ] **Step 3: Pass pageContext into buildSystemPrompt and handle create_widget tool call**

Change:
```js
    const response = await client.messages.create({
      model,
      max_tokens: 1024,
      system: buildSystemPrompt(alerts),
```
to:
```js
    const response = await client.messages.create({
      model,
      max_tokens: 1024,
      system: buildSystemPrompt(alerts, pageContext),
```

In the tool-use handling loop, add a `create_widget` case alongside the existing `create_alert` case:

```js
        if (block.name === 'create_alert') {
          const config = buildAlertConfig(block.input)
          pendingActions.push({ type: 'create_alert', config })
          result = { success: true, message: `Alert "${config.label}" will be created.` }
        } else if (block.name === 'delete_alert') {
          const { id, label } = block.input
          pendingActions.push({ type: 'delete_alert', id, label })
          result = { success: true, message: `Alert will be deleted.` }
        } else if (block.name === 'create_widget') {
          pendingActions.push({ type: 'create_widget', widgetDef: block.input })
          result = { success: true, message: `Widget of type "${block.input.type}" will be added to the page.` }
        } else {
```

- [ ] **Step 4: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Server/ai/providers/anthropic.js
git commit -m "Handle create_widget in Anthropic provider"
```

---

## Task 6: Handle create_widget in Claude CLI provider

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Server/ai/providers/claude-cli.js`

- [ ] **Step 1: Add pageContext parameter to chat() and buildContext()**

Change:
```js
async function buildContext(messages, alerts = []) {
```
to:
```js
async function buildContext(messages, alerts = [], pageContext = null) {
```

Change:
```js
export async function chat({ model, messages, alerts = [] }) {
  const [context, historyText] = await Promise.all([
    buildContext(messages, alerts),
```
to:
```js
export async function chat({ model, messages, alerts = [], pageContext = null }) {
  const [context, historyText] = await Promise.all([
    buildContext(messages, alerts, pageContext),
```

- [ ] **Step 2: Append widget schema section inside buildContext**

At the end of `buildContext`, before `return parts.join('\n\n')`, add:

```js
  // Widget creation context
  if (pageContext?.type === 'template') {
    const widgetSummary = (pageContext.widgets || [])
      .map(w => `  ${w.type}${w.text ? `: "${w.text}"` : w.symbol ? `: ${w.symbol}` : w.placeholder ? `: ${w.placeholder}` : ''}`)
      .join('\n')

    parts.push(
      `== CREATING WIDGETS ==\n` +
      `Current template page: "${pageContext.name}"\n` +
      `Existing widgets:\n${widgetSummary || '  (none)'}\n\n` +
      `When the user asks to add a widget, output a <create_widget> block. ` +
      `This is NOT a tool call — it is structured output the app reads. ` +
      `Output ONE block per widget. Strip the block from your response text.\n\n` +
      `Output format:\n` +
      `<create_widget>\n` +
      `{"type":"label","text":"@{SPY:last:5}","width":4,"height":60,"style":{"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}}\n` +
      `</create_widget>\n\n` +
      `Widget types and required fields:\n` +
      `  label   → text (string with placeholders), width, height, style\n` +
      `  badge   → placeholder, width, height, style\n` +
      `  gauge   → placeholder, min, max, segments ([{from,to,color}]), title, width, height\n` +
      `  graph   → series ([{name,expression,color,viewType}]), maxHistory, snapshotInterval, width, height\n` +
      `  chart   → symbol (ticker), width, height\n` +
      `  panel   → width, height, style, children:[]\n\n` +
      `Placeholder syntax:\n` +
      `  @{SYMBOL:property:seconds}  → live quote (last, bid, ask, change, changePercent, volume, vwap, open, high, low, close, prevClose, week52High, week52Low)\n` +
      `  @{SYMBOL:close-N:seconds}   → closing price N trading days ago\n` +
      `  #{field:seconds}            → account value (NetLiquidation, BuyingPower, TotalCashValue, UnrealizedPnL, DailyPnL, RealizedPnL, etc.)\n` +
      `  \${name:seconds}            → system value (tws_connection_status, fomc_next, fomc_countdown) or \${EXCHANGE:time|status:seconds}\n` +
      `  %[expression:seconds]       → formula e.g. %[@{HYG:last} - @{LQD:last}:30]\n\n` +
      `Default style: {"bgColor":"#2D3748","borderColor":"#4A5568","borderWidth":1,"textColor":"#FFFFFF","fontWeight":"normal","fontStyle":"normal","fontSize":1,"textAlign":"left","verticalAlign":"center"}\n\n` +
      `After the <create_widget> block, confirm the widget was added (type and what it shows).`
    )
  } else {
    parts.push(
      `== CREATING WIDGETS ==\n` +
      `The user is not currently on a template page. If they ask to add a widget, ` +
      `tell them to navigate to a template page first. Do NOT output any <create_widget> block.`
    )
  }
```

- [ ] **Step 3: Parse <create_widget> blocks in parseCliResponse**

Change `parseCliResponse` from:

```js
function parseCliResponse(raw) {
  const actions = []
  let text = raw.replace(/<create_alert>([\s\S]*?)<\/create_alert>/g, (_, json) => {
```

to:

```js
function parseCliResponse(raw) {
  const actions = []
  let text = raw.replace(/<create_widget>([\s\S]*?)<\/create_widget>/g, (_, json) => {
    try {
      const widgetDef = JSON.parse(json.trim())
      actions.push({ type: 'create_widget', widgetDef })
    } catch {}
    return ''
  })
  text = text.replace(/<create_alert>([\s\S]*?)<\/create_alert>/g, (_, json) => {
```

- [ ] **Step 4: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Server/ai/providers/claude-cli.js
git commit -m "Handle create_widget in Claude CLI provider"
```

---

## Task 7: Add addWidgetFromAI to TemplatePage

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Fe/src/pages/TemplatePage.jsx`

- [ ] **Step 1: Import useWidgetCallback and useEffect**

`useEffect` is already imported. Add the context import near the top with other module imports:

```jsx
import { useWidgetCallback } from '../modules/WidgetContext'
```

- [ ] **Step 2: Call useWidgetCallback inside the component**

Near the top of the `TemplatePage` function body, after the existing hooks:

```jsx
  const { setOnCreateWidget } = useWidgetCallback()
```

- [ ] **Step 3: Add addWidgetFromAI function**

Add this function inside the component, after `handleAddWidget`:

```jsx
  const addWidgetFromAI = async (widgetDef) => {
    if (!pageInfo) return
    try {
      const currentLabels = pageInfo.config?.labels || []
      const maxY = currentLabels.reduce((m, l) => Math.max(m, l.y || 0), -1)
      const newWidget = {
        id: crypto.randomUUID(),
        createdAt: Date.now(),
        x: 0,
        y: maxY + 1,
        ...widgetDef
      }
      const updatedConfig = {
        ...pageInfo.config,
        labels: [...currentLabels, newWidget]
      }
      const updatedRoute = await updateRoute(pageInfo.id, { config: updatedConfig })
      setPageInfo(updatedRoute)
    } catch (err) {
      toast({ title: 'Failed to add widget', description: err.message, status: 'error', duration: 3000 })
    }
  }
```

- [ ] **Step 4: Register and clear the callback**

Add a `useEffect` that registers `addWidgetFromAI` when `pageInfo` is available and clears it on unmount. Place it after the other `useEffect` blocks:

```jsx
  useEffect(() => {
    if (!pageInfo) return
    setOnCreateWidget(addWidgetFromAI)
    return () => setOnCreateWidget(null)
  }, [pageInfo])
```

- [ ] **Step 5: Verify dev server starts without errors**

Run `npm run dev` in `src/LT.Workstation/LT.Workstation.Fe/` and open a template page. No console errors expected.

- [ ] **Step 6: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/pages/TemplatePage.jsx
git commit -m "Add addWidgetFromAI and register WidgetContext callback"
```

---

## Task 8: Update AIChatPanel to send pageContext and handle create_widget

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Fe/src/components/AIChatPanel.jsx`

- [ ] **Step 1: Import useWidgetCallback and useLocation**

Add to the imports at the top:

```jsx
import { useWidgetCallback } from '../modules/WidgetContext'
import { useLocation } from 'react-router-dom'
```

`useLocation` is needed to read the current page name.

- [ ] **Step 2: Add hook calls inside AIChatPanel**

In the `AIChatPanel` component, after the existing hook calls, add:

```jsx
  const { onCreateWidget } = useWidgetCallback()
  const location = useLocation()
```

- [ ] **Step 3: Build pageContext from TemplatePage state**

The panel doesn't have direct access to `pageInfo`, but it knows whether it's on a template page via `onCreateWidget` being non-null. The widget list needs to come from the context too. Update `WidgetContext.jsx` to also store a `pageInfo` ref:

Open `src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx` and replace its entire content with:

```jsx
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
```

- [ ] **Step 4: Update TemplatePage to pass pageInfo to setOnCreateWidget**

In `src/LT.Workstation/LT.Workstation.Fe/src/pages/TemplatePage.jsx`, update the `useEffect` from Task 7:

```jsx
  useEffect(() => {
    if (!pageInfo) return
    setOnCreateWidget(addWidgetFromAI, pageInfo)
    return () => setOnCreateWidget(null, null)
  }, [pageInfo])
```

- [ ] **Step 5: Send pageContext in the POST body**

In `AIChatPanel`, update the `send()` function. The existing API call is:

```jsx
      const res = await api.post('/ai/chat', { message: text, history, alerts })
```

Change it to:

```jsx
      const pageContext = getPageContext()
      const res = await api.post('/ai/chat', { message: text, history, alerts, pageContext })
```

Add `getPageContext` to the destructure in the hook call:

```jsx
  const { onCreateWidget, setOnCreateWidget, getPageContext } = useWidgetCallback()
```

Wait — `AIChatPanel` only needs `onCreateWidget` and `getPageContext`, not `setOnCreateWidget`. Keep it as:

```jsx
  const { onCreateWidget, getPageContext } = useWidgetCallback()
```

- [ ] **Step 6: Handle create_widget action in send()**

In the `send()` function, in the `for (const action of res.data.actions || [])` loop, add after the existing `delete_alert` case:

```jsx
        } else if (action.type === 'create_widget') {
          if (onCreateWidget) {
            onCreateWidget(action.widgetDef)
            toast({ title: 'Widget added to page', status: 'success', duration: 3000, isClosable: true })
          }
        }
```

- [ ] **Step 7: Verify end-to-end manually**

1. Start both server and frontend dev servers (see `docs/development.md`)
2. Open a template page
3. Open the AI panel and type: "add a label showing SPY last price"
4. Confirm a new label widget appears on the page
5. Navigate to the home page, open AI panel, type the same request — confirm the AI responds it can't add widgets here

- [ ] **Step 8: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/modules/WidgetContext.jsx
git add src/LT.Workstation/LT.Workstation.Fe/src/pages/TemplatePage.jsx
git add src/LT.Workstation/LT.Workstation.Fe/src/components/AIChatPanel.jsx
git commit -m "Wire AI create_widget action through AIChatPanel"
```

---

## Task 9: Update docs/ai-assistant.md

**Files:**
- Modify: `docs/ai-assistant.md`

- [ ] **Step 1: Update the API section**

In `docs/ai-assistant.md`, update the `POST /api/ai/chat` request body documentation to include `pageContext`:

Find the **Body** block under `### POST /api/ai/chat` and replace it with:

```markdown
**Body:**
```json
{
  "message": "what is my net liquidation?",
  "history": [
    { "role": "user", "content": "..." },
    { "role": "assistant", "content": "..." }
  ],
  "pageContext": {
    "type": "template",
    "name": "Dashboard",
    "widgets": [{ "type": "label", "text": "@{SPY:last:5}" }]
  }
}
```

`pageContext.type` is `"template"` when on a template page, `null` otherwise. When null, the AI responds that it cannot add widgets. Omit `pageContext` entirely when not needed.
```

- [ ] **Step 2: Document the create_widget action**

In the **Response** section under `POST /api/ai/chat`, extend the `actions` documentation. After the `create_alert` example, add:

```markdown
When the AI creates a widget, `actions` contains:
```json
{
  "type": "create_widget",
  "widgetDef": {
    "type": "label",
    "text": "@{SPY:last:5}",
    "width": 4,
    "height": 60,
    "style": { "bgColor": "#2D3748", "borderColor": "#4A5568", "borderWidth": 1, "textColor": "#FFFFFF", "fontWeight": "normal", "fontStyle": "normal", "fontSize": 1, "textAlign": "left", "verticalAlign": "center" }
  }
}
```
The frontend passes `widgetDef` to `addWidgetFromAI()` in `TemplatePage`, which generates `id` and `createdAt` and appends the widget to the page.
```

- [ ] **Step 3: Commit**

```bash
git add docs/ai-assistant.md
git commit -m "Document pageContext and create_widget action in AI docs"
```

---

## Self-Review

**Spec coverage:**
- [x] `WidgetContext` (React Context, ref-based) → Task 1
- [x] `main.jsx` `WidgetProvider` mount → Task 2
- [x] Server route accepts + forwards `pageContext` → Task 3
- [x] `create_widget` tool definition → Task 4
- [x] Anthropic provider: `create_widget` pending action → Task 5
- [x] CLI provider: widget schema in system prompt + `<create_widget>` block parsing → Task 6
- [x] `TemplatePage`: `addWidgetFromAI` + register/clear callback → Task 7
- [x] `AIChatPanel`: sends `pageContext`, handles `create_widget` action → Task 8
- [x] `pageContext.type === null` guard (AI tells user to navigate to template page) → Tasks 5, 6, 8
- [x] Docs → Task 9

**Type consistency:**
- `setOnCreateWidget(fn, pageInfo)` — defined in Task 8 Step 3, used in Task 7 Step 4 ✓
- `getPageContext()` — defined in Task 8 Step 3, called in Task 8 Step 5 ✓
- `addWidgetFromAI(widgetDef)` — defined in Task 7 Step 3, registered in Task 7 Step 4 ✓
- `{ type: 'create_widget', widgetDef }` — produced by Tasks 5 and 6, consumed in Task 8 Step 6 ✓

**No placeholders:** All steps contain complete code. No TBDs.
