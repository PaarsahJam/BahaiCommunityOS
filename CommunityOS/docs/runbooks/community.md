# Community Service Runbook

## Services

- **Community API** — `src/Services/Community/CommunityOS.Community.API`.
  Project name `CommunityOS.Community.API`.

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see `docker/docker-compose.yml`).

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs
   `docker/init/01-create-databases.sql`, which creates `communityos_community`
   alongside the other service databases. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Community API (from the repo root):

   ```sh
   dotnet run --project src/Services/Community/CommunityOS.Community.API
   ```

   In development the pipeline automatically migrates the `community` schema to
   the latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used, e.g.
> `$env:DOTNET_ROLL_FORWARD="Major"; dotnet run ...`.

## Database

- Database: `communityos_community`; schema `community`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_community;Username=communityos;Password=communityos`
  (key `ConnectionStrings:CommunityDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; `CommunityDbContextFactory` provides the design-time
factory so migrations can be added without booting the host.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Community/CommunityOS.Community.Infrastructure \
  --startup-project src/Services/Community/CommunityOS.Community.API \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Community/CommunityOS.Community.Infrastructure \
  --startup-project src/Services/Community/CommunityOS.Community.API
```

> Note: `dotnet ef` requires the EF Core tools (`dotnet tool install -g
> dotnet-ef`). This repo pins the local tool manifest to `dotnet-ef` 10.0.7 in
> `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Configuration

| Key | Example | Notes |
|-----|---------|-------|
| `ConnectionStrings:CommunityDb` | `Host=localhost;Port=5432;Database=communityos_community;Username=communityos;Password=communityos` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | `http://localhost:5001` | Identity OIDC discovery endpoint (JWKS for RS256 token validation) |
| `Jwt:Issuer` / `Jwt:Audience` | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience validated |
| `Jwt:RequireHttpsMetadata` | `false` *(local)* / `true` *(prod)* | Require HTTPS when fetching the Identity discovery document |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | `communityos-community` | Audit identifier |

The Community service never reads the Authorization database. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed).

## Health and operations

- Health probe: `GET /health`.
- Logs: Serilog to console (Seq endpoint when configured).

## Testing

Unit and security regression tests do not require Docker:

```sh
dotnet test tests/Unit/CommunityOS.Community.Tests
```

Integration tests boot a real PostgreSQL via Testcontainers and require Docker:

```sh
dotnet test tests/Integration/CommunityOS.Community.IntegrationTests
```

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_community` exists (init script only runs on a fresh
  volume).
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated with the migration and that the build is up to date
  (warnings are treated as errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` and confirm the local tool was restored
  (`dotnet tool restore`).
- **Migrations folder analyzer errors** — the generated `Migrations/.editorconfig`
  disables the machine-formatting rules that conflict with generated code.
