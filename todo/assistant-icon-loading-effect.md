# Assistant Icon Loading Effect

When the AI assistant is processing a response, add a visible loading effect around the assistant icon button in the nav bar so the user knows the assistant is working.

## Description

Currently the `AssistantIcon` button in `PageNavMenu` has no feedback when the assistant is actively generating a response. Add an animated ring, pulse glow, or spinning border around the icon that plays while `loading === true` inside `AIChatPanel`.

## Implementation notes

- `AIChatPanel` already has a `loading` boolean state (line ~428) that is `true` while the assistant is generating.
- `AIChatPanel` is rendered inside `PageNavMenu` (or alongside it via `main.jsx`) — thread the `loading` state up/out so `PageNavMenu` can read it. Options:
  - Lift `loading` to a shared parent and pass it as a prop to `PageNavMenu`.
  - Use a small Zustand slice or React context (`AssistantContext`) to broadcast it.
- In `PageNavMenu`, wrap the `<IconButton>` for the assistant in a `<Box pos="relative">` and render an animated overlay when loading.
- Suggested effect options (pick one):
  - **Spinning arc** — `Box` with `border: "2px solid"`, `borderColor: "blue.400 transparent transparent transparent"`, `borderRadius: "50%"`, CSS `@keyframes spin` animation, `position: absolute`, slightly larger than the button.
  - **Pulse glow** — CSS `@keyframes pulse` that fades a `boxShadow` (e.g. `0 0 0 4px rgba(66,153,225,0.5)`) in and out on the button's `borderRadius`.
  - **Conic gradient ring** — `background: conic-gradient(blue.400, transparent)` rotating around the icon; more visually rich.

## Files

| File | Change |
|------|--------|
| `src/components/AIChatPanel.jsx` | Expose `loading` state via context or lifted prop |
| `src/components/PageNavMenu.jsx` | Accept loading state; wrap assistant `IconButton` with animated ring/glow effect |
| `src/components/AssistantLoadingRing.jsx` *(optional)* | Isolated animated ring component if the effect is complex enough to extract |
