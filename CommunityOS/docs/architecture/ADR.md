# Architecture Decision Records

This directory contains ADRs for CommunityOS.

## Index

| ID      | Title                                     | Status   |
|---------|-------------------------------------------|----------|
| ADR-001 | Modular Monolith as initial deployment    | Accepted |
| ADR-002 | Clean Architecture per bounded context    | Accepted |
| ADR-003 | MediatR for in-process CQRS              | Accepted |
| ADR-004 | Transport-independent event bus (MassTransit + RabbitMQ; NATS/Kafka future) | Accepted |
| ADR-005 | Flutter for all client applications       | Accepted |
| ADR-006 | PostgreSQL as primary data store          | Accepted |
| ADR-007 | Central Package Management (CPM)          | Accepted |
| ADR-008 | Testcontainers for integration tests      | Accepted |
| ADR-009 | Authorization as a bounded context        | Accepted |
| ADR-010 | Fail-closed permission evaluation         | Accepted |
| ADR-011 | Scoped grants with exact-match fallback   | Accepted |
| ADR-012 | Relationship tuples for per-object access | Accepted |
| ADR-013 | Delegation constrained to held authority  | Accepted |
| ADR-014 | Break-glass requires approval and expiry  | Accepted |
| ADR-015 | Reliable event publication via transactional outbox | Accepted |
| ADR-016 | Organization bounded context and Community boundary | Accepted |
| ADR-017 | Bounded context implementation sequence | Accepted |
| ADR-018 | Authentication and authorization ownership boundary | Accepted |
| ADR-019 | Community security corrections | Accepted |
| ADR-020 | Repository specifications as source of truth | Accepted |
| ADR-021 | Knowledge bounded context and Library boundary | Accepted |
| ADR-022 | Documents bounded context and artifact boundary | Accepted |
| ADR-023 | Records bounded context and official-record boundary | Accepted |
| ADR-024 | Workflow bounded context and task boundary | Accepted |

## ADR-004 — Transport-independent event bus (MassTransit + RabbitMQ; NATS/Kafka future)

**Status:** Accepted (ratified at architecture reconciliation).

CommunityOS architecture is transport-independent. Domain and integration
contracts are transport-agnostic and must never depend on a specific broker,
queueing topology or serialization technology.

- **Current implementation:** MassTransit with the RabbitMQ transport
  (`CommunityOS.EventBus`). RabbitMQ remains the implementation transport for
  the current development phase.
- **NATS** is not required to be introduced immediately. It remains an
  evaluated future transport option.
- **Kafka** remains a future option for high-scale and event-analytics
  requirements.
- Transport selection must remain isolated to the event-bus registration and
  configuration; changing transport must not require changes to contracts or
  handlers.

This supersedes the earlier "MassTransit + RabbitMQ for async events" decision.

## ADR-009 — Authorization as a bounded context

**Status:** Accepted

Authorization is its own bounded context (`CommunityOS.Authorization.*`) owning
roles, assignments, scopes, delegations, break-glass and relationship tuples.
No other service embeds authorization decisions; they call the Authorization
service's API (or, in-process, the evaluator). Clients only ever supply context
(organization unit, resource, attributes) — never roles, permissions or
decisions.

## ADR-010 — Fail-closed permission evaluation

**Status:** Accepted

The evaluator is fail-closed: any subject, permission, scope or data state that
cannot be safely established results in a Deny. Missing subjects, invalid
permission names, dangling role references, expired or revoked grants, and
unknown hierarchy relationships all deny. There is no allow-by-default path.

## ADR-011 — Scoped grants with exact-match fallback

**Status:** Accepted

Every grant carries an `AuthorizationScope` (`Global`, `National`, `Regional`,
`Local`, `OrganizationUnit`, `Committee`, `Resource`). A global grant of an
administration permission (`authz.*`) is authority at every scope; a global
grant of a data permission never grants resource access. Organizational
hierarchy awareness comes exclusively from the Organization service through
`IOrganizationContextProvider`, which defaults to exact-match only and fails
closed until that integration exists.

## ADR-012 — Relationship tuples for per-object access

**Status:** Accepted

Relationship-based access uses persisted tuples (`subject`, `relation`,
`objectType`, `objectId`, optional permissions). Tuples are read-through on
every check (no caching) so revocation is immediately effective. Permission
grants must match the requested resource exactly.

## ADR-013 — Delegation constrained to held authority

**Status:** Accepted

Delegations are always scoped, time-limited, revocable and audited. A delegator
must hold `authz.delegation.grant` and each delegated permission at the
delegation's scope (privilege-escalation protection). Self-delegation and
global delegations are forbidden.

## ADR-014 — Break-glass requires approval and expiry

**Status:** Accepted

Emergency access is an explicit, reason-required, narrowly-scoped request that
must be approved by a different subject, cannot be global, and automatically
expires. Break-glass grants never silently bypass normal authorization and emit
high-priority audit events.

## ADR-015 — Reliable event publication via transactional outbox

**Status:** Accepted (documented decision; implementation deferred).

CommunityOS requires reliable event publication. Any domain change that raises
domain/integration events must not lose those events if the message broker is
unavailable or the publish fails. The intended architecture:

```
Database transaction
        ↓
Domain state + Outbox message
        ↓
Commit
        ↓
Outbox dispatcher
        ↓
MassTransit
        ↓
RabbitMQ
```

Domain state and the outbox message commit in the same database transaction; a
dispatcher reads committed outbox rows and publishes them onto the bus via
MassTransit. Implementation is deferred until the first cross-service consumers
require guaranteed delivery; the MassTransit EF Core outbox is the intended
mechanism. Until then, publishers remain best-effort in-process delivery, and
this limitation is tracked for removal.

**Amendment (Prompt 08A-R2):** the first cross-service consumer requiring
guaranteed delivery has been identified. Records (ADR-017 slot 7) consumes
`DocumentDeactivated`/`DocumentRestored` to reconcile the protection of held
and evidence documents, and its compliance-critical events (`RecordVerified`,
`RecordCorrected`, `RecordHoldPlaced/Released`, `RecordClassified`,
`RecordDeactivated/Restored`, `RecordRetentionChanged`, and any event consumed
by Audit or Workflow) require reliable delivery. The outbox implementation gate
is therefore pulled forward to the **Records integration gate** — before
Records begins consuming `DocumentDeactivated`/`DocumentRestored`, and before
any Audit/Workflow subscription. The resulting rule is unambiguous:

- Best-effort integration events remain acceptable where there is **no**
  guaranteed-delivery consumer.
- The transactional outbox is mandatory **before Records begins consuming**
  `DocumentDeactivated`/`DocumentRestored`.
- The outbox must also protect Records events that require guaranteed delivery
  to Audit, Workflow and any future consumer.
- This amendment does **not** implement the outbox; implementation is scheduled
  at the Records integration gate (Prompt 08B). This supersedes the earlier
  "Correspondence/Audit gate" phrasing in ADR-022 and `docs/documents.md`,
  which are amended below.

## ADR-016 — Organization bounded context and Community boundary

**Status:** Accepted (ratified at architecture reconciliation).

The Organization bounded context owns:

- organizations
- organization units
- organizational hierarchy
- institutions
- committees
- teams
- appointments
- appointment terms
- jurisdiction
- organizational delegation facts
- effective-dated organizational history

The Community bounded context owns:

- persons
- households
- family relationships
- contact information
- community membership
- activities
- events
- meetings
- participation

The existing Community organizational hierarchy (National / Regional / Cluster /
Local Unit) is considered premature implementation. It is not deleted; it will
be relocated and refactored as part of the Organization implementation
(Prompt 04).

Organization will own its own database, `communityos_organization`. There is no
shared database with Community, Identity or Authorization.

## ADR-017 — Bounded context implementation sequence

**Status:** Accepted (ratified at architecture reconciliation).

The ratified implementation sequence for CommunityOS is:

1. Solution Foundation
2. Identity
3. Authorization
4. Organization
5. Community
6. Documents
7. Records
8. Workflow
9. Notifications
10. Search
11. Audit
12. Knowledge
13. Correspondence
14. Localization
15. AI Platform
16. Integrations
17. Finance
18. Communications / VoIP
19. Analytics

Implementation-sequence status:

- Slots 1–7 (Foundation, Identity, Authorization, Organization, Community,
  Documents, Records) are **implemented**; Records (slot 7) completed the full
  08A→08E gate with the transactional outbox enabled (ADR-015, Prompt 08A-R2).
- Slot 8 (**Workflow**) is **implemented** (ADR-024, Prompt 09C gate); the
  integration test suite is compile-only because Docker/Testcontainers is not
  available in the implementation environment.
- Slot 12 (**Knowledge**) was implemented early, out of sequence (ADR-021).
- Slots 9–11 and 13–19 are not started.

## ADR-018 — Authentication and authorization ownership boundary

**Status:** Accepted (ratified at architecture reconciliation; extends ADR-009).

The Identity bounded context owns authentication: accounts, credentials, MFA,
sessions, devices, and recovery.

The Authorization bounded context owns authorization: roles, permissions,
policies, relationship tuples, authorization decisions, authorization
delegation, and break-glass authorization.

The Organization bounded context owns organizational facts. The Community
bounded context owns person/community facts.

No service may access another service's database. All cross-service data access
is via the owning service's API or integration events.

## ADR-019 — Community security corrections

**Status:** Accepted (recorded; implementation deferred to the Community
re-scope).

The following Community service issues are recorded for correction during the
Community re-scope:

- HMAC JWT validation must be replaced by validation of Identity-issued RS256
  tokens using the appropriate public key / JWKS mechanism.
- `[Authorize(Roles = "...")]` must not be used for Community authorization
  decisions.
- Community must use the existing Authorization service/guard mechanism for
  authorization decisions.

These corrections are not implemented during the reconciliation phase; they are
recorded to prevent regression.

## ADR-020 — Repository specifications as source of truth

**Status:** Accepted (ratified at architecture reconciliation).

The `/specifications` directory in this repository is the authoritative
architectural baseline for CommunityOS implementation work. Files marked
PLACEHOLDER are pending import of their authoritative source documents; they do
not invent requirements and must be replaced before being relied upon.
Ratified decisions are recorded in this ADR document.

## ADR-021 — Knowledge bounded context and Library boundary

**Status:** Accepted (ratified at architecture reconciliation; positions the
Knowledge service in the ADR-017 sequence).

The Knowledge bounded context owns:

- the Library: authoritative source material (Works, Editions, Passages) with
  provenance, verification status and multilingual translations
- community questions (a lifecycle: Draft → Submitted → Published → Under
  Review → Merged / Canonicalized / Archived)
- answers and structured discussions attached to questions
- references and citations from community content to Library passages
- categories / topics / tags used to organize questions and answers
- moderation flags and review state
- AI-assist boundaries: AI-generated suggestions are first-class, clearly
  marked, non-authoritative artifacts that require human review before
  publication (see ADR-021 boundary rules below)

The Knowledge service is positioned in the implementation sequence (ADR-017)
after Search and before Correspondence. It depends on the platform services
Authorization, Search, AI, Documents, Workflow and Notifications; it never owns
persons, organization units, search indices, AI model behavior or notification
delivery — it only references them through APIs and integration events.

The following boundary rules are mandatory:

- Authoritative religious text must never be produced, edited or arbitrated by
  AI. AI-generated content is always a *suggestion* artifact with a distinct
  provenance, never a Library entry and never the canonical answer.
- The Library is the single source of truth for citation text. Community
  answers and discussions cite Library passages by stable passage id; they never
  embed authoritative text as their own content.
- Knowledge owns its own database, `communityos_knowledge`. There is no shared
  database with Community, Organization, Identity, Authorization or AI.
- All cross-service access (person authors, organization-unit scoping, AI
  suggestions, search indexing, notifications, workflow reviews) is via the
  owning service's API or integration events (`ADR-018`).

## ADR-022 — Documents bounded context and artifact boundary

**Status:** Accepted (ratified at the Prompt 07A-R gate; supersedes the Proposed
baseline recorded at the Prompt 07A gate).

The Documents bounded context owns document *artifacts* and their metadata. It
is positioned in the implementation sequence (ADR-017) at slot 6 — after
Community, before Records — and is the foundational artifact service for
Records, Workflow, Correspondence, Finance and Administration/Ticketing.

The Documents context owns:

- **Document** — the aggregate root: a named, versioned sequence of immutable
  binary-content snapshots plus the metadata that governs security,
  classification, scope, retention and lifecycle.
- **DocumentVersion** — an immutable content snapshot (content hash, object key,
  MIME type, size, filename, uploader, timestamp, source). Versions are
  append-only; a new upload creates a new version and moves the current-version
  pointer; no version is ever mutated (scan status is the sole mutable field).
- **DocumentClassificationMetadata** — security metadata: classification code
  (reserved for the ratified classification model), sensitive flag, retention
  category reference, and legal/administrative hold references.
- **DocumentOrganizationScope** — one or more organization-unit scopes; access
  is delegated to Authorization at any of the document's scopes.
- **DocumentReference** — the attachment/evidence link used by other contexts
  (Workflow tasks, Records, Correspondence, Finance, Tickets) to reference a
  document without owning storage.
- The **Organization read-model projection** (consumed unit events, mirroring
  Community and Knowledge; ADR-016).

The Documents context does **not** own: official records, workflow state,
correspondence lifecycle, financial meaning, organizational hierarchy, member
identity, authorization policy, notification delivery, or AI decisions. It
never absorbs responsibilities belonging to Records, Workflow, Correspondence,
Knowledge, Finance or Administration.

Boundary rules (mandatory):

- A document artifact is never an Official Record; Records references documents
  as evidence and owns record lifecycle, retention schedules and holds.
- A document is never a Workflow task; Workflow references documents as
  artifacts under review.
- A document is never a Correspondence letter lifecycle; Correspondence
  materializes its submitted letters as immutable document versions and owns
  the draft/confirm/submit/track lifecycle.
- A document is never Knowledge Library content; Knowledge remains the
  authority for passages and citation text and may reference source documents.
- Documents carries retention/hold *references* only; it enforces a
  deactivation-protection rule when a hold is present but never invents
  retention schedules.
- Documents never holds roles, permissions or decisions; every guarded
  operation calls the Authorization service through `AuthorizationGuard`
  (fail-closed; ADR-009/010/011/018/019). No `[Authorize(Roles = "...")]`.

Storage topology (Accepted): PostgreSQL holds all authoritative metadata;
binary content lives in S3-compatible object storage behind an
`IDocumentObjectStorage` abstraction. MinIO is the concrete self-hosted
deployment; any S3-compatible provider (AWS S3, Cloudflare R2, etc.) is a
configuration change. The concrete S3-compatible client is **AWSSDK.S3**;
application and domain layers depend only on `IDocumentObjectStorage`, never
on the SDK. Object keys are content-addressed by SHA-256
(`documents/{sha256}`), which supports integrity verification and optional
deduplication. Metadata storage and binary content storage are therefore
decoupled: the database is the authority for the version → object mapping, and
the object store is treated as immutable, addressable content. Content
integrity is verified on upload and on download where practical.

Data: Documents owns its own database, `communityos_documents` (schema
`documents`). There is no shared database with any other service (ADR-018).

Integration: domain events are forwarded as integration events onto the bus via
MassTransit under `CommunityOS.Contracts.Documents`; payloads carry identifiers
and minimal metadata only — never binary content, never secrets, never
filenames, never names. Documents consumes Organization unit events into its
read-model projection. ADR-015 (transactional outbox) remains deferred: no
Documents event requires guaranteed delivery at implementation time (no live
consumers), but guaranteed delivery becomes mandatory for the compliance-
critical subset (classification/deactivation/restore/sensitive-download/scan
events) once Audit, Records and Correspondence subscribe. Per the ADR-015
amendment (Prompt 08A-R2), the outbox therefore must land at the **Records
integration gate** — the earliest guaranteed-delivery consumer — not before
Documents.

Ratified decisions (Prompt 07A-R):

1. **Storage topology** — PostgreSQL metadata + S3-compatible object storage;
   MinIO initial deployment; `IDocumentObjectStorage` abstraction; portable to
   other S3-compatible providers.
2. **Content addressing** — SHA-256 content hashes; content-addressed object
   keys `documents/{sha256}`; integrity verified on upload and on download
   where practical.
3. **Versioning** — `DocumentVersion` is immutable and append-only; a new
   upload creates a new version and moves the current-version pointer; metadata
   changes do not create versions.
4. **Deletion** — no hard delete through normal application operations;
   deactivation preserves content and is reversible; legal/administrative
   holds prevent deactivation unless an explicitly authorized administrative
   override is used.
5. **Classification** — no classification levels are invented; a reserved
   `ClassificationCode` string plus an `IsSensitive` operational gate are used
   until the authoritative Data Classification Model is ratified.
6. **Authorization** — the `documents.*` permission matrix is adopted;
   authorization remains centralized in the Authorization service via
   `AuthorizationGuard`, fail-closed; no local RBAC, no direct Authorization
   database access; resource-level authorization is mandatory
   (`resourceType = "document"`); unauthorized enumeration is prevented with
   equivalent not-found behavior for missing and unauthorized resources.
7. **Organization scoping** — multi-scope document organization references and
   the Organization read-model approach of ADR-016 are adopted.
8. **`DocumentContentDownloaded`** — adopted. Documents owns the fact that
   protected (sensitive) document content was downloaded; the event targets
   security/audit consumers; ordinary metadata reads are never published; the
   payload carries no binary content, secrets or unnecessary personal
   information. A future Audit service may consume it; Documents does not
   depend directly on Audit.
9. **Malware scanning** — `IDocumentScanService` extension point with a no-op
   development implementation; replaceable by a real scanner later. A version
   may exist in `NotScanned` state while its document becomes `Active`;
   download policy fails closed on non-clean versions when scanning is
   enabled.
10. **Event delivery** — the existing open-generic integration-event publisher
    pattern is used; the transactional outbox is not implemented (ADR-015
    remains deferred); guaranteed delivery is documented as a requirement for
    Audit, Records and Correspondence before those consumers are introduced,
    with the outbox gate pulled forward to the Records integration gate
    (ADR-015 amendment, Prompt 08A-R2).
11. **Object-storage SDK** — **AWSSDK.S3** is the concrete S3-compatible client;
    MinIO is the initial deployment target; application/domain layers depend
    only on `IDocumentObjectStorage`.
12. **Future Correspondence** — Documents stores document artifacts and
    immutable versions; it does **not** own correspondence lifecycle,
    recipient selection, institutional routing, submission workflow, AI letter
    drafting, translation workflow or institutional responses.
13. **AI** — Documents is not an AI service. AI assistance may provide
    non-authoritative suggestions for classification, metadata extraction, OCR,
    translation, summarization and discovery; AI must never independently
    modify, publish, delete, submit or authorize a document; binary document
    content is never sent directly to AI.

Superseded decisions: none. The prior "Open decisions pending ratification"
list (permission matrix, sensitivity gating, download-audit event, retention/
hold hooks and deactivation-protection rule, malware-scanning extension point)
is resolved by items 1–13 above.

## ADR-023 — Records bounded context and official-record boundary

**Status:** Accepted (ratified at the Prompt 08A-R gate).

The Records bounded context owns **official records** and their lifecycle. It
is positioned in the implementation sequence (`ADR-017`) at slot 7 — after
Documents, before Workflow. It is the authoritative service for the community's
record-of-truth: the official record of births, marriages, deaths, membership,
appointments, community facts and administrative facts, together with the
verification, versioning, retention, hold and evidence semantics that make a
record authoritative and durable.

The Records context owns:

- **Record** — the aggregate root: an official record with a category, subject
  references (person, household, organization unit, or an external/other
  subject), organization scopes, classification metadata, a current-version
  pointer, a lifecycle status, evidence references to documents, and hold
  references.
- **RecordVersion** — an immutable snapshot of the authoritative record facts.
  Facts are never mutated in place once a record is Verified; a correction
  appends a new superseding version and moves the current-version pointer.
- **RecordCategory** — the category catalog (birth, marriage, death, membership,
  appointment, official community, administrative) as stable string codes that
  remain open for the ratified classification model — never a fixed enum.
- **RetentionSchedule / RetentionRule / RetentionPeriod** — retention policy
  owned by Records: a schedule per category, rules expressing the retention
  period and its start trigger, and disposition policy. Retention expiry raises
  a review flag; it never hard-destroys.
- **RecordHold (legal / administrative)** — the hold lifecycle owned by Records.
  An active hold freezes disposition and protects both the record and referenced
  documents (through the Documents hold-reference hook).
- **RecordEvidenceReference** — the Records-side link from a record to specific
  document versions used as evidence (mirror of the Documents `DocumentReference`
  with `SourceContext = records.record`).
- The **Organization read-model projection** (consumed unit events, mirroring
  Community, Knowledge and Documents; ADR-016).

The Records context does **not** own: document artifacts, person identity or
households, organizational hierarchy, workflow/task state, notification
delivery, AI decisions, correspondence lifecycle, Knowledge/Library content, or
financial meaning. It never stores person data (names, contact details); subject
references are stable ids and names are resolved through the Community API at
read time. It never reads another service's database (`ADR-018`).

Boundary rules (mandatory):

- **A record is never a document.** Records references documents as evidence and
  owns record lifecycle, retention schedules and holds; Documents owns the
  artifacts and honors retention/hold *references* only (`ADR-022`). Documents
  never invents retention or legal policy; Records never stores or serves binary
  content.
- **A record is never a Workflow task.** Workflow (future) may drive
  verification/review assignments, but task state stays in Workflow and record
  authority stays in Records.
- **A record is never a Correspondence letter.** Correspondence owns its
  draft/confirm/submit/track lifecycle; Records may reference letters only as
  evidence.
- **Verified facts are immutable.** After `Verified`, corrections append a
  superseding `RecordVersion`; history is preserved. No in-place mutation of
  authoritative facts.
- **No irreversible destruction** through normal application operations.
  Deactivation is a soft-delete that preserves data; retention expiry never
  destroys; disposition is review-flagged and requires a ratified override
  process.
- **Holds protect.** An active legal/administrative hold blocks deactivation of
  the record and blocks deactivation of referenced held documents (Documents
  honors the hold reference and its own deactivation-protection rule).
- **Separation of duties.** The subject who creates a record cannot be the
  subject who verifies it; the subject who places a hold cannot be the subject
  who releases it; deactivation of a held record requires an explicitly
  authorized administrative override (`records.record.admin`) with a reason.
- **Centralized authorization.** Every guarded operation calls the Authorization
  service through `AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019).
  No `[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization
  database access. Resource-level authorization is mandatory
  (`resourceType = "record"`); unauthorized enumeration is prevented with
  equivalent not-found behavior for missing and unauthorized resources.
- **Organization scoping follows ADR-016.** Records consumes
  `OrganizationUnitCreated/Updated/ParentChanged` into a read-model projection;
  a record may carry a primary scope plus additional scopes; access succeeds when
  the caller holds the permission at any of the record's scopes.
- **AI is advisory only.** AI never verifies, corrects, rejects, holds, changes
  retention or authorizes disposition of a record. Any AI assist is a clearly
  marked, non-authoritative suggestion with provenance that requires human
  review (mirroring the Knowledge `AiSuggestion` pattern).

Storage topology (Accepted): PostgreSQL holds all authoritative record data.
Database `communityos_records`, schema `records`. There is no shared database
with any other service (ADR-018).

Integration: domain events are forwarded as integration events onto the bus via
MassTransit under `CommunityOS.Contracts.Records`; payloads carry identifiers
and minimal lifecycle metadata only — never binary, never secrets, never names,
never sensitive field values, never hold reasons. Records consumes Organization
unit events (projection) and `DocumentDeactivated` / `DocumentRestored` for
hold/evidence reconciliation. Records writes hold references onto documents
through the Documents classify API (command), never by database.

**Outbox gate (ratified; consistent with the ADR-015 amendment, Prompt
08A-R2).** ADR-015 (transactional outbox) remains deferred for services with
no cross-service consumer requiring guaranteed delivery. Records creates the
earliest mandatory gate:

1. Records consumes `DocumentDeactivated` / `DocumentRestored` to reconcile the
   protection of held/evidence documents — guaranteed delivery required before
   that consumer is enabled (`docs/documents.md`).
2. Audit (slot 11) will subscribe to Records compliance events
   (`RecordVerified`, `RecordCorrected`, `RecordHoldPlaced/Released`,
   `RecordClassified`, `RecordDeactivated/Restored`, `RecordRetentionChanged`).
3. Workflow (slot 8) will rely on `RecordSubmitted` / `RecordVerified` for
   verification-task reconciliation.

Therefore the transactional outbox (MassTransit EF Core outbox) is a hard
prerequisite for the Documents↔Records hold-reconciliation consumer and for any
Audit/Workflow subscription, and must be implemented and enabled at the Records
integration gate — **not** deferred past Records to the Correspondence/Audit
gate. Best-effort in-process publication remains acceptable only for Records
events with no live consumer during initial operation.

The guaranteed-delivery rule is **non-exhaustive by design**: any Records
integration event consumed by a guaranteed-delivery consumer (Audit, Workflow,
or any future consumer) is outbox-protected whether or not its name appears in
a delivery table. A future implementation must never publish a
compliance-critical event best-effort merely because its name is absent from
the table.

Ratified decisions (Prompt 08A-R):

1. **Ownership** — Records owns official records, lifecycle, verification,
   retention schedules/rules/periods, the hold lifecycle, evidence references,
   corrections and supersession.
2. **Taxonomy** — the category catalog uses stable string codes
   (`birth`, `marriage`, `death`, `membership`, `appointment`,
   `official-community`, `administrative`); it is configuration, never a hard
   enum, and remains open to the ratified classification model.
3. **Lifecycle** — `Draft → Submitted → Under Review → Verified → Archived |
   Deactivated`, plus `Rejected` (from Under Review). `Verified` is the
   authoritative state; all transitions are guarded and audited. Non-
   authoritative fields remain editable in `Draft`, `Submitted` and `Under
   Review`; from `Verified` onward field changes require the `correct`
   operation (new superseding version), never in-place mutation.
4. **Versioning** — `RecordVersion` is immutable once written; corrections after
   verification append a superseding version and move the current-version
   pointer; evidence references pin specific document versions.
5. **Retention** — retention schedules, rules and periods are owned by Records;
   expiry flags a review disposition and never auto-destroys; there is no
   irreversible destruction through normal operations.
6. **Holds** — the legal/administrative hold lifecycle is owned by Records; an
   active hold freezes disposition and blocks deactivation of the record and of
   referenced held documents; release requires a subject different from the
   placer.
7. **Document integration** — Records↔Documents is bidirectional: Records
   attaches evidence through `DocumentReference`/`RecordEvidenceReference`, and
   consumes `DocumentDeactivated`/`DocumentRestored` for protection
   reconciliation; guaranteed delivery is required (see outbox gate).
8. **Authorization** — the `records.*` permission matrix (below) is adopted;
   centralized and fail-closed; resource-level (`resourceType = "record"`);
   separation of duties is enforced by the application layer and audited.
9. **Organization scoping** — ADR-016 read-model projection and multi-scope
   records are adopted; person/household records may also be resource-scoped via
   Authorization relationship tuples.
10. **AI** — advisory-only boundary; AI is never a subject and never has
    authority over verification, correction, holds, retention or disposition.
11. **Events** — the event catalog below is adopted; compliance-critical events
    require guaranteed delivery before Audit/Workflow subscribe.
12. **Outbox** — ADR-015 must be implemented and enabled at the Records
    integration gate (see outbox gate), consistent with the ADR-015 amendment
    (Prompt 08A-R2) and superseding the later gate noted in ADR-022.
13. **Data** — own database `communityos_records`, schema `records`; no shared
    database (ADR-018).
14. **Audit** — Records is a compliance-critical context; lifecycle,
    correction, classification, hold, retention and sensitive-read actions emit
    audit-targeted events and never leak PII, secrets, filenames or hold
    reasons into events or logs.

### Records permission matrix (ratified)

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
grant at a national scope covers descendant units via the Organization `/covers`
hierarchy resolution. A global grant of a data permission only applies to checks
with no organization context. Per-record access composes through Authorization
relationship tuples; Records never evaluates grants itself. `records.record.ai.review`
is reserved for the future AI-assist feature and is not part of the core matrix
(AI Platform, slot 15).

### Records integration events (`CommunityOS.Contracts.Records`)

Names and payloads below are the ratified baseline; each record carries a
trailing `DateTime OccurredOn`. Only stable ids and minimal lifecycle metadata
are exported — no binary, no secrets, no names, no sensitive field values, no
hold reasons.

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
hold coverage must resolve the hold's document scope through the Records API
(`GET /holds/{id}`) rather than expecting those references in the integration
event. No PII or unnecessary document metadata is added to the events.

Superseded decisions: none. Records has no prior Proposed baseline; the
documented-but-unenforced examples in `PermissionCatalog.DocumentedExamples`
(`records.record.read/create/verify`) are superseded by the ratified matrix
above and will be registered in the Authorization permission catalog at the
Prompt 08B implementation gate.

## ADR-024 — Workflow bounded context and task boundary

**Status:** Accepted (ratified at the Prompt 09B gate; implemented at the
Prompt 09C gate).

The Workflow bounded context owns **task/work-item state** and the task engine
that routes, assigns, tracks and escalates human review and approval work across
the platform. It is positioned in the implementation sequence (`ADR-017`) at
slot 8 — after Records, before Notifications. It is the routing service for
review/verification work over domain facts owned by Records, Documents and
Knowledge, and is the authoritative source of task state for Notifications,
Search and Audit consumers.

The Workflow context owns:

- **TaskDefinition** — the catalog of task types/templates (stable string codes,
  never hard enums): display name, description, the domain the task works over
  (record / document / knowledge-question / knowledge-ai-suggestion / general),
  permitted outcome codes, deadline/SLA policy, active/retired flag, and audit
  provenance. Definitions are configuration, seeded idempotently and
  extensible at runtime.
- **WorkflowTask** — the aggregate root: a task instance referencing a domain
  entity (stable ids only) or free-standing work, with a lifecycle status, an
  originator, one or more assignees, organization scopes, deadline/overdue
  state, escalation state, outcome, and append-only activity history.
- **TaskAssignment** — the assignment model (assignee, assigner, effective
  time). Reassignment and escalation are explicit, audited transitions.
- **TaskActivity** — an append-only history of every transition and note on a
  task (actor, action, timestamp, outcome). Never exported onto the bus.
- The **Organization read-model projection** (consumed unit events, mirroring
  Community, Knowledge, Documents and Records; ADR-016).

The Workflow context does **not** own: official records or record facts,
document artifacts or document metadata, Knowledge/Library content or
moderation authority, person identity or households, organizational hierarchy,
notification delivery, search indexes, audit storage, or AI decisions. It never
stores binary content, never stores person names/contact data, and never
reaches into another service's database (`ADR-018`).

Boundary rules (mandatory):

- **Task state is not domain state.** A task never replaces the authoritative
  lifecycle of a record, document, question or answer. Completing, rejecting or
  cancelling a task never changes the underlying domain entity; it only records
  the outcome of human work against that entity.
- **Workflow is a routing/reconciliation service, not a command path into other
  contexts.** Workflow consumes domain events (`RecordSubmitted`/`RecordVerified`,
  Knowledge review events) to create and reconcile tasks; it never invokes the
  Records, Documents or Knowledge mutation APIs. The domain transition (e.g.
  verify a record, publish a question) is always performed through the owning
  service by a human with the owning service's permission.
- **No cross-service database access** (`ADR-018`). All subject/domain references
  are stable ids; names and details are resolved through the owning service's
  API at read time (Community for persons, Documents for documents, Records for
  records, Knowledge for questions/suggestions).
- **Centralized authorization.** Every guarded operation calls the Authorization
  service through `AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019).
  No `[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization
  database access. Resource-level authorization is mandatory
  (`resourceType = "workflow.task"`); unauthorized enumeration is prevented with
  equivalent not-found behavior for missing and unauthorized resources.
- **Organization scoping follows ADR-016.** Workflow consumes
  `OrganizationUnitCreated/Updated/ParentChanged` into a read-model projection; a
  task carries a primary scope plus additional scopes; access succeeds when the
  caller holds the permission at **any** of the task's scopes (Records pattern).
- **AI is advisory only.** Workflow never grants AI authority; an AI suggestion
  is reviewed through a task, but accepting/rejecting the suggestion is a
  Knowledge-side governance action performed by a human (ADR-021).

Storage topology (Accepted): PostgreSQL holds all authoritative task data.
Database `communityos_workflow`, schema `workflow`. There is no shared database
with any other service (ADR-018).

Integration: domain events are forwarded as integration events onto the bus via
MassTransit under `CommunityOS.Contracts.Workflow`; payloads carry identifiers
and minimal lifecycle metadata only — never task notes, never names, never
filenames, never secrets, never sensitive domain values. Workflow consumes
Organization unit events (projection), Records events
(`RecordSubmitted`/`RecordVerified`/`RecordRejected`), and Knowledge review
events (`QuestionFlagged`/`QuestionUnderReview`/`QuestionMerged`/
`QuestionArchived`/`AiSuggestionRequested`/`AiSuggestionReviewed`).

**Outbox gate (ratified).** The transactional outbox (ADR-015, MassTransit EF
Core outbox) is enabled at the **Workflow integration gate** — the same gate
Records established (Prompt 08A-R2). This is required because:

1. Workflow is itself a guaranteed-delivery **consumer** of Records events
   (`RecordSubmitted`/`RecordVerified`), which the Records outbox already
   protects; Workflow must consume with receive-endpoint outbox semantics to
   reconcile exactly-once.
2. Workflow's compliance-significant events (`WorkflowTaskCreated`,
   `WorkflowTaskAssigned`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled`,
   `WorkflowTaskEscalated`) will be consumed by Audit (slot 11) and are intended
   for Notifications (slot 9) and Search (slot 10); they must be protected
   before those consumers subscribe.
3. Best-effort in-process publication is **not** acceptable for any Workflow
   integration event at implementation time, because the earliest consumer
   (Notifications, slot 9) already requires reliable delivery for task-routing
   digests.

The outbox-protected set is **non-exhaustive by design**: any Workflow event
consumed by a guaranteed-delivery consumer is protected whether or not its name
appears in a delivery table.

Ratified decisions (Prompt 09B):

1. **Ownership** — Workflow owns the task engine: definitions, instances,
   assignment, lifecycle, escalation, overdue tracking, outcome and activity
   history.
2. **Taxonomy** — typed task engine over a stable-string task-definition
   catalog (`record-review`, `document-review`, `knowledge-moderation`,
   `knowledge-ai-review`, `general` baseline). Task types are configuration,
   never a hard enum; the baseline is seeded idempotently.
3. **Lifecycle** — `Created → Assigned → In Progress → Completed | Cancelled`,
   with explicit `Reassign`, `Escalate`, `Cancel` and (definition-gated)
   `Reject`/outcome transitions. `Completed` and `Cancelled` are terminal;
   completed tasks are immutable. No reopen/correction of a completed task;
   a new task is created if a domain entity re-enters review.
4. **Idempotency** — task creation is idempotent per (definition, domain
   entity) via a uniqueness constraint; reconcile consumers are
   create-if-absent / close-if-open; duplicate events never create duplicate
   open tasks.
5. **Deadline/overdue** — tasks carry an optional `DueOn`; overdue is a derived,
   review-flagged state (never auto-destroys, never auto-escalates without an
   explicit ratified policy per definition). Escalation is explicit.
6. **Records boundary** — Workflow consumes `RecordSubmitted` (create a
   `record-review` task) and `RecordVerified`/`RecordRejected` (complete/close
   the open task with outcome `verified`/`rejected`). Records owns verification
   truth; Workflow never verifies or mutates record facts.
7. **Documents boundary** — Workflow references documents only through the
   existing `DocumentReference` mechanism (`SourceContext = "workflow.task"`);
   it never owns document bytes/metadata and needs **no additional Documents API
   operations**.
8. **Knowledge boundary** — Workflow consumes `QuestionFlagged`/
   `QuestionUnderReview` (create/reconcile `knowledge-moderation` tasks) and
   `AiSuggestionRequested` (create `knowledge-ai-review` tasks); outcomes are
   advisory and never authorize or complete Knowledge governance actions.
9. **Community/Organization** — stable person/org-unit ids only; assignee names
   resolved via the Community API at read time; scoping through the ADR-016
   read-model projection; multi-scope any-of-grant (Records pattern).
10. **Authorization** — the `workflow.*` permission matrix below is adopted;
    centralized and fail-closed; resource-level (`resourceType = "workflow.task"`);
    404-equivalent for unauthorized reads.
11. **Events** — the event catalog below is adopted; all exported Workflow
    events are outbox-protected at the Workflow integration gate.
12. **Outbox** — ADR-015 is implemented and enabled at the Workflow integration
    gate (mirroring the Records gate).
13. **Data** — own database `communityos_workflow`, schema `workflow`; no shared
    database (ADR-018).
14. **Audit readiness** — compliance-significant Workflow events
    (`WorkflowTaskCreated/Completed/Cancelled/Escalated` for record review) are
    outbox-protected; Audit (slot 11) consumes them later without Workflow
    changes.

### Workflow permission matrix (ratified)

| Permission | Purpose | Typical holder |
|------------|---------|----------------|
| `workflow.task.read` | List and read task metadata (non-sensitive) | all authorized users |
| `workflow.task.read.sensitive` | Read sensitive task fields/notes | higher-privilege reviewers |
| `workflow.task.create` | Create a task (directly or via event reconciliation) | clerks / coordinators |
| `workflow.task.assign` | Assign/reassign assignees | coordinators |
| `workflow.task.start` | Transition `Assigned → In Progress` | assignees |
| `workflow.task.complete` | Transition `In Progress → Completed` with outcome | assignees / reviewers |
| `workflow.task.cancel` | Transition any non-terminal → `Cancelled` | coordinators |
| `workflow.task.escalate` | Explicit escalation to a different assignee | assignees / coordinators |
| `workflow.definition.read` | List/read the task-definition catalog | authorized users |
| `workflow.definition.manage` | Manage the task-definition catalog | administrators |
| `workflow.task.admin` | Administrative override (reassign/close a blocked task, view restricted activity) | operators |

`workflow.task.admin` is an override permission exercised through the standard
task endpoints (e.g. reassigning or closing a task the caller is not assigned
to), mirroring the Records `records.record.admin` override pattern; it does not
introduce a separate administration endpoint.

Scope semantics follow `ADR-011`: data permissions are organization-scoped; a
grant at a national scope covers descendant units via the Organization `/covers`
hierarchy resolution. A global grant of a data permission only applies to checks
with no organization context. Per-task access composes through Authorization
relationship tuples; Workflow never evaluates grants itself.

### Workflow integration events (`CommunityOS.Contracts.Workflow`)

Names and payloads below are the ratified baseline; each record carries a
trailing `DateTime OccurredOn`. Only stable ids and minimal lifecycle metadata
are exported — no task notes, no names, no filenames, no secrets, no sensitive
domain values.

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `WorkflowTaskCreated` | A task is created | `TaskId`, `DefinitionCode`, `DomainType`, `DomainEntityId`, `OrganizationUnitId`, `CreatedBy` |
| `WorkflowTaskAssigned` | Assignee(s) assigned/reassigned | `TaskId`, `AssigneeIds`, `AssignedBy` |
| `WorkflowTaskStarted` | `Assigned → In Progress` | `TaskId`, `StartedBy` |
| `WorkflowTaskCompleted` | `In Progress → Completed` (with outcome) | `TaskId`, `Outcome`, `CompletedBy` |
| `WorkflowTaskCancelled` | Any non-terminal → `Cancelled` | `TaskId`, `CancelledBy` |
| `WorkflowTaskEscalated` | Explicit escalation | `TaskId`, `EscalatedTo`, `EscalatedBy` |

Deliberately **not exported**: task notes and sensitive fields (resolved through
the API), assignee names (resolved through Community), document filenames
(resolved through Documents), record field values (resolved through Records),
definition-catalog changes (configuration), and internal activity history. The
completion outcome is carried on `WorkflowTaskCompleted`; completed tasks are
immutable, so no separate outcome-change event exists.

Consumers of Workflow events: Notifications (slot 9, task-routing digests),
Search (slot 10, task metadata indexing), Audit (slot 11, compliance trail).
All are outbox-protected at the Workflow integration gate.

Superseded decisions: none. The pre-existing documented example permissions
`workflow.task.read` / `workflow.task.complete` in
`PermissionCatalog.DocumentedExamples` are superseded by the ratified matrix
above and will be registered in the Authorization permission catalog at the
Prompt 09C implementation gate.
