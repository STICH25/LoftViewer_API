# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

The REST API behind **Rey Family Loft** (https://www.reyfamilyloft.com), a site for cataloguing racing
pigeons. ASP.NET Core 10 controllers over MongoDB Atlas, deployed to Railway from the `Dockerfile`.
The React front end lives in a separate repo (`STICH25/LoftViewer_FrontEnd`, usually cloned at
`C:\repos\loft-viewer`). **Any change to a route, request shape or response shape must be checked
against that repo's `src/api/` folder.** The two are deployed independently.

## Commands

```bash
dotnet build                                   # warnings are errors (Directory.Build.props)
dotnet test                                    # xUnit v3 on Microsoft.Testing.Platform (opted in via global.json)
dotnet test -- --filter-class "*BirdsEndpointTests"
dotnet run --project src/LoftViewer --launch-profile http   # http://localhost:5053/swagger
docker build -t loftviewer-api .
```

The `http` launch profile listens on **5053**. The front end's `.env.development` points there and the
Development CORS policy allows `http://localhost:5173` (Vite's default port), so keep those three in sync.

## Layout

```
src/LoftViewer/
  Program.cs              composition root; middleware order matters (see below)
  Configuration/          strongly-typed options, validated at startup (ValidateOnStart)
  Contracts/              request/response DTOs; the only types that cross the HTTP boundary
  Controllers/            Birds, Auth, Weather
  Data/                   Mongo repositories + health check
  Models/                 Mongo documents (Bird, AppUser) and WeatherReport
  Services/               TokenService, AccountService, ImageProcessor, Weather/*
  Extensions/             DI registration, rate limiting, OpenAPI bearer transformer
tests/LoftViewer.Tests/   WebApplicationFactory API tests + unit tests
```

## Configuration and secrets

Nothing secret is committed. `appsettings.json` holds only non-secret defaults.

| Setting | Required | Local | Railway env var |
|---|---|---|---|
| `MongoDBSettings:Username` / `Password` (or `ConnectionString`) | yes | user-secrets | `MongoDBSettings__Password` etc. |
| `JwtSettings:Secret` (≥ 32 bytes) | yes outside Development | user-secrets | `JwtSettings__Secret` |
| `WeatherSettings:ApiKey` | no (weather disabled if empty) | user-secrets | `WeatherSettings__ApiKey` |
| `Cors:AllowedOrigins` | yes | appsettings.Development.json | `Cors__AllowedOrigins__0` |
| `Swagger:Enabled` | no | on in Development | `Swagger__Enabled=true` |

Options are validated on startup: a missing Mongo credential or a weak JWT secret stops the app
immediately with a clear message, rather than failing on the first request. In Development only, a
missing JWT secret falls back to a random per-process key, so tokens die on every restart.

`dotnet user-secrets list --project src/LoftViewer` shows what is set locally.

## Things that are easy to break

- **Mongo field names are the data contract.** `Bird` and `AppUser` property names map 1:1 to fields
  in existing documents (`BirdName`, `UserName`, `PasswordHash`, `_id`, ...). Renaming a property
  silently orphans production data. Both classes are `[BsonIgnoreExtraElements]` because older
  documents still carry fields that have been removed (`Image`, `ImagePath`).
- **Never load `ImageBytes` in list/detail queries.** Images are stored inline (up to ~1 MB each).
  `BirdRepository` projects them out; only `GetImageAsync` reads them. Returning them from
  `GET /api/birds` once made the list response many megabytes.
- **Updates use `$set`, not replace.** `UpdateAsync` only touches `ImageBytes` when a new image was
  uploaded; a replace would wipe the stored photo whenever the form is saved without one.
- **Validation attributes on positional records go on the parameter**, not `[property: ...]`.
  MVC throws at request time otherwise (the tests catch this).
- **Claims are short-form.** Tokens carry `sub`, `name` and `role`; `MapInboundClaims = false` and
  `RoleClaimType = "role"` make `[Authorize(Roles = "Admin")]` work. The front end may decode `name`/`role`.
- **Middleware order** in `Program.cs`: forwarded headers → exception handler → CORS → rate limiter →
  authentication → authorization. Railway terminates TLS, so there is no HTTPS redirection in the app.
- **The container listens on 8080 and the production domain targets 8080.** Do not bind to Railway's
  `PORT` variable in code: if it differs from the domain's target port, the live API goes dark.
  Railway also sets `ASPNETCORE_URLS=http://+:8080`.
- **Weather quota.** OpenWeatherMap's free tier is 1,000 calls/day and each lookup is 2 calls.
  `WeatherCallBudget` enforces `MaxCallsPerDay`; the refresh runs in `WeatherRefreshService`
  (a `BackgroundService`), never from a request.

## API surface (consumed by the website and the mobile app)

| Method | Route | Auth |
|---|---|---|
| GET | `/api/birds`, `/api/birds/{id}`, `/api/birds/{id}/image` | anonymous |
| POST | `/api/birds/addBird` (multipart) | Admin |
| POST | `/api/birds/upload-json` (multipart, JSON array) | Admin |
| PUT / DELETE | `/api/birds/{id}` | Admin |
| POST | `/api/auth/login` → `{ token, userName, role, expiresAt }` (+ `refreshToken`, `refreshTokenExpiresAt` when `issueRefreshToken: true`) | anonymous, rate-limited |
| POST | `/api/auth/refresh` `{ refreshToken }` → same shape, new tokens | anonymous, rate-limited |
| POST | `/api/auth/logout` `{ refreshToken }` → 204 | anonymous, rate-limited |
| POST | `/api/auth/register` | anonymous, rate-limited |
| GET | `/api/weather?city=`, `/api/weather/latest` | anonymous |
| GET | `/health` | anonymous |

Errors are RFC 7807 ProblemDetails (`application/problem+json`).

## Sessions: access tokens and refresh tokens

- **Access token**: a JWT, 60 minutes (`JwtSettings:ExpirationMinutes`). The website stores only this.
- **Refresh token**: opt-in, for native apps only (`issueRefreshToken: true` at login). 256 random bits,
  valid 30 days from last use (`JwtSettings:RefreshTokenDays`), kept by the app in the OS secure store.
- **Only the SHA-256 hash is stored** (`refreshTokens` collection). A database leak does not yield sessions.
- **Rotation with reuse detection.** Every `/refresh` revokes the presented token and issues a new one.
  Presenting an already-used token means it was copied or replayed, so *all* of that user's refresh
  tokens are revoked (they sign in again). A client that loses the response to a refresh and retries
  will trigger this once; that is intentional.
- **The role is re-read at refresh**, so demoting or deleting a user takes effect within an hour.
- The website never sends `issueRefreshToken`, so it never receives a refresh token (nothing
  long-lived in `localStorage`).
- Refresh and logout use a more generous limiter (`RateLimiting:RefreshPermitsPerMinute`, default 30)
  than login (`AuthPermitsPerMinute`, 10). Limits are read per request from `IConfiguration`.
- Indexes (unique hash, user id, TTL on `ExpiresAt`) are created by `MongoIndexInitializer` at startup;
  failure is a logged warning, not a crash. The Atlas user needs the `createIndex` privilege.

## Photos and caching

`GET /api/birds/{id}/image` sends `Cache-Control: public, max-age=300` and a content-derived `ETag`,
and answers `If-None-Match` with 304. A replaced photo gets a new ETag, so clients revalidate cheaply.

## Testing

`LoftViewerApiFactory` boots the real pipeline with in-memory repositories, so API tests need no
database. Add a test there for any new endpoint, including the 401/403 cases for admin routes.
Use `TestContext.Current.CancellationToken` in async tests (xUnit v3 analyzer rule).

## Conventions

- File-scoped namespaces, primary constructors, `sealed` by default, `CancellationToken` on every async path.
- Log through `[LoggerMessage]` source-generated methods, never `Console.WriteLine`. Never log tokens,
  passwords, hashes or `Authorization` headers.
- Return `Problem(...)` for errors so every failure has the same shape.
