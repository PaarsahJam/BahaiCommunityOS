# Prompt 08B — Records Completion Report

**Date:** 2026-08-19
**Scope:** Records bounded context (ADR-023): Domain, Application, Infrastructure,
API, the 17-permission matrix, the 16-event contract, transactional outbox
(ADR-015, ratified at the Records gate), Organization read-model projection,
Documents hold/evidence reconciliation, EF Core migrations, baseline catalog
seeding, configuration, unit/integration tests, and documentation.
**Method:** Static inspection, verification subagents (Application/API,
Infrastructure, Domain/tests), `dotnet ef migrations has-pending-model-changes`,
solution build, and unit test runs. Integration tests compile as part of the
solution but cannot run in this environment (no Docker available).

## 1. Deliverables

| # | Deliverable | Status | Evidence |
|---|-------------|--------|----------|
| 1 | Solution + project wiring | **DONE** | `CommunityOS.sln` references `CommunityOS.Records.Domain/Application/Infrastructure/API`, `CommunityOS.Records.Tests`, `CommunityOS.Records.IntegrationTests`. |
| 2 | Database provisioning | **DONE** | `docker/init/01-create-databases.sql` includes `CREATE DATABASE communityos_records;`. |
| 3 | EF Core migrations | **DONE** | `InitialCreateRecords` (13 domain tables + MassTransit outbox tables in `records` schema) and `AddRetentionRuleMaximumPeriod` (nullable `maximum_period` varchar(30) on `retention_rules`). `has-pending-model-changes` clean. |
| 4 | Domain aggregates + invariants | **DONE** | `Record` (guarded lifecycle, append-only corrections, multi-scope, holds, evidence, retention-expiry flag), `RetentionSchedule`/`RetentionRule` (ISO-8601 period + `MaximumPeriod`), `RecordCategory`, `OrganizationUnitReference`. |
| 5 | Application commands/queries + authorization | **DONE** | All 17 permissions enforced via `AuthorizationGuard` (fail-closed). Multi-scope access: a caller is granted when the permission is effective at **any** of the record's organization scopes (primary + additional), resourceType always `"record"`. |
| 6 | Infrastructure | **DONE** | EF `RecordsDbContext` (schema `records`), repositories, `HttpAuthorizationEvaluator`, `HttpDocumentsServiceClient`, integration publisher + consumers. |
| 7 | API | **DONE** | `RecordsController`, `HoldsController`, `RetentionController`, `CategoriesController` under `/api/v1/records`, `/holds`, `/retention/schedules`, `/categories`; ProducesResponseType annotations aligned with status-code contract. |
| 8 | Integration events | **DONE** | 16 events in `CommunityOS.Contracts.Records`; open-generic `RecordsIntegrationEventPublisher<TDomainEvent>` (MediatR `INotificationHandler<>`). |
| 9 | Outbox gate (ADR-015) | **DONE** | `AddCommunityOSEventBusWithOutbox<RecordsDbContext>` in the API wiring; outbox entities owned by `RecordsDbContext` so events commit atomically with record changes. |
| 10 | Permissions registered | **DONE** | 17 `records.*` permissions in `PermissionCatalog.cs` + Authorization development role seeds. |
| 11 | Baseline category catalog | **DONE** | `RecordsCatalogSeeder` seeds `birth, marriage, death, membership, appointment, official-community, administrative` idempotently after migrate in the dev pipeline. |
| 12 | Configuration | **DONE** | `appsettings.json` / `appsettings.Development.json` (RecordsDb, JWT, RabbitMq, AuthorizationService, DocumentsService). |
| 13 | Tests | **DONE** | Records unit tests 64/64; domain + security regression suites expanded (see §5). |
| 14 | Documentation | **DONE** | `docs/records.md`, `docs/api/records.md`, `docs/runbooks/records.md` aligned with implementation. |

## 2. Layer summary

- **Domain** (`src/Services/Records/CommunityOS.Records.Domain`): `Record`
  aggregate with guarded lifecycle `Draft → Submitted → Under Review → Verified →
  Archived | Deactivated` plus `Rejected`; verified facts frozen into an
  immutable `RecordVersion`; post-verification corrections append superseding
  versions (never in-place mutation); holds (`legal` | `administrative`, reason
  sensitive, releaser ≠ placer, blocks deactivation unless `records.record.admin`
  with reason); retention expiry flags review (never destroys); evidence
  references reconciled on `DocumentDeactivated`/`DocumentRestored`;
  `RetentionRule.MaximumPeriod` validated as ISO-8601; duplicate categories and
  duplicate retention schedules rejected.
- **Application** (`...Records.Application`): MediatR commands/queries, DTOs,
  FluentValidation validators, `AuthorizationGuard` over the HTTP evaluator,
  multi-scope authorization helper (`RecordAuthorization`,
  `RequireForRecordAsync`/`HasForRecordAsync`/`ContextsFor`), fail-closed
  filtering at the list/query boundary, sensitive-read capability gate,
  `EnsureCategoryExistsAsync` → `InvalidRecordCategoryReferenceException` (400),
  `DuplicateRecordCategoryException`/`DuplicateRetentionScheduleException` (409).
- **Infrastructure** (`...Records.Infrastructure`): `RecordsDbContext` (schema
  `records`, owns outbox entities), repositories, migrations,
  `RecordsCatalogSeeder`, `RecordsIntegrationEventPublisher<TDomainEvent>`,
  `DocumentLifecycleIntegrationEventConsumer`
  (`DocumentDeactivated`/`DocumentRestored` reconciliation),
  `OrganizationIntegrationEventConsumer` (ADR-016 read-model projection),
  `HttpAuthorizationEvaluator`, `HttpDocumentsServiceClient` (classify
  full-replacement handled: current classification/sensitive/retention values are
  read and resubmitted with the changed hold reference).
- **API** (`...Records.API`): versioned controllers, `ExceptionHandlingMiddleware`
  mapping (400/401/403/404/409/500 problem-details), JWT RS256-only validation,
  outbox wiring, seeder hook in `MigrateDbAsync`.

## 3. Database / migration

- Database `communityos_records`, schema `records`. Tables: `records`,
  `record_versions`, `record_field_values`, `record_working_fields`,
  `record_scopes`, `record_categories`, `retention_schedules`, `retention_rules`
  (incl. `maximum_period`), `record_holds`, `record_hold_document_references`,
  `record_evidence_references`, `record_classification`,
  `organization_unit_references`, plus MassTransit `InboxState`/`OutboxMessage`/
  `OutboxState`.
- Migration `AddRetentionRuleMaximumPeriod` (`20260819103859`) adds the nullable
  `maximum_period` column. `has-pending-model-changes` reports no drift.
- Two ratified implementation choices (recorded in `docs/records.md` → Deviations):
  working field set persisted in `record_working_fields` (frozen into
  `record_versions` at verification), and the ratified `record_lifecycle_events`
  table is not created (lifecycle transitions flow through the outbox).

## 4. Integration

- **Outbox (ADR-015, ratified at the Records gate):** `AddCommunityOSEventBusWithOutbox<RecordsDbContext>`
  registers `UsePostgres` + `UseBusOutbox`; domain events are published
  *before* `SaveChanges` so forwarded integration events and the record row
  commit atomically. This satisfies the hard guaranteed-delivery prerequisite for
  the `DocumentDeactivated`/`DocumentRestored` consumer and for future
  Audit/Workflow subscriptions.
- **Documents:** Records commands the Documents reference/classify surface as the
  `communityos-records` service principal (evidence attach, hold references);
  consumes `DocumentDeactivated`/`DocumentRestored` to flag/clear evidence
  references (consumer registered only when the outbox gate is on). Records never
  reads the Documents database.
- **Organization:** consumes `OrganizationUnitCreated/Updated/ParentChanged` into
  `organization_unit_references` (ADR-016 pattern); scoping never depends on a
  live Organization query.
- **Authorization:** no direct Authorization DB access; `AuthorizationGuard` →
  `HttpAuthorizationEvaluator` → Authorization check API (ADR-018/019).
- **Community:** subject ids are stable person/household ids; names resolved at
  read time; no PII stored.

## 5. Permission matrix and event contract

- 17 permissions ratified and enforced: `records.record.create/read/read.sensitive/
  update/submit/verify/correct/archive/deactivate/restore/classify/scope.manage/
  evidence.manage/admin`, `records.retention.manage`, `records.hold.manage`,
  `records.category.manage`.
- 16 integration events implemented (`CommunityOS.Contracts.Records`). Payloads
  carry only stable ids and minimal lifecycle metadata; no field values, no
  secrets, no names, no hold reasons (locked by unit tests). Hold events
  intentionally carry no document references (resolved via `GET /holds/{id}`).
- Multi-scope semantics: read/list/guarded operations evaluate the permission at
  **any** of the record's organization scopes; denied reads surface as `404`
  (no enumeration oracle).

## 6. Tests

- Records unit tests **64/64** passing (`tests/Unit/CommunityOS.Records.Tests`):
  domain invariants (lifecycle, verified-fact immutability, superseding versions,
  hold-type invariant, ISO-8601 period/maximum-period validation, retention
  expiry never destroying, separation of duties), security regression
  (fail-closed authorization, unreachable evaluator, missing-subject denial,
  denied-read = 404, list filtering, sensitive-gate, multi-scope grant via
  additional scope only, multi-scope denial at neither scope, no persistence on a
  denied update/verify/deactivate, no PII/hold-reasons in integration events,
  baseline catalog, `DocumentDeactivated`/`DocumentRestored` reconciliation),
  and JWT RS256-only validation.
- Other unit suites unaffected and green: Knowledge 49/49, Documents 56/56,
  Authorization 118/118, Community 106/106.
- Integration tests (`tests/Integration/CommunityOS.Records.IntegrationTests`)
  compile as part of the solution (EF mapping, both migrations, retention-rule
  maximum-period roundtrip, outbox tables) but are **not runnable in this
  environment: no Docker/PostgreSQL available**. They must be run in an
  environment with Docker before production use.

## 7. Build and EF verification

- `dotnet build CommunityOS.sln`: **0 warnings, 0 errors**.
- `dotnet ef migrations has-pending-model-changes`: **no pending model changes**.
- Verification commands used `$env:DOTNET_ROLL_FORWARD="Major"` (this machine
  lacks the .NET 9 runtime; .NET 10 runtime used).

## 8. Deviations and follow-ups

- **Documents base URL default is empty** in `appsettings.json`; the Documents
  HTTP client fails closed with a clear `InvalidOperationException` until
  `DocumentsService:BaseUrl` is configured per environment (the Documents API port
  is not established in the repo).
- **Integration tests cannot be executed here** (no Docker). Report honestly:
  they compile only.
- **Non-blocking findings** carried from verification: event schema-evolution /
  versioning convention is namespace-based only (ADR-004/015); the read model is
  not self-healing for a lost `OrganizationUnitCreated` (pre-existing pattern).
- Prompt 08B is complete. **Prompt 08C is not begun.**

## 9. Result

**Prompt 08B: COMPLETE.** Records is implemented end-to-end, the solution builds
clean, all unit suites pass, EF model is in sync, the outbox gate is wired, and
the docs are aligned with the implementation. The only unverifiable item in this
environment is the Testcontainers integration suite (requires Docker).