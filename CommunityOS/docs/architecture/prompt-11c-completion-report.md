# Prompt 11C — Search Completion Report

**Date:** 2026-08-21
**Scope:** Search bounded context (ADR-026, ADR-017 slot 10): Domain,
Application, Infrastructure, API, the 3-permission matrix, inbox-only event
consumption from Records/Documents/Workflow/Knowledge/Organization (23
integration events), PostgreSQL full-text projection (`tsvector`/GIN), EF Core
migrations, configuration, unit tests, compile-only integration tests, and
documentation.
**Method:** Static inspection, `dotnet build CommunityOS.sln`,
`dotnet ef migrations has-pending-model-changes`, and `dotnet test` for the
unit suite. Integration tests compile as part of the solution but cannot run in
this environment (no Docker available).

## 1. Deliverables

| # | Deliverable | Status | Evidence |
|---|-------------|--------|----------|
| 1 | Solution + project wiring | **DONE** | `CommunityOS.sln` references `CommunityOS.Search.Domain/Application/Infrastructure/API`, `CommunityOS.Search.Tests`, `CommunityOS.Search.IntegrationTests`, nested under a `Search` solution folder. |
| 2 | Database provisioning | **DONE** | `docker/init/01-create-databases.sql` includes `CREATE DATABASE communityos_search;`. |
| 3 | EF Core migrations | **DONE** | `InitialCreateSearch` (`20260821150633`): schema `search`; tables `search_documents`, `search_document_scopes`, `search_index_log`, `organization_unit_references` + MassTransit `InboxState`/`OutboxMessage`/`OutboxState`; GIN index on `search_vector`; unique `(source_type, source_id)` idempotency index. `has-pending-model-changes` clean. |
| 4 | Domain model | **DONE** | `SearchDocument` aggregate keyed by `(source_type, source_id)` with sparse-merge `Apply` semantics, stale-event guard (`occurredOn < IndexedOn` rejected; equal accepted as idempotent), owned scope child table, `NpgsqlTsVector` column. Display-title privacy rules enforced at the consumer mapping layer (Records = category code only; Knowledge = literal `"Question"`). |
| 5 | Consumers (23 events) | **DONE** | Five consumers: Records (5 events), Documents (6), Workflow tasks (5), Knowledge questions (6), Organization unit projection (3). Field mappings verified against the actual contract records; no PII or sensitive text fields are ever indexed. |
| 6 | Inbox-only consumption | **DONE** | New `AddCommunityOSEventBusWithInbox<TDbContext>` variant registers the MassTransit EF Core inbox without bus outbox (Search emits no outbound events at this gate); all five consumers registered with exactly-once semantics over `SearchDbContext`. |
| 7 | Query surface + authorization | **DONE** | `GET /search` and `GET /search/{sourceType}` enforce `search.result.read` (coarse gate) and then per-row any-of batch authorization across primary + additional scopes (global unscoped rows use a resource-exact context), mirroring the Records/Workflow/Notifications pattern. Sensitive rows additionally require `search.result.read.sensitive` at row scope. No count or marker of inaccessible hits. |
| 8 | Admin endpoints | **DONE** | `GET /admin/health` (per-source document counts, last-event timestamp, applied-event count) and `POST /admin/reindex[/{sourceType}]` guarded by `search.index.manage`; reindex is a synchronous internal-consistency repair serialized by a PostgreSQL advisory lock. |
| 9 | Permissions registered | **DONE** | `search.result.read`, `search.result.read.sensitive`, `search.index.manage` in `PermissionCatalog.cs`; seeded to GlobalAdministrator and NationalAdministrator in `AuthorizationSeeder.cs`. |
| 10 | Validation pipeline | **DONE** | FluentValidation validators + `ValidationPipelineBehavior` registered via `AddSearchApplication` (Notifications pattern): blank query, unknown type codes, unknown reindex source type → 400; `limit < 1` → 400; negative offset → 400. |
| 11 | API pipeline | **DONE** | Records-style `Program.cs` + `ConfigurePipelineAsync`: `ExceptionHandlingMiddleware` (400/401/403/500 problem-details), Swagger + dev migration-on-startup, HTTPS redirection, JWT RS256-only validation. |
| 12 | Configuration | **DONE** | `appsettings.json` / `appsettings.Production.json` (`SearchDb`, JWT, RabbitMq, AuthorizationService, `Search:DefaultLanguage`, `Search:MaxResultsPerPage` bound to `SearchOptions`). |
| 13 | Tests | **DONE** | Search unit tests **25/25 passing**; integration suite compiles but is not runnable here (no Docker). |
| 14 | Documentation | **DONE** | `docs/search.md`, `docs/api/search.md`, `docs/runbooks/search.md` status headers updated to RATIFIED AND IMPLEMENTED; ADR-017 slot 10 updated to implemented (Prompt 11C gate). |

## 2. Findings classification

### A. Implemented as ratified

- Six-project layout, `communityos_search` database, `search` schema,
  snake_case projection tables, unique `(source_type, source_id)` idempotency,
  GIN-indexed `tsvector`, owned scope child table.
- Stale-event guard: newer-or-equal `OccurredOn` may update; older events are
  rejected without touching the index log.
- Consumer→contract mappings for all 23 events, including the deliberate
  omissions: `RecordCreated.SubjectType/SubjectId/CreatedBy` never indexed,
  `QuestionFlagged.FlagReason` never indexed, `WorkflowTaskAssigned`
  carries no unit (sparse merge preserves the created unit).
- Terminal statuses (`Deactivated`, `Archived`, `Merged`, `Cancelled`)
  excluded from results by default; explicitly requested statuses override the
  default exclusion (per the ratified query-parameter table).
- Inbox-only MassTransit wiring; no Search outbox usage at this gate.
- Fail-closed JWT (RS256-only algorithms, JWKS discovery, issuer/audience/
  lifetime/signing-key validation, subject claim required).

### B. Corrected during implementation

The working tree contained an unfinished attempt. The following defects were
found by inspection against the ratified contracts and corrected:

1. **Fail-closed scoping hole (critical).** The search endpoint applied only a
   coarse permission check and then trusted an unrestricted SQL query: when no
   `organizationUnitId` filter was supplied the store returned rows across
   *all* scopes regardless of the caller's grants. Replaced with the
   established authorize-then-paginate walk (as in Records
   `ListRecordsQueryHandler` / Notifications inbox): candidate batches are
   fetched deterministically (rank desc, indexed-on desc, id), each row is
   authorized via `EvaluateBatchAsync` any-of over its contexts
   (`AuthorizationContext(unit, "search.result", sourceId)` per scope;
   resource-exact context when unscoped), and sensitive rows require a second
   sensitive-permission pass at row scope.
2. **Enumeration oracle via `count(*) OVER()`.** The previous SQL reported the
   raw match count before authorization, leaking how many inaccessible hits
   existed. `totalCount` is now the count of *authorized* rows only.
3. **Index health log was never written.** `GET /admin/health` read
   `search_index_log`, but nothing inserted into it — every deployment would
   report empty sources. The projection writer now upserts the log row in the
   same transaction as the projection write (last-event timestamp moves
   forward only; `indexed_count` increments once per accepted event; stale
   events touch nothing).
4. **Reindex was a no-op.** `NoOpProjectionReconciler` silently did nothing
   while the runbook promises repair. Implemented `SearchProjectionReconciler`:
   one transaction, `pg_advisory_xact_lock('communityos-search-reindex')`,
   recompute all tsvectors from stored `display_title || ' ' || type_code`,
   resynchronize per-source-type log counts from actual rows. First-gate
   semantics preserved: no cross-service reads.
5. **Missing validation pipeline.** Handlers threw bare `ArgumentException`s;
   the ratified contract requires 400 responses through the established
   FluentValidation pipeline. Added validators + behavior; controller now binds
   `limit` as nullable so "absent" (default 25) is distinct from invalid (<1 →
   400); values above max clamp to `Search:MaxResultsPerPage`.
6. **API pipeline gaps.** No exception middleware, no Swagger, no
   migration-on-startup. Ported the Records `ConfigurePipelineAsync` pattern
   wholesale (problem-details middleware incl. FluentValidation error bodies,
   Swagger in Development, dev-time `MigrateAsync`).
7. **Consumer observability.** Consumers logged nothing (ADR-026 decision 14
   wants minimal structured logging). Added `LoggerMessage`-based logging of
   operation/source type/source id only — never titles or field values.
8. **Hardcoded limits / language.** Page size was hardcoded to 50 ignoring
   `Search:MaxResultsPerPage`, and the text-search config was read ad hoc from
   `IConfiguration` into interpolated SQL. Both now flow through bound
   `SearchOptions`; the language is whitelisted to `english`/`simple` before
   it ever reaches SQL.
9. **Build hygiene.** Missing `Persistence/Migrations/.editorconfig`
   (CA1861/CA1062 suppression convention used by all eight other services),
   misaligned `Npgsql` entry in `Directory.Packages.props`, non-conforming
   comma placement in the `AuthorizationSeeder` role arrays.

### C. Deviations & rationale

- **`Npgsql` version pin added to central package management.** The Domain
  project uses `NpgsqlTsVector` directly and references no EF package, so an
  explicit `Npgsql` package reference is required. Version `9.0.4` satisfies
  the `>= 9.0.2` floor imposed by `Npgsql.EntityFrameworkCore.PostgreSQL
  9.0.0`. No new third-party dependency enters the graph (Testcontainers
  image set unchanged: PostgreSQL only).
- **Reindex scope interpretation.** The older design sketch described
  "truncate + re-consume from source APIs"; ADR-026's ratified first-gate
  decision forbids cross-service reads, so reindex repairs internal
  consistency (vectors + log counts), exactly as the runbook troubleshooting
  section describes.
- **Unscoped rows authorize via resource-exact context.** Consistent with
  Records/Workflow/Notifications: organization-scoped grants do not match
  resource-exact contexts, so unscoped rows are visible only to holders of
  resource-scoped grants or relationship tuples on that source id. This
  mirrors the ratified behavior of the existing services; no new grant
  semantics were invented.

## 3. Database / migration

- Database `communityos_search`, schema `search`. Tables:
  `search_documents` (projection; tsvector + GIN; unique natural key),
  `search_document_scopes` (owned additional scopes, cascade delete),
  `search_index_log` (per-source health bookkeeping),
  `organization_unit_references` (event-built hierarchy cache), plus
  MassTransit `InboxState`/`OutboxMessage`/`OutboxState` (inbox-only usage).
- Migration `InitialCreateSearch` (`20260821150633`).
  `dotnet ef migrations has-pending-model-changes`: **no pending changes**.

## 4. Integration

- **Consumers:** five consumer classes over 23 events, each writing through
  `SearchProjectionWriter.UpsertAsync` (transactional upsert + vector refresh
  + index-log bookkeeping, stale events short-circuit before any write).
  `OrganizationUnitProjectionConsumer` maintains the local reference table;
  Search never calls the Organization service at query time.
- **Authorization:** no direct DB access to Authorization; coarse gates go
  through `AuthorizationGuard` and per-row checks through the registered
  `HttpAuthorizationEvaluator` (fail-closed HTTP evaluator with batch support).
- **Event publication:** none at this gate (consumption-only service).

## 5. Permission matrix

- 3 permissions ratified and enforced: `search.result.read`,
  `search.result.read.sensitive`, `search.index.manage`. Role seeds:
  GlobalAdministrator and NationalAdministrator receive all three.
- Sensitive results are doubly gated: capability required coarsely (request
  level) and per-row at the row's own scopes.

## 6. Tests

- Search unit tests **25/25 passing**
  (`tests/Unit/CommunityOS.Search.Tests`):
  - `SearchProjectionTests` — category-code-only record projection, literal
    knowledge-question title, sparse-merge preservation, stale/equal guard.
  - `SearchAuthorizationTests` — supplied-scope denial fails closed before any
    data access; out-of-scope rows silently dropped with truthful
    `totalCount`; additional-scope any-of rule; sensitive rows require the
    sensitive capability at row scope even after passing the read pass;
    pagination slices authorized rows only; limit clamps to configured max;
    missing actor fails fast; reindex requires `search.index.manage`.
  - `SearchQueryValidatorTests` — blank query, unknown types/sourceType,
    `limit < 1`, negative offset rejected; valid request passes; unknown
    reindex source type rejected.
  - `SearchConsumerTests` — contract-safe metadata mapping for Records,
    Documents, Workflow, Knowledge (incl. flag-reason exclusion), verified
    against the real contract record signatures.
- Integration tests (`tests/Integration/CommunityOS.Search.IntegrationTests`)
  compile as part of the solution (migration creates all tables/schema/indexes,
  writer upsert idempotency + FTS round-trip, stale rejection leaves log
  untouched, log forward-motion + counting, reconciler vector/count repair)
  but are **not runnable in this environment: no Docker/Testcontainers
  available**. They must be executed in CI before production use.

## 7. Build and EF verification

- `dotnet build CommunityOS.sln`: **0 warnings, 0 errors**, including the
  modified shared projects (EventBus building block, Authorization Application/
  Infrastructure).
- `dotnet test`: all **10 unit test projects pass** — Search 25/25,
  Authorization 118/118 (covers the catalog/seeder changes), Notifications
  73/73, Workflow 57/57, Records 73/73, Documents 56/56, Community 106/106,
  Organization 79/79, Knowledge 49/49, Identity 26/26. All nine integration
  test projects (eight pre-existing plus Search) fail solely because
  Docker/Testcontainers is unavailable (pre-existing environment limitation,
  identical failure mode across services).
- Verification used `$env:DOTNET_ROLL_FORWARD="LatestMajor"` (this machine
  lacks the .NET 9 runtime; .NET 10 runtime used).

## 8. Git scope review

Modified tracked files — all Prompt 11C scope, nothing unrelated:

| File | Change |
|------|--------|
| `CommunityOS.sln` | Six Search projects + solution folder |
| `docker/init/01-create-databases.sql` | `communityos_search` |
| `src/BuildingBlocks/CommunityOS.EventBus/EventBusServiceExtensions.cs` | Inbox-only registration variant |
| `src/Directory.Packages.props` | `Npgsql` version pin (+ alignment fix) |
| `.../Permissions/PermissionCatalog.cs` | Three `search.*` permissions |
| `.../Persistence/AuthorizationSeeder.cs` | Role seeds for the three permissions |
| `docs/search.md`, `docs/api/search.md`, `docs/runbooks/search.md`, `docs/architecture/ADR.md` | Status updates |

New untracked trees: `src/Services/Search/` (four projects + migration),
`tests/Unit/CommunityOS.Search.Tests/`, `tests/Integration/CommunityOS.Search.IntegrationTests/`.

## 9. Environment limitations

- **Docker unavailable** — the Search Testcontainers suite (and all
  pre-existing services' suites) cannot execute here; they compile and are
  wired into CI where a container runtime exists.
- **No .NET 9 runtime installed** — tests execute on the .NET 10 runtime via
  roll-forward; builds target net9.0 cleanly.

## 10. Result

The Search bounded context is implemented end-to-end against ratified ADR-026:
the full-text projection consumes all 23 first-gate integration events with
exactly-once inbox semantics, queries are fail-closed down to individual rows
with no enumeration oracle, admin health/reindex operate on real data with
advisory-lock serialization, the permission matrix is seeded and enforced, the
solution builds clean, all unit tests pass, the EF model is in sync, and the
documentation reflects implementation status.

SEARCH: IMPLEMENTED — Prompt 11C COMPLETE
