# News Widgets

Add widget types that display financial news on template pages. Requires a new backend news data source and new frontend widget components.

## Widget types

### `news` — Scrollable news feed

```js
{
  id: "uuid",
  type: "news",
  width: 8,
  height: 200,
  x: 0,
  y: 0,
  symbols: ["SPY", "AAPL"],  // empty = general market news
  maxItems: 10,
  refreshInterval: 300,       // seconds
  style: {
    bgColor: "#1a202c",
    borderColor: "#4A5568",
    borderWidth: 1,
    textColor: "#E2E8F0",
    fontSize: 0.85
  }
}
```

Each row: headline text, source, age ("2h ago"), optional sentiment badge (positive / negative / neutral). Clicking opens the article URL in a new tab.

### `news-ticker` — Scrolling marquee (stretch)

Single-line horizontally scrolling headline strip — useful as a narrow header widget.

```js
{
  id: "uuid",
  type: "news-ticker",
  width: 20,
  height: 40,
  symbols: [],
  refreshInterval: 300,
  style: { bgColor: "#1a202c", textColor: "#E2E8F0", fontSize: 0.85 }
}
```

Use CSS `@keyframes translateX`, not the deprecated `<marquee>` element.

## Backend — `GET /api/news`

`GET /api/news?symbols=SPY,AAPL&limit=10`

Caches results server-side for `refreshInterval` seconds. Response shape:

```json
[
  {
    "headline": "Fed holds rates steady",
    "source": "Reuters",
    "url": "https://...",
    "publishedAt": 1714900000,
    "sentiment": "neutral",
    "symbols": ["SPY"]
  }
]
```

### Data source options

| Source | Notes |
|--------|-------|
| **Finnhub** | Free tier: 60 req/min; company + market news; recommended starting point |
| **Alpha Vantage** | Free tier; `NEWS_SENTIMENT` endpoint includes sentiment scores |
| **IBKR** | `reqHistoricalNews` via existing TWS connection; requires a news subscription |

Recommended: **Finnhub** — free, reliable, JSON API, supports both symbol-specific and general market news.

## Settings dialog

- **Symbols** — comma-separated ticker input (empty = market news)
- **Max items** — number input (default 10)
- **Refresh every** — seconds (default 300)
- **Show sentiment badge** — toggle
- Standard style fields: background, border, text color, font size

## Registration

- Add `news` and `news-ticker` to the `TemplatePage.jsx` render switch and Add Widget modal
- Register in `WidgetTreePanel.jsx`: icon, color (orange suggested), snippet (`symbols.join(", ")` or `"Market news"`)
- If AI widget creation (see `ai-create-widgets.md`) is implemented first, add `news` to the `create_widget` tool schema

## Files

| File | Change |
|------|--------|
| `src/components/TemplatePageNews.jsx` | New feed widget component |
| `src/components/TemplatePageNewsTicker.jsx` | New ticker component (stretch) |
| `src/pages/TemplatePage.jsx` | Register new types in render switch + Add Widget modal |
| `src/components/WidgetTreePanel.jsx` | Icon, color, snippet for new types |
| `server/routes/news.js` | `GET /api/news` with caching |
| `server/index.js` | Mount news router |
| `docs/template-widgets.md` | Document new widget types and data structures |
