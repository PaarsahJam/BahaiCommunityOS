# Prompt 09C — Workflow Completion Report

**Date:** 2026-08-19
**Scope:** Workflow bounded context (ADR-024, ADR-017 slot 8): Domain, Application,
Infrastructure, API, the 11-permission matrix, the 6-event integration contract,
transactional outbox (ADR-015), Organization read-model projection (ADR-016),
Records/Knowledge reconciliation consumers, EF Core migrations, baseline
task-definition catalog seeding, configuration, unit tests, compile-only
integration tests, and documentation.
**Method:** Static inspection, `dotnet build CommunityOS.sln`, `dotnet ef
migrations has-pending-model-changes`, and `dotnet test` for the unit suite.
Integration tests compile as part of the solution but cannot run in this
environment (no Docker available).

## 1. Deliverables

| # | Deliverable | Status | Evidence |
|---|-------------|--------|----------|
| 1 | Solution + project wiring | **DONE** | `CommunityOS.sln` references `CommunityOS.Workflow.Domain/Application/Infrastructure/API`, `CommunityOS.Workflow.Tests`, `CommunityOS.Workflow.IntegrationTests`, nested under a `Workflow` solution folder. |
| 2 | Database provisioning | **DONE** | `docker/init/01-create-databases.sql` includes `CREATE DATABASE communityos_workflow;`. |
| 3 | EF Core migrations | **DONE** | `InitialCreateWorkflow` (`20260819161356`): schema `workflow`; tables `task_definitions`, `workflow_tasks`, `task_assignments`, `task_assignment_assignees`, `task_scopes`, `task_activity`, `task_definition_outcomes`, `organization_unit_references` + MassTransit `InboxState`/`OutboxMessage`/`OutboxState`. `has-pending-model-changes` clean. |
| 4 | Domain aggregates + invariants | **DONE** | `WorkflowTask` (guarded lifecycle `Created → Assigned → In Progress → Completed \| Cancelled`, start-requires-assignee with `adminOverride`, outcome-gated complete, escalate-keeps-status, no reopen, `CompleteForReconciliation` idempotent, sensitive notes/escalation reason), `TaskDefinition` (outcome codes, retirement with open-task guard), `TaskAssignment`/`TaskAssignee` (multi-assignee), `TaskScope`, `TaskActivity`, `OrganizationUnitReference`. |
| 5 | Application commands/queries + authorization | **DONE** | 11 `workflow.*` permissions enforced via `AuthorizationGuard` (fail-closed), batch list authorization (`EvaluateBatchAsync`), sensitive-read gate (`workflow.task.read.sensitive`), activity listing notes-excluded, DTOs never expose EF entities. |
| 6 | Infrastructure | **DONE** | EF `WorkflowDbContext` (schema `workflow`, owns outbox entities), repositories (`IWorkflowTaskRepository` incl. `AddIfAbsentAsync`, `ITaskDefinitionRepository` incl. `HasOpenTasksAsync`, `IOrganizationUnitReferenceRepository`), `WorkflowCatalogSeeder`, `WorkflowReconciliation` helper (SystemActorId `00000000-0000-0000-0000-000000000002`), integration publisher + three consumers. |
| 7 | API | **DONE** | `TasksController` (list/get/get-sensitive/create/assign/start/complete/cancel/escalate/activity) and `DefinitionsController` (list/get/create/update/retire) under `/api/v1/workflow/tasks` and `/api/v1/workflow/definitions`. |
| 8 | Integration events | **DONE** | 6 events in `CommunityOS.Contracts.Workflow` (`WorkflowTaskCreated/Assigned/Started/Completed/Cancelled/Escalated`); `WorkflowIntegrationEventPublisher` (MediatR `INotificationHandler<>`). |
| 9 | Outbox gate (ADR-015) | **DONE** | `AddCommunityOSEventBusWithOutbox<WorkflowDbContext>` in the API wiring; Workflow integration events and task rows commit atomically. |
| 10 | Permissions registered | **DONE** | 11 `workflow.*` permissions in `PermissionCatalog.cs` + Authorization development role seeds (GlobalAdministrator, NationalAdministrator). |
| 11 | Baseline task-definition catalog | **DONE** | `WorkflowCatalogSeeder` seeds `record-review, document-review, knowledge-moderation, knowledge-ai-review, general` (DueIn "P14D") idempotently after migrate in the dev pipeline. |
| 12 | Configuration | **DONE** | `appsettings.json` / `appsettings.Development.json` / `appsettings.Production.json` (`WorkflowDb`, JWT, RabbitMq, AuthorizationService, DocumentsService, CommunityService; `Jwt:RequireHttpsMetadata: "true"` in Production). |
| 13 | Tests | **DONE** | Workflow unit tests 55/55 (see §6). |
| 14 | Documentation | **DONE** | `docs/workflow.md`, `docs/api/workflow.md`, `docs/runbooks/workflow.md` aligned with implementation; ADR-024 and ADR-017 slot 8 status updated. |

## 2. Layer summary

- **Domain** (`src/Services/Workflow/CommunityOS.Workflow.Domain`): `WorkflowTask`
  aggregate with guarded lifecycle; statuses are an enumeration (`WorkflowStatus`)
  persisted as int; multi-assignee via owned `TaskAssignee` entities; complete is
  gated by the definition's `PermittedOutcomes`; start requires the actor be an
  assignee unless `workflow.task.admin` override; escalate reassigns keeping
  status; cancel is legal from any non-terminal state; reject is an outcome, not
  a state; no reopen. `CompleteForReconciliation(outcome, completedBy, occurredOn)`
  is idempotent, bypasses checks, and no-ops on terminal tasks. Notes and
  escalation reasons are sensitive (excluded from list/activity DTOs).
- **Application** (`...Workflow.Application`): MediatR commands/queries, DTOs,
  FluentValidation validators, `AuthorizationGuard` over the HTTP evaluator,
  batch authorization for list queries, sensitive-read capability gate,
  `ValidationPipelineBehavior` (400), 12 domain exceptions mapped to
  `409`/`404`/`400`. Task create is **idempotent per (definition, domain entity)**:
  a duplicate create returns the existing open task (see §8).
- **Infrastructure** (`...Workflow.Infrastructure`): `WorkflowDbContext` (schema
  `workflow`, owns outbox entities), repositories, migration,
  `WorkflowCatalogSeeder`, `WorkflowIntegrationEventPublisher`,
  `WorkflowReconciliation` (shared create-if-absent/close-if-open helper for the
  consumers; close path dispatches domain events **before** `tasks.UpdateAsync`
  so the outbox commits atomically), three consumers, `HttpAuthorizationEvaluator`,
  `HttpDocumentsServiceClient` (task document references), `HttpCommunityServiceClient`
  (assignee person existence, fail-closed, `X-Client-Id: communityos-workflow`).
- **API** (`...Workflow.API`): versioned controllers, `ExceptionHandlingMiddleware`
  mapping (400/401/403/404/409/500 problem-details), JWT RS256-only validation
  (JWKS, fail-closed `ValidAlgorithms=[RsaSha256]`), outbox wiring, seeder hook in
  `MigrateDbAsync`.

## 3. Database / migration

- Database `communityos_workflow`, schema `workflow`. Tables: `task_definitions`,
  `task_definition_outcomes`, `workflow_tasks`, `task_assignments`,
  `task_assignment_assignees`, `task_scopes`, `task_activity`,
  `organization_unit_references`, plus MassTransit `InboxState`/`OutboxMessage`/
  `OutboxState`.
- `workflow_tasks` carries `status` as int, `notes` (sensitive, nullable),
  `originator_id`, `escalated_to/by`, `due_on`. The unique filtered index
  `ix_workflow_tasks_open_definition_domain` on `(definition_code, domain_type,
  domain_entity_id)` WHERE `status < 4 AND domain_entity_id IS NOT NULL` is the
  concurrency backstop for idempotent create.
- Migration `InitialCreateWorkflow` (`20260819161356`). `has-pending-model-changes`
  reports no drift.

## 4. Integration

- **Outbox (ADR-015):** `AddCommunityOSEventBusWithOutbox<WorkflowDbContext>`
  registers `UsePostgres` + `UseBusOutbox`; domain events are published *before*
  `SaveChanges` so forwarded integration events and the task row commit
  atomically. Satisfies the guaranteed-delivery prerequisite for Workflow's own
  consumers and for future Notifications/Search/Audit subscriptions.
- **Organization (ADR-016):** consumes `OrganizationUnitCreated/Updated/
  ParentChanged` into `organization_unit_references` (read-model projection).
- **Records:** consumes `RecordSubmitted` → create `record-review`;
  `RecordVerified`/`RecordRejected` → close the open task with outcome
  `verified`/`rejected`. Reconcile-created tasks are identifier-only.
- **Knowledge:** consumes `QuestionFlagged`/`QuestionUnderReview` → create
  `knowledge-moderation`; `QuestionMerged`/`QuestionArchived` → close with
  `merged`/`archived`; `AiSuggestionRequested` → create `knowledge-ai-review`;
  `AiSuggestionReviewed` → close with the reviewed `Outcome`.
- **Authorization/Documents/Community:** no direct DB access; authorization via
  `AuthorizationGuard` → `HttpAuthorizationEvaluator` (ADR-018/019); document
  references commanded via `HttpDocumentsServiceClient`; assignee existence via
  `HttpCommunityServiceClient`. All HTTP clients fail closed when a base URL is
  unset.

## 5. Permission matrix and event contract

- 11 permissions ratified and enforced: `workflow.task.read`, `workflow.task.read.
  sensitive`, `workflow.task.create`, `workflow.task.assign`, `workflow.task.start`,
  `workflow.task.complete`, `workflow.task.cancel`, `workflow.task.escalate`,
  `workflow.definition.read`, `workflow.definition.manage`, `workflow.task.admin`.
- 6 integration events implemented (`CommunityOS.Contracts.Workflow`). Payloads
  carry only stable ids and minimal lifecycle metadata (`WorkflowTaskId`,
  `DefinitionCode`, `DomainType`, `DomainEntityId`, `OccurredOn`, `Outcome` on
  completed); **never** notes, escalation reasons, or names (locked by unit
  tests). Events raised on reconcile-close carry the same minimal contract.

## 6. Tests

- Workflow unit tests **55/55** passing (`tests/Unit/CommunityOS.Workflow.Tests`):
  domain lifecycle (`WorkflowTaskLifecycleTests`: creation, assign, start,
  non-assignee denial, admin override, outcome gate, immutability after
  completion, cancel, escalate, reassign, `CompleteForReconciliation` idempotence,
  notes sensitivity, overdue), `TaskDefinitionTests`, JWT RS256-only validation,
  integration-event security (no notes/reason/PII in payloads), the three
  consumers (`RecordsReviewIntegrationEventConsumerTests`,
  `KnowledgeReviewIntegrationEventConsumerTests`,
  `OrganizationIntegrationEventConsumerTests`), and `WorkflowCatalogSeederTests`.
- Integration tests (`tests/Integration/CommunityOS.Workflow.IntegrationTests`)
  compile as part of the solution (EF mapping, task round-trip incl. assignees/
  scopes/activity, filtered unique-index rejection, outbox tables) but are **not
  runnable in this environment: no Docker/PostgreSQL available**. They must be
  run in an environment with Docker before production use.

## 7. Build and EF verification

- `dotnet build CommunityOS.sln`: **0 warnings, 0 errors** (all other solution
  projects unaffected).
- `dotnet ef migrations has-pending-model-changes`: **no pending model changes**.
- Verification commands used `$env:DOTNET_ROLL_FORWARD="Major"` (this machine
  lacks the .NET 9 runtime; .NET 10 runtime used).

## 8. Deviations and follow-ups

- **Docker unavailable → integration tests compile-only.** Report honestly:
  they have not been executed against PostgreSQL.
- **Idempotent-create contract contradiction (ratified docs vs. runbook).**
  `docs/api/workflow.md` states duplicate create returns the existing open task;
  the runbook troubleshooting entry implied `409`. **Resolution chosen:** the
  handler returns the existing open task (200), and the filtered unique index
  `ix_workflow_tasks_open_definition_domain` is the concurrency backstop — a
  genuine race surfaces as `DbUpdateException`, which the repository converts
  back into the existing task. The runbook entry was updated to match.
- **`Assignees` modeled as owned `TaskAssignee` entities.** `IReadOnlyList<Guid>`
  cannot be `OwnsMany`'d (requires `TProperty : class`), so assignee ids persist
  via the owned `task_assignment_assignees` table with the computed `AssigneeIds`
  surface.
- **Reconcile-created tasks carry no domain data and use the system actor**
  `00000000-0000-0000-0000-000000000002` (distinct from the seeder baseline
  actor `...000001`).
- Prompt 09C is complete. **Prompt 09D is not begun.**

## 9. Result

**Prompt 09C: COMPLETE.** Workflow is implemented end-to-end, the solution builds
clean, all unit tests pass, the EF model is in sync, the outbox gate is wired, the
permission matrix and event contract are ratified and enforced, and the docs are
aligned with the implementation. The only unverifiable item in this environment
is the Testcontainers integration suite (requires Docker).