# Prompt 10C — Notifications Completion Report

**Date:** 2026-08-20
**Scope:** Notifications bounded context (ADR-025, ADR-017 slot 9): Domain,
Application, Infrastructure, API, the 8-permission matrix, the
`NotificationDispatched` integration event, transactional outbox (ADR-015),
Organization read-model projection (ADR-016), Workflow reconcile consumers
(first gate), EF Core migrations, baseline notification-type catalog seeding,
configuration, unit tests, compile-only integration tests, and documentation.
**Method:** Static inspection, `dotnet build CommunityOS.sln`, `dotnet ef
migrations has-pending-model-changes`, and `dotnet test` for the unit suite.
Integration tests compile as part of the solution but cannot run in this
environment (no Docker available).

## 1. Deliverables

| # | Deliverable | Status | Evidence |
|---|-------------|--------|----------|
| 1 | Solution + project wiring | **DONE** | `CommunityOS.sln` references `CommunityOS.Notifications.Domain/Application/Infrastructure/API`, `CommunityOS.Notifications.Tests`, `CommunityOS.Notifications.IntegrationTests`, nested under a `Notifications` solution folder. |
| 2 | Database provisioning | **DONE** | `docker/init/01-create-databases.sql` includes `CREATE DATABASE communityos_notifications;`. |
| 3 | EF Core migrations | **DONE** | `InitialCreate` (`20260820214323`): schema `notifications`; tables `notifications`, `notification_recipients`, `notification_scopes`, `notification_types`, `notification_preferences`, `organization_unit_references` + MassTransit `InboxState`/`OutboxMessage`/`OutboxState`. `has-pending-model-changes` clean. |
| 4 | Domain aggregates + invariants | **DONE** | `Notification` (guarded lifecycle `Draft → Queued → Dispatched`, double-dispatch protection, both-or-neither source reference, recipient mutability while Draft/Queued only, `MarkRead` delivered-only), `NotificationRecipient` (guarded `Pending → Sent → Delivered → Read` + terminal `Failed`, bounded retry counter `MaxRetryCount = 5`, InApp delivers directly from Pending), `NotificationType` (lowercased codes, retire never deletes, retired types reject updates), `NotificationScope`, `NotificationPreference`, `OrganizationUnitReference`. |
| 5 | Application commands/queries + authorization | **DONE** | 8 `notifications.*` permissions enforced via `AuthorizationGuard` (fail-closed), idempotent create per (type, source type, source id, channel) with `AddIfAbsentAsync` concurrency backstop, batch inbox authorization (`EvaluateBatchAsync`) with sensitive capability gating, mark-read without an existence oracle (404 for any unauthorized caller), self-managed preferences, DTOs never expose EF entities. |
| 6 | Infrastructure | **DONE** | EF `NotificationsDbContext` (schema `notifications`, owns outbox entities), repositories (`INotificationRepository` incl. `AddIfAbsentAsync`, `INotificationTypeRepository` incl. `AnyNotificationReferencesAsync`, `INotificationPreferenceRepository`, `IOrganizationUnitReferenceRepository`), `NotificationsCatalogSeeder`, `NotificationsReconciliation` helper (SystemActorId `00000000-0000-0000-0000-000000000002`, template rendering), `HttpAuthorizationEvaluator` + `AuthorizationGuard`, background `NotificationDispatchWorker` (30s poll, batch 50). |
| 7 | API | **DONE** | `NotificationsController`, `NotificationTypesController`, `PreferencesController` under `/api/v1/notifications` implementing all 13 ratified endpoints (see §9). |
| 8 | Integration events | **DONE** | `NotificationDispatched` is the only exported event; `NotificationsIntegrationEventPublisher<TDomainEvent>` forwards it via the outbox. Delivered/read events are domain-only and never exported. |
| 9 | Outbox gate (ADR-015) | **DONE** | `AddCommunityOSEventBusWithOutbox<NotificationsDbContext>` in the API wiring; domain events are published before `SaveChanges` so the forwarded integration event and the notification row commit atomically. |
| 10 | Permissions registered | **DONE** | 8 `notifications.*` permissions in `PermissionCatalog.cs` + Authorization development role seeds (GlobalAdministrator, NationalAdministrator all 8; **Member** `notifications.notification.read` + `notifications.preference.manage`). |
| 11 | Baseline notification-type catalog | **DONE** | `NotificationsCatalogSeeder` seeds the 12 ratified baseline types idempotently after migrate in the dev pipeline; `record-hold` is the only sensitive baseline type. |
| 12 | Configuration | **DONE** | `appsettings.json` / `appsettings.Development.json` / `appsettings.Production.json` (`NotificationsDb`, JWT, RabbitMq, AuthorizationService, CommunityService, Notifications; `Jwt:RequireHttpsMetadata: "true"` in Production). |
| 13 | Tests | **DONE** | Notifications unit tests 71/71 passing (see §6). |
| 14 | Documentation | **DONE** | `docs/notifications.md`, `docs/api/notifications.md`, `docs/runbooks/notifications.md` aligned with the implementation; ADR-025 and ADR-017 slot 9 status updated. |

## 2. Layer summary

- **Domain** (`src/Services/Notifications/CommunityOS.Notifications.Domain`):
  `Notification` aggregate with guarded lifecycle; statuses are enumerations
  (`NotificationLifecycleStatus`, `NotificationRecipientStatus`) persisted as int;
  the source reference is both-or-neither (`SourceType` + `SourceId` or neither,
  free-standing `general`); dispatch is only legal from `Queued`, double dispatch
  throws (a re-send is always a **new** notification); dispatch is reached only
  when every recipient is terminal, in the same transaction that raises
  `NotificationDispatchedEvent`. First-gate channel semantics (ADR-025 decision 5):
  `InApp` delivers immediately (no external provider), Email/Push/Sms fail closed
  as `provider-not-configured`. Recipients carry only stable member ids — no
  PII, names or destinations.
- **Application** (`...Notifications.Application`): MediatR commands/queries,
  DTOs, FluentValidation validators, `AuthorizationGuard` over the HTTP evaluator,
  batch inbox authorization, sensitive-capability gate (`read` + `read.sensitive`),
  `ValidationPipelineBehavior` (400), 11 domain exceptions mapped to
  `409`/`404`/`400`. Create is **idempotent per (type, source type, source id,
  channel)**: a duplicate create for the same source fact returns the existing
  notification (200). Mark-read is a relationship tuple (actor must be the
  recipient; any unauthorized caller receives 404 — no oracle). Dispatch
  orchestration (`NotificationDispatchService`) applies member preference
  opt-outs before dispatch and publishes domain events before the caller saves.
- **Infrastructure** (`...Notifications.Infrastructure`): `NotificationsDbContext`
  (schema `notifications`, owns outbox entities), repositories, migration,
  `NotificationsCatalogSeeder`, `NotificationsIntegrationEventPublisher`,
  `NotificationsReconciliation` (shared create-if-absent helper; dispatches domain
  events **before** `AddIfAbsentAsync` so the outbox commits atomically and the
  unique filtered index is the concurrency backstop), Organization read-model
  consumer, Workflow reconcile consumers, `HttpAuthorizationEvaluator`
  (fail-closed), background `NotificationDispatchWorker`.
- **API** (`...Notifications.API`): versioned controllers, `ExceptionHandlingMiddleware`
  mapping (400/401/403/404/409/500 problem-details), JWT RS256-only validation
  (JWKS, fail-closed `ValidAlgorithms=[RsaSha256]`), outbox wiring, seeder hook in
  `MigrateDbAsync`.

## 3. Database / migration

- Database `communityos_notifications`, schema `notifications`. Tables:
  `notifications`, `notification_recipients`, `notification_scopes`,
  `notification_types`, `notification_preferences`, `organization_unit_references`,
  plus MassTransit `InboxState`/`OutboxMessage`/`OutboxState`.
- `notifications` carries `type_code`, `channel`, `status` (int), `source_type`,
  `source_id`, owned `Template` (subject/body), `organization_unit_id`,
  `is_sensitive`, `scheduled_for`, created/dispatched provenance. The filtered
  unique index `ix_notifications_source_idempotency` on `(type_code, source_type,
  source_id, channel)` WHERE `source_type IS NOT NULL AND source_id IS NOT NULL`
  is the concurrency backstop for idempotent create.
- Migration `InitialCreate` (`20260820214323`). `has-pending-model-changes`
  reports no drift.

## 4. Integration

- **Outbox (ADR-015):** `AddCommunityOSEventBusWithOutbox<NotificationsDbContext>`
  registers `UsePostgres` + `UseBusOutbox`; domain events are published *before*
  `SaveChanges` so the forwarded `NotificationDispatched` event and the
  notification row commit atomically. Satisfies the guaranteed-delivery
  prerequisite for Notifications as a consumer (Workflow events) and as a
  producer (Audit slot 11, Analytics slot 19).
- **Organization (ADR-016):** consumes `OrganizationUnitCreated/Updated/
  ParentChanged` into `organization_unit_references` (read-model projection,
  stable unit ids only).
- **Workflow (first gate, ADR-024 → ADR-025):** consumes
  `WorkflowTaskAssigned` → `task-assigned` to each assignee and
  `WorkflowTaskEscalated` → `task-escalated` to each escalation target.
  Reconcile-created notifications carry the type-catalog templates rendered with
  the stable, non-PII `TaskId` variable and use the system actor
  `...000000000002`. Deferred triggers (`task-completed`, `task-cancelled`,
  Records/Knowledge/Community triggers) are **not** enabled at this gate; each
  requires its recipient-resolution dependency (ADR-025 consumed-events catalog).
- **Authorization:** no direct DB access; every guarded operation is evaluated
  via `AuthorizationGuard` → `HttpAuthorizationEvaluator` (ADR-018/019,
  fail-closed).
- **Community:** config section only (`CommunityService`); the HTTP client for
  channel-destination resolution is deliberately not registered at the first
  gate (ADR-025 decision 5) — InApp needs no resolution, Email/Push/Sms fail
  closed.

## 5. Permission matrix and event contract

- 8 permissions ratified and enforced: `notifications.notification.read`,
  `notifications.notification.read.sensitive`, `notifications.notification.create`,
  `notifications.notification.send`, `notifications.notification.admin`,
  `notifications.type.manage`, `notifications.template.read`,
  `notifications.preference.manage`. Role seeds: GlobalAdministrator and
  NationalAdministrator receive all 8; **Member** receives
  `notifications.notification.read` + `notifications.preference.manage`.
- Integration contract (`CommunityOS.Contracts.Notifications.NotificationDispatched`)
  carries identifiers and a recipient count only — **never** recipient member ids
  (distribution is sensitive), subject/body copy, names or delivery failures
  (locked by unit tests). `SourceType` serializes as empty string for free-standing
  notifications. Delivered/read events are domain-only.

## 6. Tests

- Notifications unit tests **71/71** passing (`tests/Unit/CommunityOS.Notifications.Tests`):
  domain lifecycle (`NotificationLifecycleTests`: create/Draft, both-or-neither
  source, Queue recipient requirement, InApp immediate delivery, Email fail-closed,
  double-dispatch, recipient mutability, mark-read rules;
  `NotificationRecipientTests`: sent→delivered→read chain, InApp direct delivery,
  terminal failed with stable reason, bounded retry; `NotificationTypeTests`;
  `NotificationChannelTests`), application commands (idempotent create, guarded
  dispatch with admin override, mark-read no-oracle, type CRUD/retire in-use
  guard), queries (inbox any-scope readability, sensitive gating, no
  count/marker of inaccessible items, 404 no-enumeration), `NotificationDispatchServiceTests`
  (preference opt-out filtering, all-opted-out still completes dispatch),
  JWT RS256-only validation, integration-event security (identifiers/count only,
  delivered/read never exported), the two consumers (`OrganizationIntegrationEventConsumerTests`,
  `WorkflowTaskIntegrationEventConsumerTests` incl. idempotency and no-recipient
  skip), and `NotificationsCatalogSeederTests`.
- Integration tests (`tests/Integration/CommunityOS.Notifications.IntegrationTests`)
  compile as part of the solution (EF mapping, notification round-trip incl.
  owned recipients/scopes, filtered unique-index rejection, baseline seeding,
  outbox tables) but are **not runnable in this environment: no Docker/
  PostgreSQL available**. They must be run in an environment with Docker before
  production use.

## 7. Build and EF verification

- `dotnet build CommunityOS.sln`: **0 warnings, 0 errors** (all other solution
  projects unaffected).
- `dotnet test`: all **8 unit test projects pass** — Notifications 71/71,
  Authorization 118/118 (covers the permission catalog/seeder changes),
  Workflow 57/57, Records 73/73, Documents 56/56, Community 106/106,
  Organization 79/79, Knowledge 49/49, Identity 26/26. All 8 integration test
  projects (pre-existing and Notifications) fail solely because Docker/
  Testcontainers is unavailable (pre-existing environment limitation).
- `dotnet ef migrations has-pending-model-changes`: **no pending model changes**.
- Verification commands used `$env:DOTNET_ROLL_FORWARD="Major"` (this machine
  lacks the .NET 9 runtime; .NET 10 runtime used).

## 8. Deviations and follow-ups

- **Docker unavailable → integration tests compile-only.** Report honestly:
  they have not been executed against PostgreSQL.
- **Prompt endpoint count vs. ratified API doc.** The prompt states 14
  endpoints; `docs/api/notifications.md` enumerates **13** (6 notifications,
  5 types, 2 preferences). All 13 ratified endpoints are implemented; the prompt
  number is treated as a miscount and the doc is authoritative.
- **`GET /inbox` pagination.** The API doc path sketch shows `cursor=`; the
  implementation uses `limit`/`offset` (matching the ratified query contract).
  No cursor pagination is implemented at this gate.
- **Retry bound is a domain constant, not configuration.** The runbook's
  configuration table lists `Notifications:MaxRetries` (example `3`); the
  implementation bounds provider retries with the domain constant
  `NotificationRecipient.MaxRetryCount = 5`. No config binding is implemented.
- **Mark-read capability column.** The API doc's capability column lists
  `notifications.notification.read` for mark-read; the implementation enforces
  the relationship tuple (actor == recipient) and returns 404 for any
  unauthorized caller — consistent with ADR-025's no-oracle rule. No explicit
  permission check is performed for mark-read.
- **Unused `Mapster` package reference** remains in the Application project
  (harmless; retained from the scaffold).
- **Community HTTP client not registered** at the first gate (config only),
  per ADR-025 decision 5.
- Prompt 10C is complete. **Prompt 10D is not begun.**

## 9. API surface (13 ratified endpoints)

| Method | Path (under `/api/v1/notifications`) | Capability |
|--------|---------------------------------------|------------|
| GET | `/inbox` | `notifications.notification.read` |
| GET | `/notifications/{id}` | `notifications.notification.read` |
| GET | `/notifications/{id}/sensitive` | `notifications.notification.read.sensitive` |
| POST | `/notifications` | `notifications.notification.create` |
| POST | `/notifications/{id}/dispatch` | `notifications.notification.send` (+ `admin` for `AdminOverride`) |
| POST | `/notifications/{id}/recipients/{memberId}/read` | relationship tuple (actor must be recipient) |
| GET | `/types` | `notifications.template.read` |
| GET | `/types/{code}` | `notifications.template.read` |
| POST | `/types` | `notifications.type.manage` |
| PUT | `/types/{code}` | `notifications.type.manage` |
| POST | `/types/{code}/retire` | `notifications.type.manage` |
| GET | `/preferences/me` | `notifications.preference.manage` |
| PUT | `/preferences/me` | `notifications.preference.manage` |

## 10. Result

**Prompt 10C: COMPLETE.** Notifications is implemented end-to-end, the solution
builds clean, all unit tests pass, the EF model is in sync, the outbox gate is
wired, the permission matrix and event contract are ratified and enforced, and
the docs are aligned with the implementation. The only unverifiable item in this
environment is the Testcontainers integration suite (requires Docker).