# App Version Display

Show the application version in the UI so the user knows which build they are running. Use `package.json` as the single source of truth.

## Implementation

### 1. Expose the version from the Express backend

Add a lightweight endpoint so the frontend doesn't have to bundle the full `package.json`:

```js
// server/index.js (or a new routes/meta.js)
import { version } from '../../package.json' assert { type: 'json' }

app.get('/api/version', (_req, res) => res.json({ version }))
```

### 2. Fetch and display in the header

In `App.jsx`, fetch `/api/version` once on mount (no polling needed) and render the version string as muted text next to the `ARAnalysis` heading:

```jsx
<Heading size="lg" color="blue.300">ARAnalysis</Heading>
<Text fontSize="xs" color="gray.500" alignSelf="flex-end" mb={1}>
  v{appVersion}
</Text>
```

Keep it unobtrusive — small, muted, no tooltip needed.

### 3. Bump version before each release

Update `"version"` in `package.json` manually (or via `npm version patch/minor/major`) before tagging a release. No automation required for now.

## Files to Touch

| File | Change |
|------|--------|
| `anal/ARAnalysis/server/index.js` (or `routes/meta.js`) | Add `GET /api/version` endpoint |
| `anal/ARAnalysis/src/App.jsx` | Fetch version on mount, render beside heading |
