# Records Service

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 08B).** The architectural decisions
> for the Records bounded context were ratified in ADR-023 (Accepted) and recorded
> in this document, and the service has been implemented in Prompt 08B (Domain,
> Application, Infrastructure, API, permissions, EF migration, tests). Decisions
> are binding unless a later ratified ADR amends them.

The Records bounded context owns the community's **official records**: the
authoritative record of births, marriages, deaths, membership, appointments,
community facts and administrative facts — together with the verification,
versioning, retention, hold and evidence semantics that make a record
authoritative and durable. Records is positioned in the implementation sequence
(`ADR-017`) at slot 7, immediately after Documents (`ADR-022`), and is the
compliance-critical source of record-of-truth for Workflow, Notifications,
Audit, Search, Correspondence and Finance.

It is not — and must never become — the owner of document artifacts, person
identity, organizational hierarchy, workflow state, notification delivery, AI
decisions, correspondence lifecycle, Knowledge/Library content or financial
meaning.

## What a "Record" is (and is not)

**Record** — the aggregate root. An official, categorized, versioned record of
a community or administrative fact, with an immutable authoritative version
once verified, references to document evidence, retention and hold governance,
and a guarded lifecycle.

| Concept | Owned by | Relationship to Records |
|---------|----------|-------------------------|
| **Official Record** | Records | The record facts, lifecycle, verification, versioning, retention schedules, holds. |
| **Document artifact** | Documents | Records *references* documents as evidence via `RecordEvidenceReference` / `DocumentReference` (`SourceContext = records.record`). Documents never owns record lifecycle, retention schedules or holds. |
| **Workflow verification** | Workflow (future) | Workflow may drive verification/review tasks from Records events; task state stays in Workflow, record authority stays in Records. |
| **Person / household identity** | Community | Records stores only stable person/household ids; names are resolved through the Community API at read time. |
| **Organizational hierarchy** | Organization | Records scopes records to organization units through the ADR-016 read-model projection. |
| **Correspondence letter** | Correspondence (future) | Records may reference submitted letters as evidence; it never owns letter lifecycle. |
| **Financial record** | Finance (future) | Finance references records/documents as needed; Records never attaches financial meaning. |
| **Knowledge / Library** | Knowledge | Records never duplicates Library content; citations stay in Knowledge. |

Records carries retention and hold **semantics** (schedules, rules, periods,
legal/administrative hold lifecycle); Documents carries only retention/hold
*references* and honors them for deactivation protection (`ADR-022`).

## Model

### Record

- **Identity** — `Guid Id`, stable, referenced by every other context.
- **Category** — `RecordCategoryCode` (string; baseline catalog below). Records
  are grouped by category so retention, sensitivity and governance can be
  applied uniformly.
- **Lifecycle** — `Draft → Submitted → Under Review → Verified → Archived |
  Deactivated`, plus `Rejected` (from `Under Review`). `Verified` is the
  authoritative state. Non-authoritative fields remain editable in `Draft`,
  `Submitted` and `Under Review`; from `Verified` onward field changes require
  the `correct` operation (new superseding version), never in-place mutation.
- **Subject references** — `SubjectType` (`Person` | `Household` |
  `OrganizationUnit` | `Other`) and `SubjectId`. Person/household ids are stable
  references resolved through the Community API; never names or PII stored
  locally.
- **Organization scope** — `OrganizationUnitId` (primary scope, nullable
  reference) plus an optional set of additional scopes (`RecordOrganizationScope`).
  A record may belong to multiple organization scopes; person/household records
  may also be resource-scoped via Authorization relationship tuples.
- **Security metadata** — see Classification below.
- **CurrentVersionId** — pointer to the current authoritative `RecordVersion`.
- **Provenance** — `CreatedBy`/`UpdatedBy`/`VerifiedBy` (stable person ids);
  never names/PII.
- **Evidence references** — a set of `RecordEvidenceReference` links to document
  versions used as evidence.
- **Hold references** — references to active `RecordHold` records that freeze
  disposition.

### RecordVersion (entity)

Immutable snapshot of the authoritative record facts. **Every field is
immutable once written.** Facts are stored as typed field values, never as a
JSON blob (so schema evolution and sensitive-field gating stay first-class).

- **Identity** — `Guid Id`.
- **VersionNumber** — 1-based, monotonically increasing per record, assigned by
  the service (never client-supplied).
- **Fields** — a set of `RecordFieldValue` rows (`FieldKey`, `FieldValue`,
  `IsSensitive`).
- **SupersedesVersionNumber** — the version this version replaces (nullable).
- **AppliedBy / AppliedOn** — actor reference and UTC timestamp.
- **ChangeReason** — required when the version is a post-verification
  correction.

A `Draft`/`Submitted`/`Under Review` record holds a *working* field set; the
first `Verified` version freezes those facts into the authoritative baseline.
Corrections after verification append a new superseding `RecordVersion`; the
prior versions are never mutated.

### RecordCategory (catalog)

Stable string codes (baseline): `birth`, `marriage`, `death`, `membership`,
`appointment`, `official-community`, `administrative`. Each category may carry a
default sensitivity and a default `RetentionScheduleCode`. The catalog is
configuration, not a hard enum, and remains open to the ratified Data
Classification Model (`specifications/06`, currently PLACEHOLDER).

### RecordClassificationMetadata

Security metadata carried by every record, following the ratified convention
(ClassificationCode string + `IsSensitive` gate) so the Data Classification
Model can be consumed without schema redesign.

- **ClassificationCode** — nullable string reserved for the ratified
  classification level code. No fixed allowed set until the model is ratified.
- **IsSensitive** — `bool` (default `false`). Operational gate: when `true`,
  sensitive record fields require `records.record.read.sensitive`. Ordinary
  reads of non-sensitive metadata are never gated by the sensitive flag.
- **RetentionScheduleCode** — reference to the active `RetentionSchedule`.
- **ClassifiedBy / ClassifiedOn** — audit provenance.

### RetentionSchedule / RetentionRule / RetentionPeriod

Retention governance owned by Records.

- **RetentionSchedule** — named schedule (e.g. `administrative-7y`), linked to
  record categories by default. Contains one or more `RetentionRule` records.
- **RetentionRule** — expresses a retention window: `RetentionPeriod`
  (duration), a `StartTrigger` (`recordDate` | `verifiedDate`), a `Disposition`
  (`review` — the only disposition in this prompt; no destruction), and an
  optional `MaximumPeriod` (ISO-8601 duration) that caps total retention for
  records governed by the rule.
- **RetentionPeriod** — a duration (e.g. `P7Y`) with an optional maximum
  (`MaximumPeriod`, e.g. `P20Y`) that caps total retention; both are validated
  as ISO-8601 durations.
- **Expiry** — when a record's retention period lapses, the record is flagged
  `RetentionExpired → Review` for a ratified disposition review process.
  **Retention expiry never destroys data** through normal operations.

### RecordHold (entity)

The hold lifecycle owned by Records.

- **Identity** — `Guid Id`.
- **HoldType** — `legal` | `administrative`.
- **Reason** — free-text reason (sensitive; never exported in events/logs).
- **Scope** — targets a `RecordId` and/or specific `DocumentId`/`VersionNumber`
  references (the Documents hold-reference hook).
- **PlacedBy / PlacedOn**, **ReleasedBy / ReleasedOn** — provenance. Release
  requires a subject different from the placer.
- **Effect (ratified)** — while active, the hold freezes disposition: the
  target record cannot be deactivated, and referenced held documents cannot be
  deactivated (Documents honors the reference and its own
  deactivation-protection rule; `docs/documents.md`).

### RecordEvidenceReference (entity)

The Records-side link binding a record to document evidence.

- **Identity** — `Guid Id`.
- **RecordId** — the record.
- **DocumentId / VersionNumber** — the specific document version used as
  evidence.
- **ReferenceType** — e.g. `evidence`, `supporting`, `certificate`.
- **AttachedBy / AttachedOn** — provenance.
- Unique per `(RecordId, DocumentId, VersionNumber, ReferenceType)`.

The Documents-side mirror is `DocumentReference` with `SourceContext =
records.record`. Records does not read the Documents database; evidence is
attached by commanding the Documents classify/reference surface and reconciled
with `DocumentDeactivated`/`DocumentRestored`.

### OrganizationUnitReference (read model)

Records mirrors the Community/Knowledge/Documents pattern: it consumes
`OrganizationUnitCreated/Updated/ParentChanged` into
`organization_unit_references` so record scoping never depends on a live
Organization query (`ADR-016`). It never introduces a second hierarchy and never
influences authorization decisions (authorization is resolved live by the
Authorization service).

## Key rules

- **A verified record is immutable.** After `Verified`, facts are never mutated
  in place; a correction appends a superseding `RecordVersion` and moves the
  current-version pointer.
- **Working fields stay editable until verification.** Non-authoritative fields
  may be updated in `Draft`, `Submitted` and `Under Review`
  (`records.record.update`). Once `Verified`, field changes require the
  `correct` operation with a change reason — never an in-place update.
- **Metadata changes do not create versions.** Only authoritative fact changes
  create versions; classification, scopes, retention and hold changes mutate the
  record and are audited through events.
- **Lifecycle is guarded and audited.** Every transition is a guarded operation
  with a dedicated permission and emits an integration event.
- **`Verified` is the authoritative state.** Only verified records may be
  archived; only verified records expose the authoritative baseline to
  consumers. Corrections are explicit and reviewed.
- **Deletion is never hard deletion** during normal operation. `Deactivated`
  preserves data; restoration is a first-class operation.
- **Holds protect.** An active legal/administrative hold blocks deactivation of
  the record (and of referenced held documents); release requires a subject
  different from the placer; a held-record deactivation requires
  `records.record.admin` with a reason.
- **Retention never destroys.** Expiry flags a review disposition; there is no
  automatic or irreversible destruction through normal application operations.
- **A record may belong to multiple organization scopes**; access is granted
  when the caller holds the permission at **any** of the record's scopes.
- **Sensitive fields are a separate capability.** Reading non-sensitive
  metadata never implies reading sensitive fields
  (`records.record.read.sensitive`).
- **Separation of duties.** The creator of a record cannot verify it; the
  placer of a hold cannot release it; deactivation of a held record is an
  administrative override with a reason.
- **Unauthorized enumeration is prevented.** List/search returns only records
  the caller may read (fail-closed filtering at the query boundary);
  `GET /records/{id}` returns `404` for both *missing* and *not readable*
  records — no existence oracle.
- **Resource-level authorization is explicit.** Every guarded operation passes
  `resourceType = "record"` and the record id as resource context to the
  Authorization check API, so per-record grants (relationship tuples) and
  organization-scoped grants compose exactly as in other services.

## Privacy and permissions

Records is the most compliance-sensitive data surface in the platform. Security
metadata follows the ratified convention below; every guarded operation is
evaluated by the Authorization service through `AuthorizationGuard`
(fail-closed). No `[Authorize(Roles = "...")]`, no local RBAC, no direct
Authorization database access (`ADR-009/018/019`).

### Permission matrix (ratified)

| Permission | Purpose | Typical holder |
|------------|---------|----------------|
| `records.record.create` | Create a Draft record | clerks / services |
| `records.record.read` | List and read non-sensitive record metadata/fields | authorized readers |
| `records.record.read.sensitive` | Read sensitive record fields | higher-privilege readers |
| `records.record.update` | Update non-authoritative fields of a Draft/Submitted/Under-Review record | clerks |
| `records.record.submit` | `Draft → Submitted` | clerks |
| `records.record.verify` | `Submitted → Under Review` and `Under Review → Verified`; `→ Rejected` (separation of duties: not the creator) | verifiers / institutions |
| `records.record.correct` | Apply a post-verification correction (new superseding version) | verifiers / administrators |
| `records.record.archive` | `Verified → Archived` | administrators |
| `records.record.deactivate` | `→ Deactivated` (soft-delete; blocked by active holds) | administrators |
| `records.record.restore` | Restore from `Archived`/`Deactivated` | administrators |
| `records.record.classify` | Set classification, sensitive flag, retention schedule reference | classifiers / Records service |
| `records.record.scope.manage` | Add/remove organization scopes (changes effective audience) | administrators |
| `records.record.evidence.manage` | Attach/remove document evidence references | clerks / verifiers |
| `records.retention.manage` | Manage retention schedules, rules and periods | administrators |
| `records.hold.manage` | Place/release legal or administrative holds | administrators / legal officer |
| `records.category.manage` | Manage the category catalog | administrators |
| `records.record.admin` | Administrative overrides (hold-protected deactivation, forensics) | operators |

Scope semantics follow `ADR-011`: data permissions are organization-scoped; a
grant at a national scope covers descendant units via the Authorization service
hierarchy resolution (Organization `/covers`). A global grant of a data
permission only applies to checks with no organization context. Per-record
grants use Authorization relationship tuples; Records never evaluates them
itself. `records.record.ai.review` is reserved for the future AI-assist feature
(AI Platform, slot 15) and is not part of the core matrix.

### Resource-level authorization

- Every guarded operation calls the Authorization check API with
  `resourceType = "record"`, the record id as `resourceId`, and the effective
  organization-unit scope (primary scope by default; access succeeds when the
  permission is effective at **any** of the record's scopes).
- Organization-scoped grants and per-record relationship-tuple grants compose
  through the Authorization service — Records implements neither.
- Fail-closed: any inability to establish the grant (unreachable Authorization
  service, unknown scope, missing tuple) is a Deny (`403`).

### Separation of duties

- **Creator ≠ verifier.** The subject in `CreatedBy` cannot be the subject in
  `VerifiedBy` (or the subject that moves the record into/out of Under Review).
  Violations are rejected (`409`).
- **Placer ≠ releaser.** The subject that places a hold cannot release that
  same hold; release requires a subject with `records.hold.manage` different
  from the placer.
- **Held-record deactivation is an override.** Deactivating a record with an
  active hold requires `records.record.admin` and a reason; the action is
  audited as a high-priority event.
- All four facts are audited through events and never bypass normal
  authorization.

### Unauthorized enumeration

- List/search applies fail-closed read filtering: the query returns only rows
  the caller is authorized to read; it never returns a count or marker of
  records the caller cannot read.
- `GET /records/{id}` and all version/evidence/hold lookups return `404` for
  missing **and** unauthorized records alike (no existence oracle).
- Reading sensitive fields without `records.record.read.sensitive` is denied
  without revealing that the fields exist.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Record metadata, category, status, subject refs, scopes, classification, retention, holds, version descriptors, evidence references. Sensitive field values only under `records.record.read.sensitive`. |
| **Logs** | Record/version/hold ids, action, actor, outcome. Never field values, never secrets, never names, never hold reasons. |
| **Audit events** | Ids, actor, action, status/classification/retention/hold transitions, sensitive-read records. Never field values, never names, never hold reasons. |
| **Integration events** | Identifiers and minimal lifecycle metadata only. Never field values, never secrets, never names, never hold reasons. |
| **Search indexes** | Metadata and (future) extracted fields, gated by classification. Never secrets. |
| **AI pipelines** | Metadata and (future) non-sensitive field summaries only, gated by classification and the AI Governance model; AI is advisory only and never authoritative. |

## HTTP API

All endpoints are versioned under `/api/v1/records` and require a valid access
token. Metadata access and sensitive-field access are separate capabilities.
See `docs/api/records.md` for the full endpoint reference. No EF entities are
exposed; DTOs are returned.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (`CommunityOS.Contracts.Records`), following the open-generic
  publisher pattern used by every existing service. See below for the contract.
- **Organization** — Records consumes `OrganizationUnitCreated/Updated/
  ParentChanged` into `organization_unit_references` (ADR-016 pattern). It never
  reads the Organization database.
- **Community** — subject ids are stable person/household ids; Records resolves
  names through the Community API at read time and never stores person data.
- **Documents** — Records attaches evidence by commanding the Documents
  reference/classify surface (`SourceContext = records.record`) and consumes
  `DocumentDeactivated`/`DocumentRestored` to reconcile the protection of
  held/evidence documents. Guaranteed delivery is required for that consumer
  (see Outbox gate). Records never reads the Documents database. **Note:
  `POST /documents/{id}/classify` is a full-replacement operation** — when
  Records places or releases a hold it must read and resubmit the document's
  existing classification/sensitive/retention values alongside the changed hold
  reference, or those values would be overwritten (see the runbook).
- **Authorization** — Records never reads the Authorization database.
  `AuthorizationGuard` is bound to the same HTTP evaluator
  (`HttpAuthorizationEvaluator`) as every other service, calling the
  Authorization check API as a service principal (`ADR-018/019`).
- **Future consumers** — Audit, Search, Workflow, Notifications, Correspondence
  and Finance subscribe to `CommunityOS.Contracts.Records` events; none cross
  the Records database boundary.

### Integration events (`CommunityOS.Contracts.Records`)

Names and payloads below are the **ratified baseline**, implemented in Prompt
08B. Each record carries a trailing `DateTime OccurredOn`. Only stable ids and
minimal lifecycle metadata are exported — no binary, no secrets, no names, no
sensitive field values, no hold reasons.

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `RecordCreated` | A Draft record is created | `RecordId`, `Category`, `Status`, `SubjectType`, `SubjectId`, `OrganizationUnitId`, `CreatedBy` |
| `RecordSubmitted` | `Draft → Submitted` | `RecordId`, `Status` |
| `RecordUnderReview` | `Submitted → Under Review` | `RecordId`, `ReviewerId` |
| `RecordVerified` | `Under Review → Verified` | `RecordId`, `VerifiedBy` |
| `RecordRejected` | `Under Review → Rejected` | `RecordId`, `RejectedBy` |
| `RecordCorrected` | A post-verification correction is applied | `RecordId`, `VersionNumber`, `SupersedesVersionNumber`, `CorrectedBy` |
| `RecordArchived` | `Verified → Archived` | `RecordId` |
| `RecordDeactivated` | `→ Deactivated` | `RecordId` |
| `RecordRestored` | Restored from `Archived`/`Deactivated` | `RecordId`, `Status` |
| `RecordClassified` | Classification/sensitivity/retention/hold assignment | `RecordId`, `ClassificationCode`, `IsSensitive` |
| `RecordHoldPlaced` | A legal/administrative hold is placed | `HoldId`, `RecordId`, `HoldType`, `PlacedBy` |
| `RecordHoldReleased` | A hold is released | `HoldId`, `RecordId`, `HoldType`, `ReleasedBy` |
| `RecordRetentionChanged` | Retention schedule/period changes | `RecordId`, `RetentionScheduleCode`, `RetentionPeriod` |
| `RecordEvidenceAttached` | A document version is attached as evidence | `RecordId`, `DocumentId`, `VersionNumber`, `ReferenceType` |
| `RecordEvidenceRemoved` | Evidence is removed | `RecordId`, `DocumentId`, `VersionNumber` |
| `RecordRetentionExpired` | Retention expiry is flagged for review (never destroys) | `RecordId`, `RetentionScheduleCode`, `ExpiredOn` |

Deliberately **not exported**: field-level edits (consumers read fields through
the API), category catalog changes, hold reasons (sensitive), person names
(resolved through Community API), and document filenames (resolved through the
Documents API).

**Hold coverage (resolve via API):** `RecordHoldPlaced`/`RecordHoldReleased`
intentionally carry no document references. Consumers that need document-level
hold coverage (e.g. which documents a hold protects) must resolve the hold's
document scope through the Records API (`GET /holds/{id}`) rather than
expecting those references in the integration event. No PII or unnecessary
document metadata is added to the events.

### Event delivery and outbox gate

Publication is best-effort in-process today (ADR-015 outbox deferred). For
Records this is safe **only** for events with no live consumer. The following
consumers create a hard guaranteed-delivery prerequisite:

| Consumer | Guaranteed delivery required | When |
|----------|------------------------------|------|
| Search indexing | No — best-effort; re-index reconciles | always safe |
| Notifications | No — notification loss is tolerable | safe best-effort now |
| **Workflow (verification tasks)** | **Yes** — `RecordSubmitted`/`RecordVerified` must not be lost once Workflow reconciles tasks | before Workflow subscribes |
| **Audit (compliance trail)** | **Yes** — `RecordVerified`, `RecordCorrected`, `RecordHoldPlaced/Released`, `RecordClassified`, `RecordDeactivated/Restored`, `RecordRetentionChanged` must not be lost | required before Audit subscribes (ADR-015 outbox) |
| **Documents (hold/evidence reconciliation)** | **Yes** — Records consuming `DocumentDeactivated`/`DocumentRestored` protects held documents | required before that consumer is enabled |

**Ratified decision:** the transactional outbox (ADR-015; MassTransit EF Core
outbox) is a hard prerequisite for the Documents↔Records hold-reconciliation
consumer and for any Audit/Workflow subscription, and must be implemented and
enabled at the Records integration gate — not deferred past Records. This is
consistent with the ADR-015 amendment (Prompt 08A-R2) and supersedes the later
Correspondence/Audit gate noted in ADR-022, because Records (slot 7) is the
earliest service with a guaranteed-delivery consumer.

The table above is **non-exhaustive by design**: any Records integration event
consumed by a guaranteed-delivery consumer (Audit, Workflow, or any future
consumer) is outbox-protected whether or not its name appears in the table. A
future implementation must never publish a compliance-critical event
best-effort merely because its name is absent from the table.

## Dependency map

```
Community ──┐   (subject refs: persons/households; never owns persons)
Organization ─┼──►  Records  ──►  (event consumers, future)
Authorization ─┘     ▲  │              │
Identity (authN)     │  └── Events ──►  Audit / Search / Workflow /
                     │                   Notifications / Correspondence / Finance
                     │  ◄── consumes OrganizationUnitCreated/Updated/ParentChanged
                     │      (read-model projection, ADR-016)
                     │  ◄── consumes DocumentDeactivated/DocumentRestored
                     │      (hold/evidence reconciliation, guaranteed delivery)
                     └── resolves names via Community API (read time)
```

- **Depends on (existing):** Identity (authentication), Authorization
  (AuthorizationGuard over the check API), Organization (unit events + scope
  references), Community (person/household references resolved at read time),
  Documents (evidence references + hold-protection events).
- **Does not depend on (yet):** Knowledge, Search, Workflow, Notifications,
  Audit, Correspondence, Finance, AI — Records is a leaf service today (its
  consumers are future).
- **Provides for (future):** Workflow (verification tasks), Notifications
  (hold/expiry alerts), Audit (compliance event stream), Search (indexing),
  Correspondence/Finance (record references), AI Platform (non-authoritative
  suggestions only, gated).

## Data

- Database: `communityos_records` (PostgreSQL), schema `records`.
- Tables (as migrated, Prompt 08B): `records`, `record_versions`,
  `record_field_values`, `record_working_fields`, `record_scopes`,
  `record_categories`, `retention_schedules`, `retention_rules`,
  `record_holds`, `record_hold_document_references`,
  `record_evidence_references`, `record_classification`,
  `organization_unit_references`, plus the MassTransit outbox tables
  (`InboxState`, `OutboxMessage`, `OutboxState`).
- Schema is managed by EF Core migrations (created at implementation time).
- **Implementation deviations (Prompt 08B):** the editable working field set is
  persisted in its own `record_working_fields` table (frozen into
  `record_versions` at verification) rather than stored in place on `records`;
  and the ratified `record_lifecycle_events` table is **not** created — lifecycle
  transitions are captured as domain/integration events through the transactional
  outbox instead of a persisted events table. Audit/WF consumers rebuild their
  read models from the outbox event stream.
- Records stores **no binary content**; evidence bytes live in Documents object
  storage and are referenced by id/version.

## Storage

- **Metadata storage** — PostgreSQL (`communityos_records`). Authoritative.
- **No binary storage** — Records never stores, streams or forwards file bytes;
  evidence is referenced by `DocumentId`/`VersionNumber` and served by the
  Documents API.
- **Backup / restore / DR** — PostgreSQL dump/PITR; a reconciliation job
  verifies record↔evidence consistency (a record's evidence references resolve
  to live document versions).

## Retention and legal hold

Retention and hold **semantics belong to Records** (`ADR-023`); Documents
carries references only and honors them for deactivation protection.

- **RetentionSchedule** — named schedule linked to categories; contains
  `RetentionRule`s.
- **RetentionRule / RetentionPeriod** — period + start trigger + disposition.
  Disposition in this prompt is `review` only; no destruction.
- **Expiry flow** — when a retention period lapses, the record is flagged
  `RetentionExpired → Review`; `RecordRetentionExpired` is raised. A ratified
  disposition review process decides archiving; there is no auto-destroy.
- **LegalHold / AdministrativeHold** — active holds freeze disposition and
  block deactivation of the record and of referenced held documents. Release
  requires a subject different from the placer. When a hold targets a document,
  Records writes the hold reference onto the document through the Documents
  classify surface so Documents enforces its own deactivation-protection rule.
- **Relationship to Documents** — Records creates record versions that
  reference specific document versions as evidence; Records owns the retention
  schedule and hold lifecycle; Documents only honors the references
  (`docs/documents.md`).
- **Consumption of `DocumentDeactivated`/`DocumentRestored`** — Records
  reconciles its evidence and hold references when a referenced document is
  deactivated or restored, protecting held/evidence documents and keeping the
  record's evidence set consistent. Requires guaranteed delivery (outbox gate).

## Configuration (ratified)

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `RecordsDb` | `Host=localhost;Port=5432;Database=communityos_records;...` | PostgreSQL connection string |
| `Jwt` | `Issuer` / `Audience` / `MetadataAddress` | *(local)* | Token validation for the API |
| `RabbitMq` | `Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Message bus |
| `AuthorizationService` | `BaseUrl` / `AccessToken` / `ClientId` | `communityos-records` | Authorization check API configuration |
| `DocumentsService` | `BaseUrl` / `AccessToken` / `ClientId` | *(see runbook)* | Documents classify/reference surface for evidence + hold references |
| `Records` | `InternalClientId` | *(reserved)* | Trusted in-process caller (future fact queries) |
| `Records:Retention` | `ReviewDispositionEnabled` | `true` | Expiry flags review instead of destroying |

The Records service never reads the Authorization, Organization, Community or
Documents databases. If `AuthorizationService:BaseUrl` or the presented token is
misconfigured, every guarded endpoint returns `403 Forbidden` (fail-closed).

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Records.Tests`) — domain invariants
  (lifecycle transitions, verified-fact immutability, superseding versions,
  sensitive-field gating, hold-type invariant, retention expiry never
  destroying, ISO-8601 period/maximum-period validation, hold
  protection/deactivation blocking, separation of duties) and security
  regression tests (fail-closed authorization, resource-level record checks,
  org-scoped multi-scope access, sensitive-read gating, no persistence on a
  denied update/verify/deactivate, no PII or hold reasons in integration
  events, baseline category catalog, `DocumentDeactivated`/`DocumentRestored`
  reconciliation, JWT RS256-only validation) through the MediatR pipeline with
  substitute persistence.
- **Integration tests** (`tests/Integration/CommunityOS.Records.IntegrationTests`)
  — EF mapping, both migrations, retention-rule maximum-period roundtrip, and
  DocumentDeactivated/DocumentRestored reconciliation against PostgreSQL via
  Testcontainers (requires Docker; the outbox gate must be in place before these
  tests are enabled).

## Deviations

The documented-but-unenforced examples in `PermissionCatalog.DocumentedExamples`
(`records.record.read/create/verify`) are superseded by the ratified matrix and
were registered in the Authorization permission catalog at the Prompt 08B gate.
Two ratified implementation choices (Prompt 08B) are recorded in the Data
section above and are binding unless a later ratified ADR amends them: the
editable working field set is persisted in `record_working_fields` rather than
in place on `records`, and the ratified `record_lifecycle_events` table is not
created — lifecycle transitions are captured as domain/integration events
through the transactional outbox instead of a persisted events table.