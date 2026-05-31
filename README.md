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

Elovo Chat is a realtime messenger built with ASP.NET Core, SignalR, Razor Views, and PostgreSQL. It provides a responsive dark interface for private conversations, media messages, and browser-based voice calls.

Live deployment: [elovo-app.onrender.com](https://elovo-app.onrender.com/)

## Features 🚀

- Realtime text messages, typing indicators, read state, and online presence through SignalR.
- Friend requests, friend removal, and conversations available only after a request is accepted.
- Profile images converted to `256 x 256` WebP files.
- Profile image privacy: users outside the friend list see a placeholder instead of the real avatar.
- Image messages stored in Supabase Storage with upload, download, and cleanup support.
- Voice messages with duration and FFmpeg-based media processing.
- Browser voice calls with incoming-call notifications, active-call banners, microphone controls, and call modal minimization.
- Call summary cards in chat with duration and `Answered`, `Rejected`, or `Missed` status.
- JWT authentication stored in the `ElovoAuthToken` cookie.
- Optional email two-factor authentication with a 7-digit code.
- Firebase Cloud Messaging support for offline message and incoming-call notifications.
- Responsive mobile UI, animated status feedback, and a styled error page.

## Message Storage Model 📩

The application intentionally uses a pending-delivery model instead of a permanent server-side chat archive:

- Messages and call summary cards are stored in `PendingMessages` while delivery is pending.
- After the receiving client acknowledges them, they are removed from the database.
- Delivered chat history remains in the browser's local storage.
- Active call metadata is removed after a call is completed, rejected, or missed.

## Architecture 🧱

```text
Elovo Chat/
|-- Assets/                         # Static HTML demo assets
|-- Elovo.NET/
|   |-- Elovo.Domain/               # Entities and repository contracts
|   |-- Elovo.Application/          # DTOs, validation, services, SignalR hub
|   |-- Elovo.Infrastructure/       # EF Core context, repositories, migrations
|   `-- Elovo.Web/                  # MVC app, controllers, views, wwwroot
|-- Dockerfile
`-- README.md
```

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core MVC, Razor Views, SignalR |
| Auth | JWT Bearer, secure cookie flow, BCrypt |
| Data | Entity Framework Core, Npgsql, PostgreSQL |
| Storage | Supabase Storage |
| Notifications | Firebase Admin SDK, FCM |
| Media | ImageSharp, FFmpeg |
| Validation | FluentValidation |
| Deployment | Docker, Render |

## Local Setup ⚙️

Requirements:

- .NET 10 SDK
- PostgreSQL
- FFmpeg available on `PATH` for voice media processing
- Supabase project and storage buckets for images and voice messages

Configure secrets with environment variables or .NET user secrets:

```bash
dotnet user-secrets init --project Elovo.NET/Elovo.Web/Elovo.Web.csproj
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "ConnectionStrings:Default" "Host=...;Database=...;Username=...;Password=..."
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Jwt:Secret" "replace-with-a-long-random-secret"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Supabase:Url" "https://your-project.supabase.co"
dotnet user-secrets set --project Elovo.NET/Elovo.Web/Elovo.Web.csproj "Supabase:ServiceRoleKey" "your-private-storage-key"
```

Run the application:

```bash
dotnet run --project Elovo.NET/Elovo.Web/Elovo.Web.csproj
```

EF Core migrations are applied automatically during application startup. The health-check endpoint is available at `/health`.

## Configuration 🔧

Use environment variables in production. ASP.NET Core maps double underscores to nested configuration keys.

| Variable | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Default` | Yes | PostgreSQL connection string |
| `Jwt__Secret` | Yes | JWT signing secret |
| `Jwt__Issuer` | Recommended | JWT issuer, defaults to `Elovo` in the template |
| `Jwt__Audience` | Recommended | JWT audience, defaults to `ElovoUsers` in the template |
| `Jwt__ExpiryDays` | Optional | Token lifetime in days, defaults to `7` |
| `Supabase__Url` | Yes | Supabase project URL |
| `Supabase__ServiceRoleKey` | Yes | Private Supabase service-role key |
| `Supabase__StorageBucket` | Optional | Image-message bucket, defaults to `chat-images` |
| `Supabase__ProfileImagesBucket` | Optional | Profile-image bucket, defaults to `profile-images` |
| `Supabase__VoiceMessagesBucket` | Optional | Voice-message bucket, defaults to `chat-voices` |
| `FIREBASE_CREDENTIALS_JSON` | For push notifications | Firebase service-account JSON |
| `Email__SmtpHost` | For 2FA email | SMTP host |
| `Email__SmtpPort` | For 2FA email | SMTP port |
| `Email__SmtpUsername` | For 2FA email | SMTP username |
| `Email__SmtpPassword` | For 2FA email | SMTP password |
| `Email__From` | For 2FA email | Sender email address |
| `Email__EnableSsl` | For 2FA email | SMTP TLS setting |
| `Render__ExternalUrl` | Optional | Public URL used by the keep-alive health request |

An example configuration file is available at `Elovo.NET/Elovo.Web/appsettings.Example.json`.

## Request Limits 🚦

- Regular HTTP requests are limited to `100` requests per minute per client IP.
- SignalR traffic under `/chatHub` is excluded from the HTTP rate limiter.
- Request and response bandwidth is throttled to `1 MiB/s` per request.
- Rejected HTTP requests receive status code `429 Too Many Requests`.

## Docker 🐳

Build and run the container:

```bash
docker build -t elovo-chat .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=..." \
  -e Jwt__Secret="replace-with-a-long-random-secret" \
  -e Supabase__Url="https://your-project.supabase.co" \
  -e Supabase__ServiceRoleKey="your-private-storage-key" \
  elovo-chat
```

The container installs FFmpeg and listens on `${PORT:-8080}`, which is suitable for Render and similar container platforms.

## Security Notes 🔐

- Do not commit production connection strings, JWT secrets, SMTP passwords, Supabase service-role keys, or Firebase credentials.
- Profile image paths are hidden from users who are not friends.
- Pending messages are removed after client acknowledgement instead of being stored as a permanent server-side history.
- The `/api/calls/reject` endpoint is intentionally anonymous so an incoming call can be rejected from a push-notification action.

---

<div align="center">
  Built for focused conversations, secure sessions, and a little bit of neon calm. 💙
</div>
