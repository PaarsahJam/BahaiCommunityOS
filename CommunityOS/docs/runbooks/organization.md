# Organization Service Runbook

## Services

- **Organization API** — `src/Services/Organization/CommunityOS.Organization.API`.
  Project name `CommunityOS.Organization.API`; runs on its own port in
  development (Authorization is expected on `http://localhost:5007`).

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 (see `docker/docker-compose.yml`).

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs
   `docker/init/01-create-databases.sql`, which creates `communityos_organization`
   alongside the other service databases. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Organization API (from the repo root):

   ```sh
   dotnet run --project src/Services/Organization/CommunityOS.Organization.API
   ```

   In development the pipeline automatically migrates the `organization` schema
   to the latest migration and seeds a minimal national hierarchy when the
   `organizations` table is empty. Swagger is available at `/swagger`.

## Database

- Database: `communityos_organization`; schema `organization`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_organization;Username=communityos;Password=communityos`
  (key `ConnectionStrings:OrganizationDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Organization/CommunityOS.Organization.Infrastructure \
  --startup-project src/Services/Organization/CommunityOS.Organization.API
```

Regenerate the model snapshot when the model changes:

```sh
dotnet ef migrations remove \
  --project src/Services/Organization/CommunityOS.Organization.Infrastructure \
  --startup-project src/Services/Organization/CommunityOS.Organization.API
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Organization/CommunityOS.Organization.Infrastructure \
  --startup-project src/Services/Organization/CommunityOS.Organization.API
```

> Note: `dotnet ef` requires the EF Core tools: `dotnet tool install -g dotnet-ef`.

## Configuration

| Key | Example | Notes |
|-----|---------|-------|
| `ConnectionStrings:OrganizationDb` | `Host=localhost;Port=5432;Database=communityos_organization;Username=communityos;Password=communityos` | PostgreSQL |
| `AuthorizationService:BaseUrl` | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | *(empty in dev)* | Service-principal bearer token with `authz.check` |
| `AuthorizationService:ClientId` | `communityos-organization` | Audit identifier |
| `Organization:InternalClientId` | `communityos-authorization` | Matches `X-Client-Id` on the internal `/covers` endpoint |

The Organization service never reads the Authorization database. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed).

The Authorization service similarly resolves organization-scoped grants over
HTTP using the `OrganizationService:BaseUrl` / `OrganizationService:AccessToken`
settings on its side; `X-Client-Id` must be `communityos-authorization` to
match `Organization:InternalClientId`.

## Health and operations

- Health probe: `GET /health`.
- Logs: Serilog to console (Seq endpoint when configured).

## Testing

Unit tests do not require Docker:

```sh
dotnet test tests/Unit/CommunityOS.Organization.Tests
```

Integration tests boot a real PostgreSQL via Testcontainers and require Docker:

```sh
dotnet test tests/Integration/CommunityOS.Organization.IntegrationTests
```

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_organization` exists (init script only runs on a fresh
  volume).
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated with the migration and that the build is up to date
  (warnings are treated as errors in this repo).
- **Migrations folder analyzer errors** — the generated `Migrations/.editorconfig`
  disables the machine-formatting rules that conflict with generated code.
