<div align="center">
  <img src="Assets/Images/readme-hero.svg" alt="Elovo Chat - realtime messenger" width="100%" />

  <br />

  <img src="https://img.shields.io/badge/live%20app-elovo--app.onrender.com-4f8cff?style=for-the-badge&labelColor=121832" alt="Live app elovo-app.onrender.com" />
  <img src="https://img.shields.io/badge/.NET-10.0-6d67ff?style=for-the-badge&labelColor=121832" alt=".NET 10.0" />
  <img src="https://img.shields.io/badge/realtime-SignalR-8b7cff?style=for-the-badge&labelColor=121832" alt="SignalR" />
  <img src="https://img.shields.io/badge/auth-JWT%20Cookie-53d6a4?style=for-the-badge&labelColor=121832" alt="JWT cookie authentication" />
  <img src="https://img.shields.io/badge/database-PostgreSQL-7780b8?style=for-the-badge&labelColor=121832" alt="PostgreSQL" />
</div>

# Elovo Chat 💬

Elovo Chat is a private realtime messenger built with ASP.NET Core MVC, SignalR, Razor Views, PostgreSQL, Supabase Storage, and a browser-first local history model. It focuses on fast conversations, friend-only visibility, media messaging, voice calls, account security, and a polished responsive interface.

Live deployment: [elovo-app.onrender.com](https://elovo-app.onrender.com/)

## Features 🚀

- Realtime text messages, typing indicators, read/delivered state, and online presence through SignalR.
- Friend requests, friend removal, hidden chats, and conversations gated by accepted friendships.
- Local browser chat history with pending server delivery for offline recipients.
- Fast chat entry: text history renders immediately while image and voice files load in batches of 5 with loaders.
- Image messages with validation, compression, megapixel labels, local IndexedDB caching, download actions, and Supabase Storage cleanup.
- Voice messages recorded in the browser, converted to MP3, cached locally, and rendered with waveform-style controls.
- Browser audio calls with WebRTC, Cloudflare TURN credentials, active-call banners, incoming-call UI, mute controls, and call summaries.
- Profile images cropped to `256 x 256` WebP files with friend-only privacy.
- Activity visibility settings for full status, online-only status, or hidden presence.
- App preferences for theme, language, and automatic local message cleanup.
- Premium modal, premium badges, premium-only actions page, and premium-aware settings navigation.
- JWT authentication stored in the `ElovoAuthToken` cookie.
- Optional email two-factor authentication with a 7-digit code and cooldown handling.
- Google sign-in for accounts with verified Google emails.
- Firebase Cloud Messaging support for offline messages and incoming-call notifications.
- Multi-language frontend assets for English, Russian, and Azerbaijani.
- Responsive mobile UI, animated feedback, modal flows, and theme-optimized SVG icons.

## Quick Start ⚡

```bash
dotnet restore Elovo.NET/Elovo.Web/Elovo.Web.csproj
dotnet run --project Elovo.NET/Elovo.Web/Elovo.Web.csproj
```

The app redirects `/` to `/auth/login`, exposes the authenticated chat at `/chat`, and serves a plain-text health check at `/health`.

> EF Core migrations are applied automatically during startup, so the configured PostgreSQL database must be reachable before the app starts.

## Message Storage Model 📩

Elovo intentionally avoids a permanent server-side chat archive:

- Messages and call summary cards live in `PendingMessages` while delivery is pending.
- After the receiving client acknowledges them, pending rows are removed from the database.
- Delivered chat history remains in the browser's local storage.
- Images and voice files live in Supabase Storage and are cached locally in IndexedDB.
- Chat cleanup preferences remove old local messages and cached media on the current device.
- Active call metadata is removed after a call is completed, rejected, missed, or timed out.

## Media Loading Model 🧩

The chat UI is optimized so users do not wait for media before entering a conversation:

- Text messages render immediately.
- Only the latest 5 media messages are allowed to load at first.
- Older media messages show loaders until the user scrolls back to them.
- Each scroll boundary unlocks the next batch of 5 media files.
- Cached files are read from IndexedDB before falling back to network URLs.
- New media received through SignalR joins the same lazy-loading flow instead of forcing background downloads for inactive chats.

## Architecture 🧱

```text
Elovo Chat/
|-- Assets/                         # Static HTML demo assets and README hero art
|-- Elovo.NET/
|   |-- Elovo.Domain/               # Entities and repository contracts
|   |-- Elovo.Application/          # DTOs, validation, services, SignalR hub
|   |-- Elovo.Infrastructure/       # EF Core context, repositories, migrations
|   `-- Elovo.Web/                  # MVC app, controllers, views, wwwroot assets
|-- Dockerfile
`-- README.md
```

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core MVC, Razor Views, SignalR |
| Auth | JWT Bearer, secure cookie flow, BCrypt, Google OAuth |
| Data | Entity Framework Core, Npgsql, PostgreSQL |
| Storage | Supabase Storage, browser localStorage, IndexedDB |
| Notifications | Firebase Admin SDK, FCM |
| Calls | WebRTC, SignalR, Cloudflare TURN |
| Media | ImageSharp, FFmpeg |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Deployment | Docker, Render |

## Local Setup ⚙️

Requirements:

- .NET 10 SDK
- PostgreSQL
- FFmpeg available on `PATH` for voice media processing
- Supabase project and storage buckets for images, profile images, and voice messages
- Firebase service account JSON if push notifications are enabled
- SMTP credentials if email verification or two-factor authentication is enabled
- Google OAuth client credentials if Google sign-in is enabled
- Cloudflare TURN key and API token if production WebRTC relay credentials are needed

Configure secrets with environment variables or .NET user secrets:

```bash
dotnet user-secrets init --project Elovo.NET/Elovo.Web/Elovo.Web.csproj
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "ConnectionStrings:Default" "Host=...;Database=...;Username=...;Password=..."
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Jwt:Secret" "replace-with-a-long-random-secret"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Jwt:Issuer" "Elovo"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Jwt:Audience" "ElovoUsers"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Supabase:Url" "https://your-project.supabase.co"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Supabase:ServiceRoleKey" "your-private-storage-key"
```

Run the application:

```bash
dotnet run --project Elovo.NET/Elovo.Web/Elovo.Web.csproj
```

Create or update migrations from the solution directory when the data model changes:

```bash
dotnet ef migrations add MigrationName \
  --project Elovo.NET/Elovo.Infrastructure/Elovo.Infrastructure.csproj \
  --startup-project Elovo.NET/Elovo.Web/Elovo.Web.csproj
```

## Configuration 🔧

Use environment variables in production. ASP.NET Core maps double underscores to nested configuration keys.

| Variable | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Default` | Yes | PostgreSQL connection string |
| `Jwt__Secret` | Yes | JWT signing secret |
| `Jwt__Issuer` | Yes | JWT issuer |
| `Jwt__Audience` | Yes | JWT audience |
| `Jwt__ExpiryDays` | Optional | Token lifetime in days, defaults to `7` |
| `Supabase__Url` | Yes | Supabase project URL |
| `Supabase__ServiceRoleKey` | Yes | Private Supabase service-role key |
| `Supabase__StorageKey` | Optional | Legacy fallback key for storage access |
| `Supabase__AnonKey` | Optional | Legacy fallback key for storage access |
| `Supabase__StorageBucket` | Optional | Image-message bucket, defaults to `chat-images` |
| `Supabase__ProfileImagesBucket` | Optional | Profile-image bucket, defaults to `profile-images` |
| `Supabase__VoiceMessagesBucket` | Optional | Voice-message bucket, defaults to `chat-voices` |
| `FIREBASE_CREDENTIALS_JSON` | For push notifications | Firebase service-account JSON |
| `Email__SmtpHost` | For email features | SMTP host |
| `Email__SmtpPort` | For email features | SMTP port |
| `Email__SmtpUsername` | For email features | SMTP username |
| `Email__SmtpPassword` | For email features | SMTP password |
| `Email__From` | For email features | Sender email address |
| `Email__EnableSsl` | For email features | SMTP TLS setting |
| `GoogleAuth__ClientId` | For Google sign-in | Google OAuth client ID |
| `GoogleAuth__ClientSecret` | For Google sign-in | Google OAuth client secret |
| `CLOUDFLARE_TURN_KEY_ID` | For TURN credentials | Cloudflare Calls TURN key ID |
| `CLOUDFLARE_TURN_API_TOKEN` | For TURN credentials | Cloudflare API token used to generate ICE servers |
| `Render__ExternalUrl` | Optional | Public URL used by the keep-alive health request |

## HTTP & Realtime Surface 🌐

| Route | Purpose |
| --- | --- |
| `/auth/login` | Login page and form submit |
| `/auth/register` | Registration page and form submit |
| `/auth/two-factor` | Email 2FA verification |
| `/google-login` | Google OAuth callback and redirect entry |
| `/chat` | Authenticated chat UI |
| `/settings/profile` | Profile, email, 2FA, image, and activity visibility settings |
| `/settings/chat` | Theme, language, and local cleanup preferences |
| `/settings/premium-actions` | Premium-only management page |
| `/chatHub` | SignalR hub for messages, presence, typing, and calls |
| `/api/conversations` | Conversation list |
| `/api/users` | Friend search |
| `/api/friend-requests` | Friend request list and creation |
| `/api/messages/images` | Image message upload |
| `/api/messages/voice` | Voice message upload and retrieval |
| `/api/profile` | Profile read and update endpoints |
| `/api/turn-credentials` | Cloudflare TURN ICE server generation |
| `/api/users/fcm-token` | FCM token registration |
| `/health` | Health check |

## Request Limits 🚦

- Regular HTTP requests are limited to `100` requests per minute per client IP.
- SignalR traffic under `/chatHub` is excluded from the HTTP rate limiter.
- Request and response transfer speed is throttled to `1 MiB/s` per request; this is not a per-user storage quota.
- Each voice message upload is limited to `1 MiB` per file.
- Each image or profile media upload is limited to `10 MiB` per file.
- Rejected HTTP requests receive status code `429 Too Many Requests`.

## Docker 🐳

Build and run the container:

```bash
docker build -t elovo-chat .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=..." \
  -e Jwt__Secret="replace-with-a-long-random-secret" \
  -e Jwt__Issuer="Elovo" \
  -e Jwt__Audience="ElovoUsers" \
  -e Supabase__Url="https://your-project.supabase.co" \
  -e Supabase__ServiceRoleKey="your-private-storage-key" \
  elovo-chat
```

The container installs FFmpeg and listens on `${PORT:-8080}`, which is suitable for Render and similar container platforms.

## Deployment Notes 🚢

- Set `ASPNETCORE_ENVIRONMENT=Production` for deployed containers.
- Provide `PORT` when the hosting platform injects a dynamic port.
- Configure Render or another uptime service to call `/health` if cold starts are a concern.
- Add `Render__ExternalUrl` when the keep-alive background service should ping the public health endpoint.
- Make sure Google OAuth redirect URLs include `/google-login` for each deployed domain.
- Keep Supabase buckets public only when generated media URLs are intended to be browser-accessible.
- Provide Cloudflare TURN credentials for reliable audio calls across restrictive networks.

## Security Notes 🔐

- Do not commit production connection strings, JWT secrets, SMTP passwords, Supabase service-role keys, Google OAuth secrets, Cloudflare TURN tokens, or Firebase credentials.
- JWT tokens are read from the `ElovoAuthToken` cookie and validated server-side.
- Google sign-in verifies the returned ID token and requires a verified email address.
- Profile image paths are hidden from users who are not friends.
- Activity visibility can hide online and last-seen state.
- Pending messages are removed after client acknowledgement instead of being stored as a permanent server-side history.
- The `/api/calls/reject` endpoint is intentionally anonymous so an incoming call can be rejected from a push-notification action.

---

<div align="center">
  Built for focused conversations, secure sessions, and a little bit of neon calm. 💙
</div>
