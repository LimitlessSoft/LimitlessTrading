# AI Assistant — Copy Conversation to Clipboard

Add a button to the AI chat panel that copies the entire conversation history to the clipboard as plain text.

## UI

Add a copy icon button in the panel header, alongside the existing gear and X buttons. Only show it when there is at least one message in the conversation.

On click:
- Format the conversation as plain text (see below)
- Write to clipboard via `navigator.clipboard.writeText()`
- Show a brief confirmation — swap the copy icon for a checkmark for ~2 seconds, then revert

## Clipboard format

```
[User]
What is my net liquidation?

[Assistant]
Your net liquidation is $1.23M.

[User]
Set an alert when SPY crosses 500.

[Assistant]
Done — alert created for SPY above 500.
```

Each message separated by a blank line. No timestamps needed unless the user requests them later.

## Files

| File | Change |
|------|--------|
| `src/components/AIChatPanel.jsx` | Copy button in header + clipboard logic + checkmark feedback |
