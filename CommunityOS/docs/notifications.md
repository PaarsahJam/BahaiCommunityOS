# Notifications Service

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 10C).** Architectural design for
> the Notifications bounded context (`ADR-025`), positioned at ADR-017 slot 9.

The Notifications bounded context owns **notification records, recipient
delivery state, channel selection, notification types/templates and delivery
orchestration**. It is the authoritative record of what was notified, to whom
(by stable id), on which channel, when, and with what delivery outcome. It is
the delivery endpoint for the task-routing digests Workflow (`ADR-024`)
produces.

The service must preserve the boundary rules of `ADR-025`: delivery is
orchestration (never domain authority), bodies carry no authoritative content
and no PII, destinations are resolved through the Community API and never
stored, authorization is centralized and fail-closed, and InApp is domain-owned
while Email/SMS/Push are provider-deferred.

## Model

- **Notification** — the aggregate root: a notification instance referencing a
  triggering domain fact (stable `SourceType` + `SourceId`) or free-standing
  `general` work, with a notification-type code, a channel, a subject/body
  template, a primary organization scope plus additional scopes, an optional
  `ScheduledFor`, a lifecycle status, an `IsSensitive` flag, and a recipient
  set.

- **NotificationRecipient** — the per-recipient delivery state: stable
  `MemberId`, channel, status, `DeliveredAt`/`ReadAt` provenance, `FailureReason`,
  bounded `RetryCount`. Recipient rows store **no contact details**; channel
  destinations are resolved through the Community API at dispatch time.

- **NotificationType** — the catalog of notification types (stable string
  codes) and their default subject/body templates with `{{var}}` placeholders.
  Types are configuration: seeded idempotently at migration time, extensible at
  runtime, retired (never deleted). The baseline is `task-assigned`,
  `task-escalated`, `task-completed`, `task-cancelled`, `record-verified`,
  `record-rejected`, `record-hold` (sensitive), `question-flagged`,
  `community-activity`, `community-event`, `community-meeting`, `general`.

- **NotificationPreference** — the per-member opt-in/opt-out rule per type and
  channel applied by the dispatch worker when deriving recipients.

- **OrganizationUnitReference** — the ADR-016 read-model projection (consumed
  `OrganizationUnitCreated/Updated/ParentChanged`), mirroring Community,
  Knowledge, Documents, Records and Workflow.

## Lifecycle

Authoritative aggregate states: `Draft → Queued → Dispatched`.

- **Draft** — the notification exists; recipients may be added or removed.
- **Queued** — ready for dispatch (immediate or at `ScheduledFor`); requires at
  least one recipient. Recipient changes are still permitted.
- **Dispatched** — **terminal**. Reached only when every recipient is terminal,
  in the same transaction that publishes `NotificationDispatched`. Completed
  notifications are immutable: no re-dispatch, no recipient changes, no reopen.
  If a domain fact requires re-notification, a **new notification** is created
  (Workflow "no reopen" philosophy).

Recipient states: `Pending → Sent → Delivered → Read`, with `Failed` terminal
from `Pending`/`Sent`.

- **Pending** — created, not yet dispatched.
- **Sent** — handed to a channel provider (Email/SMS/Push). InApp skips `Sent`
  and delivers directly.
- **Delivered** — delivery confirmed (InApp immediate; provider ack for Email/
  SMS/Push).
- **Read** — terminal; only from `Delivered` (a notification that was never
  delivered or failed can never be read).
- **Failed** — terminal; only from `Pending`/`Sent`. `FailureReason` records
  the cause (e.g. `provider-not-configured`). No auto-re-send after terminal
  failure; a re-send is a new notification.

**Terminal-state invariants (mandatory):**

| Transition | Guard |
|------------|-------|
| `Dispatch()` on an already-`Dispatched` notification | throws `InvalidNotificationTransitionException` (double-dispatch protection) |
| `MarkSent` from any state other than `Pending` | throws |
| `MarkDelivered` from any state other than `Sent` (or `Pending` for InApp) | throws |
| `MarkRead` from any state other than `Delivered` | throws |
| `MarkFailed` from any state other than `Pending`/`Sent` | throws |
| Re-dispatch / re-send of a terminal recipient | throws; a re-send is a new notification |

- **Idempotency** — notification creation is idempotent per (type, source
  type, source id, channel): a uniqueness constraint prevents a duplicate
  notification for the same source fact; reconcile consumers create-if-absent.
  Duplicate integration events never produce duplicate notifications.
  Duplicate delivery/read acks are no-ops (guarded terminal states).
- **Retry** — provider retries are worker-level and bounded (configuration-
  driven backoff) and never regress persisted domain state; only the terminal
  outcome is persisted. MassTransit transport retry covers consume failures.
- **Reconciliation** — lost-then-redelivered events converge on the same
  notification; no duplicate deliverable notifications.

## Channels and delivery semantics

| Channel | Code | Owner | First-gate behavior |
|---------|------|-------|---------------------|
| InApp | `InApp(3)` | **Notifications (domain-owned)** | Implemented: dispatch writes a readable inbox row (`Pending → Delivered`); `Read` tracked via the API. No external provider. |
| Email | `Email(1)` | State model domain-owned; **provider deferred** | No configured provider → dispatch fails closed (`Failed`, reason `provider-not-configured`). |
| Push | `Push(2)` | State model domain-owned; **provider deferred** | Same fail-closed behavior. |
| SMS | `Sms(4)` | State model domain-owned; **provider deferred** | Same fail-closed behavior. |

Provider sending (SMTP/SES, SMS gateway, APNS/FCM) is behind
`INotificationChannelDispatcher`, a **future infrastructure integration**
(ADR-025 decision 5). The channel code + recipient status are the only schema
that exists; provider payloads are transport details resolved at dispatch time.

## Templates and content ownership

- `MessageTemplate` (subject/body) **remains part of Notifications**. The
  `NotificationType` catalog owns the default template per type and is seeded
  idempotently at migration time (`NotificationCatalogSeeder`), mirroring the
  Workflow task-definition catalog.
- Bodies are rendered at dispatch time with a validated, **non-PII variable
  set** (ids, dates, codes). Notifications never embeds record field values,
  task notes, hold reasons, document filenames, Library passage text or person
  names in a body. Authoritative content is never owned by Notifications
  (ADR-021 citation rule applies to notification copy).

## Boundaries

### Workflow (ADR-024)

| Workflow event | Notifications action |
|----------------|----------------------|
| `WorkflowTaskAssigned` | Create a `task-assigned` notification to each `AssigneeId` (first gate) |
| `WorkflowTaskEscalated` | Create a `task-escalated` notification to each `EscalatedTo` (first gate) |
| `WorkflowTaskCompleted` | Create a `task-completed` originator digest (deferred; originator via Workflow API read) |
| `WorkflowTaskCancelled` | Create a `task-cancelled` originator digest (deferred; originator via Workflow API read) |

- Workflow is the authoritative source of task state (ADR-024); Notifications
  never creates, assigns, completes or cancels tasks.
- Task-routing digests are the ADR-024-stated purpose of the Notifications
  consumer. `WorkflowTaskCreated` is not a trigger (a task is not assigned
  until `WorkflowTaskAssigned`).

### Records (ADR-023)

| Records event | Notifications action |
|---------------|----------------------|
| `RecordVerified` | Create a `record-verified` notification to the submitter (deferred; submitter via Records API read) |
| `RecordRejected` | Create a `record-rejected` notification to the submitter (deferred; submitter via Records API read) |
| `RecordHoldPlaced` / `RecordHoldReleased` | Create a sensitive `record-hold` notification to stakeholders (deferred; recipients via Records API read) |

- Records owns verification truth; a notification never verifies or changes a
  record.
- `record-hold` notifications are always `IsSensitive`.

### Knowledge (ADR-021)

| Knowledge event | Notifications action |
|-----------------|----------------------|
| `QuestionFlagged` / `QuestionUnderReview` | Create a `question-flagged` moderation digest (deferred; moderators via Knowledge API read) |

- Moderation authority stays in Knowledge; Notifications only informs
  moderators.

### Community and Organization

- **Stable identifiers only.** Recipients are stable member ids; organization
  scopes are stable org-unit ids. Names, email addresses and phone numbers are
  resolved through the Community API at dispatch time and are **never stored**
  in Notifications. InApp needs no destination resolution.
- **No cross-service database access** (ADR-018).
- **Organization-unit scoping** follows ADR-016: Notifications consumes
  `OrganizationUnitCreated/Updated/ParentChanged` into
  `OrganizationUnitReference` and a notification carries a primary scope plus
  additional scopes; access succeeds when the caller holds the permission at
  **any** of the notification's scopes (Records/Workflow pattern).
- Community event digests (`ActivityCreated`, `CommunityEventCreated`,
  `MeetingCreated`) are deferred triggers pending an audience-selection product
  decision.

### Search

- Search (slot 10) is a future consumer of notification metadata. Notifications
  remains the source of truth for delivery state; Search indexes a projection
  and never writes delivery state back.

### Audit

- `NotificationDispatched` is outbox-protected from the Notifications
  integration gate, so Audit (slot 11) can subscribe later without Notifications
  changes. Audit itself is **not implemented now**.
- The notification record is the in-context audit trail (type, channel, source,
  recipient count, provenance).

## Authorization

Every guarded operation calls the Authorization service through
`AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019). No
`[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization database
access. Resource-level authorization is mandatory
(`resourceType = "notification"`); unauthorized enumeration is prevented with
equivalent not-found behavior for missing and unauthorized resources (404).

See `docs/api/notifications.md` for the permission-to-endpoint mapping. The
ratified matrix is in `ADR-025`.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Notification metadata, type code, channel, status, source refs, scopes, recipient ids, provenance. Sensitive fields (failure reasons, distribution) only under `notifications.notification.read.sensitive`. |
| **Logs** | Notification/type ids, action, actor, channel, status. Never bodies, never names, never destinations, never failure details beyond the code. |
| **Integration events** | Identifiers and a recipient count only. Never recipient member ids, never bodies, never names, never delivery failures. |
| **Search indexes** | Notification metadata gated by classification/authorization; no bodies, no recipient ids. |
| **AI pipelines** | Not applicable; Notifications is not an AI service. |

## HTTP API

All endpoints are versioned under `/api/v1/notifications` and require a valid
access token. No EF entities are exposed; DTOs are returned. See
`docs/api/notifications.md` for the full endpoint reference.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (`CommunityOS.Contracts.Notifications`), following the
  open-generic publisher pattern used by every existing service. The only
  exported event is `NotificationDispatched`
  (`NotificationId`, `TypeCode`, `Channel`, `SourceType`, `SourceId`,
  `RecipientCount`, `OccurredOn`); it is outbox-protected.
- **Consumed** — Organization unit events (ADR-016 projection) and, in the
  first gate, Workflow `WorkflowTaskAssigned` / `WorkflowTaskEscalated`.
  Deferred consumers: Workflow `WorkflowTaskCompleted`/`WorkflowTaskCancelled`,
  Records `RecordVerified`/`RecordRejected`/`RecordHoldPlaced`/
  `RecordHoldReleased`, Knowledge `QuestionFlagged`/`QuestionUnderReview`,
  Community `ActivityCreated`/`CommunityEventCreated`/`MeetingCreated`
  (ADR-025 consumed-events catalog).
- **Outbox/inbox** — the transactional outbox (ADR-015, MassTransit EF Core
  outbox) and the receive-endpoint inbox are enabled at the **Notifications
  integration gate** (Prompt 10C), mirroring Records (Prompt 08B) and Workflow
  (Prompt 09C). Notifications is itself a guaranteed-delivery consumer of
  Workflow events, and its `NotificationDispatched` targets Audit (slot 11) and
  Analytics (slot 19). Best-effort publication is **not** acceptable for any
  Notifications integration event at implementation time.

## Data

- Database: `communityos_notifications` (PostgreSQL), schema `notifications`.
- Core tables: `notifications`, `notification_recipients`,
  `notification_types`, `notification_preferences`,
  `organization_unit_references` (projection), plus MassTransit
  `InboxState`/`OutboxMessage`/`OutboxState`.
- Uniqueness: one notification per (type, source type, source id, channel);
  unique type code; recipient identity preserved (one row per member per
  notification).
- Immutable vs mutable: dispatched notifications and terminal recipients are
  immutable; recipients and scheduling are mutable only while the aggregate is
  `Draft`/`Queued`. No hard deletes through normal operations; dispatched
  notifications are retained as history. Retention expiry flags a review
  disposition and never auto-destroys; hard purge requires an
  `notifications.notification.admin` override with a reason.
- Baseline seeds: notification-type catalog seeded idempotently at migration
  time.

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:NotificationsDb` | | `Host=localhost;Port=5432;Database=communityos_notifications;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-notifications` | Audit identifier |
| `CommunityService:BaseUrl` | | *(empty in dev)* | Community API base URL (channel-destination resolution at dispatch time) |
| `CommunityService:AccessToken` | | *(empty in dev)* | Service token presented to Community |
| `CommunityService:ClientId` | | `communityos-notifications` | `X-Client-Id` sent to Community |
| `Notifications:MaxRetries` | | `3` | Bounded worker-level provider retry count |
| `Notifications:RetentionWindow` | | `P180D` | Review-flagged retention disposition window |
| `Notifications:InternalClientId` | | *(reserved)* | Trusted in-process caller for future fact queries |

The Notifications service never reads the Authorization, Organization,
Community, Records, Workflow or Knowledge databases. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed). If
`CommunityService:BaseUrl` is unset, InApp dispatch still works (no destination
resolution needed); Email/SMS/Push dispatch fails closed at dispatch time.

## Dependencies

- **Identity** — RS256 JWT authentication of actors.
- **Authorization** — `notifications.*` permission evaluation via
  `AuthorizationGuard`.
- **Organization** — org-unit scoping facts via ADR-016 read-model projection
  (event consumption).
- **Community** — channel-destination resolution for non-InApp dispatch at
  dispatch time (InApp requires none).
- **Workflow** — consumes `WorkflowTaskAssigned`/`WorkflowTaskEscalated` for
  task-routing digests (first gate).
- **Transactional outbox/inbox** — enabled at the Notifications integration
  gate (ADR-015).

## Runbook

See `docs/runbooks/notifications.md` for the implemented operational plan.