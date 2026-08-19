# Workflow Service

> **STATUS: RATIFIED (Prompt 09B).** Architectural design for the Workflow
> bounded context (`ADR-024`), positioned at ADR-017 slot 8. Not implemented;
> implementation proceeds in Prompt 09C.

The Workflow bounded context owns **task/work-item state** and the task engine
that routes, assigns, tracks and escalates human review and approval work across
the platform. It is the routing service for review/verification work over domain
facts owned by Records, Documents and Knowledge, and is the authoritative source
of task state for Notifications, Search and Audit consumers (`ADR-024`).

The service must preserve the boundary rules of `ADR-024`: task state is never
domain state, Workflow is a routing/reconciliation service (never a command path
into other contexts), all references are stable ids, authorization is
centralized and fail-closed, and AI is advisory only.

## Model

- **TaskDefinition** — the catalog of task types/templates. A stable string
  `Code` (never a hard enum), display name, description, the `DomainType` the
  task works over (`record`, `document`, `knowledge-question`,
  `knowledge-ai-suggestion`, `general`), permitted outcome codes, deadline/SLA
  policy, active/retired flag, and audit provenance. Definitions are
  configuration: seeded idempotently, extensible at runtime, never deleted
  (retired instead).

  Baseline catalog (seeded idempotently at migration time):
  `record-review`, `document-review`, `knowledge-moderation`,
  `knowledge-ai-review`, `general`.

- **WorkflowTask** — the aggregate root: a task instance. Carries the
  definition code, a domain reference (stable `DomainType` + `DomainEntityId`,
  or free-standing `general` work), lifecycle status, an originator, one or more
  assignees, a primary organization scope plus additional scopes, optional
  `DueOn`, escalation state, outcome, and an append-only activity history.

- **TaskAssignment** — the assignment model: assignee(s), assigner, effective
  time. Reassignment and escalation are explicit, audited transitions.

- **TaskActivity** — an append-only history of every transition and note on a
  task (actor, action, timestamp, outcome). Stored; **never** exported onto the
  bus.

- **OrganizationUnitReference** — the ADR-016 read-model projection (consumed
  `OrganizationUnitCreated/Updated/ParentChanged`), mirroring Community,
  Knowledge, Documents and Records.

## Lifecycle

Authoritative states: `Created → Assigned → In Progress → Completed | Cancelled`.

- **Created** — the task exists; may already carry an assignee if assigned at
  creation.
- **Assigned** — one or more assignees set. Reassignment
  (`workflow.task.assign`) is allowed from `Assigned` and `In Progress`.
- **In Progress** — started by an assignee (`workflow.task.start`).
- **Completed** — terminal. Reached from `In Progress` via
  `workflow.task.complete` with an outcome code (e.g. `verified`, `rejected`,
  `approved`, `clarification-requested`, definition-gated). Completed tasks are
  **immutable**: no in-place edits, no reopen, no correction. If a domain entity
  re-enters review, a **new task** is created.
- **Cancelled** — terminal. Reached from any non-terminal state via
  `workflow.task.cancel`.

**Explicit transitions (all audited):**

| From | To | Action | Permission |
|------|----|--------|-----------|
| Created | Assigned | assign | `workflow.task.assign` |
| Created | In Progress | start (when already self-assigned) | `workflow.task.start` |
| Assigned | In Progress | start | `workflow.task.start` |
| Assigned | Assigned | reassign | `workflow.task.assign` |
| In Progress | Assigned | reassign | `workflow.task.assign` |
| In Progress | Completed | complete (with outcome) | `workflow.task.complete` |
| any non-terminal | Cancelled | cancel | `workflow.task.cancel` |
| Assigned/In Progress | Assigned (escalated) | escalate (outcome + new assignee) | `workflow.task.escalate` |

- **Reject** is an *outcome*, not a state: a review task is completed with
  outcome `rejected` (definition-gated); the task reaches the terminal
  `Completed` state with that outcome. Rejection of the underlying domain entity
  is performed by the owning service (e.g. Records `reject`), never by Workflow.
- **Idempotency** — task creation is idempotent per (definition, domain entity):
  a uniqueness constraint prevents a second open task for the same domain
  entity+definition; reconcile consumers create-if-absent / close-if-open.
  Duplicate integration events never produce duplicate open tasks.
- **Deadline/overdue** — tasks carry an optional `DueOn`. Overdue is a derived,
  review-flagged state (surfaced in reads); it never auto-destroys and never
  auto-escalates without an explicit, per-definition ratified policy. Escalation
  is always an explicit human action.

## Boundaries

### Records (ADR-023)

Workflow **consumes** Records integration events for reconciliation:

| Records event | Workflow action |
|---------------|-----------------|
| `RecordSubmitted` | Create a `record-review` task (idempotent) |
| `RecordVerified` | Complete the open `record-review` task with outcome `verified` |
| `RecordRejected` | Complete the open `record-review` task with outcome `rejected` |

- **Records owns verification truth.** The verify/reject transition is always
  performed through the Records API by a human holding
  `records.record.verify`. Workflow never calls the Records verify/reject/
  correct/mutate APIs and never alters record facts.
- Task completion with outcome `verified` does **not** verify the record; it
  only records that the human review task is done. The `RecordVerified` event is
  the authoritative signal.
- References are stable `RecordId`s; record details are resolved through the
  Records API at read time. Workflow never stores record field values.

### Documents (ADR-022)

- Workflow references documents only through the existing `DocumentReference`
  mechanism with `SourceContext = "workflow.task"`. Documents remains the owner
  of artifact bytes and metadata.
- Workflow needs **no additional Documents API operations** beyond what exists
  (create reference, read metadata). No document content or metadata is stored
  in Workflow.

### Knowledge (ADR-021)

| Knowledge event | Workflow action |
|-----------------|-----------------|
| `QuestionFlagged` / `QuestionUnderReview` | Create/reconcile a `knowledge-moderation` task |
| `QuestionMerged` / `QuestionArchived` | Close the open moderation task with outcome `merged`/`archived` |
| `AiSuggestionRequested` | Create a `knowledge-ai-review` task |
| `AiSuggestionReviewed` | Complete the open ai-review task with the recorded outcome |

- Moderation/review-task semantics: the task routes and tracks human review;
  the **outcome is advisory**. Accepting/rejecting an AI suggestion or
  publishing a question is a Knowledge-side governance action performed by a
  human through the Knowledge API (`knowledge.moderation.review`,
  `knowledge.ai.review`). AI suggestions remain advisory and never authorize or
  complete governance actions (`ADR-021`).

### Community and Organization

- **Stable identifiers only.** Assignees/participants are stable person ids;
  organization scopes are stable org-unit ids. Names and details are resolved
  through the Community API (persons) and Organization API/read model at read
  time. No PII is stored.
- **No cross-service database access** (`ADR-018`). Workflow never reads the
  Community, Organization, Records, Documents or Knowledge databases.
- **Organization-unit scoping** follows ADR-016: Workflow consumes
  `OrganizationUnitCreated/Updated/ParentChanged` into `OrganizationUnitReference`
  and a task carries a primary scope plus additional scopes; access succeeds
  when the caller holds the permission at **any** of the task's scopes (Records
  pattern).

### Notifications

- Workflow **produces** task-state integration events
  (`WorkflowTaskCreated/Assigned/Started/Completed/Cancelled/Escalated`).
  Notifications (slot 9) will consume them later for task routing/reminder
  digests.
- Workflow does **not** own notification delivery, templates, channels or
  delivery state.
- All Workflow integration events are outbox-protected at the Workflow
  integration gate, so they are guaranteed-delivery for the future
  Notifications/Search/Audit consumers.

### Search

- Search (slot 10) is a future consumer/indexer of task metadata. Workflow
  remains the source of truth for task state; Search indexes a projection and
  never writes task state back.

### Audit

- Compliance-significant Workflow events:
  `WorkflowTaskCreated`, `WorkflowTaskCompleted` (outcome),
  `WorkflowTaskCancelled`, `WorkflowTaskEscalated` for `record-review` tasks.
- These are protected by the transactional outbox (ADR-015) from the Workflow
  integration gate, so Audit (slot 11) can subscribe later without Workflow
  changes. Audit itself is **not implemented now**.

## Authorization

Every guarded operation calls the Authorization service through
`AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019). No
`[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization database
access. Resource-level authorization is mandatory
(`resourceType = "workflow.task"`); unauthorized enumeration is prevented with
equivalent not-found behavior for missing and unauthorized resources (404).

See `docs/api/workflow.md` for the permission-to-endpoint mapping. The ratified
matrix is in `ADR-024`.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Task metadata, definition code, status, domain refs, scopes, assignee ids, deadlines, outcome. Sensitive fields/notes only under `workflow.task.read.sensitive`. |
| **Logs** | Task/definition ids, action, actor, outcome. Never notes, never names, never secrets, never domain field values. |
| **Integration events** | Identifiers and minimal lifecycle metadata only. Never task notes, never names, never filenames, never secrets. |
| **Search indexes** | Task metadata (definition, status, scope, assignee ids, deadline), gated by classification/authorization. Never notes, never secrets. |
| **AI pipelines** | Non-sensitive task summaries only, gated by classification; AI is advisory only. |

## HTTP API

All endpoints are versioned under `/api/v1/workflow` and require a valid access
token. No EF entities are exposed; DTOs are returned. See `docs/api/workflow.md`
for the full endpoint reference.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (`CommunityOS.Contracts.Workflow`), following the open-generic
  publisher pattern used by every existing service. All exported events are
  outbox-protected.
- **Consumed** — Organization unit events (ADR-016 projection), Records
  `RecordSubmitted/RecordVerified/RecordRejected`, Knowledge
  `QuestionFlagged/QuestionUnderReview/QuestionMerged/QuestionArchived/
  AiSuggestionRequested/AiSuggestionReviewed`.
- **Outbox** — the transactional outbox (ADR-015, MassTransit EF Core outbox) is
  enabled at the **Workflow integration gate**, mirroring Records (Prompt
  08A-R2). Workflow is itself a guaranteed-delivery consumer of Records events,
  and its own compliance-significant events target Notifications/Search/Audit.
  Best-effort publication is **not** acceptable for any Workflow integration
  event at implementation time.

## Data

- Database: `communityos_workflow` (PostgreSQL), schema `workflow`.
- Core tables: `task_definitions`, `workflow_tasks`, `task_assignments`,
  `task_activity`, `organization_unit_references` (projection), plus MassTransit
  `InboxState`/`OutboxMessage`/`OutboxState`.
- Uniqueness: one open task per (definition, domain entity, domain id);
  unique definition code; assignee identity preserved.
- Immutable vs mutable: task activity and completed tasks are immutable;
  assignment, deadline and outcome fields are mutable only while the task is
  non-terminal. No hard deletes through normal operations; cancelled/completed
  tasks are retained as history.
- Baseline seeds: task-definition catalog seeded idempotently at migration time.

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:WorkflowDb` | | `Host=localhost;Port=5432;Database=communityos_workflow;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-workflow` | Audit identifier |
| `DocumentsService:BaseUrl` | | *(empty in dev)* | Documents API base URL (task document references); unset, the client fails closed |
| `DocumentsService:AccessToken` | | *(empty in dev)* | Service token presented to Documents |
| `DocumentsService:ClientId` | | `communityos-workflow` | `X-Client-Id` sent to Documents |
| `CommunityService:BaseUrl` | | *(empty in dev)* | Community API base URL (assignee resolution at read time) |
| `CommunityService:AccessToken` | | *(empty in dev)* | Service token presented to Community |
| `CommunityService:ClientId` | | `communityos-workflow` | `X-Client-Id` sent to Community |
| `Workflow:InternalClientId` | | *(reserved)* | Trusted in-process caller for future fact queries |

The Workflow service never reads the Authorization, Organization, Community,
Documents, Records or Knowledge databases. If `AuthorizationService:BaseUrl` or
the presented token is misconfigured, every guarded endpoint returns
`403 Forbidden` (fail-closed).

## Dependencies

- **Identity** — RS256 JWT authentication of actors.
- **Authorization** — `workflow.*` permission evaluation via `AuthorizationGuard`.
- **Organization** — org-unit scoping facts via ADR-016 read-model projection
  (event consumption).
- **Community** — person resolution for assignees at read time.
- **Documents** — document references for tasks under review (`SourceContext =
  "workflow.task"`); read-only at runtime.
- **Records** — consumes `RecordSubmitted/RecordVerified/RecordRejected` for
  record-review task reconciliation.
- **Knowledge** — consumes review events for moderation/ai-review task
  reconciliation.
- **Transactional outbox** — enabled at the Workflow integration gate (ADR-015).

## Runbook

See `docs/runbooks/workflow.md`.