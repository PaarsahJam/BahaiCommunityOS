# Prompt 09E — Workflow Remediation Report

**Date:** 2026-08-19
**Scope:** Remediation of the six non-blocking findings from Prompt 09D
(`docs/architecture/prompt-09d-verification-report.md`, Section 16), strictly
within the ratified ADR-024 / ADR-015 / ADR-017(slot 8) contract. No new
architecture was invented and the Workflow bounded context was not expanded.

## Finding classification

| # | Finding | Defect type |
|---|---------|-------------|
| F1 | Reconcile-created tasks never emit `WorkflowTaskCreated` | **Implementation defect** (comment at `WorkflowReconciliation.cs:65-66` claimed publication that did not exist; ADR-024 states the event is raised when a task is created) |
| F2 | `WorkflowTask.AddNote` mutates terminal tasks (no guard) | **Implementation defect** (terminal-task immutability invariant not enforced; dead code today) |
| F3 | List query param `definitionCode` vs documented `?definition=` | **Contract defect** — implementation deviated from the ratified doc; fixed the implementation to match the contract |
| F4 | `ProducesResponseType` incomplete on several actions | **Contract/metadata defect** (Swagger fidelity only; runtime behavior was already correct) |
| F5 | Stale "duplicate open task → 409" row | **Contract/documentation defect** (contradicts ratified idempotent-create 200/201 behavior) |
| F6 | Runbook documents `GET /health` that does not exist | **Documentation defect / platform-convention** (no ratified health-probe convention — Records also has none; no endpoint added) |

## Remediation

### F1 — Reconcile-created tasks emit `WorkflowTaskCreated` through the outbox

- `WorkflowReconciliation.CreateIfAbsentAsync` now accepts `IMediator mediator`
  and calls `DomainEventPublisher.PublishAsync(task, mediator, ct)` **before**
  `AddIfAbsentAsync`, mirroring the direct-creation path
  (`WorkflowCommands.cs:83-84`) and the reconcile-close path
  (`WorkflowReconciliation.cs:96-97`). The comment that previously claimed
  publication is now accurate and explains the race semantics.
- All three consumer call sites pass `mediator`:
  - `RecordsReviewIntegrationEventConsumer` — `RecordSubmitted` → record-review.
  - `KnowledgeReviewIntegrationEventConsumer` — `QuestionFlagged` /
    `QuestionUnderReview` → knowledge-moderation; `AiSuggestionRequested` →
    knowledge-ai-review.
- **Outbox/atomicity:** the publish is captured by the bus outbox into the same
  scoped `WorkflowDbContext` (`WorkflowIntegrationEventPublisher<TDomainEvent>`
  is registered open-generic as `INotificationHandler<>` →
  `WorkflowInfrastructureServiceExtensions.cs:49`; the message row and the task
  row commit in one SaveChanges). On a lost race the filtered unique index
  (`ix_workflow_tasks_open_definition_domain`) rejects the insert, the whole
  SaveChanges rolls back, and the uncommitted outbox row is discarded with the
  scope — no spurious `WorkflowTaskCreated` is ever emitted for a task that was
  not persisted.
- **Idempotency:** `FindOpenAsync` short-circuit still returns null (no task,
  no publish) when an open task already exists; duplicate events therefore
  produce neither a duplicate open task nor a duplicate Created event.

### F2 — Terminal-task immutability in `AddNote`

- `WorkflowTask.AddNote` now throws
  `InvalidWorkflowTaskTransitionException` (mapped to 409) when `IsTerminal`,
  consistent with `Cancel`/`Complete`. Regression coverage added.

### F3 — `?definition=` query contract aligned

- `TasksController.List` binds `[FromQuery] string? definition` (was
  `definitionCode`), matching the ratified `docs/api/workflow.md` row
  `?definition=`. The internal query record property `DefinitionCode` and the
  JSON **body** property `definitionCode` (create body / DTOs) are unchanged —
  they are not part of the query-string contract.

### F4 — `ProducesResponseType` aligned with the ratified status-code contract

- Metadata only — **no runtime semantics changed.** Declarations now match the
  ratified error table (400/401/403/404/409/500) and each handler's actual
  behavior:
  - Tasks List → 200, 401, 403, 500 (guard → 403; no 400/404 on this action).
  - Tasks GetById / GetSensitive / Activity → 200, 401, 404, 500 (unauthorized
    reads are 404 — no enumeration oracle; no 403 by design).
  - Tasks Create → 201, 400, 401, 403, 500 (idempotent duplicate returns the
    existing task via the same 201; no 409 after F5).
  - Tasks Assign/Start/Complete/Cancel/Escalate → 200, 400, 401, 403, 404,
    409, 500.
  - Definitions List → 200, 401, 403, 500; GetByCode → 200, 401, 403, 404, 500;
    Create → 201, 400, 401, 403, 409, 500; Update → 200, 400, 401, 403, 404,
    409, 500; Retire → 200, 401, 403, 404, 409, 500.

### F5 — Stale duplicate-open-task 409 documentation removed

- `docs/api/workflow.md` error table: `409` now reads "Invalid transition,
  duplicate definition code, retire-in-use definition". This matches the
  ratified idempotent-create behavior (existing open task is returned 200/201,
  never 409) and the runbook, which was already aligned.

### F6 — Runbook health documentation corrected (no endpoint added)

- No new health endpoint was introduced: there is no ratified CommunityOS
  health-probe convention (the Records runbook equally documents none and no
  `/health` exists there), so adding one would be scope expansion.
- `docs/runbooks/workflow.md` now states the Workflow API exposes no dedicated
  health probe endpoint (consistent with Records) and directs operators to
  Serilog console/Seq logs and container/infrastructure probes.

## Verification

### Unit / security tests — Workflow

`dotnet test tests/Unit/CommunityOS.Workflow.Tests`: **57/57 passed**
(was 55; +2 for F2 `AddNote` coverage). Includes the existing lifecycle,
idempotency, JWT RS256-only, integration-event payload security, catalog seeder,
and all three reconcile-consumer suites.

### F1 regression coverage added

- `RecordSubmitted_creates_record_review_task_if_absent` →
  `mediator.Received(1).Publish(WorkflowTaskCreatedEvent, ...)`.
- `RecordSubmitted_does_not_create_when_open_task_exists` →
  `DidNotReceive().Publish(WorkflowTaskCreatedEvent, ...)` (duplicates never
  re-emit).
- `QuestionFlagged_creates_moderation_task_if_absent` → Received(1).
- `QuestionUnderReview_does_not_duplicate_when_open_task_exists` →
  DidNotReceive.
- `AiSuggestionRequested_creates_ai_review_task_if_absent` → Received(1).

### F2 regression coverage added

- `AddNote_appends_sensitive_note_on_open_task` (positive).
- `Terminal_tasks_reject_notes` (guard throws, notes and activity unchanged).

### Cross-service unit regression

| Suite | Result |
|-------|--------|
| Workflow | 57/57 |
| Records | 73/73 |
| Knowledge | 49/49 |
| Documents | 56/56 |
| Authorization | 118/118 |
| Community | 106/106 |
| Organization | 79/79 |
| Identity | 26/26 |

(Content/Enrollment/Events/Notifications/Reporting test assemblies contain no
test discoverers — pre-existing empty projects, unchanged.)

### Build & EF

- `dotnet build CommunityOS.sln`: **0 warnings, 0 errors**.
- `dotnet ef migrations has-pending-model-changes` (Workflow): **no pending
  model changes** — no persistence shape changed.

### Outbox / reconciliation event publication

Verified by construction and tests: the single `WorkflowTaskCreated` event per
reconcile-create flows `WorkflowTask.Create` → `DomainEventPublisher.PublishAsync`
→ `WorkflowIntegrationEventPublisher<WorkflowTaskCreatedEvent>` → `IPublishEndpoint`
(bus outbox on the same scoped DbContext) → `AddIfAbsentAsync` SaveChanges —
identical to the direct-creation path. Race case leaves no committed event.

### Authorization / multi-scope / 404-no-enumeration / JWT / event-privacy

No authorization, multi-scope, JWT, or event-payload code was touched; the
full Workflow suite (JWT RS256-only, event security, lifecycle) passes and no
cross-service suite regressed. F3 only renames the HTTP query-string binding;
the query record, handlers, guard paths, and 404-no-oracle behavior are
unchanged.

### Git diff review

Only the intended files changed (11 modified, plus the untracked 09D report
created in the previous step):
- `docs/api/workflow.md` (F3 doc row already correct; F5 409 row)
- `docs/runbooks/workflow.md` (F6)
- `TasksController.cs` (F3 + F4), `DefinitionsController.cs` (F4)
- `WorkflowTask.cs` (F2), `WorkflowReconciliation.cs` + the two consumers (F1)
- `WorkflowTaskLifecycleTests.cs`, `RecordsReviewIntegrationEventConsumerTests.cs`,
  `KnowledgeReviewIntegrationEventConsumerTests.cs` (F1 + F2 regression)

No migrations, configuration, infrastructure, or other services changed.

## Environment limitation

Docker/Testcontainers remain unavailable on this machine, so the
`CommunityOS.Workflow.IntegrationTests` suite is **compile-only** — it compiles
as part of the solution build but its tests were **not executed**. Integration
tests are **not claimed to have passed**. They must be run in CI before
production use (as documented in the 09C report, the runbook, and ADR-017
slot 8). `$env:DOTNET_ROLL_FORWARD="Major"` is required on this machine (no .NET 9
runtime installed).

## Result

All six Prompt 09D findings remediated (F1–F5 fixed in implementation/tests/docs;
F6 corrected in documentation only). Full solution build clean, EF in sync, all
active unit suites green.

**Prompt 09E: COMPLETE. Prompt 09F is not begun.**