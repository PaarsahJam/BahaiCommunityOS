# Audit Service

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 12C).** Architectural design for
> the Audit bounded context (`ADR-027`), positioned at ADR-017 slot 11.

The Audit bounded context owns an **append-only compliance journal**: a
write-once, queryable record of selected integration events raised by producer
contexts across CommunityOS. It is a historical/compliance record of selected
events — **never a replacement source of truth** for any domain fact. Every
producer service remains authoritative for its own state; where an upstream
fact is deliberately never exported onto the bus (task activity notes,
notification recipient distribution, hold reasons), the journal is partial by
design.

The service must preserve the boundary rules of `ADR-027`: entries are
immutable at both the application and database level, no cross-service database
access, authorization is centralized and fail-closed, payloads are reduced to
identifiers and codes at ingest (never PII), consumption is exactly-once, and
there is no Search-style "reindex" — history is never rewritten.

## Technology

PostgreSQL 16 (`ADR-006`) on the shared instance under a dedicated database
`communityos_audit` and schema `audit`. EF Core + Npgsql following the existing
service conventions. MassTransit receive-endpoint **inbox** (exactly-once
consume) over RabbitMQ (`ADR-004`). Zero new infrastructure: no new container,
no new NuGet packages beyond those every service already uses, no new
Testcontainers images beyond `Testcontainers.PostgreSql`.

Native PostgreSQL triggers enforce row immutability at the database level — a
deliberate exception to "no database tricks elsewhere" justified by the
compliance role of this context (see [Immutability](#immutability)).

## Model

### AuditEntry (immutable journal row)

One row per persisted occurrence of a ratified source event. Write-once.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Synthetic row id (UUID v4), PK |
| `OccurredOn` | `DateTime` | UTC — when the producer says the fact occurred (event time) |
| `IngestedOn` | `DateTime` | UTC — when Audit durably recorded it. Deliberately distinct from `OccurredOn`; the gap exposes delivery latency |
| `SourceService` | `string(50)` | Producing bounded context: `records`, `workflow`, `notifications`, `documents`, `authorization` |
| `SourceEventType` | `string(100)` | Contract event type name (e.g. `RecordVerified`) |
| `SourceEventHash` | `string(64)` | Deterministic SHA-256 over canonical (source service, event type, resource ids, occurred-on, discriminating scalar fields). The idempotency identity — UNIQUE |
| `Action` | `string(100)` | Normalized action code (e.g. `record-verified`, `hold-placed`, `task-completed`, `break-glass-approved`) |
| `Outcome` | `string(100)?` | Normalized outcome/status code carried by the payload (workflow outcome, scan status) |
| `ResourceType` | `string(50)` | What the fact is about: `record`, `document`, `workflow-task`, `notification`, `authz-role`, `authz-delegation`, `break-glass-request` |
| `ResourceId` | `Guid` | Primary aggregate id from the payload |
| `SecondaryResourceId` | `Guid?` | Sub-object id where applicable (hold id, version id, assignment/request id) |
| `SubjectId` | `Guid?` | Person/household the fact concerns, only when the payload carries one (e.g. `RecordCreated.SubjectId`). Never resolved or enriched against another service |
| `ActorId` | `Guid?` | Who performed the action, only when the payload carries one. Nullable because some payloads lack actors entirely (e.g. `RoleAssigned` has no assigner field) |
| `OrganizationUnitId` | `Guid?` | Primary organization scope from the payload; null = global/unscoped fact |
| `Sensitivity` | `enum` | `Normal \| Sensitive` — deterministic, payload-driven (see Privacy) |
| `CorrelationId` | `Guid?` | Reserved for future envelope enrichment (F-02 option C); unpopulated at the first gate |
| `CausationId` | `Guid?` | Reserved as above |
| `MetadataJson` | `jsonb?` | Allowlist-validated flat scalar key/value pairs (see Privacy). Never arbitrary payload serialization |
| `RetentionClass` | `string(50)` | Retention classification code applied at ingest |
| `RetentionExpiresOn` | `DateTime?` | Computed at ingest from class configuration; null = retain indefinitely |

No update path exists for any field. There is no row-version column because
nothing is ever concurrently mutated.

### AuditEntryHold (mutable companion table)

Legal/administrative hold references that exempt entries from retention purge.
Hold state lives here — never on the immutable entry itself.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Hold id |
| `EntryId` | `Guid` (FK) | Held journal row |
| `HoldType` | `string` | `legal` \| `administrative` |
| `PlacedBy` | `Guid` | Actor who placed the hold |
| `PlacedOn` | `DateTime` | UTC |
| `ReleasedBy` | `Guid?` | Set on release |
| `ReleasedOn` | `DateTime?` | Set on release |
| `ReasonCode` | `string(50)` | Short code from a fixed set (e.g. `investigation`, `legal-request`). Never free text |

An active hold (released-on null) blocks purge regardless of retention expiry.
Placing and releasing holds are themselves journaled (audit-of-audit).

### OrganizationUnitReference (projection)

ADR-016 read-model pattern: `OrganizationUnitCreated/Updated/ParentChanged`
are consumed into a local reference table used for scope-aware queries and
hierarchy context. Projection rows are mutable by design (they mirror upstream
state) and are **not** audit entries.

### MassTransit inbox/outbox tables

The host registers the bus with `AddCommunityOSEventBusWithInbox<AuditDbContext>`
(inbox-only; Search pattern): `InboxState` is active (exactly-once consumption).
`OutboxMessage`/`OutboxState` are provisioned but unused — Audit publishes no
integration events at the first gate; activating an outbox requires an explicit
ADR amendment.

## First-Gate Event Catalog

Exhaustive classification of every event in every existing contract
(`CommunityOS.Contracts.*`). Prompt 12C implements exactly the FIRST GATE set
plus the projection consumers — nothing else. Gated sets activate only when
their producer's outbox gate completes. Deferred/not-classified events are
never subscribed without an ADR amendment.

| Classification | Count | Events |
|----------------|-------|--------|
| CONSUMED AND PERSISTED — first gate | 22 | Records: all 16 lifecycle/compliance events. Workflow: `WorkflowTaskCreated`, `WorkflowTaskAssigned`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled`, `WorkflowTaskEscalated`. Notifications: `NotificationDispatched` |
| CONSUMED AND PERSISTED — Authorization security subset | 7 | `RoleAssigned`, `RoleRevoked`, `DelegationGranted`, `DelegationRevoked`, `BreakGlassRequested`, `BreakGlassApproved`, `BreakGlassRevoked` |
| CONSUMED AND PERSISTED — gated on producer outbox gate | 5 | Documents (Documents outbox gate): `DocumentClassified`, `DocumentDeactivated`, `DocumentRestored`, `DocumentContentDownloaded`, `DocumentScanCompleted` |
| CONSUMED BUT NOT PERSISTED — projection only | 3 | Organization: `OrganizationUnitCreated`, `OrganizationUnitUpdated`, `OrganizationUnitParentChanged` |
| DEFERRED | 17 | `WorkflowTaskStarted`; Identity account-security subset (`UserAccountLocked`, `UserAccountUnlocked`, `CredentialChanged`, `MfaMethodEnrolled`, `MfaMethodRemoved`, `ExternalIdentityLinked`, `ExternalIdentityUnlinked`); Organization governance facts (`DelegationFactGranted`, `DelegationFactRevoked`); Knowledge moderation/AI subset (`QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`, `QuestionArchived`, `AiSuggestionRequested`, `AiSuggestionReviewed`); `DocumentArchived` |
| NOT AN AUDIT EVENT | 41 | Documents: `DocumentCreated`, `DocumentMetadataUpdated`, `DocumentVersionAdded`. Identity: `UserAccountRegistered` (carries email), `UserAccountVerified`, `UserAccountDeactivated`, `DeviceRegistered` (carries device name), `RefreshTokenIssued` (session telemetry). Organization: `OrganizationCreated/Updated`, `Committee*`, `AppointmentAssigned/Ended`. Community: all 14 person/household/membership/activity/event/meeting/participation events (official membership/appointment facts belong to Records). Knowledge: Library pipeline and answer/category events (`WorkImported`, `EditionImported`, `EditionVerified`, `PassageImported`, `PassageCorrected`, `QuestionSubmitted`, `QuestionPublished`, `AnswerAdded`, `AnswerUpdated`, `AnswerAccepted`, `CategoryCreated`, `CategoryUpdated`) |

Rationale highlights:

- **Records — all 16.** ADR-023 designates Records compliance-critical; the
  full lifecycle is already outbox-protected, so complete coverage costs
  nothing extra.
- **Workflow — 5 of 6.** Created/assigned/completed/cancelled/escalated bracket
  task accountability (who was asked, what happened, who decided);
  `WorkflowTaskStarted` adds review-latency detail only and is deferred.
- **Notifications — dispatch completion only.** The sole exportable fact;
  recipient distribution is deliberately absent from the contract (ADR-025).
- **Documents — compliance subset, gated.** Exactly the five events
  `docs/documents.md` has always marked guaranteed-delivery-required.
  Existence/metadata/version events are not compliance facts.
- **Authorization — security subset, active.** Role/delegation changes are
  privilege facts; break-glass auditing is mandated by ADR-014 ("high-priority
  audit events"). The producer gate completed and the seven consumers are
  registered (below); Documents remains the only gated set.

### Producer delivery gates

| Producer | Publication today | Gate before Audit consumes |
|----------|-------------------|----------------------------|
| Records | Outbox-protected (`AddCommunityOSEventBusWithOutbox`) | none — clear |
| Workflow | Outbox-protected | none — clear |
| Notifications | Outbox-protected | none — clear |
| Documents | Best-effort (`AddCommunityOSEventBus`) | **Documents outbox gate**: upgrade to transactional outbox publication for the compliance subset (ADR-022 amendment note) |
| Authorization | Outbox-protected (`AddCommunityOSEventBusWithOutbox`, authorization outbox gate) | **Complete** — the seven security-subset consumers are registered through Audit's inbox (consumer configuration gate) |
| Organization | n/a (projection feed) | none — projection consumers may register immediately |

Until a producer's gate completes, the Audit implementation must not register
that producer's consumers. This upgrades documented policy into concrete,
scheduled prerequisites; it does not change any producer's behavior silently.

### Consumer configuration gate — Authorization ×7

Registered through Audit's inbox-only configuration
(`AddConsumer<AuthorizationAuditConsumer>` inside
`AddCommunityOSEventBusWithInbox<AuditDbContext>`); Audit remains inbox-only and
publishes nothing. The seven mappings use only payload fields the contracts
already carry:

| Event | Action | ResourceType / ResourceId | SubjectId | ActorId | OrganizationUnitId | Metadata | Sensitivity |
|-------|--------|---------------------------|-----------|---------|--------------------|----------|-------------|
| `RoleAssigned` | `role-assigned` | `authz-role` / `AssignmentId` | `SubjectId` | null (no assigner field) | `ScopeId` only when the scope is an org-unit tier | `role_code`, `scope_type` | Normal |
| `RoleRevoked` | `role-revoked` | `authz-role` / `AssignmentId` | `SubjectId` | null | null | `role_code` | Normal |
| `DelegationGranted` | `delegation-granted` | `authz-delegation` / `DelegationId` | `DelegateId` | `DelegatorId` | null (global) | — | Normal |
| `DelegationRevoked` | `delegation-revoked` | `authz-delegation` / `DelegationId` | `DelegateId` | `DelegatorId` | null (global) | — | Normal |
| `BreakGlassRequested` | `break-glass-requested` | `break-glass-request` / `RequestId` | null | `RequesterId` | null (global) | — | **Sensitive** |
| `BreakGlassApproved` | `break-glass-approved` | `break-glass-request` / `RequestId` | null | `ApproverId` | null (global) | — | **Sensitive** |
| `BreakGlassRevoked` | `break-glass-revoked` | `break-glass-request` / `RequestId` | null | `RevokedBy` | null (global) | — | **Sensitive** |

`role_code` and `scope_type` are the **only** metadata keys newly allowlisted by
this gate. No `ActorId` is invented where the contract carries none, no
fabricated scope is persisted, and `CorrelationId`/`CausationId` remain null.

## Ingest Pipeline

1. Producer publishes an integration event (outbox-dispatched where its gate
   has completed).
2. MassTransit delivers it to the Audit consumer via RabbitMQ.
3. The consumer maps the payload through the per-event-type ingest mapping:
   - resolve `Action`, `ResourceType/ResourceId`, optional actor/subject/scope,
   - derive `Sensitivity`,
   - apply the retention class → compute `RetentionExpiresOn`,
   - validate `MetadataJson` against the allowlist (keys ⊆ per-event-type
     allowlist; values scalar; ≤ 10 keys),
   - compute `SourceEventHash`.
4. Insert the `AuditEntry` in one database transaction together with the inbox
   row (atomic, exactly-once). Hash collision with an existing row ⇒ success
   no-op (idempotent duplicate suppression).

### Idempotency semantics

| Situation | Behavior |
|-----------|----------|
| Duplicate delivery of the same broker message | Suppressed by the MassTransit inbox |
| Same fact redelivered under a new broker message | Identical `SourceEventHash` ⇒ insert is a success no-op. Broker redelivery can never create a duplicate entry |
| Redelivery after transaction failure | Inbox retry; ordinary insert path |
| Same logical event re-published with different `OccurredOn` | Different hash ⇒ **new distinct occurrence**, journaled as its own entry (the producer restated a fact) |
| Malformed / unmappable event identity | Dead-letter queue + error log. Never persisted as an entry, never partially written |

### Ordering and eventual consistency

Entries reflect arrival order, not causal order: `OccurredOn` preserves the
producer's claim of when a fact occurred, `IngestedOn` records receipt, and the
two can interleave across producers. Consumers of the query API must treat the
journal as eventually consistent with respect to any single producer's
sequence. Causal-chain reconstruction is explicitly **not guaranteed** at the
first gate (no ratified correlation identifiers — see F-02 resolution in
ADR-027 decision 7).

## Immutability

Layered enforcement — both levels ratified (ADR-027 decision 2):

- **Application level:** `AuditEntry` is write-once. No command, handler,
  repository method or endpoint updates or deletes an entry. Corrections,
  reversals and superseding facts arrive as **new events**, which become
  **new** entries (e.g. a corrected record produces a further `RecordCorrected`
  entry; nothing about the earlier entry changes).
- **Database level:** the initial migration ships native triggers rejecting
  `UPDATE`/`DELETE` on `audit_entries` unless the session sets
  `app.audit_purge_authorized = 'on'` via `SET LOCAL`. Only the ratified purge
  operation sets it. Even an application bug therefore cannot rewrite history.

Retention purge is **not** an ordinary correction: it writes a purge-marker
entry in the same transaction before deleting the expired batch, so deletion
itself remains auditable after the rows are gone. Legal holds live in the
separate mutable `audit_entry_holds` table and block purge regardless of expiry.

## Privacy

Persisted by default: stable identifiers; codes carried by payloads (category,
classification, definition, outcome, scan status); timestamps; event type;
action/outcome codes; resource identifiers; organization identifiers;
sensitivity classification; allowlisted structured metadata.

Never persisted: names, addresses, email addresses, telephone numbers, document
or message bodies, notification destinations, credentials, secrets, access
tokens, hold reasons, arbitrary domain field values, uploaded content,
unnecessary personal attributes. Payloads carrying such fields are either not
consumed at all or mapped so those fields are dropped at ingest (e.g.
`UserAccountRegistered.Email`, `DocumentCreated.Title`,
`CommunityEventCreated.Title` would be dropped if their types were ever
ratified later).

**Sensitivity model (deterministic, never inferred):**

| Rule | Result |
|------|--------|
| Event type in the ratified sensitive list: `BreakGlassRequested`, `BreakGlassApproved`, `BreakGlassRevoked`, `RecordHoldPlaced`, `RecordHoldReleased`, `DocumentContentDownloaded` | `Sensitive` |
| Payload explicitly carries `IsSensitive = true` (`RecordClassified`, `DocumentClassified`) | `Sensitive` |
| Everything else | `Normal` |

Sensitive entries are excluded entirely from standard query results and require
`audit.entry.read.sensitive` to appear anywhere (Search sensitive pattern) —
including export opt-in.

**Metadata discipline:** `MetadataJson` stores flat key/value pairs only. Keys
must come from a per-event-type allowlist maintained in code/configuration;
values must be scalars (ids, codes, enums, counts, booleans); maximum ten keys.
A mapping that cannot conform dead-letters the message rather than sanitizing
or truncating. Deterministic structured metadata replaces arbitrary event
serialization.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Entry ids, source service/event type, action/outcome codes, resource ids, subject/actor ids, scope, sensitivity flag, timestamps, retention class, allowlisted metadata. Sensitive entries only under `audit.entry.read.sensitive`. |
| **Logs** | Entry/source ids, action, outcome, ingest outcome. Never payload field values, never names, never metadata values beyond codes. |
| **Integration events** | None — Audit publishes nothing at the first gate. |
| **Exports** | The same fields as API responses, bounded by the export cap; sensitive rows only on explicit opt-in with the second permission. |

## Authorization

Every exposed operation calls the Authorization service through
`AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019). No
`[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization database
access.

Permissions (registered at the implementation gate):

| Permission | Purpose |
|------------|---------|
| `audit.entry.read` | Query/list/read non-sensitive entries |
| `audit.entry.read.sensitive` | Second pass for `Sensitive` entries (additive to read) |
| `audit.entry.export` | Run capped synchronous exports (sensitive rows additionally need `.read.sensitive`) |
| `audit.entry.admin` | Place/release holds, execute retention purge batches, view restricted diagnostics |

`admin` does **not** imply `read` (capability separation mirrors the
Records/Workflow override pattern); administrative results still require
`read`.

Scoping follows ADR-011: national/regional/local grants cover descendant units
via Organization `/covers` resolution; a Global-scope grant of
`audit.entry.read` matches checks with **no** organization context — which is
how null-scoped entries (global facts such as break-glass requests) become
visible exclusively to Global-grant holders. An entry whose scope cannot be
established is unreadable (fail-closed). Anti-enumeration: single reads return
`404` for missing and unauthorized alike; lists/queries silently filter and
counts exclude inaccessible rows.

See `docs/api/audit.md` for the permission-to-endpoint mapping.

## HTTP API

All endpoints are versioned under `/api/v1/audit`, require a valid RS256
access token, and return DTOs — EF entities are never exposed. Six endpoints
at the first gate:

| Method | Path | Capability |
|--------|------|------------|
| GET | `/api/v1/audit` | `audit.entry.read` |
| GET | `/api/v1/audit/{id}` | `audit.entry.read` (+ `.read.sensitive` for sensitive rows) |
| POST | `/api/v1/audit/export` | `audit.entry.export` (+ `.read.sensitive` opt-in) |
| POST | `/api/v1/audit/holds` | `audit.entry.admin` |
| POST | `/api/v1/audit/holds/{id}/release` | `audit.entry.admin` |
| POST | `/api/v1/audit/admin/purge-expired` | `audit.entry.admin` |

Resource/subject convenience endpoints are deliberately omitted — the query
filter surface covers them. Query supports filters (source service, event
type/action, resource type/id, subject, actor, organization unit, occurred-on
date range), fixed `OccurredOn` descending order (ascending opt-in),
`limit` default 25 / maximum 100 (clamped) with offset paging. Export is
synchronous with a hard cap of 10,000 rows and streams CSV or NDJSON; an
asynchronous job system is not justified at this scale.

Full contract: `docs/api/audit.md`.

## Retention Lifecycle

Architecture-level behavior (deployment policy configures durations; no legal
requirements are asserted here):

1. **Classification at ingest** — each event type maps to a retention class
   (configuration: code → ISO-8601 duration). The default class retains
   indefinitely (`RetentionExpiresOn` null) until deployment policy defines
   durations.
2. **Expiry detection** — eligibility is evaluated on demand against the
   partial `(retention_class, retention_expires_on)` index: each purge
   invocation selects expired, unheld entries directly and reports its
   remaining-expired estimate. There is no background sweep worker at this
   gate; expiry alone never deletes.
3. **Two-step purge** — execution requires the `audit.entry.admin` purge
   endpoint: one bounded batch of expired, unheld entries per call; a
   purge-marker entry is written in the same transaction before the batch is
   deleted under the trigger guard. Repeated invocation drains the backlog.
4. **Holds override expiry** — an active legal/administrative hold exempts its
   entries from every purge batch.
5. **Purge mechanics** — hard row deletion (cryptographic erasure schemes are
   not justified by any ratified requirement). Archival (cold-storage
   export-and-delete) is deferred unless a future ADR requires it.

## Audit-of-Audit

Explicit first-gate model (ADR-027 decision 15):

| Activity | Journaled? |
|----------|------------|
| Reading/querying entries | **No** (recursion/noise prevention) |
| Exporting entries | **Yes** — actor, filter-criteria summary, row count, format; never row contents |
| Administrative mutations (hold place/release, purge execution) | **Yes** |
| Failed authorization attempts against Audit | **No** — the Authorization check API remains the enforcement record; journaling denials would hand attackers a probe oracle inside the guarded store |

Loop prevention: audit-generated entries are inserted directly within the
handling request's transaction and are never published onto the bus, so no
consumer feedback loop can form.

## Replay, Reconstruction and Schema Evolution

- **No reindex/rebuild — ever.** Unlike Search, truncating and rebuilding would
  fabricate or destroy history and is forbidden.
- **Replay is safe by construction:** dedupe keys on content hash, so
  re-consuming old messages converges to zero new rows. Replay uses the
  ordinary consumption path (operator requeues from the broker); there is no
  replay/rebuild API at the first gate.
- **Lost history stays lost:** events pre-dating Audit activation, or lost
  before a producer's outbox gate, cannot be reconstructed. This limitation is
  accepted openly and mitigated by the guaranteed-delivery prerequisites rather
  than hidden.
- **Forward-only schema evolution:** ingest mappings may change for newly
  consumed messages; existing entries are never reinterpreted retroactively.

## Integration

- **Consumes** — exactly the ratified first-gate catalog above via MassTransit
  under `CommunityOS.Contracts.{Records,Workflow,Notifications,Documents,
  Authorization,Organization}`. Consumer registration per producer is gated on
  that producer's delivery gate.
- **Produces** — nothing. No integration events at the first gate; outbox
  activation requires an ADR amendment.
- **Authorization** — guard over the check API as a service principal; no
  direct database access (ADR-018).
- **Organization** — unit events projected into `organization_unit_references`;
  scoping never depends on a live Organization query.
- **Community/Knowledge/Search** — no interaction. Community events are
  classified NOT AN AUDIT EVENT at the first gate; Knowledge events are
  deferred/not-classified; Search indexes domain facts, not audit history.

### Dependency map

```
Records ──────┐
Workflow ─────┤ (outbox-protected today)
Notifications ┘        │
                       ▼
Documents ──── (gated: Documents outbox gate) ──►  AUDIT  ──►  (query/export API)
Authorization (gated: Authorization outbox gate)┘    │
Organization ──► unit reference projection           ▼
                                        Authorization check API (guard)
```

- **Depends on:** Identity (authentication), Authorization (guard/check API),
  Organization (unit reference projection), RabbitMQ (delivery), the producers'
  own delivery guarantees per gate table.
- **Does not depend on:** Community, Knowledge, Search, Notifications APIs,
  AI — Audit never resolves names or enriches facts; ids stay ids.
- **Provides for:** auditors/compliance officers (query/export surface); future
  Analytics (slot 19) may read through the API under the same permissions — it
  receives no event feed unless a future ADR amends decision 9.

## Data

- Database: `communityos_audit` (PostgreSQL 16), schema `audit`.
- Tables: `audit_entries`, `audit_entry_holds`, `organization_unit_references`,
  plus MassTransit `InboxState` (active) and `OutboxMessage`/`OutboxState`
  (provisioned, unused).
- Indexes: PK; UNIQUE `(source_event_hash)`; `(occurred_on)`;
  `(resource_type, resource_id)`; `(subject_id)`; `(actor_id)`;
  `(organization_unit_id)`; `(sensitivity)`; partial
  `(retention_class, retention_expires_on)` WHERE
  `retention_expires_on IS NOT NULL`.
- Immutability triggers ship in the initial migration.
- Schema managed by EF Core migrations created at implementation time.
- Uniqueness: one entry per `source_event_hash` (idempotency identity).
- Immutable vs mutable: `audit_entries` immutable forever outside the
  guarded purge path; `audit_entry_holds` and `organization_unit_references`
  are ordinary mutable companion state.

## Storage

- **Metadata storage** — PostgreSQL (`communityos_audit`). Authoritative for
  the journal itself.
- **No binary storage** — Audit never receives or stores bytes, blobs or file
  references beyond ids already present in payloads.
