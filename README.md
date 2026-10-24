# SaasPlatform

Multi-tenant SaaS starter: **ASP.NET Core (.NET 10)** API + **Angular** + **Tailwind CSS 3.4** + **SQLite** (local file database).

## Structure

```
SaasPlatform/
├── SaasPlatform.slnx
├── Directory.Build.props        shared compiler settings
├── Directory.Packages.props     central NuGet versions
├── src/
│   ├── SaasPlatform.Domain          entities, no dependencies
│   ├── SaasPlatform.Application     use cases, interfaces (depends on Domain)
│   ├── SaasPlatform.Infrastructure  EF Core, JWT, hashing (implements Application interfaces)
│   └── SaasPlatform.Api             controllers, DI wiring, auth config
└── web/                         Angular app (standalone components, signals, Tailwind 3.4)
```

Dependencies point inward: `Api → Infrastructure → Application → Domain`.

## Run locally

Prerequisites: .NET 10 SDK and Node 20+. No database install and no Docker needed.

```powershell
# 0. One-time: store a JWT signing key outside the repo (user-secrets)
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($b)) --project src/SaasPlatform.Api
```

```bash
# 1. API  (http://localhost:5000, health check at /health)
dotnet run --project src/SaasPlatform.Api

# 2. Web  (http://localhost:4200)
cd web
npm install
npm start
```

The database is a file, `saasplatform.db`, created automatically on first run in development.
Delete it to reset all data.

## How multi-tenancy works

- Registering creates a `Tenant` and its first `Admin` user.
- The JWT carries a `tenant_id` claim; `CurrentTenant` reads it per request.
- `AppDbContext` applies a global query filter on tenant-scoped entities, so queries only see the caller's tenant.
- Login and register use `IgnoreQueryFilters()` because no tenant is known yet.

## Later (deliberately postponed)

- Docker / Dockerfiles, CI pipeline, cloud hosting
- Switch SQLite to SQL Server or PostgreSQL: change the EF provider package and `UseSqlite` in
  `Infrastructure/DependencyInjection.cs`, plus the connection string
- Replace `Database.EnsureCreated()` in `Program.cs` with EF Core migrations
- Set `Jwt:Key` via user-secrets or environment variables, never in committed files
- Refresh tokens, FluentValidation, rate limiting, tests
- **Third-party integrations come last**, after the core product is done: Stripe billing, email delivery and verification, hosted logging/monitoring (e.g. Serilog sinks), and anything else that calls an external service. Until then, features that need them use local stand-ins behind an interface.

## Status

- Backend: `dotnet build` passes with 0 warnings and 0 errors (.NET SDK 10.0.402).
- Frontend: `npm install && npm run build` passes.
- API verified by running it: register, login, JWT auth (missing and tampered tokens return 401), tenant isolation on `/api/me/team` (two companies each see only their own users), duplicate-email and short-password errors, and CORS for `http://localhost:4200`.
- UI verified in a browser: register, redirect to `/dashboard`, and the team list loaded from the API.
- **Not yet verified:** sign out and back in, and the token-expiry (401) logout.
- `Directory.Packages.props` pins some transitive packages (OpenApi, SQLite native lib, Pkcs) to patched versions. Keep those pins in sync with the main packages when upgrading.
