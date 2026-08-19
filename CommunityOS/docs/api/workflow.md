# Workflow Service API

> **STATUS: RATIFIED (Prompt 09B).** Contract for the future Workflow service,
> aligned with ratified ADR-024 and `docs/workflow.md`. Not implemented;
> implementation proceeds in Prompt 09C.

All endpoints are versioned under `/api/v1/workflow`, require a valid access
token (`[Authorize]`), and return DTOs — **EF entities are never exposed**.
Every guarded operation is evaluated against the Authorization service
(fail-closed). Task metadata and sensitive task fields/notes are separate
capabilities with separate permissions. Workflow stores no binary content and
no person data; assignees are stable person ids resolved through the Community
API at read time.

## Tasks — `/workflow/tasks`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/tasks?definition=&status=&assigneeId=&domainType=&domainEntityId=&organizationUnitId=&overdue=` | `workflow.task.read` | List accessible task metadata (non-sensitive) |
| GET | `/tasks/{id}` | `workflow.task.read` | Get task metadata, assignees, scopes, deadline, outcome, activity summary (non-sensitive) |
| GET | `/tasks/{id}/sensitive` | `workflow.task.read.sensitive` | Get the sensitive fields/notes of the task |
| POST | `/tasks` | `workflow.task.create` | Create a task |
| POST | `/tasks/{id}/assign` | `workflow.task.assign` | Assign/reassign assignee(s) |
| POST | `/tasks/{id}/start` | `workflow.task.start` | Transition `Assigned → In Progress` |
| POST | `/tasks/{id}/complete` | `workflow.task.complete` | Transition `In Progress → Completed` with an outcome |
| POST | `/tasks/{id}/cancel` | `workflow.task.cancel` | Transition any non-terminal state → `Cancelled` |
| POST | `/tasks/{id}/escalate` | `workflow.task.escalate` | Escalate to a different assignee with a reason |
| GET | `/tasks/{id}/activity` | `workflow.task.read` | Get the append-only activity history |

`GET /tasks` returns only tasks the caller is authorized to read; it never
returns a count or marker of tasks the caller cannot read. `GET /tasks/{id}`
returns `404` for missing **and** unauthorized tasks alike (no enumeration
oracle).

**Create** body:

```json
{
  "definitionCode": "record-review",
  "domainType": "record",
  "domainEntityId": "00000000-0000-0000-0000-000000000001",
  "organizationUnitId": "00000000-0000-0000-0000-000000000002",
  "additionalScopes": [],
  "assigneeIds": ["00000000-0000-0000-0000-000000000003"],
  "dueOn": "2026-09-01T12:00:00Z",
  "notes": "Review the attached evidence before verifying."
}
```

The task is created in `Created` (or `Assigned` when `assigneeIds` is
provided). Creation is idempotent per (definition, domain entity): a second
create for an already-open task returns the existing task.

**Complete** body:

```json
{
  "outcome": "verified",
  "notes": "Evidence confirmed against the official certificate."
}
```

`outcome` must be one of the definition's permitted outcome codes (e.g.
`verified`, `rejected`, `approved`, `clarification-requested`). `notes` are
sensitive; they are stored, visible only under `workflow.task.read.sensitive`,
and **never exported** in integration events or logs.

**Escalate** body:

```json
{
  "escalateTo": "00000000-0000-0000-0000-000000000004",
  "reason": "Assignee unavailable; re-routed to coordinator."
}
```

Escalation reassigns the task to `escalateTo` and records the escalation
outcome; it does not change the task's lifecycle status.

## Definitions — `/workflow/definitions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/definitions` | `workflow.definition.read` | List the task-definition catalog |
| GET | `/definitions/{code}` | `workflow.definition.read` | Get a task definition |
| POST | `/definitions` | `workflow.definition.manage` | Create a task definition |
| PUT | `/definitions/{code}` | `workflow.definition.manage` | Update a task definition |
| POST | `/definitions/{code}/retire` | `workflow.definition.manage` | Retire a task definition (only when unused) |

**Create definition** body:

```json
{
  "code": "document-review",
  "displayName": "Document review",
  "description": "Human review of a document artifact.",
  "domainType": "document",
  "permittedOutcomes": ["approved", "rejected", "clarification-requested"],
  "dueIn": "P14D",
  "requiresHumanReview": true
}
```

The baseline catalog is `record-review`, `document-review`,
`knowledge-moderation`, `knowledge-ai-review`, `general`. Codes are stable
strings, extensible via configuration. The baseline is seeded idempotently at
migration time; a fresh database can create tasks immediately. Creating a
definition with an existing code returns `409`; retiring a definition in active
use returns `409`.

## DTOs (conceptual)

- `WorkflowTaskDto` — id, definitionCode, domainType, domainEntityId, status,
  primary org unit, additional scopes, assigneeIds, dueOn, overdue, escalation
  state, outcome, originatorId, created/updated/started/completed provenance.
- `WorkflowTaskSummaryDto` — id, definitionCode, domainType, domainEntityId,
  status, primary scope, assigneeIds, dueOn, outcome, updated timestamp (for
  lists).
- `TaskDefinitionDto` — code, displayName, description, domainType,
  permittedOutcomes, dueIn, requiresHumanReview, active, created/updated
  provenance.
- `TaskActivityDto` — id, taskId, action, actorId, occurredOn, outcome (notes
  never included).
- Request records — `CreateTaskRequest`, `AssignTaskRequest`,
  `StartTaskRequest`, `CompleteTaskRequest`, `CancelTaskRequest`,
  `EscalateTaskRequest`, `CreateTaskDefinitionRequest`.

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure, invalid definition code, invalid outcome, invalid subject/scope reference |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Task / definition not found (also for unauthorized reads) |
| 409 | Invalid transition, duplicate open task, duplicate definition code, retire-in-use definition |

## Service-to-service notes

Workflow exposes no cross-service fact endpoint yet. Task notes and sensitive
fields are resolved only through this API under `workflow.task.read.sensitive`.
The `RecordSubmitted`/`RecordVerified`/`RecordRejected` and Knowledge review
event consumers are inbound integrations, not exposed endpoints; they require
the transactional outbox gate (ADR-015) to be enabled before production use.