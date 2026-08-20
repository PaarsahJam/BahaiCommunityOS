# Prompt 09D — Workflow Verification Report

**Date:** 2026-08-19
**Gate type:** Read-only verification. No source, documentation, migration,
configuration, or infrastructure was modified. Working tree confirmed clean
before and after.
**Scope:** The implemented Workflow bounded context (ADR-024, ADR-017 slot 8)
verified against the complete ratified contract (`ADR-024`,
`docs/workflow.md`, `docs/api/workflow.md`, `docs/runbooks/workflow.md`).
**Method:** Parallel subagent code inspection (Domain, Application,
Infrastructure, API/JWT/config) cross-checked against the ratified docs;
direct source reads; full solution build; `dotnet ef migrations
has-pending-model-changes`; unit-test runs across all active suites;
cross-service regression.

## Verdict

**PASS WITH FINDINGS.** No blocking findings. Six non-blocking findings
(Section 16). **Prompt 09E is not begun.**

## 1. Architecture and ownership (ADR-024) — PASS

Workflow owns the task engine only: `TaskDefinition`, `WorkflowTask`,
`TaskAssignment`/`TaskAssignee`, `TaskActivity`, `OrganizationUnitReference`
projection. It does not own record facts, document artifacts, Knowledge
content/moderation authority, PII, or notification/search/audit storage. No
cross-service DB access anywhere in `src/Services/Workflow` (grep for all other
services' DbContexts returned zero matches); csproj references only
Workflow.Application, CommunityOS.Infrastructure.Common, CommunityOS.EventBus.
Boundary rules "task state is not domain state" and "Workflow never commands
other contexts' mutation APIs" are upheld (consumers only reconcile).

## 2. Lifecycle — PASS (1 latent finding)

States `Created=1 → Assigned=2 → In Progress=3 → Completed=4 | Cancelled=5`
stored as int via `WorkflowStatus.FromId`. Verified against the ratified
transition table: assign from Created/Assigned/InProgress; start requires
`IsAssignee(actorId)` unless `adminOverride` (Created self-assigned →
InProgress supported); complete only from InProgress gated by definition
`PermittedOutcomes`; cancel any non-terminal; escalate reassigns keeping
status; reject is an outcome not a state; completed tasks immutable (no
reopen/correction exists); `CompleteForReconciliation` idempotent, bypasses
checks, no-ops on terminal. **Finding 2 (non-blocking):** `WorkflowTask.AddNote`
(`WorkflowTask.cs:430-445`) mutates notes/provenance on a terminal task
without a guard, contradicting immutability — but it is **dead code** (never
invoked in src or tests), so latent, not an active breach.

## 3. Idempotency — PASS

Creation idempotent per (definition, domain entity): handler `FindOpenAsync`
first → returns existing open task (`WorkflowCommands.cs:59-64`); concurrency
backstop is the filtered unique index `ix_workflow_tasks_open_definition_domain`
on `(definition_code, domain_type, domain_entity_id)` WHERE
`status < 4 AND domain_entity_id IS NOT NULL` (`WorkflowConfigurations.cs:102-105`,
migration lines 385-391), with `AddIfAbsentAsync` converting `DbUpdateException`
back to the existing task (`WorkflowRepositories.cs:60-76`). Duplicate events
never produce duplicate open tasks (create-if-absent / close-if-open consumers;
unit tests cover both).

## 4. Authorization and multi-scope — PASS

All 11 ratified `workflow.*` permissions exist as constants and are enforced via
`AuthorizationGuard` (fail-closed) with `resourceType = "workflow.task"`; zero
`[Authorize(Roles = ...)]` / local RBAC in the Workflow layer. Multi-scope
any-of-grant implemented (primary + additional scopes via
`WorkflowTaskAuthorization.ContextsFor`/`HasForTaskAsync`); batch
`EvaluateBatchAsync` for list reads; sensitive gate requires both `task.read`
and `task.read.sensitive`, denies as 404. No enumeration oracle: denied/missing
reads both throw `WorkflowTaskNotFoundException` → 404. Permissions registered
in `PermissionCatalog` and both role seeds (GlobalAdministrator,
NationalAdministrator).

## 5. Boundaries — PASS

- **Records:** `RecordSubmitted` → create record-review; `RecordVerified` /
  `RecordRejected` → close with `verified`/`rejected`; never calls Records
  mutation APIs.
- **Knowledge:** QuestionFlagged/UnderReview → knowledge-moderation; Merged /
  Archived → close `merged`/`archived`; AiSuggestionRequested →
  knowledge-ai-review; AiSuggestionReviewed → close with `Outcome`. Outcomes
  advisory only.
- **Documents:** `HttpDocumentsServiceClient` POSTs
  `/api/v1/documents/{id}/references` with `SourceContext = "workflow.task"`;
  fails closed when BaseUrl unset.
- **Community/Organization:** stable ids only; `HttpCommunityServiceClient`
  `PersonExistsAsync` for assignee resolution; Organization consumers project
  `OrganizationUnitReference` (ADR-016) add-if-missing.
- No direct access to Authorization/Organization/Community/Documents/Records/
  Knowledge databases.

## 6. Six integration events — PASS (1 contract finding)

All 6 ratified events (`WorkflowTaskCreated/Assigned/Started/Completed/
Cancelled/Escalated`) exist in `CommunityOS.Contracts.Workflow`, each
identifier-only + `DateTime OccurredOn`; Completed carries Outcome; Escalated
carries EscalatedTo; Assigned carries AssigneeIds. **Never** notes, names,
filenames, secrets (locked by `WorkflowIntegrationEventSecurityTests`).
Publisher mapping lossless; `EscalationReason` deliberately excluded from
events.
**Finding 1 (non-blocking, resolve before slot 9):** reconcile-created tasks
never emit `WorkflowTaskCreated`. `WorkflowReconciliation.CreateIfAbsentAsync`
(`:40-69`) raises the domain event inside `WorkflowTask.Create` but never
dispatches it — no `IMediator` is passed and no `PublishAsync` is called,
contradicting both ADR-024's "raised when a task is created" and the code's own
comment at `:65-66`. Direct API creates publish correctly; reconcile-created
tasks (from `RecordSubmitted`/`QuestionFlagged`/`AiSuggestionRequested`) produce
no Created event until a later transition. Impact today is nil (no consumer
subscribed; slots 9–11 not started) but must be reconciled before
Notifications/Search/Audit subscribe.

## 7. Transactional outbox (ADR-015) — PASS

`AddCommunityOSEventBusWithOutbox<WorkflowDbContext>`
(`ServiceCollectionExtensions.cs:35-42`) registers the three consumers with
bus-outbox + receive-endpoint outbox; `WorkflowDbContext` owns
InboxState/OutboxMessage/OutboxState; migration creates all three in schema
`workflow`. Domain events are published **before** SaveChanges (create
`WorkflowCommands.cs:83→84`; reconcile-close `WorkflowReconciliation.cs:96→97`)
so forwarded events and the task row commit atomically. Outbox-protected set
correctly non-exhaustive.

## 8. Failure behavior — PASS

`ExceptionHandlingMiddleware` maps ValidationException → 400 (with errors,
problem+json), Unauthorized → 401, `AuthorizationForbiddenException` /
`TaskNotAssignableToActor` → 403, task/definition not found → 404 (also for
unauthorized reads), transition/duplicate-definition/retire-in-use → 409,
outcome/definition/domain-type/scope/entity-id → 400, unexpected → 500. HTTP
clients fail closed (Authorization deny-on-transport-error; Documents/Community
throw clear `InvalidOperationException` when BaseUrl unset). If Authorization
misconfigured → 403 everywhere.

## 9. JWT security — PASS

RS256-only (`ValidAlgorithms = [RsaSha256]`), JWKS via Identity OIDC discovery
path, ValidateIssuer/Audience/Lifetime/SigningKey all on, `RequireHttpsMetadata`
false in base/Dev and **true** in Production (https PLACEHOLDER-identity
.example.com), no HMAC secrets, `GetSubjectId` via
`JwtRegisteredClaimNames.Sub` → 401 if absent.

## 10. API/status-code fidelity — PASS WITH FINDINGS (all non-blocking)

All 15 endpoints (10 task + 5 definition) exist with correct routes, verbs,
`[Authorize]`, and DTO-only returns. Middleware status codes match the error
table. Findings:
- **Finding 3 (non-blocking):** List query param is bound as `definitionCode`
  (`TasksController.cs:23`) but `docs/api/workflow.md:18` documents
  `?definition=`. Clients following the doc get no filtering. The doc or the
  binding must be aligned.
- **Finding 4 (non-blocking):** `ProducesResponseType` is incomplete on several
  actions (e.g., Tasks Create omits 409; List/Definitions List declare only 200;
  401/500 not declared). Middleware behavior is correct; only Swagger metadata
  fidelity is affected. Matches the Records pattern.
- **Finding 5 (non-blocking):** `docs/api/workflow.md:137` still lists "duplicate
  open task" under 409 while `:50-51` and the handler return the existing task
  (200) — the residual half of the idempotent-create contradiction already
  acknowledged in the 09C report; runbook was aligned, api doc row remains.
- **Finding 6 (non-blocking):** `docs/runbooks/workflow.md:175` documents
  `GET /health` but no health endpoint exists (Records also lacks one, so this is
  a doc-vs-impl mismatch in the runbook, not a Workflow-specific regression).

## 11. Persistence/EF — PASS

Schema `workflow`; tables `task_definitions`, `task_definition_outcomes`,
`workflow_tasks`, `task_assignments`, `task_assignment_assignees`, `task_scopes`,
`task_activity`, `organization_unit_references` + outbox tables.
`dotnet ef migrations has-pending-model-changes`: **no pending model changes**.
Migration `InitialCreateWorkflow` (20260819161356) + Designer + Snapshot
consistent. Status int-backed; owned `TaskAssignee`/`TaskScope`/`TaskActivity`
children; filtered unique index present in both configuration and migration.

## 12. Tests — PASS

`dotnet test` results:
- Workflow **55/55** (lifecycle, immutability, idempotency, JWT RS256-only,
  event-payload security, seeder, all three consumers with substitute
  persistence).
- Cross-service regression: Records 73/73, Knowledge 49/49, Documents 56/56,
  Authorization 118/118, Community 106/106, Organization 79/79, Identity 26/26.
  (Content/Enrollment/Events/Notifications/Reporting test assemblies contain no
  test discoverers — pre-existing empty projects.)
- Integration suite `CommunityOS.Workflow.IntegrationTests` **compiles** as part
  of the solution but is **not executable here** (Docker unavailable).

## 13. Documentation alignment — PASS WITH FINDINGS (see 10)

Headers of `docs/workflow.md`, `docs/api/workflow.md`, `docs/runbooks/workflow.md`
are correctly marked RATIFIED AND IMPLEMENTED (Prompt 09C); ADR-017 slot 8 and
ADR-024 status updated; runbook implementation-status section and
duplicate-create troubleshooting entry aligned with actual behavior.

## 14. Cross-service regression — PASS

Full `dotnet build CommunityOS.sln`: **0 warnings, 0 errors**. All active unit
suites green (Section 12). Nothing outside Workflow / Authorization catalog &
seeder / sln / docker-init was touched by Prompt 09C; nothing was touched by
Prompt 09D.

## 15. Environment limitations — VERIFIED AS DOCUMENTED

Docker/Testcontainers unavailable → integration suite compile-only, honestly
reported in the 09C report, runbook, and ADR-017 slot-8 note. Must be executed
in CI before production use. `$env:DOTNET_ROLL_FORWARD="Major"` used (no .NET 9
runtime on this machine).

## 16. Findings summary

| # | Severity | Finding |
|---|----------|---------|
| 1 | Non-blocking (resolve before slot 9) | Reconcile-created tasks never emit `WorkflowTaskCreated`; code comment at `WorkflowReconciliation.cs:65-66` contradicts behavior. |
| 2 | Non-blocking | `WorkflowTask.AddNote` (`WorkflowTask.cs:430-445`) mutates terminal tasks without a guard; dead code today. |
| 3 | Non-blocking | List query param `definitionCode` vs documented `?definition=` (`TasksController.cs:23` vs `docs/api/workflow.md:18`). |
| 4 | Non-blocking | `ProducesResponseType` incomplete on several actions (Swagger metadata only). |
| 5 | Non-blocking | Residual "duplicate open task → 409" row at `docs/api/workflow.md:137` vs actual 200 idempotent return. |
| 6 | Non-blocking | Runbook `GET /health` documented but not implemented (matches Records). |

**Blocking findings: none.** Build clean, EF in sync, all active unit suites
pass, security posture solid, no functional breakage in the current scope, and
no consumer of the flagged event gap exists yet.

**Prompt 09D: COMPLETE (PASS WITH FINDINGS). Prompt 09E is not begun.**