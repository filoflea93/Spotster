# Spotster

**Free parking spots, shared by the community.**  
Web app to report available spaces, publish parking requests, and collaborate in real time.  
Stack: **ASP.NET Core 8**, **Entity Framework Core**, **SQL Server**, **SignalR**, **vanilla JS** (ES modules) frontend + **Leaflet**.

## Quick start

Open **`Spotster.sln`** in Visual Studio.

```bash
dotnet run --project Spotster.csproj
```

| URL | Description |
|-----|-------------|
| http://localhost:5124 | Application |
| http://localhost:5124/presentazione.html | Product presentation (IT/EN) |

## Demo credentials

On first startup, demo users are created (if not already present):

| Username | Password |
|----------|----------|
| demo1    | demo123  |
| demo2    | demo123  |
| demo3    | demo123  |

Register a new account from the login screen if you prefer not to use the demo users.

## Registration

New accounts are created from the **Registrati** tab on the login screen.

**Required fields:** first name, last name, email, date of birth (minimum age **18**), username, password (min. **6** characters).

**Flow:**

1. `POST /api/auth/register` — account is created with `EmailConfirmed = false` (no JWT issued).
2. A confirmation email is sent (SMTP required in production).
3. User clicks the link → `GET /api/auth/confirm-email?userId=&code=` → redirect to `/?emailConfirmed=1`.
4. User logs in via `POST /api/auth/login` — JWT access + refresh tokens are returned only after email confirmation.

If login fails with *email not confirmed*, the UI shows **Resend confirmation email** (`POST /api/auth/resend-confirmation`).

**Local development without SMTP:** registration still succeeds; the confirmation link is written to the application log (see `EmailSender`). Copy the link from the console to confirm the account.

Demo users (`demo1`–`demo3`) skip email confirmation and can log in immediately.

## Main features

### Map and dashboard

- **Location permission** overlay on first login: grant GPS or **pick a point on the map**
- Full-screen **Leaflet** map with user GPS position (blue pin marker with **person icon**)
- **Collapsible sidebar** (state persisted in `localStorage`) with **Available** (green reports) and **Requests** (blue listings) tabs
- **Center on my location** button above the FAB to recenter the map
- **Visible zone** dropdown (500 m – 5 km) to filter list and map
- Request filters: **All** / **Open** / **In negotiation** (requests in negotiation hide the radius circle)
- **FAB** with quick actions: report free parking / search parking
- Layout adapts to **zoom** and top bar; **modals** center on the map area (excluding the sidebar)
- **Mobile-first** UI, bilingual **IT / EN**

### Free parking reports

- Mandatory **photo** upload (JPG, PNG, WEBP — max 5 MB)
- Automatic GPS position, aggregation into **virtual zones**
- **Detail modal** from marker or list: photo, reliability, time remaining
- **Community vote** (confirm / deny) with reliability score and “already voted” state
- Automatic expiration (TTL) and background cleanup service
- Delete your own listing

### Parking requests

- **Geocoding** with address autocomplete and radius preview on the map
- Configurable search radius (200 m – 5 km)
- **Optional reward** (up to €100) with payment methods (cash, Satispay, PayPal, bank transfer, Revolut, other) or **no reward**
- Edit, renew, and delete your own listing
- **Auto-fulfillment** when a free parking report is posted near the request

### Integrated chat

- Text messages per request
- **GPS location** sharing (flag on map, location in chat)
- **Photo** sharing in chat; location/photo buttons next to send
- **Multi-conversation** for listing owners (compact thread sidebar)
- **In negotiation** status (owner only, after chat exchange): other participants can no longer write
- **Block** and **unblock** users by the owner
- **Large, responsive** chat modal (height/width proportional to screen)
- Live negotiation/block status updates in the conversation sidebar

### Profile and reputation

- **Profile photo** (upload / remove)
- Change password
- **Star reviews** (1–5) between users who chatted on the same request
- User search from the top bar
- Public profile with received reviews

### Security and infrastructure

- **Email confirmation** required before first login (`RequireConfirmedEmail`); registration does **not** return tokens — only login after confirmation does; resend available from UI and API; bilingual confirmation email (IT/EN)
- Anti-fraud: rate limiting on reports, geo anti-spoofing, temporary **account suspensions**
- **Distributed cache** (Redis when configured, in-memory fallback) for active listings and IP rate limits
- **SQL Server spatial queries** (`geography`) for nearby parking and requests
- **SignalR** with map **grid groups** (~1 km) and per-request chat groups — no global broadcast storm
- Optional **Redis backplane** for multi-instance SignalR
- Optional **Azure Blob Storage** for photos (local disk fallback in development)
- **API rate limiting**: auth, geocoding, and write endpoints
- Server-side localization (`.resx` resources IT/EN)
- **JWT authentication** (ASP.NET Identity): access token + refresh token

## Frontend architecture

The SPA in `wwwroot/` uses **ES modules** (no bundler). Entry point: `wwwroot/js/app.js` → `app/main.js`.

```
wwwroot/js/
├── i18n.js                 ← client-side translations (IT/EN)
├── core/
│   ├── api.js              ← JWT, fetch helpers
│   ├── constants.js, state.js, modals.js, dom.js
│   ├── i18n-bridge.js      ← t(), prop(), translateError
│   └── map-engine.js       ← map abstraction (swap adapter here)
├── adapters/
│   └── leaflet-adapter.js  ← only file that calls Leaflet (L.*) directly
├── features/
│   ├── auth.js, map.js, parkings.js, requests.js, report.js
│   ├── search.js, vote.js, signalr.js, profile.js, fab.js
└── app/
    ├── main.js             ← module registration + shared hub
    └── bootstrap.js        ← global events, language change
```

Modules share a **`hub`** object (dependency injection by convention): each `register*(hub, t)` exposes functions used across features.

## Product presentation

Standalone file at `wwwroot/presentazione.html`: visual overview of all features, Spotster styling, IT/EN language switch.

## Main API

### Authentication (JWT)

Protected requests require the header:

```
Authorization: Bearer {accessToken}
```

**Login** (`POST /api/auth/login`) returns tokens only if the email is confirmed:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "accessTokenExpiresAt": "2026-06-25T15:18:42.081Z",
  "user": {
    "userId": "...",
    "username": "demo1",
    "reputationScore": 65,
    "accuracyRate": 1,
    "status": "Active",
    "suspendedUntil": null,
    "suspiciousScore": 0,
    "profilePhotoUrl": null
  }
}
```

**Registration** (`POST /api/auth/register`) returns a pending message (no tokens):

```json
{
  "message": "Registration complete. Check your email and click the link to activate your account.",
  "email": "f***o@gmail.com"
}
```

| Endpoint | Auth | Description |
|----------|------|-------------|
| `POST /api/auth/register` | No | Create account (first name, last name, email, date of birth, username, password). Sends confirmation email. Returns message + masked email — **no JWT** |
| `POST /api/auth/login` | No | Login (email must be confirmed). Returns access + refresh tokens |
| `POST /api/auth/refresh` | No | Body: `{ "refreshToken": "..." }` — new access + refresh (rotation) |
| `POST /api/auth/logout` | Yes | Body: `{ "refreshToken": "..." }` — revoke refresh token |
| `GET /api/auth/confirm-email` | No | Email confirmation link (query: `userId`, `code`). Redirects to `/?emailConfirmed=1` or `/?emailConfirmError=1` |
| `POST /api/auth/resend-confirmation` | No | Body: `{ "email": "..." }` — resend confirmation email (always `204`, even if email unknown or already confirmed) |
| `GET /api/auth/me` | Yes | Current user profile |

**Token lifetime** (configurable in `appsettings.json` → `Jwt` section):

- Access token: **30 minutes**
- Refresh token: **14 days**

**SignalR**: connect to `/hubs/parking` with JWT ( `Authorization` header or `?access_token=` query).

**Public endpoints** (no token): map (`/api/parking/active`, `/nearby`), geocoding, leaderboard, public profiles, etc.

**Production**: set `Jwt:Key` via User Secrets or environment variables (minimum 32 characters).

### Auth (route summary)

```
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout
GET    /api/auth/confirm-email
POST   /api/auth/resend-confirmation
GET    /api/auth/me
```

### Parking reports

```
GET    /api/parking/active
GET    /api/parking/nearby?lat=&lng=&radius=
GET    /api/parking/reports/mine
POST   /api/parking/report          (multipart: lat, lng, photo)
POST   /api/parking/vote
DELETE /api/parking/report/{id}
```

### Parking requests

```
POST   /api/parking/request
PUT    /api/parking/requests/{id}
DELETE /api/parking/requests/{id}
GET    /api/parking/requests/nearby?lat=&lng=&radius=
GET    /api/parking/requests/mine
POST   /api/parking/requests/{id}/renew
POST   /api/parking/requests/{id}/reserve
POST   /api/parking/requests/{id}/unreserve
POST   /api/parking/requests/{id}/block
POST   /api/parking/requests/{id}/unblock
GET    /api/parking/geocode?address=
GET    /api/parking/geocode/suggest?q=
GET    /api/parking/payment-methods
```

### Request messaging

```
GET    /api/parking/requests/{id}/conversations
GET    /api/parking/requests/{id}/messages?with=
POST   /api/parking/requests/{id}/messages
POST   /api/parking/requests/{id}/messages/photo   (multipart)
```

### Users

```
GET    /api/users/search?q=
GET    /api/users/{id}/profile
GET    /api/users/{id}/reviews
GET    /api/users/{id}/reviews/summary
POST   /api/users/{id}/reviews
PUT    /api/users/me/password
PUT    /api/users/me/location          (body: latitude, longitude)
POST   /api/users/me/profile-photo
DELETE /api/users/me/profile-photo
GET    /api/users/leaderboard
```

### Real-time

```
WS     /hubs/parking
```

## Database

Database: **SpotsterDb** (LocalDB). Migrations and seed run automatically on first startup.

To start fresh:

```sql
DROP DATABASE IF EXISTS FreeParkingDb;
DROP DATABASE IF EXISTS SpotsterDb;
```

Then restart the application.

## Production and scalability

The app runs on a **single instance** out of the box (LocalDB, local file storage, in-memory distributed cache). For production traffic or **multiple app instances**, configure the sections below in `appsettings.json`, environment variables, or User Secrets.

### SQL Server

Use a managed SQL Server (Azure SQL, RDS, etc.) and set `ConnectionStrings:DefaultConnection`. The default string includes connection pooling (`Max Pool Size=200`, `Min Pool Size=5`).

Migrations run automatically on startup. Spatial columns (`Location` as `geography`) are required for efficient nearby queries.

### Redis (recommended for production)

When `Redis:ConnectionString` is set:

- **Distributed cache** is shared across instances (active parking/request lists, anti-fraud IP counters)
- **SignalR backplane** synchronizes real-time events between servers

```json
"Redis": {
  "ConnectionString": "your-redis-host:6379,password=..."
}
```

If Redis is empty, the app uses an in-process cache (fine for local development only).

### Blob storage (optional)

Photos are stored under `wwwroot/uploads` by default. For cloud or multi-instance deployments, set Azure Blob Storage:

```json
"BlobStorage": {
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
  "ContainerName": "spotster",
  "PublicBaseUrl": "https://your-cdn-or-blob-endpoint/spotster"
}
```

`PublicBaseUrl` can point to a CDN in front of the blob container.

### JWT and SMTP

- `Jwt:Key` — minimum 32 characters; never commit production keys to source control
- `Smtp` — required for registration confirmation emails in production
- Without SMTP configured, confirmation emails are **not sent** but registration is not blocked — the HTML body (including the confirmation URL) is logged at `Information` level for local testing
- `App:PublicBaseUrl` must match the public URL users reach (used in confirmation email links)

### Rate limits (built-in)

| Policy   | Limit        | Applies to                          |
|----------|--------------|-------------------------------------|
| `auth`   | 20 / minute  | Login, register, refresh, resend    |
| `geocode`| 40 / minute  | Address geocoding and suggestions     |
| `write`  | 120 / minute | POST / PUT / DELETE API (per user or IP) |

### SignalR clients

Browsers call `SetMapViewport(lat, lng, radius)` to join map grid groups and receive only nearby events. Chat uses `JoinRequestChat(requestId)` for request-scoped messages.

### Multi-instance checklist

1. SQL Server (shared database)
2. Redis (cache + SignalR backplane)
3. Blob storage or shared file volume for uploads
4. HTTPS termination and `Jwt:Key` / SMTP via secrets
5. CDN optional for static assets and blob `PublicBaseUrl`

## Recent changes (June 2026)

- **Frontend modularization**: monolithic `app.js` split into ES modules (`core/`, `features/`, `adapters/`, `app/`) with shared `hub` and `map-engine` abstraction over Leaflet
- **Location UX**: GPS permission overlay, map pick fallback, sidebar collapse with persisted state
- **Scalability**: spatial SQL nearby queries, distributed cache, SignalR grid groups, API rate limiting, optional Redis and Azure Blob
- **JWT authentication + ASP.NET Identity**: access/refresh tokens, `[Authorize]` on APIs, legacy password hash migration
- **Map UI**: location button, user pin marker with person icon, adaptive sidebar/zoom layout
- **Modals** with unified style (dark header, blue-gray body) and centering on map area
- **Report detail**: modal with community vote and “already voted” state (`hasVotedByMe`)
- **Chat**: optimized layout, taller modal, live negotiation/block updates, **user unblock**
- **API** `POST /api/parking/requests/{id}/unblock` for listing owners
- Left sidebar with Available / Requests tabs and request status filters
- Configurable visible zone from sidebar
- Chat: GPS location, photos, multi-thread, in negotiation, user blocking
- User profile photo
- Request reward made **optional** (listings without reward)
- Bilingual product presentation (`presentazione.html`)
- New tagline: *Free parking spots, shared by the community*
- **Registration docs**: clarified email-confirmation flow (no JWT on register, login after confirm, dev SMTP fallback via logs)
