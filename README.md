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
| Auth | Register a company (creates a tenant + first Admin), login, JWT |
| Roles | `Admin` and `Member`. Admins manage people and can delete projects; Members can create and edit work. Enforced on the API, mirrored in the UI |
| Team | List members; admins change roles and remove people. A company always keeps at least one admin |
| Invitations | Admin creates an invite and gets a one-time link to share by hand (email is postponed). Only a SHA-256 hash of the token is stored; links expire in 7 days and work once |
| Projects and tasks | Per-company projects with a To do / In progress / Done board, assignees, due dates, archive. Tasks can only be assigned to people in the same company |

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
- Refresh tokens, FluentValidation, rate limiting, automated tests
- Opening an invite link while already signed in redirects to the dashboard (the `/auth` routes are guest-only)
- **Third-party integrations come last**, after the core product is done: Stripe billing, email delivery and verification, hosted logging/monitoring (e.g. Serilog sinks), and anything else that calls an external service. Until then, features that need them use local stand-ins behind an interface.

## Status

- Backend: `dotnet build` passes with 0 warnings and 0 errors (.NET SDK 10.0.402).
- Frontend: `npm install && npm run build` passes.
- API verified by running it against two companies: auth, role enforcement (403 for members on admin actions), last-admin protection, the full invite flow (single use, revoke, cross-company isolation), projects and tasks including cross-company 404s and cross-company assignee rejection, archive, and cascade delete.
- UI verified in a browser earlier: register and the dashboard. The Team, Projects, project board, accept-invite pages and the new dashboard compile under strict template checking but have **not yet been clicked through in a browser**.
- Not yet verified: sign out and back in, and the token-expiry (401) logout.
- There are no automated tests yet; the API checks above were run by hand against a live server.
- `Directory.Packages.props` pins some transitive packages (OpenApi, SQLite native lib, Pkcs) to patched versions. Keep those pins in sync with the main packages when upgrading.
