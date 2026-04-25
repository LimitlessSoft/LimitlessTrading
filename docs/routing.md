# Routing & Navigation

## Overview

React Router is set up with a catch-all pattern. The main page (`/`) renders `App.jsx`; all other paths render `TemplatePage.jsx`.

```jsx
// src/main.jsx
<BrowserRouter>
  <Routes>
    <Route path="/" element={<App />} />
    <Route path="*" element={<TemplatePage />} />
  </Routes>
</BrowserRouter>
```

## PageNavMenu

**Location:** `src/components/PageNavMenu.jsx`

Sticky header bar shared by `FedCountdown` (main page) and `TemplatePage` (custom pages).

**Props:**
- `children` — content rendered after the hamburger menu
- `bg` — background color (default: `gray.800`)
- `borderColor` — bottom border color (default: `gray.700`)

**Features:**
- Sticky at top (`zIndex: 1000`)
- Hamburger menu: Main Page link, list of custom routes, Add New Page modal

```jsx
// Used in FedCountdown
<PageNavMenu bg={style.bg} borderColor={style.borderColor}>
  {/* countdown content */}
  <Spacer />
  {/* right-side indicators */}
</PageNavMenu>

// Used in TemplatePage
<PageNavMenu />
```

## TemplatePage

**Location:** `src/pages/TemplatePage.jsx`

Renders all custom/dynamic pages. Reads the current path, fetches matching route from the backend, and renders its controls.

**Behavior:**
1. Reads current path via `useLocation()`
2. Fetches all routes via `fetchRoutes()`
3. Finds matching route by path
4. Renders controls from `route.config.labels`; shows raw path if no route found

**Route object shape:**
```js
{
  id: "uuid",
  path: "/custom-page",
  name: "Custom Page",
  createdAt: 1706000000000,
  updatedAt: 1706000000000,
  config: {
    labels: []  // array of control objects
  }
}
```

## Routes API

**Backend:** `server/routes/routes.js`  
**Frontend client:** `src/modules/routesService.js`  
**Storage:** `server/data/routes-config.json` (auto-created, gitignored)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/routes` | List all routes |
| POST | `/api/routes` | Create route `{ name, path, config? }` |
| PUT | `/api/routes/:id` | Update route |
| DELETE | `/api/routes/:id` | Delete route |

**Frontend functions:**
- `fetchRoutes()` — get all routes
- `createRoute(name, path)` — create new route
- `updateRoute(id, updates)` — update route
- `deleteRoute(id)` — delete route
