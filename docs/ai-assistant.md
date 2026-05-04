# AI Assistant

A floating chat panel available on every page of ARAnalysis. It answers natural language questions about the user's account and live market data, and can create alerts on request.

## UI

The panel is rendered globally in `src/main.jsx` via `<AIChatPanel />`. It appears as a blue circular button fixed to the bottom-right corner of the screen. Clicking it opens a 360×480px chat panel.

The panel header has two icon buttons:
- **Gear** — switches to the settings view
- **X** — closes the panel

## Settings

Open with the gear icon. Settings are persisted server-side via `PUT /api/user-data/settings/:key`.

| Setting key | Description |
|-------------|-------------|
| `ai.provider` | Active provider (`claude_subscription` or `anthropic`) |
| `ai.model` | Model ID (e.g. `claude-sonnet-4-6`) |
| `ai.anthropic.apiKey` | API key, only used when provider is `anthropic` |

On first open, missing settings are auto-saved with defaults (`claude_subscription` + first model in the list).

## Providers

### Claude Subscription (`claude_subscription`)

Uses the local `claude` CLI (`~/.local/bin/claude`) as a subprocess. Requires Claude Code to be authenticated (`~/.claude/.credentials.json` must contain a valid `claudeAiOauth` token).

**How it works:**

1. Server reads credentials from `~/.claude/.credentials.json` and checks `expiresAt`
2. Before each request, the server pre-fetches live context data:
   - **Account summary** — always fetched (NetLiquidation, TotalCashValue, BuyingPower, etc.)
   - **Live quotes** — ticker symbols detected in the user's message (up to 5) are fetched
3. Context data is injected into the `--system-prompt` passed to the CLI
4. The CLI is invoked non-interactively:
   ```
   claude --print --output-format json --no-session-persistence \
     --tools "" --mcp-config '{"mcpServers":{}}' --strict-mcp-config \
     --model <model> --system-prompt <context> <message>
   ```
5. The JSON response (`result.result`) is parsed by `parseCliResponse()`, which extracts any `<create_alert>` blocks into `actions` and returns `{ text, actions }`

Conversation history is formatted as a plain transcript and prepended to the current message so the CLI has full context without native session persistence.

**Isolation flags:** `--tools ""` disables built-in tools. `--mcp-config '{"mcpServers":{}}'` + `--strict-mcp-config` block all MCP servers. `CLAUDECODE` and `CLAUDE_CODE_*` env vars are stripped from the subprocess environment to prevent the parent Claude Code session's tool context (Figma, LSP, etc.) from bleeding into the assistant.

**Alert creation:** The system prompt instructs the model to emit `<create_alert>{...}</create_alert>` blocks when asked to create an alert. `parseCliResponse()` extracts these, converts them to `createAlert()` configs via `buildAlertConfig()`, and returns them as `actions`. The block is stripped from the displayed text.

### Anthropic API (`anthropic`)

Uses the `@anthropic-ai/sdk` directly with a user-supplied API key from `console.anthropic.com`.

**How it works:**

Uses an agentic tool-use loop (`server/ai/providers/anthropic.js`):
1. Sends the conversation to the Anthropic API with all tools defined
2. If Claude calls a tool, the server executes it and sends the result back
3. `create_alert` calls are not executed server-side — the config is built via `buildAlertConfig()` and collected as a pending action
4. Loop continues until Claude returns a final text response, then returns `{ text, actions }`

Tools available in this mode:

| Tool | Description |
|------|-------------|
| `get_account_summary` | Fetches all account fields via `getAccountSummary()` |
| `get_live_quote` | Fetches a live quote for a ticker via `getDetailedQuote()` |
| `create_alert` | Creates a price/data alert for a ticker (executed client-side) |

## File Structure

```
anal/ARAnalysis/
├── src/components/AIChatPanel.jsx      # Floating chat UI + settings pane
├── server/routes/ai.js                 # POST /api/ai/chat, GET /api/ai/subscription-status
└── server/ai/
    ├── tools.js                        # Provider-agnostic tool definitions + implementations
    └── providers/
        ├── anthropic.js                # Anthropic SDK adapter (tool-use agentic loop)
        └── claude-cli.js               # Claude CLI subprocess adapter
```

## API

### `POST /api/ai/chat`

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

`pageContext.type` is `"template"` when on a template page, `null` otherwise. When `null`, the AI responds that it cannot add widgets. The field is optional — omit it when not on a template page.

**Response:**
```json
{
  "response": "Your net liquidation is $1.23M.",
  "actions": []
}
```

When the AI creates an alert, `actions` contains entries like:
```json
{
  "type": "create_alert",
  "config": {
    "label": "SPY above 500",
    "schedule": { "type": "expression", "expression": "@{SPY:last:30}", "operator": ">", "value": "500" },
    "actions": ["sound", "message"],
    "message": "SPY price is above 500"
  }
}
```
The frontend passes `config` directly to `createAlert()` from `alertService.js`.

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

Errors return `{ "error": "..." }` with an appropriate status code.

### `GET /api/ai/subscription-status`

Returns whether the Claude CLI OAuth token is valid.

```json
{ "valid": true, "subscriptionType": "max", "rateLimitTier": "default_claude_max_5x" }
```

## Available Models

Both providers currently expose the same Claude model list:

| ID | Label |
|----|-------|
| `claude-opus-4-7` | Claude Opus 4 |
| `claude-sonnet-4-6` | Claude Sonnet 4 |
| `claude-haiku-4-5-20251001` | Claude Haiku 4.5 |

## Adding a New Provider

1. Create `server/ai/providers/<name>.js` exporting `async function chat({ model, messages }): { text: string, actions: Action[] }`
2. Add the provider case to the `if/else` block in `server/routes/ai.js`
3. Add `{ id: '<name>', label: '...' }` to `PROVIDERS` in `AIChatPanel.jsx`
4. Add the provider's model list to `MODELS_BY_PROVIDER` in `AIChatPanel.jsx`
5. If the provider needs credentials, add a field to the settings pane conditionally on `provider === '<name>'`
