# What's New

Add a "What's New" section that surfaces release notes / changelog entries to users. Entries are managed in the LT Public API (stored in Postgres) and exposed via a public, unauthenticated endpoint. Both the LT Web site and the LT Workstation frontend consume the same endpoint.

## Data model

```json
{
  "id": 1,
  "version": "0.2.0",
  "title": "Widgets Gallery",
  "body": "Browse and import community-shared widget layouts directly from the app.",
  "publishedAt": "2026-05-05T00:00:00Z",
  "tags": ["workstation", "widgets"]
}
```

- `version` — semver string; displayed as the entry heading
- `title` — short summary line
- `body` — Markdown-formatted release notes (rendered on the client)
- `publishedAt` — ISO 8601 UTC; entries are returned in descending order
- `tags` — optional labels (e.g. `"workstation"`, `"api"`, `"web"`) for future filtering

## Public API — `GET /api/whats-new`

No authentication required.

```
GET https://api.limitlessworkstation.com/api/whats-new?limit=10&offset=0
```

Response shape:

```json
[
  {
    "id": 1,
    "version": "0.2.0",
    "title": "Widgets Gallery",
    "body": "Browse and import community-shared widget layouts directly from the app.",
    "publishedAt": "2026-05-05T00:00:00Z",
    "tags": ["workstation", "widgets"]
  }
]
```

Query params: `limit` (default 10, max 50), `offset` (default 0) for pagination.

Entries are created manually by the team (no public POST endpoint needed for MVP). An admin `POST /api/whats-new` can be added later behind auth.

## LT Web integration

Add a "What's New" page at `/whats-new` (Astro SSR, fetches from the Public API at build time or on request). Each entry rendered as a card: version badge, title, date, and Markdown body.

## Workstation integration

Display the What's New section on the main page (`App.jsx` / `FedCountdown`). Fetch from the Public API on load.

Layout: a vertically stacked list of entry cards. Each card shows:
- Version badge + title on the same row
- Date (formatted as "May 5, 2026")
- Markdown body rendered as HTML

Show only the latest 5 entries by default; add a "See all" link that opens the LT Web `/whats-new` page in the system browser.

## Files

| File | Change |
|------|--------|
| `LT.Public.Api/Models/WhatsNewEntry.cs` | EF Core entity |
| `LT.Public.Api/Data/AppDbContext.cs` | Add `WhatsNewEntries` DbSet + migration |
| `LT.Public.Api/Controllers/WhatsNewController.cs` | `GET /api/whats-new` (public) |
| `LT.Public.Api/Migrations/` | EF Core migration via `dotnet ef` |
| `docs/public-api.md` | Document new endpoint |
| `src/LT.Workstation/LT.Workstation.Fe/src/pages/App.jsx` | Fetch + render What's New section |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/whats-new.astro` | New page on LT Web |
