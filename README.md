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

The database is a file, `saasplatform.db`, created automatically on first run in development
(EF Core migrations are applied on startup). Delete it to reset all data.

### Changing the data model

```powershell
dotnet ef migrations add <Name> --project src/SaasPlatform.Infrastructure --startup-project src/SaasPlatform.Infrastructure --output-dir Persistence/Migrations
dotnet build   # rebuild BEFORE running with --no-build, or the app won't see the new migration
```

`dotnet-ef` is pinned as a local tool (`dotnet-tools.json`); run `dotnet tool restore` once on a new machine.

## Features

| Area | What it does |
|---|---|
| Auth | Register a company (creates a tenant + first Admin), login. Access tokens last 15 minutes; a rotating refresh token (14 days, stored hashed) renews them silently. Replaying a used refresh token revokes every session for that user. Changing your password signs out every other device |
| Rate limiting | Built into ASP.NET Core (no external service): login, register, refresh and invite endpoints allow 10 attempts per minute per client IP, configurable under `RateLimiting` in appsettings |
| Validation | FluentValidation rules for every request. The API returns field-level errors that the forms show under each input |
| Roles | `Admin` and `Member`. Admins manage people and can delete projects; Members can create and edit work. Enforced on the API, mirrored in the UI |
| Team | List members; admins change roles and remove people. A company always keeps at least one admin. Removing someone ends their sessions and unassigns their tasks |
| Invitations | Admin creates an invite and gets a one-time link to share by hand (email is postponed). Only a SHA-256 hash of the token is stored; links expire in 7 days and work once |
| Projects and tasks | Per-company projects with a drag-and-drop To do / In progress / Done board, priority, assignees, due dates and overdue flags, task comments, search and filters, archive |
| Dashboard | Open and overdue counts, work finished this week, "my tasks", progress, and a live activity feed |
| Activity feed | Append-only record of who did what, shown newest first |
| Settings | Edit your profile, rename the company (admins), choose light / dark / system theme, change password |
| UI | Angular 21 standalone components and signals, Tailwind 3.4 with a semantic token system, dark mode, toasts, accessible confirm dialogs, loading skeletons |

## How multi-tenancy works

- Registering creates a `Tenant` and its first `Admin` user.
- The JWT carries a `tenant_id` claim; `CurrentTenant` reads it per request.
- `AppDbContext` applies a global query filter on tenant-scoped entities, so queries only see the caller's tenant.
- Login and register use `IgnoreQueryFilters()` because no tenant is known yet.

## Later (deliberately postponed)

- Docker / Dockerfiles, CI pipeline, cloud hosting
- Switch SQLite to SQL Server or PostgreSQL: change the EF provider package and `UseSqlite` in
  `Infrastructure/DependencyInjection.cs`, plus the connection string
- Set `Jwt:Key` via user-secrets or environment variables, never in committed files
- Upgrade Angular 21 to 22: `cd web && npx ng update @angular/core@22 @angular/cli@22`
- Rate-limit counters are in memory, so they reset on restart and aren't shared between several server instances
- Access tokens can't be revoked before they expire (up to 15 minutes); refresh tokens can
- Browser-level (end-to-end) tests for the UI
- **Third-party integrations come last**, after the core product is done: Stripe billing, email delivery and verification, hosted logging/monitoring (e.g. Serilog sinks), and anything else that calls an external service. Until then, features that need them use local stand-ins behind an interface.

## Status

- Backend: `dotnet build` passes with 0 warnings and 0 errors (.NET SDK 10.0.402).
- Frontend: `npm install && npm run build` passes.
- **68 automated xUnit tests pass** (`dotnet test`). They start the real API in memory against SQLite and cover tenant isolation (including a mutation check: removing a query filter makes 7 tests fail), roles and the last-admin rule, the invite lifecycle (expiry, reuse, revoke, hashed storage), refresh rotation and theft detection, password change, rate limiting, validation, projects, tasks, comments, dashboard numbers and the activity feed.
- Verified against the running servers: CORS preflight for every endpoint the browser uses, and the 401 → refresh → retry sequence the Angular interceptor relies on.
- **Not verified in a browser:** the redesigned Angular pages build under strict template checking and match the live API's response shapes, but nobody has clicked through them yet. The registration and dashboard flow was confirmed in a browser before the redesign.
- `Directory.Packages.props` pins some transitive packages (OpenApi, SQLite native lib, Pkcs) to patched versions. Keep those pins in sync with the main packages when upgrading.
