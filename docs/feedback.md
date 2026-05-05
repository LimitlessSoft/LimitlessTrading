# Feedback Portal

The Feedback Portal lets licensed users submit feature requests, bug reports, and improvement ideas. Users can upvote items and leave threaded comments. Admins can manage item statuses and mark comments as official responses.

## Architecture

```
Frontend (React)  →  Workstation Node server  →  Public API (.NET)  →  PostgreSQL
                      /api/feedback proxy           /feedback endpoints    ltfeedback db
```

- **Public API** owns all data and business logic. It reads the `X-User-Email` header injected by the proxy to identify the caller.
- **Workstation Node server** proxies `/api/feedback/*` to the Public API, injecting the cached license email as `X-User-Email`.
- **Frontend** renders the list and detail pages under `/feedback` and `/feedback/:id`.

## API Routes (Public API)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/feedback` | List feedback; supports `?category=`, `?status=`, `?sort=votes\|date` |
| POST | `/feedback` | Create feedback (requires `X-User-Email`) |
| GET | `/feedback/:id` | Get single item with nested comments |
| PATCH | `/feedback/:id` | Update status (admin only) |
| POST | `/feedback/:id/upvote` | Toggle upvote (requires `X-User-Email`) |
| POST | `/feedback/:id/comments` | Add comment or reply (requires `X-User-Email`) |
| PATCH | `/feedback/:id/comments/:commentId` | Toggle `isOfficialResponse` (admin only) |

## Authorization Model

- **Read**: open to all (unauthenticated users see feedback but cannot upvote or comment)
- **Write** (create, upvote, comment): requires a valid `X-User-Email` header — set automatically when the user has an active license
- **Admin** (status changes, official responses): email must be listed in `Feedback:AdminEmails` config (comma-separated)

## Configuration

`appsettings.Development.json` / environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ltfeedback;Username=ltuser;Password=ltpass"
  },
  "Feedback": {
    "AdminEmails": "admin@example.com"
  }
}
```

## Local Development

PostgreSQL runs in Docker on port 5432:

```
Host: localhost:5432
Database: ltfeedback
Username: ltuser
Password: ltpass
```

EF Core migrations are applied automatically on startup (`db.Database.Migrate()`). To create new migrations manually:

```bash
cd src/LimitlessTrading/LimitlessTrading.Public/LT.Public.Api
dotnet ef migrations add <MigrationName> --project LT.Public.Api/LT.Public.Api.csproj
dotnet ef database update --project LT.Public.Api/LT.Public.Api.csproj
```

## Key Files

| File | Purpose |
|------|---------|
| `LT.Public.Api/Models/Feedback.cs` | Feedback entity |
| `LT.Public.Api/Models/FeedbackUpvote.cs` | Upvote join entity |
| `LT.Public.Api/Models/FeedbackComment.cs` | Comment entity (supports one level of nesting) |
| `LT.Public.Api/Data/AppDbContext.cs` | EF Core DbContext |
| `LT.Public.Api/Controllers/FeedbackController.cs` | All feedback endpoints |
| `LT.Workstation.Server/routes/feedback.js` | Node proxy for `/api/feedback` |
| `LT.Workstation.Fe/src/pages/FeedbackPage.jsx` | List page at `/feedback` |
| `LT.Workstation.Fe/src/pages/FeedbackDetailPage.jsx` | Detail page at `/feedback/:id` |
| `LT.Workstation.Fe/src/components/FeedbackCard.jsx` | Card with inline upvote |
| `LT.Workstation.Fe/src/components/FeedbackCommentThread.jsx` | Threaded comment UI |
| `LT.Workstation.Fe/src/components/NewFeedbackModal.jsx` | Submission modal |
