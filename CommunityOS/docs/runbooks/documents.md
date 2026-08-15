# Documents Service Runbook

> **STATUS: RATIFIED (Prompt 07A-R).** Operational notes for the future
> Documents service (ADR-022 Accepted, `docs/documents.md` ratified).
> Implementation-time steps (project scaffolding, database creation, migrations,
> container additions) are listed here so they are not forgotten, but are
> **not** performed in this prompt.

## Services

- **Documents API** — `src/Services/Documents/CommunityOS.Documents.API`
  (proposed). Project name `CommunityOS.Documents.API`.

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16, RabbitMQ **and MinIO** (see Implementation steps).

## Implementation-time steps (Prompt 07B)

1. Add the service projects under `src/Services/Documents/` and reference them in
   `CommunityOS.sln` (mirror the Knowledge project structure: Domain /
   Application / Infrastructure / API).
2. Add `CREATE DATABASE communityos_documents;` to
   `docker/init/01-create-databases.sql`.
3. Add a MinIO service to `docker/docker-compose.yml` (e.g.
   `quay.io/minio/minio server /data --console-address ":9001"`) and a
   `communityos-documents` bucket (created by an init script or the service
   bootstrap).
4. Create the initial EF Core migration (`InitialCreateDocuments`).
5. Add `AWSSDK.S3` to the Infrastructure project and implement the
   `IDocumentObjectStorage` S3 adapter (MinIO-compatible endpoint via
   `Documents:Storage:*`); domain/application layers must never reference the
   SDK directly.
6. Wire `AddCommunityOSEventBus`, `ConfigureAuthorizationService` +
   `HttpAuthorizationEvaluator` + `AuthorizationGuard`, and the open-generic
   `INotificationHandler<>` integration publisher — all following the existing
   Knowledge wiring exactly. Publish `DocumentContentDownloaded` on sensitive
   content downloads (ratified).

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which must include `communityos_documents`. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Documents API (from the repo root):

   ```sh
   dotnet run --project src/Services/Documents/CommunityOS.Documents.API
   ```

   In development the pipeline automatically migrates the `documents` schema to
   the latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used.

## Database

- Database: `communityos_documents`; schema `documents`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_documents;Username=communityos;Password=communityos`
  (key `ConnectionStrings:DocumentsDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `DocumentsDbContextFactory` provides the
design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Documents/CommunityOS.Documents.Infrastructure \
  --startup-project src/Services/Documents/CommunityOS.Documents.Infrastructure \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Documents/CommunityOS.Documents.Infrastructure \
  --startup-project src/Services/Documents/CommunityOS.Documents.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Object storage

- Binary content lives in S3-compatible object storage behind an
  `IDocumentObjectStorage` abstraction. MinIO is the self-hosted default; any
  S3-compatible provider is a configuration change. The concrete client is
  **AWSSDK.S3**; only the Infrastructure adapter references it.
- Object keys are content-addressed: `documents/{sha256}`.
- Bucket: one per environment (e.g. `communityos-documents-dev`).
- Enable bucket versioning and (where available) replication for DR.

### MinIO health and operations

- Console: `http://localhost:9001`; S3 API: `http://localhost:9000`.
- Local backup example (installed separately):

  ```sh
  mc alias set minio http://localhost:9000 <access> <secret>
  mc mirror minio/communityos-documents-dev ./documents-backup
  ```

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:DocumentsDb` | | `Host=localhost;Port=5432;Database=communityos_documents;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-documents` | Audit identifier |
| `Documents:Storage:Endpoint` | | `localhost:9000` | S3-compatible endpoint |
| `Documents:Storage:AccessKey` / `SecretKey` | | MinIO credentials | Object storage credentials |
| `Documents:Storage:Bucket` | | `communityos-documents-dev` | Bucket name |
| `Documents:Storage:UseHttp` | | `true` *(local)* / `false` *(prod)* | Secure transport in prod |
| `Documents:Storage:EncryptionAtRest` | | `false` | Recorded storage-layer encryption status |
| `Documents:Upload:MaxFileSizeBytes` | | `52428800` | Maximum upload size (50 MiB) |
| `Documents:Upload:AllowedMimeTypes` | | *(allowlist)* | Allowed content types |
| `Documents:Integrity:VerifyHashOnRead` | | `true` | Recompute SHA-256 on download |
| `Documents:MalwareScanning:Enabled` | | `false` | Enable the scan extension point |

The Documents service never reads the Authorization database. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed).

## Health and operations

- Health probe: `GET /health`.
- Logs: Serilog to console (Seq endpoint when configured). Never log content or
  filenames.

## Testing

Unit and security regression tests do not require Docker:

```sh
dotnet test tests/Unit/CommunityOS.Documents.Tests
```

Integration tests boot PostgreSQL and MinIO via Testcontainers and require
Docker:

```sh
dotnet test tests/Integration/CommunityOS.Documents.IntegrationTests
```

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_documents` exists (init script only runs on a fresh
  volume).
- **Object storage failures** — verify MinIO is up, the bucket exists, and the
  configured credentials match; check `Documents:Storage:UseHttp` matches the
  local scheme.
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated and the build is up to date (warnings are treated as
  errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.