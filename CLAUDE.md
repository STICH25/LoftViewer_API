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
- **Weather quota.** OpenWeatherMap's free tier is 1,000 calls/day and each lookup is 2 calls.
  `WeatherCallBudget` enforces `MaxCallsPerDay`; the refresh runs in `WeatherRefreshService`
  (a `BackgroundService`), never from a request.

## API surface (consumed by the front end)

| Method | Route | Auth |
|---|---|---|
| GET | `/api/birds`, `/api/birds/{id}`, `/api/birds/{id}/image` | anonymous |
| POST | `/api/birds/addBird` (multipart) | Admin |
| POST | `/api/birds/upload-json` (multipart, JSON array) | Admin |
| PUT / DELETE | `/api/birds/{id}` | Admin |
| POST | `/api/auth/login` → `{ token, userName, role, expiresAt }` | anonymous, rate-limited |
| POST | `/api/auth/register` | anonymous, rate-limited |
| GET | `/api/weather?city=`, `/api/weather/latest` | anonymous |
| GET | `/health` | anonymous |

Errors are RFC 7807 ProblemDetails (`application/problem+json`).

## Testing

`LoftViewerApiFactory` boots the real pipeline with in-memory repositories, so API tests need no
database. Add a test there for any new endpoint, including the 401/403 cases for admin routes.
Use `TestContext.Current.CancellationToken` in async tests (xUnit v3 analyzer rule).

## Conventions

- File-scoped namespaces, primary constructors, `sealed` by default, `CancellationToken` on every async path.
- Log through `[LoggerMessage]` source-generated methods, never `Console.WriteLine`. Never log tokens,
  passwords, hashes or `Authorization` headers.
- Return `Problem(...)` for errors so every failure has the same shape.
