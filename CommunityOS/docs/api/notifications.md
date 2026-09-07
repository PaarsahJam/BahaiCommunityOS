# Notifications Service API

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 10C).** Contract for the
> Notifications service, aligned with ratified ADR-025 and
> `docs/notifications.md`.

> **MEMBER READ CONTRACT (ADR-027, Prompt 43):** the `/my-notifications`
> surface below is an additive member-safe read contract. The original
> `/notifications` surface is unchanged.

All endpoints are versioned under `/api/v1/notifications`, require a valid
access token (`[Authorize]`), and return DTOs — **EF entities are never
exposed**. Every guarded operation is evaluated against the Authorization
service (fail-closed). Notification metadata and sensitive fields (failure
reasons, distribution) are separate capabilities with separate permissions.
Notifications stores no binary content and no person data; recipients are
stable member ids and channel destinations are resolved through the Community
API at dispatch time.

## Member notifications — `/my-notifications` (member read contract)

Member-safe read surface for a member's **own** notifications (ADR-027). The
recipient is always the authenticated subject — there is **no client-supplied
`memberId`** route or query parameter anywhere on this surface, no organization
membership/role inference, and no client-side filtering. The persisted
recipient relationship is the sole server-side authorization (fail-closed).
`my-notifications` is a distinct first segment so the API Gateway can expose
exactly this surface without exposing the administrative `/notifications`
endpoints.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/my-notifications?limit=50&offset=0` | List the caller's own notifications, newest-first, with content and recipient-specific read state |
| GET | `/my-notifications/unread-count` | Count the caller's delivered-but-unread notifications |
| POST | `/my-notifications/{id}/read` | Mark the caller's delivered notification as read (actor must be the recipient) |

Semantics:

- **Content** — each item carries the notification's own `title` and `body`
  (the per-notification subject/body rendered at dispatch time with the
  non-PII variable set). Bodies never embed person data; a member's body is
  the notification's body (not per-recipient).
- **Read state** — per item: `isRead` and nullable `readAt` are the
  authenticated recipient's own state. One recipient reading a shared
  notification never changes another recipient's read state.
- **Unread count** — `{ "count": n }`, count of that actor's notifications in
  recipient status `Delivered` (delivered, not yet read). No notification
  details, no distribution. Decrements after mark-as-read.
- **Pagination** — `limit` default 50, maximum 100; `offset` default 0; ordering
  newest-first (`createdOn` descending). Retrieval is always server-side
  bounded.
- **Sensitive notifications** — `IsSensitive` rows are **excluded** from the
  member surface (list, count and mark-as-read) and surface `404` for any
  direct access. Sensitive content and distribution remain on the
  `/notifications` admin surface only (fail-closed).
- **Failure behavior** — `404` for missing, sensitive, or non-recipient
  notifications alike (no existence oracle, no recipient enumeration); `409`
  for a mark-as-read transition on a notification that was never delivered
  (recipient not yet `Delivered`).

## Notifications — `/notifications`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/inbox?type=&channel=&status=&organizationUnitId=&limit=50&offset=0` | `notifications.notification.read` | List the caller's own inbox (relationship-tuple access; returns only items the caller may read) |
| GET | `/notifications/{id}` | `notifications.notification.read` | Get notification metadata, recipients, scopes, status, provenance (non-sensitive) |
| GET | `/notifications/{id}/sensitive` | `notifications.notification.read.sensitive` | Get the sensitive fields (failure reasons, full distribution) and `IsSensitive` notification content |
| POST | `/notifications` | `notifications.notification.create` | Create a notification (directly or via event reconciliation) |
| POST | `/notifications/{id}/dispatch` | `notifications.notification.send` | Trigger dispatch / admin re-send |
| POST | `/notifications/{id}/recipients/{memberId}/read` | `notifications.notification.read` | Mark a delivered notification read (actor must be the recipient) |

`GET /inbox` returns only notifications the caller is authorized to read
(the caller is a recipient via a relationship tuple, or holds scope-level
read); it never returns a count or marker of inaccessible notifications.
`GET /notifications/{id}` returns `404` for missing **and** unauthorized
notifications alike (no enumeration oracle).

**Create** body:

```json
{
  "typeCode": "general",
  "channel": "InApp",
  "sourceType": "workflow-task",
  "sourceId": "00000000-0000-0000-0000-000000000001",
  "organizationUnitId": "00000000-0000-0000-0000-000000000002",
  "additionalScopes": [],
  "recipientIds": ["00000000-0000-0000-0000-000000000003"],
  "scheduledFor": null,
  "isSensitive": false,
  "subject": "Notice",
  "body": "A workflow task requires your attention."
}
```

Creation is idempotent per (type, source type, source id, channel): a second
create for the same source fact returns the existing notification (200) rather
than creating a duplicate. The notification is created in `Draft`; it moves to
`Queued` and is dispatched by the dispatch worker.

**Mark read** body: none. The endpoint is a state transition
(`Delivered → Read`) and requires the actor to be the recipient (relationship
tuple); an unauthorized caller receives `404`.

## Notification types — `/notifications/types`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/types` | `notifications.template.read` | List the notification-type/template catalog |
| GET | `/types/{code}` | `notifications.template.read` | Get a notification type |
| POST | `/types` | `notifications.type.manage` | Create a notification type |
| PUT | `/types/{code}` | `notifications.type.manage` | Update a notification type |
| POST | `/types/{code}/retire` | `notifications.type.manage` | Retire a notification type (only when unused) |

**Create type** body:

```json
{
  "code": "task-assigned",
  "displayName": "Task assigned",
  "defaultChannel": "InApp",
  "subjectTemplate": "A task has been assigned to you",
  "bodyTemplate": "Task {{TaskId}} has been assigned to you.",
  "isSensitive": false
}
```

The baseline catalog is `task-assigned`, `task-escalated`, `task-completed`,
`task-cancelled`, `record-verified`, `record-rejected`, `record-hold`,
`question-flagged`, `community-activity`, `community-event`,
`community-meeting`, `general`. Codes are stable strings, extensible via
configuration. The baseline is seeded idempotently at migration time.
Creating a type with an existing code returns `409`; retiring a type in active
use returns `409`.

## Preferences — `/notifications/preferences`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/preferences/me` | `notifications.preference.manage` | Get the caller's notification preferences |
| PUT | `/preferences/me` | `notifications.preference.manage` | Update the caller's notification preferences |

**Update preferences** body:

```json
{
  "rules": [
    { "typeCode": "task-assigned", "channels": ["InApp"], "enabled": true },
    { "typeCode": "community-event", "channels": ["InApp"], "enabled": false }
  ]
}
```

The dispatch worker applies preferences when deriving recipients: an opt-out
for a (type, channel) suppresses that notification.

## DTOs (conceptual)

- `MemberNotificationSummaryDto` — id, typeCode, channel, status, title, body,
  isRead, readAt, createdOn (member read contract; no distribution, no
  source/scope metadata).
- `MemberUnreadCountDto` — `count` (recipient-scoped unread total).
- `NotificationDto` — id, typeCode, channel, status, sourceType, sourceId,
  primary org unit, additional scopes, recipientIds, scheduledFor, isSensitive,
  created/dispatched provenance.
- `NotificationSummaryDto` — id, typeCode, channel, status, sourceId, primary
  scope, created timestamp (for inbox lists).
- `NotificationSensitiveFieldsDto` — failureReasons per recipient, full
  distribution, delivery diagnostics.
- `NotificationTypeDto` — code, displayName, defaultChannel, subjectTemplate,
  bodyTemplate, isSensitive, active, created/updated provenance.
- `NotificationPreferenceDto` — rules: typeCode, channels, enabled.
- Request records — `CreateNotificationRequest`, `CreateNotificationTypeRequest`,
  `UpdateNotificationTypeRequest`, `UpdatePreferencesRequest`.

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure, invalid type code, invalid channel, invalid member/scope reference |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Notification / type / recipient not found (also for unauthorized reads) |
| 409 | Invalid transition (double dispatch, mark-read from a non-delivered state, etc.), duplicate type code, retire-in-use type |

## Service-to-service notes

The first gate exposes no cross-service fact endpoint; the inbox is the
member-facing read surface. `WorkflowTaskAssigned`/`WorkflowTaskEscalated`
consumers are inbound integrations, not exposed endpoints; they require the
transactional outbox/inbox gate (ADR-015) to be enabled before production use.
`NotificationDispatched` is the only outbound integration event (outbox-
protected; consumed by Audit at slot 11 later).