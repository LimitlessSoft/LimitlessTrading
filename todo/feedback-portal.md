# Feedback Portal

Add a feedback section to the Workstation app where licensed users can submit feature requests and bug reports, upvote existing entries, and discuss them via comments. Developers can then triage, respond, and track implementation status from the same UI.

## Features

### User-facing
- Submit new feedback (title, description, category: feature/bug/improvement)
- Upvote feedback items (one vote per user)
- Post threaded comments on any feedback item
- See current status of each item (open, in-review, planned, done, rejected)
- Search and filter by category, status, or most-voted

### Developer/admin-facing
- Change status of any feedback item
- Pin a reply as the official developer response
- Merge duplicate feedback items
- See submitter identity (email/username)

## Data model

### `Feedback`
| Field | Type | Notes |
|-------|------|-------|
| `id` | uuid | |
| `title` | string | |
| `description` | string | |
| `category` | enum | `feature`, `bug`, `improvement` |
| `status` | enum | `open`, `in-review`, `planned`, `done`, `rejected` |
| `authorId` | string | user identifier from license |
| `upvotes` | int | cached count |
| `createdAt` | datetime | |
| `updatedAt` | datetime | |

### `FeedbackUpvote`
| Field | Type | Notes |
|-------|------|-------|
| `id` | uuid | |
| `feedbackId` | uuid | FK → Feedback |
| `userId` | string | one vote per user per item |
| `createdAt` | datetime | |

### `FeedbackComment`
| Field | Type | Notes |
|-------|------|-------|
| `id` | uuid | |
| `feedbackId` | uuid | FK → Feedback |
| `parentCommentId` | uuid? | null = top-level; non-null = reply |
| `authorId` | string | |
| `isOfficialResponse` | bool | developer pin |
| `body` | string | |
| `createdAt` | datetime | |

## API routes

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/feedback` | List feedback; supports `?category=`, `?status=`, `?sort=votes\|date` |
| `POST` | `/api/feedback` | Submit new feedback |
| `GET` | `/api/feedback/:id` | Single item with comments |
| `PATCH` | `/api/feedback/:id` | Update status / merge (admin only) |
| `POST` | `/api/feedback/:id/upvote` | Toggle upvote for calling user |
| `POST` | `/api/feedback/:id/comments` | Add comment or reply |
| `PATCH` | `/api/feedback/:id/comments/:commentId` | Edit or pin comment (admin only) |

## UI layout

### Feedback list page (`/feedback`)
- Sortable card list: title, category badge, status badge, upvote count
- Upvote button on each card (toggleable, shows own vote state)
- "New Feedback" button → modal form
- Sidebar filters: category, status

### Feedback detail page (`/feedback/:id`)
- Full description at top
- Status banner (prominent when planned/done)
- Upvote button
- Threaded comment section — official developer responses highlighted

## Authorization

- Any licensed user can read, submit, upvote, and comment
- Admin/developer role required to change status, pin comments, or merge items
- Users can only edit/delete their own comments

## Files

| File | Change |
|------|--------|
| `server/models/Feedback.js` | Sequelize model |
| `server/models/FeedbackUpvote.js` | Sequelize model |
| `server/models/FeedbackComment.js` | Sequelize model |
| `server/routes/feedback.js` | All `/api/feedback` routes |
| `server/index.js` | Mount feedback router |
| `src/pages/FeedbackPage.jsx` | List page with filters |
| `src/pages/FeedbackDetailPage.jsx` | Detail + comment thread |
| `src/components/FeedbackCard.jsx` | Card component used in list |
| `src/components/FeedbackCommentThread.jsx` | Threaded comment component |
| `src/components/NewFeedbackModal.jsx` | Submission form modal |
| `src/router.jsx` | Add `/feedback` and `/feedback/:id` routes |
| `docs/feedback.md` | Feature documentation |
