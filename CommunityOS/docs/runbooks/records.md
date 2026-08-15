# Records Service Runbook

> **STATUS: RATIFIED (Prompt 08A-R); NOT IMPLEMENTED.**
> Operational notes for the future Records service (ADR-023 Accepted,
> `docs/records.md` ratified). Implementation proceeds in Prompt 08B.

## Services

- **Records API** — `src/Services/Records/CommunityOS.Records.API` (proposed).
  Project name `CommunityOS.Records.API`.

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see Implementation steps).
- The **transactional outbox** (ADR-015) must be implemented and enabled before
  the `DocumentDeactivated`/`DocumentRestored` reconciliation consumer is
  enabled in a shared environment. See "Outbox gate" below.

## Implementation-time steps (Prompt 08B)

1. Add the service projects under `src/Services/Records/` and reference them in
   `CommunityOS.sln` (mirror the Knowledge/Documents project structure: Domain /
   Application / Infrastructure / API).
2. Add `CREATE DATABASE communityos_records;` to
   `docker/init/01-create-databases.sql`.
3. Create the initial EF Core migration (`InitialCreateRecords`).
4. Wire `AddCommunityOSEventBus`, `ConfigureAuthorizationService` +
   `HttpAuthorizationEvaluator` + `AuthorizationGuard`, and the open-generic
   `INotificationHandler<>` integration publisher — following the existing
   wiring exactly.
5. Register the ratified `records.*` permission matrix (17 permissions,
   including `records.record.scope.manage`) in the Authorization permission
   catalog (supersedes the `records.record.read/create/verify` documented
   examples).
6. Implement the `OrganizationUnitCreated/Updated/ParentChanged` consumer into
   `organization_unit_references` (ADR-016 pattern).
7. Implement the `DocumentDeactivated`/`DocumentRestored` consumer for
   hold/evidence reconciliation — **requires the transactional outbox** (see
   below).
8. Configure the `DocumentsService` section so Records can command the
   Documents reference/classify surface as the `communityos-records` service
   principal (evidence attachment, hold references).

> **Classify is a full-replacement operation (implementation note).**
> `POST /documents/{id}/classify` replaces the document's entire classification
> metadata (classification code, sensitive flag, retention category, and both
> hold references) in a single write. When Records places or releases a hold
> through that API, it must first read the document's current classification
> metadata and resubmit the existing classification/sensitive/retention values
> alongside the changed hold reference — otherwise those values are
> unintentionally overwritten with defaults. This is a Prompt 08B Records-side
> implementation concern; the Documents contract itself is correct.

## Outbox gate (ADR-015)

Best-effort publication is safe only for Records events with no live consumer.
The transactional outbox is a **hard prerequisite** before:

- enabling the `DocumentDeactivated`/`DocumentRestored` reconciliation
  consumer, and
- any Audit or Workflow subscription to `CommunityOS.Contracts.Records`.

The MassTransit EF Core outbox is the intended mechanism (ADR-015, amended at
Prompt 08A-R2). Records is the earliest service with a guaranteed-delivery
consumer, so the outbox lands at the Records integration gate — not deferred
to the Correspondence/Audit gate. The outbox-protected set is **non-exhaustive
by design**: any Records event consumed by a guaranteed-delivery consumer
(Audit, Workflow, or any future consumer) is protected whether or not it is
listed in `docs/records.md`; never publish a compliance-critical event
best-effort merely because its name is absent from the table.

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which must include `communityos_records`. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Records API (from the repo root):

   ```sh
   dotnet run --project src/Services/Records/CommunityOS.Records.API
   ```

   In development the pipeline automatically migrates the `records` schema to
   the latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used.

## Database

- Database: `communityos_records`; schema `records`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_records;Username=communityos;Password=communityos`
  (key `ConnectionStrings:RecordsDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `RecordsDbContextFactory` provides the
design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Records/CommunityOS.Records.Infrastructure \
  --startup-project src/Services/Records/CommunityOS.Records.Infrastructure \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Records/CommunityOS.Records.Infrastructure \
  --startup-project src/Services/Records/CommunityOS.Records.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:RecordsDb` | | `Host=localhost;Port=5432;Database=communityos_records;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-records` | Audit identifier |
| `DocumentsService:BaseUrl` | | *(see Documents runbook)* | Documents API base URL (evidence + hold references) |
| `DocumentsService:AccessToken` | | *(empty in dev)* | Service token presented to Documents |
| `DocumentsService:ClientId` | | `communityos-records` | `X-Client-Id` sent to Documents |
| `Records:Retention:ReviewDispositionEnabled` | | `true` | Expiry flags review instead of destroying |
| `Records:InternalClientId` | | *(reserved)* | Trusted in-process caller for future fact queries |

The Records service never reads the Authorization, Organization, Community or
Documents databases. If `AuthorizationService:BaseUrl` or the presented token is
misconfigured, every guarded endpoint returns `403 Forbidden` (fail-closed).

## Health and operations

- Health probe: `GET /health`.
- Logs: Serilog to console (Seq endpoint when configured). Never log field
  values, hold reasons, secrets or names.

## Testing

Unit and security regression tests do not require Docker:

```sh
dotnet test tests/Unit/CommunityOS.Records.Tests
```

Integration tests boot PostgreSQL via Testcontainers and require Docker; they
cover EF mapping, migrations, and the `DocumentDeactivated`/`DocumentRestored`
reconciliation consumer. These tests are enabled only once the transactional
outbox is in place:

```sh
dotnet test tests/Integration/CommunityOS.Records.IntegrationTests
```

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`409` on `verify` for a creator** — separation of duties: the creator
  cannot verify the same record; the action is rejected and audited.
- **`409` on `deactivate` for a held record** — an active legal/administrative
  hold blocks deactivation; release the hold (`POST /holds/{id}/release`) or use
  `records.record.admin` with a reason.
- **Evidence on a deactivated document** — the `DocumentDeactivated` consumer
  flags the evidence reference; reconcile by re-verifying the document or
  attaching alternative evidence. If this consumer is missing events, check that
  the transactional outbox is enabled (best-effort delivery loses events).
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_records` exists (init script only runs on a fresh volume).
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated and the build is up to date (warnings are treated as
  errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.