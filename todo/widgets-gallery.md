# Widgets Gallery

Add a public-facing widgets gallery page served by the LT Public API that showcases all available template widget types. Now that the API is live at `api.limitlessworkstation.com`, it can serve gallery data independently of the Node/Express backend.

## Overview

A gallery page (hosted in LT Web or as a standalone route) lists every widget type with a name, description, preview screenshot or live preview, and the JSON config snippet needed to add it to a template. This lowers the barrier to discovering and using widgets.

## Backend — `GET /api/widgets`

Add a new endpoint to `LT.Public.Api` that returns the catalog of available widget types:

```
GET https://api.limitlessworkstation.com/api/widgets
```

Response shape:

```json
[
  {
    "type": "chart",
    "name": "Chart",
    "description": "Candlestick / line chart with drawing tools and indicators.",
    "tags": ["price", "technicals"],
    "previewImageUrl": "/gallery/previews/chart.png",
    "configSnippet": {
      "type": "chart",
      "symbol": "SPY",
      "width": 12,
      "height": 400
    }
  }
]
```

The catalog can be static (hardcoded in a `WidgetCatalogService`) to start — no database reads needed.

## Frontend — Gallery page

A new page in LT Web (e.g. `/gallery`) that:

- Fetches `GET /api/widgets` from the Public API
- Renders a card grid — one card per widget type
- Each card shows: name, tags, description, preview image, and a "Copy config" button that copies the JSON snippet to the clipboard
- Supports basic filtering by tag (price, news, portfolio, tools, etc.)
- Links to the relevant section of `docs/template-widgets.md` if hosted publicly

## Widget types to catalog

| Type | Tags |
|------|------|
| `chart` | price, technicals |
| `watchlist` | price |
| `positions` | portfolio |
| `options-chain` | options |
| `news` | news |
| `news-ticker` | news |
| `clock` | tools |
| `notes` | tools |

Add any additional types registered in `TemplatePage.jsx`.

## Files

| File | Change |
|------|--------|
| `LT.Public.Api/Controllers/WidgetsController.cs` | New controller — `GET /api/widgets` |
| `LT.Public.Api/Services/WidgetCatalogService.cs` | Static widget catalog data |
| `LT.Public.Api/Models/WidgetEntry.cs` | Response model |
| `public/gallery/previews/*.png` | Preview screenshots per widget type |
| `src/pages/GalleryPage.jsx` | New gallery page |
| `src/components/WidgetGalleryCard.jsx` | Individual widget card component |
| `src/App.jsx` | Register `/gallery` route |
| `docs/public-api.md` | Document new `GET /api/widgets` endpoint |
