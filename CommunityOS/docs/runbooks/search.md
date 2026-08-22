# Search Service Runbook

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 11C).** Operational notes for the
> Search service (`ADR-026` Accepted, `docs/search.md` ratified). The service
> is implemented at `src/Services/Search/CommunityOS.Search.API` with unit and
> integration tests.

## Services

- **Search API** — implemented at
  `src/Services/Search/CommunityOS.Search.API`. Four projects:
  Domain / Application / Infrastructure / API, plus unit and integration test
  projects. The Search service owns only projection data; no source-of-truth
  domain facts reside here.

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see Implementation steps).
- The `communityos_search` database must be created before the first run (added
  to `docker/init/01-create-databases.sql` at Prompt 11C).
- MassTransit EF Core **inbox** enabled (`AddCommunityOSEventBusWithOutbox` or
  the inbox-only variant) so every consumer runs with exactly-once semantics.
  Search emits no outbound events at the first gate; the outbox tables are
  provisioned but unused.

## Implementation plan (Prompt 11C — first gate)

The following steps are the ratified implementation plan to be executed at the
Prompt 11C gate:

1. **Projects** — create `CommunityOS.Search.Domain`, `CommunityOS.Search.Application`,
   `CommunityOS.Search.Infrastructure`, `CommunityOS.Search.API`, unit test
   project `CommunityOS.Search.Tests`, integration test project
   `CommunityOS.Search.IntegrationTests`. Add all six to `CommunityOS.sln`.

2. **Database** — add `CREATE DATABASE communityos_search;` to
   `docker/init/01-create-databases.sql`.

3. **Contracts** — add Search permission codes to
   `CommunityOS.Authorization.Application/Permissions/PermissionCatalog.cs`:
   `search.result.read`, `search.result.read.sensitive`, `search.index.manage`.
   Add them to the development role seed in `AuthorizationSeeder.cs`.

4. **EF Core migration** — generate `InitialCreateSearch` with tables:
   - `search.search_documents` — primary projection table (`SearchDocument`)
   - `search.search_document_scopes` — additional org-unit scopes (`AdditionalScope`)
   - `search.organization_unit_references` — ADR-016 read-model
   - `search.search_index_log` — reconciliation diagnostics
   - `search.InboxState`, `search.OutboxMessage`, `search.OutboxState` — MassTransit
   - `GIN` index on `search_documents.search_vector`
   - Unique index on `(source_type, source_id)` for idempotent upsert
   - Index on `organization_unit_id` for scope filtering
   - Unique index on `organization_unit_references.organization_unit_id`

5. **Integration event consumers** — implement one consumer class per producer
   event group:
   - `RecordsIndexConsumer` — handles `RecordCreated`, `RecordClassified`,
     `RecordArchived`, `RecordDeactivated`, `RecordRestored`
   - `DocumentsIndexConsumer` — handles `DocumentCreated`, `DocumentMetadataUpdated`,
     `DocumentClassified`, `DocumentArchived`, `DocumentDeactivated`, `DocumentRestored`
   - `WorkflowIndexConsumer` — handles `WorkflowTaskCreated`, `WorkflowTaskAssigned`,
     `WorkflowTaskStarted`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled`
   - `KnowledgeIndexConsumer` — handles `QuestionSubmitted`, `QuestionPublished`,
     `QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`, `QuestionArchived`
   - `OrganizationUnitProjectionConsumer` — handles `OrganizationUnitCreated`,
     `OrganizationUnitUpdated`, `OrganizationUnitParentChanged` (ADR-016 read model)

   Each consumer upserts by `(SourceType, SourceId)` and skips the upsert when
   the incoming `OccurredOn` is older than the current `IndexedOn` (stale-event
   guard). The MassTransit inbox prevents duplicate upserts from redelivered messages.

6. **API surface** — implement the ratified endpoints from `docs/api/search.md`:
   - `GET /api/v1/search` — full-text query across all source types
   - `GET /api/v1/search/{sourceType}` — full-text query scoped to one type
   - `GET /api/v1/search/admin/health` — index health
   - `POST /api/v1/search/admin/reindex` — full rebuild
   - `POST /api/v1/search/admin/reindex/{sourceType}` — partial rebuild

7. **Authorization wiring** — wire `ConfigureAuthorizationService` +
   `HttpAuthorizationEvaluator` + `AuthorizationGuard` (same pattern as all
   implemented services). Fail-closed: if `AuthorizationService:BaseUrl` is
   misconfigured every guarded endpoint returns `403 Forbidden`.

8. **Configuration** — ship `appsettings.json` / `appsettings.Development.json` /
   `appsettings.Production.json` for the `ConnectionStrings:SearchDb`, `Jwt`,
   `RabbitMq`, `AuthorizationService`, and `Search` sections.
   `Jwt:RequireHttpsMetadata: "true"` must appear in `appsettings.Production.json`
   (fail-closed; must not fall back to `false`).

9. **Tests** — ship unit tests covering:
   authorization denial, stale-event guard, idempotent upsert, sensitive exclusion,
   pagination bounds, source-type validation, `POST /admin/reindex` idempotency,
   no-PII projection assertions, organization scope filtering, `ts_rank` ordering.
   Integration tests compile-only until Docker is available.

10. **ADR-017 status** — update the slot-10 entry in `docs/architecture/ADR.md`
    from `NOT STARTED` to `IMPLEMENTED` after Prompt 11C gate passes.

## Outbox / inbox gate

Search is an **inbox-only** service at the first gate:

- **Inbox**: the MassTransit EF Core inbox is enabled so each consumer
  processes each message exactly once. Redelivered events are deduplicated
  at the receive endpoint before reaching the consumer handler.
- **Outbox**: provisioned (tables exist) but unused; Search emits no
  integration events at the first gate. If a future gate introduces a
  `SearchIndexed` audit event, the outbox will be enabled at that gate
  (ADR-015 amendment pattern).
- **No transactional outbox required on producers**: Search is a best-effort
  projection consumer. A missed event leaves a document temporarily absent from
  search results; it remains fully accessible via the source service's own API.
  The `POST /admin/reindex` endpoint corrects drift.

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which must include `communityos_search`. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Search API (from the repo root):

   ```sh
   dotnet run --project src/Services/Search/CommunityOS.Search.API
   ```

   In development the pipeline automatically migrates the `search` schema to the
   latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used.

## Database

- Database: `communityos_search`; schema `search`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_search;Username=communityos;Password=communityos`
  (key `ConnectionStrings:SearchDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `SearchDbContextFactory` provides the design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Search/CommunityOS.Search.Infrastructure \
  --startup-project src/Services/Search/CommunityOS.Search.Infrastructure \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Search/CommunityOS.Search.Infrastructure \
  --startup-project src/Services/Search/CommunityOS.Search.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:SearchDb` | | `Host=localhost;Port=5432;Database=communityos_search;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod (`appsettings.Production.json`) |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-search` | Audit identifier |
| `Search:DefaultLanguage` | | `english` | PostgreSQL text-search dictionary |
| `Search:MaxResultsPerPage` | | `50` | Hard upper limit on `limit` query parameter |
| `Search:InternalClientId` | | *(reserved)* | Trusted in-process caller for rebuild |

The Search service never reads the Authorization, Organization, Community,
Records, Workflow, Knowledge, Documents, Notifications, or Identity databases.
If `AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed). There is no per-channel
configuration at the first gate.

## Health and operations

- Health: the Search API will expose **no dedicated health probe endpoint**
  (Records, Documents, Knowledge, Community, Workflow, and Notifications likewise
  expose none; only Organization and Authorization register `GET /health`).
  Observe availability through Serilog console/Seq logs and
  container/infrastructure probes.
- Index health: the admin endpoint `GET /api/v1/search/admin/health`
  (requires `search.index.manage`) reports document counts and last-event
  timestamps per source type.
- Logs: Serilog to console (Seq endpoint when configured). Never log
  `DisplayTitle` values, query result content, source field values, or any
  information that could contain PII.

## Testing

Unit and security regression tests do not require Docker:

```sh
dotnet test tests/Unit/CommunityOS.Search.Tests
```

Integration tests boot PostgreSQL via Testcontainers and require Docker; they
cover EF mapping, GIN index creation, migration, and consumer upsert/idempotency:

```sh
dotnet test tests/Integration/CommunityOS.Search.IntegrationTests
```

> Note: this environment has no Docker, so integration tests are expected to
> compile only and must be reported honestly as not executed.

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **Documents missing from search results** — the consumer may have missed
  events (broker downtime, consumer failure). Trigger `POST /api/v1/search/admin/reindex`
  (requires `search.index.manage`) to rebuild the index from source APIs.
  Check the inbox log for failed deliveries.
- **`400` on `types` parameter** — verify the source type code matches the
  first-gate taxonomy exactly: `record`, `document`, `workflow-task`, `knowledge-question`.
- **Sensitive results not returned** — caller must hold `search.result.read.sensitive`;
  standard `search.result.read` silently excludes sensitive rows (no enumeration oracle).
- **`GIN` index not being used by query plan** — run `ANALYZE search.search_documents`
  and verify the `tsvector` column is populated. If `search_vector` is null for
  any rows, trigger a reindex for the affected source type.
- **Consumer skipping events (stale-event guard)** — if an older event arrives
  after a newer one was already processed, the consumer intentionally skips the
  upsert (the `OccurredOn` guard). This is correct behavior; no action needed.
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated and the build is up to date (warnings are treated as
  errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.
- **Concurrent reindex races** — `POST /admin/reindex` is serialized by a
  PostgreSQL advisory lock; concurrent calls wait rather than producing
  duplicate rows.
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_search` exists (init script only runs on a fresh volume).
