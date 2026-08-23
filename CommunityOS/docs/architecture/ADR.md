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
| ADR-025 | Notifications bounded context and delivery boundary | Accepted |
| ADR-026 | Search bounded context and full-text projection boundary | Accepted |
| ADR-027 | Audit bounded context and compliance-journal boundary | Accepted |
| ADR-028 | Correspondence bounded context and communication-lifecycle boundary | Accepted |


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
- Slot 9 (**Notifications**) is **implemented** (ADR-025, Prompt 10C gate);
  the integration test suite is compile-only because Docker/Testcontainers is
  not available in the implementation environment.
- Slot 10 (**Search**) is **implemented** (ADR-026, Prompt 11C gate); the
  integration test suite is compile-only because Docker/Testcontainers is not
  available in the implementation environment.
- Slot 11 (**Audit**) is **implemented** (ADR-027, Prompt 12C gate) and
  **closed** by the Prompt 12F final read-only closure verification (verdict:
  PASS; all eight Prompt 12D findings remediated at Prompt 12E). The
  integration test suite is compile-only because Docker/Testcontainers is not
  available in the implementation environment.
- Slot 12 (**Knowledge**) was implemented early, out of sequence (ADR-021).
- Slot 13 (**Correspondence**) is **RATIFIED — NOT IMPLEMENTED (Prompt 12H)**
  at the ADR-028 gate. Correspondence is the next bounded context in the
  ratified sequence. Its implementation prerequisites include the Documents
  transactional-outbox upgrade (the Documents producer reliability gate;
  ADR-022 amendment note, ADR-028 decision 8).
- Slots 14–19 are not started.
- The **Content, Enrollment, Events and Reporting** service folders are inert
  pre-ratification scaffold remnants. They are **not part of the ADR-017
  sequence**, are not ratified implementation candidates, and must not be
  extended or treated as ratified. Any future work on these domains requires a
  new ADR that adds the context to the sequence (ADR-025, decision 15).

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

**Amendment (Prompt 12B, ADR-027):** the Audit bounded context (slot 11) is now
ratified. The guaranteed-delivery prerequisite for the Documents compliance
subset (`DocumentClassified`, `DocumentDeactivated`, `DocumentRestored`,
`DocumentContentDownloaded`, `DocumentScanCompleted`) becomes concrete:
Documents must be upgraded to transactional outbox publication for that subset
at the **Documents outbox gate**, and the Audit implementation gate must not
enable any Documents consumer before that gate completes. The upgrade is a
producer-side change scheduled with the Audit implementation work; nothing here
changes Documents' current best-effort behavior before that gate. See ADR-027
decisions 4–5.

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

## ADR-025 — Notifications bounded context and delivery boundary

**Status:** Accepted (ratified at the Prompt 10B gate; **RATIFIED AND
IMPLEMENTED** at the Prompt 10C gate).

The Notifications bounded context owns **notification records, recipient
delivery state, channel selection, notification types/templates and delivery
orchestration**. It is positioned in the implementation sequence (ADR-017) at
slot 9 — after Workflow, before Search. It is the authoritative record of what
was notified, to whom (by stable id), on which channel, when, and with what
delivery outcome; it is the delivery endpoint for the task-routing digests
Workflow (ADR-024) produces.

The Notifications context owns:

- **Notification** — the aggregate root: a notification instance referencing a
  triggering domain fact (stable `SourceType` + `SourceId`) or free-standing
  work, with a notification-type code, a channel, a subject/body template, a
  primary organization scope plus additional scopes, an optional `ScheduledFor`,
  a lifecycle status, and a recipient set.
- **NotificationRecipient** — the per-recipient delivery state (member id,
  channel, status, delivered/read provenance, failure reason, retry count).
- **NotificationType** — the catalog of notification types (stable string
  codes) and their default subject/body templates with `{{var}}` placeholders.
  Types are configuration: seeded idempotently, extensible at runtime, retired
  (never deleted).
- **NotificationPreference** — the per-member opt-in/opt-out rule per type and
  channel that the dispatch worker applies when deriving recipients.
- The **Organization read-model projection** (consumed unit events, mirroring
  Community, Knowledge, Documents, Records and Workflow; ADR-016).

The Notifications context does **not** own: the domain facts that trigger
notifications (records, tasks, questions, documents, events — referenced by
stable id only), person identity or contact details (resolved through the
Community API at dispatch time, never stored), authoritative content (Library
passages, record facts, task notes — never embedded in bodies), search
indexes, audit storage, or AI decisions. It never reaches into another
service's database (ADR-018).

Boundary rules (mandatory):

- **Delivery is orchestration, not domain authority.** A notification never
  changes the lifecycle of the record, task, question or document it refers to;
  it only records that people were informed. Notifications never invokes the
  mutation APIs of other contexts.
- **No authoritative content, no PII in bodies.** Stored subject/body copy is
  short operational text with stable-id references and links to the owning
  context's API. Notifications never embeds record field values, task notes,
  hold reasons, document filenames, Library passage text or person names in a
  body. Per-recipient personalization is deferred; any future personalization
  is resolved at provider-dispatch time and never persisted.
- **Destinations are resolved, never stored.** Recipients are stable member
  ids. Channel destinations (email address, phone number, push token) are
  resolved through the Community API at dispatch time and are never persisted
  in Notifications. InApp delivery needs no destination resolution (the member
  id is the destination).
- **Centralized authorization.** Every guarded operation calls the
  Authorization service through `AuthorizationGuard` (fail-closed;
  ADR-009/010/011/018/019). No `[Authorize(Roles = "...")]`, no local RBAC, no
  direct Authorization database access. Resource-level authorization is
  mandatory (`resourceType = "notification"`); unauthorized enumeration is
  prevented with equivalent not-found behavior for missing and unauthorized
  resources.
- **Organization scoping follows ADR-016.** A notification carries a primary
  scope plus additional scopes; access succeeds when the caller holds the
  permission at **any** of the notification's scopes (Records/Workflow
  pattern). A member additionally reads their **own** inbox through
  Authorization relationship tuples (recipient relation); never through scope
  grants alone for another member's notification.
- **Sensitive notifications.** A notification flagged `IsSensitive` (e.g.
  record-hold notifications) is readable only under
  `notifications.notification.read.sensitive` in addition to
  `notifications.notification.read`, mirroring the Records/Documents
  sensitive-field gate.
- **Channels: domain-owned vs provider-deferred.** InApp is a domain-owned
  channel implemented by Notifications (persisted inbox, no external provider).
  Email, SMS and Push are ratified logical channels whose state model is
  domain-owned but whose concrete sending is a **future provider integration**
  behind `INotificationChannelDispatcher`; a recipient on a channel with no
  configured provider fails closed at dispatch time
  (`Failed`, reason `provider-not-configured`). See decision 5.
- **No irreversible destruction.** Notifications are never hard-deleted
  through normal operations. Retention expiry flags a review disposition and
  never auto-destroys (Records pattern); hard purge requires an explicitly
  authorized administrative override (`notifications.notification.admin`) with
  a reason.
- **Privacy of exports.** Outbound integration events carry identifiers and
  counts only — **never recipient member ids, never bodies, never names, never
  delivery failures** (see produced events).

Storage topology (Accepted): PostgreSQL holds all authoritative notification
data. Database `communityos_notifications`, schema `notifications`. There is no
shared database with any other service (ADR-018).

Integration: Notifications consumes integration events (below) via
MassTransit under `CommunityOS.Contracts.Workflow`, `CommunityOS.Contracts.Records`,
`CommunityOS.Contracts.Knowledge`, `CommunityOS.Contracts.Community` and the
Organization ADR-016 projection; it produces `NotificationDispatched` under
`CommunityOS.Contracts.Notifications`. The transactional outbox (ADR-015) is
enabled at the **Notifications integration gate** (Prompt 10C) — see the outbox
gate below.

**Outbox gate (ratified).** The transactional outbox and the receive-endpoint
inbox are enabled at the Notifications integration gate (Prompt 10C), mirroring
the Records (Prompt 08B) and Workflow (Prompt 09C) gates:

1. Notifications is itself a guaranteed-delivery **consumer** of Workflow
   events (`WorkflowTaskAssigned`/`WorkflowTaskEscalated`), which the Workflow
   outbox already protects; Notifications must consume with receive-endpoint
   outbox semantics to reconcile exactly-once.
2. Notifications' own `NotificationDispatched` event is consumed by Audit
   (slot 11) and Analytics (slot 19); it must be outbox-protected before those
   consumers subscribe.
3. Best-effort in-process publication is **not** acceptable for any Notifications
   integration event at implementation time.

The outbox-protected set is **non-exhaustive by design**: any Notifications
event consumed by a guaranteed-delivery consumer is protected whether or not
its name appears in a delivery table.

Ratified decisions (Prompt 10B):

1. **Ownership** — Notifications owns notification records, recipient delivery
   state, channel selection, type/template catalog, preferences and delivery
   orchestration; never the domain facts that trigger notifications.
2. **Scaffold disposition** — the pre-existing `Notifications.Domain` scaffold
   (prototype, `src/Services/Notifications/`) is superseded by this ADR as
   authoritative. **Retained as-is (reusable):** `MessageTemplate` (subject/
   body + `{{var}}` rendering), `NotificationChannel` (Email/Push/InApp/SMS)
   enumeration, the recipient-dedup rule (`AddRecipient` no-op on duplicate),
   `NotificationNotFoundException`. **Superseded/refined:** `Notification`
   aggregate (adds parameterized `occurredOn`, lifecycle with terminal guard,
   source reference, org scopes, `IsSensitive`, type code, `ScheduledFor`;
   removes hardcoded `DateTime.UtcNow`); `NotificationRecipient` (adds
   terminal-state guards on `MarkSent`/`MarkDelivered`/`MarkRead`/
   `MarkFailed`, bounded retry count; removes direct `DateTime.UtcNow` in
   delivery/read provenance); `NotificationStatus` (replaced by the ratified
   aggregate lifecycle + recipient status below);
   `NotificationDispatchedEvent` (kept, extended for integration export);
   `NotificationDeliveredEvent`/`NotificationReadEvent` (kept as **domain
   events only**, never exported); `INotificationRepository` (extended for
   reconcile create-if-absent and inbox queries); `NotificationsDomainExceptions`
   (adds `InvalidNotificationTransitionException`).
3. **Aggregate and lifecycle** — `Notification` is the aggregate root with
   `Draft → Queued → Dispatched`. `Dispatched` is terminal: reached only when
   every recipient is terminal, in the same transaction that publishes
   `NotificationDispatched`. `Dispatch()` throws
   `InvalidNotificationTransitionException` if invoked when already
   `Dispatched` (double-dispatch protection). Recipients may be added in
   `Draft`/`Queued`; `Queue()` requires at least one recipient.
4. **Recipient lifecycle and terminal-state invariants** —
   `Pending → Sent → Delivered → Read`, with `Failed` terminal from
   `Pending`/`Sent`. Transitions are guarded: `MarkSent` only from `Pending`;
   `MarkDelivered` only from `Sent` (provider ack) or directly from `Pending`
   (InApp immediate delivery); `MarkRead` only from `Delivered` (a notification
   that was never delivered or that failed can never be read); `MarkFailed`
   only from `Pending`/`Sent`. Any other transition throws
   `InvalidNotificationTransitionException`. `Delivered`, `Read` and `Failed`
   are terminal per recipient; no regress, no reopen — a re-send is always a
   **new** notification (Workflow "no reopen" philosophy).
5. **Channels and provider boundary** — ratified channel catalog:
   `InApp(3)`, `Email(1)`, `Push(2)`, `Sms(4)`. **InApp** is domain-owned and
   implemented in the first gate: dispatch writes a readable inbox row
   (`Pending → Delivered`), and `Read` is tracked via the API. **Email, SMS and
   Push** are ratified logical channels with domain-owned state semantics
   (`Sent` = handed to a provider; `Delivered` = provider ack; `Read` = read
   receipt where the provider supports it), but their concrete providers (SMTP/
   SES, SMS gateway, APNS/FCM) are **future infrastructure integrations** behind
   `INotificationChannelDispatcher`. With no provider configured, dispatch fails
   closed (`Failed`, reason `provider-not-configured`). No channel-specific
   schema exists beyond the channel code + status: provider payloads are
   transport details resolved at dispatch time.
6. **Templates/content ownership** — `MessageTemplate` remains part of
   Notifications. The `NotificationType` catalog owns the default subject/body
   template per type and is seeded idempotently at migration time
   (`NotificationCatalogSeeder`), mirroring the Workflow task-definition
   catalog. Bodies are rendered at dispatch time with a validated, non-PII
   variable set (ids, dates, codes). Notifications never owns authoritative
   content (ADR-021 rule for citations applies to notification copy).
7. **Permission matrix** — the `notifications.*` matrix below is adopted;
   centralized and fail-closed; resource-level (`resourceType =
   "notification"`); self-inbox access via relationship tuples; 404-equivalent
   for unauthorized reads.
8. **Scoping and sensitive access** — ADR-016 read-model projection; multi-scope
   any-of-grant; `IsSensitive` notifications require the sensitive-read
   permission; sensitive fields (failure reasons, distribution) are returned
   only under `notifications.notification.read.sensitive`.
9. **Consumed events** — the catalog below is adopted; the first gate
   implements only the events that carry their own recipient lists
   (`WorkflowTaskAssigned`, `WorkflowTaskEscalated`); all other triggers are
   ratified and gated on a recipient-resolution dependency.
10. **Produced events** — `NotificationDispatched` is the only exported
    integration event; `NotificationDeliveredEvent`/`NotificationReadEvent` are
    domain events only. Outbound payloads never carry recipient member ids,
    bodies, names or failures (privacy rule).
11. **Outbox/inbox** — ADR-015 transactional outbox + receive-endpoint inbox
    enabled at the Notifications integration gate (see outbox gate).
12. **Idempotency, retry, failure, reconciliation** — notification creation is
    idempotent per (type, source type, source id, channel) via a uniqueness
    constraint; reconcile consumers create-if-absent; duplicate events never
    create duplicate notifications. Duplicate delivery/read acks are no-ops
    (guarded terminal states). Provider retries are worker-level and bounded
    (configuration-driven backoff) and never regress persisted domain state;
    permanent failure is terminal `Failed` with a reason. Reconciliation
    converges on redelivered events to the same notification.
13. **Persistence** — own database `communityos_notifications`, schema
    `notifications`; tables `notifications`, `notification_recipients`,
    `notification_types`, `notification_preferences`,
    `organization_unit_references` (projection), plus MassTransit
    `InboxState`/`OutboxMessage`/`OutboxState`. EF migrations via the
    Infrastructure project. Retention: no hard delete through normal
    operations; expiry review-flagged; data privacy per boundary rules.
14. **API contract** — versioned `/api/v1/notifications`; DTOs only;
    inbox/read/sensitive/catalog/preferences endpoints per
    `docs/api/notifications.md`; permission-to-endpoint equality verified at
    the 10C gate; 404 for missing and unauthorized (no enumeration oracle).
15. **Scaffold-remnant disposition (Content, Enrollment, Events, Reporting)** —
    these four service folders are inert pre-ratification scaffolds **not in
    the ADR-017 sequence**. They are **not ratified implementation candidates**
    and must not be extended, migrated or treated as authoritative. They are
    recorded in the ADR-017 status block as non-sequence scaffolds; any future
    work on those domains requires a new ADR that explicitly adds the context
    to the sequence and supersedes the prototype. This ADR makes Notifications
    the sole ratified slot-9 work.
16. **Audit readiness** — `NotificationDispatched` is outbox-protected from the
    Notifications integration gate so Audit (slot 11) can subscribe later
    without Notifications changes; Audit is not implemented now.

### Notifications permission matrix (ratified)

| Permission | Purpose | Typical holder |
|------------|---------|----------------|
| `notifications.notification.read` | List/read notification metadata and one's own inbox (self via relationship tuple) | all authorized users |
| `notifications.notification.read.sensitive` | Read sensitive notification fields (failure reasons, distribution) and `IsSensitive` notifications | higher-privilege / privacy roles |
| `notifications.notification.create` | Create a notification (directly or via event reconciliation) | services / coordinators |
| `notifications.notification.send` | Trigger dispatch / admin re-send | dispatch operators |
| `notifications.notification.admin` | Administrative override (re-queue, force-fail, view restricted diagnostics) | operators |
| `notifications.type.manage` | Manage the notification-type/template catalog | administrators |
| `notifications.template.read` | Read the notification-type/template catalog | authorized users |
| `notifications.preference.manage` | Manage notification preferences (self via relationship tuple) | members |

`notifications.notification.admin` is an override permission exercised through
the standard endpoints (e.g. re-dispatching a failed notification), mirroring
the Records/Workflow override pattern; it does not introduce a separate
administration endpoint.

Scope semantics follow `ADR-011`: data permissions are organization-scoped; a
grant at a national scope covers descendant units via the Organization `/covers`
hierarchy resolution. A global grant of a data permission only applies to
checks with no organization context. Per-member inbox access composes through
Authorization relationship tuples (recipient relation); Notifications never
evaluates grants itself.

### Notifications integration events (`CommunityOS.Contracts.Notifications`)

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `NotificationDispatched` | A notification completes dispatch (all recipients terminal) | `NotificationId`, `TypeCode`, `Channel`, `SourceType`, `SourceId`, `RecipientCount`, `OccurredOn` |

Payloads are identifiers and a count only. **Deliberately not exported**:
recipient member ids (distribution is sensitive), subject/body copy, names,
delivery failures, and read/delivery provenance. `NotificationDeliveredEvent`
and `NotificationReadEvent` are **domain events only** and are never placed on
the bus.

### Notifications consumed events (ratified catalog)

Recipient resolution is the gate criterion: the first gate (Prompt 10C)
implements only triggers that carry their own recipient list and need no
cross-service read.

| Producer | Event | Notification type | Recipients | Gate |
|----------|-------|-------------------|------------|------|
| Organization | `OrganizationUnitCreated/Updated/ParentChanged` | (projection only, not a notification trigger) | — | First gate |
| Workflow | `WorkflowTaskAssigned` | `task-assigned` | `AssigneeIds` (from event) | **First gate** |
| Workflow | `WorkflowTaskEscalated` | `task-escalated` | `EscalatedTo` (from event) | **First gate** |
| Workflow | `WorkflowTaskCompleted` | `task-completed` (originator digest) | originator via Workflow API read | Deferred |
| Workflow | `WorkflowTaskCancelled` | `task-cancelled` (originator digest) | originator via Workflow API read | Deferred |
| Records | `RecordVerified` | `record-verified` | submitter via Records API read | Deferred |
| Records | `RecordRejected` | `record-rejected` | submitter via Records API read | Deferred |
| Records | `RecordHoldPlaced` | `record-hold` (sensitive) | stakeholders via Records API read | Deferred |
| Records | `RecordHoldReleased` | `record-hold` (sensitive) | stakeholders via Records API read | Deferred |
| Knowledge | `QuestionFlagged` | `question-flagged` (moderation) | moderators via Knowledge API read | Deferred |
| Knowledge | `QuestionUnderReview` | `question-flagged` (moderation) | moderators via Knowledge API read | Deferred |
| Community | `ActivityCreated` | `community-activity` | audience selection (product decision) | Deferred |
| Community | `CommunityEventCreated` | `community-event` | audience selection (product decision) | Deferred |
| Community | `MeetingCreated` | `community-meeting` | audience selection (product decision) | Deferred |

`WorkflowTaskCreated` is not a trigger (a task is not assigned until
`WorkflowTaskAssigned`). Deferred triggers are ratified contracts, not
implemented in the first gate; each states its recipient-resolution dependency
so it cannot silently fire without recipients.

### Notification type catalog (ratified baseline)

Seeded idempotently at migration time; codes are stable strings, extensible
via configuration; types are retired, never deleted:

| Code | Purpose | Default channel | Sensitive |
|------|---------|-----------------|-----------|
| `task-assigned` | A workflow task is assigned to the recipient | InApp | no |
| `task-escalated` | A workflow task is escalated to the recipient | InApp | no |
| `task-completed` | A task the recipient originated is completed (deferred trigger) | InApp | no |
| `task-cancelled` | A task the recipient originated is cancelled (deferred trigger) | InApp | no |
| `record-verified` | A record the recipient submitted is verified (deferred trigger) | InApp | no |
| `record-rejected` | A record the recipient submitted is rejected (deferred trigger) | InApp | no |
| `record-hold` | A hold is placed/released on a record (deferred trigger) | InApp | yes |
| `question-flagged` | A question is flagged for moderation (deferred trigger) | InApp | no |
| `community-activity` / `community-event` / `community-meeting` | Community activity digests (deferred triggers) | InApp | no |
| `general` | Free-standing / administrative notifications (API-created) | InApp | no |

Superseded decisions: none. The pre-existing `Notifications.Domain` scaffold is
dispositioned by decision 2 above; it is superseded by this ADR, not ratified.

## ADR-026 — Search bounded context and full-text projection boundary

**Status:** Accepted (ratified at Prompt 11B gate).

### Context

CommunityOS requires a cross-domain search surface enabling authorized users
to locate records, documents, workflow tasks, and knowledge questions from a
single query interface. Nine bounded contexts are already implemented; each
owns its own database and read API. A dedicated Search projection service is
needed so that callers can issue a single query across multiple source types
without each caller needing to query every source API individually.

The key architectural constraint is that no new infrastructure component may be
introduced unless explicitly justified: every service already depends on
PostgreSQL 16 (ADR-006). Introducing Elasticsearch, OpenSearch, Meilisearch, or
any other dedicated search engine requires: a new Docker compose service, new
NuGet packages (not used elsewhere), new Testcontainers images, and significant
operational overhead not present in the current stack.

### Decision

**PostgreSQL full-text search (`tsvector` / `tsquery`)** is selected as the
Search engine for the first gate. The rationale:

1. **Zero new infrastructure** — the shared `postgres:16-alpine` instance is
   already required by all 9 implemented services. No new Docker compose entry
   is needed.
2. **Zero new NuGet packages** — `Npgsql.EntityFrameworkCore.PostgreSQL` is
   already the project-wide ORM; `NpgsqlTsVector` support ships with it.
3. **Zero new Testcontainers images** — integration tests already use
   `Testcontainers.PostgreSql`.
4. **Sufficient capability for the first gate** — multi-word and phrase queries
   (`websearch_to_tsquery`), `ts_rank` relevance ordering, `ts_headline`
   snippet generation, language dictionaries, and `GIN` index support are all
   available natively in PostgreSQL 16.
5. **Operational continuity** — no separate engine, no separate backup strategy,
   no additional monitoring surface.

Dedicated search engines (Elasticsearch, OpenSearch) remain valid future options
if the platform outgrows PostgreSQL FTS; the Search bounded context abstraction
ensures that replacing the engine is an infrastructure-only change.

### Search is a projection-only service

Search is a **read projection** service. It owns only `SearchDocument`
projections derived from integration events published by source services.
It never owns the domain facts it indexes. Source services remain the
source of truth for their own state.

Consequences:
- A missed event leaves a document temporarily absent from search results; it
  remains fully accessible through each source service's own API.
- This is acceptable because search is explicitly best-effort (see decision 12
  and `docs/records.md`).
- The `POST /admin/reindex` endpoint corrects drift.

### Ratified decisions (Prompt 11B)

1. **Ownership** — Search owns `SearchDocument` projections, `AdditionalScope`
   child rows, `OrganizationUnitReference` read-model projections (ADR-016
   pattern), and `SearchIndexLog` diagnostics. It never owns the domain facts
   it indexes.

2. **Technology** — PostgreSQL full-text search (`tsvector`/`tsquery`) on the
   existing shared PostgreSQL 16 instance. Database: `communityos_search`;
   schema: `search`. `GIN` index on `search_vector`. Unique index on
   `(source_type, source_id)` for idempotent upsert. See `docs/search.md` for
   the full deployment model.

3. **First-gate index taxonomy** — four source types are indexed at Prompt 11C:
   - `record` — from `CommunityOS.Contracts.Records` events
   - `document` — from `CommunityOS.Contracts.Documents` events
   - `workflow-task` — from `CommunityOS.Contracts.Workflow` events
   - `knowledge-question` — from `CommunityOS.Contracts.Knowledge` events
   Deferred: Knowledge Library (Works/Editions/Passages), Knowledge answers,
   Community persons/households (PII gate required), Community events/meetings/
   activities, Organization units, Notifications.

4. **Privacy rules (mandatory)** — `DisplayTitle` for records is the category
   code only (never a person's name or any field value). `DisplayTitle` for
   Knowledge questions is the literal string `"Question"` (text resolved via
   Knowledge API). No names, contact details, field values, hold reasons, task
   notes, passage text, or document filenames are ever stored in any projection
   field. `IsSensitive = true` projections are indexed but excluded from
   standard results; only `search.result.read.sensitive` callers see them.

5. **Permission matrix** — three permissions, registered in
   `PermissionCatalog` and development role seeds at Prompt 11C:
   - `search.result.read` — execute queries, receive non-sensitive results
   - `search.result.read.sensitive` — receive sensitive-flagged results
   - `search.index.manage` — trigger reindex, view index health

6. **API surface** — five endpoints at first gate (see `docs/api/search.md`):
   - `GET /api/v1/search` — full-text query across all source types
   - `GET /api/v1/search/{sourceType}` — query scoped to one source type
   - `GET /api/v1/search/admin/health` — index health (`search.index.manage`)
   - `POST /api/v1/search/admin/reindex` — full rebuild (`search.index.manage`)
   - `POST /api/v1/search/admin/reindex/{sourceType}` — partial rebuild

7. **Authorization** — every guarded endpoint calls the Authorization service
   through `AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019). No
   `[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization
   database access. Search result filtering is fail-closed: queries return only
   results the caller is authorized to read; unauthorized results are silently
   absent (no enumeration oracle).

8. **Organization scoping** — follows ADR-016. The `SearchDocument` projection
   carries `OrganizationUnitId` (primary scope) and zero or more `AdditionalScope`
   child rows. Any-of-scope semantics: a caller is authorized to see a result when
   they hold `search.result.read` at **any** of the result's scopes. Mirrors the
   Records/Workflow/Notifications any-of-grant pattern (ADR-011).

9. **Pagination** — `limit` / `offset` parameters. Default `limit = 25`;
   maximum `limit = 50` (configurable via `Search:MaxResultsPerPage`). Values
   above the maximum are clamped. Results ordered by `ts_rank` descending; ties
   by `IndexedOn` descending.

10. **Idempotency** — consumers upsert by `(SourceType, SourceId)` unique key.
    A stale-event guard (compare incoming `OccurredOn` to current `IndexedOn`)
    prevents late re-delivery of an older event from regressing a newer
    projection. The MassTransit EF Core inbox provides exactly-once consume
    semantics at the receive endpoint.

11. **Outbox / inbox** — Search is inbox-only at the first gate. The MassTransit
    EF Core inbox (`InboxState` table) is enabled for exactly-once consume
    semantics. No outbox is needed: Search publishes no integration events at
    the first gate. Outbox tables (`OutboxMessage`, `OutboxState`) are
    provisioned but unused; they will be activated if a future gate introduces
    Search-published events (ADR-015 amendment pattern).

12. **Best-effort delivery** — Search does not require guaranteed delivery from
    producers (explicitly aligned with `docs/records.md`). A missed event means
    a document is temporarily absent from search results only — the document
    remains fully accessible through the source service's own API. This differs
    from Workflow and Audit consumers which have compliance obligations.

13. **Soft-delete model** — when a source object enters a terminal state
    (Archived, Deactivated, Merged, Cancelled), the `SearchDocument.Status` is
    updated to the terminal status string. The row is retained for diagnostics
    but is excluded from default search results via a status filter; terminal
    rows may still be surfaced explicitly by requesting their status through
    the `status` query parameter (see `docs/api/search.md`). Hard deletion
    requires an admin rebuild.

14. **Logs / events privacy** — logs record only indexed source type, source id,
    operation, and outcome. `DisplayTitle` values, query strings containing PII,
    and source field values are never logged. Search publishes no integration
    events at the first gate; if future events are added, they must contain
    identifiers only (no PII, no field values, no display titles).

15. **Scaffold status** — there is no pre-existing Search scaffold. Search is a
    greenfield bounded context at Prompt 11C.

Superseded decisions: none. ADR-017 slot 10 status updated from NOT STARTED to
RATIFIED (Prompt 11B), then to IMPLEMENTED (Prompt 11C gate).

## ADR-027 — Audit bounded context and compliance-journal boundary

**Status:** Accepted (ratified at the Prompt 12B gate; implementation not
started).

### Context

Audit is positioned in the implementation sequence (`ADR-017`) at slot 11 —
after Search, before Knowledge. Prompt 12A verified that Audit is completely
blank-slate: no source projects, contracts, tests, database, permissions,
configuration or documentation exist for it, and no inert scaffold could be
mistaken for it. Multiple ratified ADRs already name Audit as a future
guaranteed-delivery consumer: ADR-015 (amended) requires the transactional
outbox before any Audit subscription; ADR-023 decision 14 designates Records a
compliance-critical context whose lifecycle events target Audit; ADR-024
decision 14 reserves Workflow compliance events for Audit; ADR-025 decision 16
reserves `NotificationDispatched` for Audit; ADR-022 ratifies
`DocumentContentDownloaded` as targeting security/audit consumers; ADR-014
requires break-glass operations to emit high-priority audit events.

The authoritative Security Architecture, Data Classification Model and
Authorization Model specifications are PLACEHOLDER (ADR-020) and must not be
relied upon. Audit-specific privacy, retention and immutability rules are
therefore ratified here directly.

### Decision

The Audit bounded context owns an **append-only compliance journal**: a
write-once, queryable record of selected integration events raised by producer
contexts. It is a historical/compliance record of selected events — **never a
replacement source of truth** for any domain fact.

### Ratified decisions (Prompt 12B)

1. **Ownership model.** Audit owns: audit entries, audit-entry identity,
   ingest metadata, retention state, legal-hold references on entries, the
   Organization read-model reference table required for authorization
   (ADR-016 pattern), idempotency state and inbox state. Audit does **not**
   own: domain facts, Records state, Document state or content, Workflow task
   state or task activity history, Notification delivery state, recipient
   distribution, or any source-service business rule. Where an upstream fact is
   not exported onto the bus (task activity notes, notification distribution,
   hold reasons), Audit's journal is deliberately partial by design; the
   upstream service remains authoritative.

2. **Immutability model — application AND database enforced (both).** The
   strongest practical model consistent with this codebase is layered:

   - **Application level:** `AuditEntry` is a write-once aggregate. No command,
     handler, repository method or API path updates or deletes an existing
     entry. Corrections, reversals and superseding facts arrive as **new events
     from producers**, which become **new** entries.
   - **Database level:** the initial migration ships native PostgreSQL triggers
     that reject `UPDATE`/`DELETE` on `audit_entries` unless the session sets a
     purge authorization setting (`app.audit_purge_authorized = 'on'`) via
     `SET LOCAL`. Only the ratified retention-purge operation sets it. Zero new
     infrastructure is introduced (PostgreSQL-native, no new service, package
     or container).
   - **Retention purge is not an ordinary correction.** Purge is a separate
     controlled lifecycle operation (decision 13): it writes a purge-marker
     entry in the same transaction before deleting the expired batch, so the
     deletion itself remains auditable after the fact.
   - **Legal holds never touch immutable rows.** Hold state lives in a separate
     mutable `audit_entry_holds` table referencing entries; an active hold
     exempts its entries from purge regardless of retention expiry. Audit may
     place administrative holds on its own entries; each placement/release is
     itself journaled (decision 14).

3. **Blank-slate status.** No scaffold disposition is required. There is no
   pre-existing Audit artifact anywhere in the repository (verified at Prompt
   12A). Audit is greenfield.

4. **Producer reliability prerequisites (F-01 resolution, generalized).**
   Guaranteed delivery to Audit is a property of the **producer**. Verified
   publication status per producer: Records, Workflow and Notifications publish
   through the transactional outbox (`AddCommunityOSEventBusWithOutbox`) and
   are clear for Audit subscription. **Documents publishes best-effort** (plain
   `AddCommunityOSEventBus`, no outbox entities) although `docs/documents.md`
   has always marked its compliance subset as guaranteed-delivery-required
   "before Audit is implemented"; and **Authorization also publishes
   best-effort**, including the ADR-014-mandatory break-glass events.
   Therefore:

   - Before Audit consumes **any** Documents compliance event, Documents must
     be upgraded to transactional outbox publication for the ratified
     compliance subset — the **Documents outbox gate** (see the amendment note
     inside ADR-022 above). This upgrades existing documented policy to a
     concrete, scheduled prerequisite; it does not silently override Documents'
     current behavior.
   - Symmetrically, before Audit consumes Authorization events, Authorization
     must be upgraded to outbox publication for the security subset — the
     **Authorization outbox gate** — because break-glass auditing (ADR-014) is
     a mandatory compliance obligation, not best-effort telemetry.
   - Both gates are producer-side changes executed with the Audit
     implementation work (Prompt 12C or a dedicated remediation step inside
     that gate). Until a producer's gate completes, Audit's implementation
     must not register that producer's consumers.

5. **First-gate event catalog.** Every event in every existing contract
   (`CommunityOS.Contracts.*`) is classified below. The catalog is exhaustive:
   Prompt 12C implements exactly the FIRST GATE set plus the projection
   consumers, and nothing else. GATED events activate only when their
   producer's outbox gate completes. Deferred/not-classified events are never
   subscribed without a new ADR amendment.

   **CONSUMED AND PERSISTED — first gate (22):**

   | Producer | Events |
   |----------|--------|
   | Records (16) | `RecordCreated`, `RecordSubmitted`, `RecordUnderReview`, `RecordVerified`, `RecordRejected`, `RecordCorrected`, `RecordArchived`, `RecordDeactivated`, `RecordRestored`, `RecordClassified`, `RecordHoldPlaced`, `RecordHoldReleased`, `RecordRetentionChanged`, `RecordEvidenceAttached`, `RecordEvidenceRemoved`, `RecordRetentionExpired` |
   | Workflow (5) | `WorkflowTaskCreated`, `WorkflowTaskAssigned`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled`, `WorkflowTaskEscalated` |
   | Notifications (1) | `NotificationDispatched` |

   Rationale: Records' full lifecycle is the compliance core named by ADR-023;
   consuming all 16 costs nothing extra because they are already
   outbox-protected. Assignment/completion/cancellation/escalation bracket task
   accountability (ADR-024); task *start* adds review-latency detail only.
   Dispatch completion is the sole exportable notification fact (ADR-025).

   **CONSUMED AND PERSISTED — gated on producer outbox gate (12):**

   | Producer | Gate | Events |
   |----------|------|--------|
   | Documents (5) | Documents outbox gate | `DocumentClassified`, `DocumentDeactivated`, `DocumentRestored`, `DocumentContentDownloaded`, `DocumentScanCompleted` |
   | Authorization (7) | Authorization outbox gate | `RoleAssigned`, `RoleRevoked`, `DelegationGranted`, `DelegationRevoked`, `BreakGlassRequested`, `BreakGlassApproved`, `BreakGlassRevoked` |

   Rationale: exactly the subset `docs/documents.md` has always listed as
   guaranteed-delivery-required, plus the entire authorization-security set
   (role/delegation changes are privilege facts; break-glass is mandated by
   ADR-014).

   **CONSUMED BUT NOT PERSISTED AS AUDIT ENTRIES — projection only (3):**
   Organization `OrganizationUnitCreated/Updated/ParentChanged` →
   `organization_unit_references` (ADR-016 scoping infrastructure; never
   journaled as entries).

   **DEFERRED (17)** — candidate second-gate material; none subscribed now:
   `WorkflowTaskStarted`; Identity account-security subset (`UserAccountLocked`,
   `UserAccountUnlocked`, `CredentialChanged`, `MfaMethodEnrolled`,
   `MfaMethodRemoved`, `ExternalIdentityLinked`, `ExternalIdentityUnlinked`);
   Organization governance facts (`DelegationFactGranted`,
   `DelegationFactRevoked`); Knowledge moderation/AI-governance subset
   (`QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`,
   `QuestionArchived`, `AiSuggestionRequested`, `AiSuggestionReviewed`);
   `DocumentArchived`.

   **NOT AN AUDIT EVENT (41):** Documents `DocumentCreated`,
   `DocumentMetadataUpdated`, `DocumentVersionAdded`; Identity
   `UserAccountRegistered` (carries email), `UserAccountVerified`,
   `UserAccountDeactivated`, `DeviceRegistered` (carries device name),
   `RefreshTokenIssued` (high-volume session telemetry); Organization
   `OrganizationCreated/Updated`, `Committee*`, `AppointmentAssigned/Ended`;
   all 14 Community events (person/household/membership/activity/meeting/
   participation — routine community lifecycle whose official membership and
   appointment facts already belong to Records); Knowledge Library pipeline and
   answer/category events (`WorkImported`, `EditionImported`, `EditionVerified`,
   `PassageImported`, `PassageCorrected`, `QuestionSubmitted`,
   `QuestionPublished`, `AnswerAdded`, `AnswerUpdated`, `AnswerAccepted`,
   `CategoryCreated`, `CategoryUpdated`).

6. **Privacy and PII rules.** Because the Security/Data-Classification
   specifications are PLACEHOLDER (F-04), these rules are self-ratified.
   Persisted by default: stable identifiers, codes (category/classification/
   definition/outcome/scan-status strings from payloads), timestamps, event
   type, action/outcome codes, resource identifiers, organization identifiers,
   sensitivity classification and minimal structured metadata. **Never
   persisted:** names, addresses, emails, phone numbers, document or message
   bodies, notification destinations, credentials, secrets, tokens, hold
   reasons, arbitrary domain field values, uploaded content, or unnecessary
   personal attributes. Payloads that carry such fields (e.g.
   `UserAccountRegistered.Email`, `DocumentCreated.Title`,
   `CommunityEventCreated.Title`) are either not consumed at all or mapped so
   those fields are dropped at ingest. **Sensitivity model:** an entry is
   `Sensitive` iff (a) its event type is in the ratified sensitive-event list
   (`BreakGlassRequested/Approved/Revoked`, `RecordHoldPlaced`,
   `RecordHoldReleased`, `DocumentContentDownloaded`), or (b) the payload
   explicitly carries `IsSensitive = true` (`RecordClassified`,
   `DocumentClassified`). Sensitivity is deterministic and payload-driven —
   never inferred. **Metadata:** no arbitrary payload serialization.
   `MetadataJson` stores flat key/value pairs only; keys must come from a
   per-event-type allowlist maintained in code/configuration; values must be
   scalars (ids, codes, enums, counts, booleans); maximum ten keys. An ingest
   mapping that cannot conform dead-letters the message rather than sanitizing
   or truncating.

7. **Correlation/causation (F-02 resolution): option C with reserved columns.**
   Existing contracts carry `OccurredOn` but no correlation/causation
   identifiers, and broker-assigned message ids are transport details that
   ADR-004 forbids relying on. The entry schema therefore carries nullable
   `CorrelationId`/`CausationId` columns that remain unpopulated at the first
   gate; causal-chain reconstruction is explicitly **not guaranteed**. Enriching
   the shared event envelope with ratified correlation identifiers is a
   potential future cross-cutting contracts gate requiring its own ADR
   amendment; it is deliberately **not** part of Audit's gate and no contract
   changes are made here. No correlation data is invented.

8. **Entry schema.** `audit_entries` (schema `audit`):

   | Field | Required | Purpose | Privacy | Mutable | Indexed |
   |-------|----------|---------|---------|---------|---------|
   | `Id` (Guid) | yes | Synthetic row identity | non-PII | no | PK |
   | `OccurredOn` (DateTime UTC) | yes | When the producer says the fact occurred (event occurrence time) | non-PII | no | yes |
   | `IngestedOn` (DateTime UTC) | yes | When Audit durably recorded it — deliberately distinct from `OccurredOn`; the gap exposes delivery latency | non-PII | no | no |
   | `SourceService` (string 50) | yes | Producing bounded context | non-PII | no | yes |
   | `SourceEventType` (string 100) | yes | Contract event type name | non-PII | no | yes |
   | `SourceEventHash` (string 64) | yes | Deterministic SHA-256 over canonical (source service, event type, resource ids, occurred-on, discriminating scalar fields); the idempotency identity — unique | non-PII | no | UNIQUE |
   | `Action` (string 100) | yes | Normalized action code (e.g. `record-verified`, `hold-placed`, `task-completed`, `break-glass-approved`) | non-PII | no | yes (with date filters) |
   | `Outcome` (string 100) | no | Normalized outcome/status code carried by the payload (workflow outcome, scan status) | non-PII | no | no |
   | `ResourceType` (string 50) | yes | What the fact is about (`record`, `document`, `workflow-task`, `notification`, `authz-role`, `authz-delegation`, `break-glass-request`) | non-PII | no | yes (composite) |
   | `ResourceId` (Guid) | yes | Primary aggregate id from the payload | non-PII | no | composite with type |
   | `SecondaryResourceId` (Guid?) | no | Sub-object id where applicable (hold id, version id, assignment/request id) | non-PII | no | no |
   | `SubjectId` (Guid?) | no | Person/household the fact concerns, when the payload carries one (e.g. `RecordCreated.SubjectId`); null otherwise — never resolved or enriched | person-id reference | no | yes |
   | `ActorId` (Guid?) | no | Who performed the action, when the payload carries one (`VerifiedBy`, `PlacedBy`, `CompletedBy`, `RequesterId`, …); nullable because some payloads lack actors (e.g. `RoleAssigned`) | person-id reference | no | yes |
   | `OrganizationUnitId` (Guid?) | yes (nullable column) | Primary scope from the payload; null means global/unscoped fact | non-PII | no | yes |
   | `Sensitivity` (enum) | yes | `Normal \| Sensitive` per decision 6 rules | classification | no | yes |
   | `CorrelationId` (Guid?) | no | Reserved for F-02 option A activation; unpopulated at first gate | non-PII | no | no |
   | `CausationId` (Guid?) | no | Reserved as above | non-PII | no | no |
   | `MetadataJson` (jsonb) | no | Allowlist-validated flat scalar metadata per decision 6 | allowlisted scalars only | no | no |
   | `RetentionClass` (string 50) | yes | Retention classification code driving expiry | non-PII | no | partial composite |
   | `RetentionExpiresOn` (DateTime?) | no | Computed at ingest from class configuration; null = retain indefinitely | non-PII | no | partial composite |

   Additional tables: `audit_entry_holds` (`Id`, `EntryId` FK, `HoldType`
   `legal|administrative`, `PlacedBy`, `PlacedOn`, optional `ReleasedBy`/
   `ReleasedOn`, `ReasonCode` — a short code, never free text); mutable by
   design. `organization_unit_references` (ADR-016 projection). MassTransit
   inbox/outbox tables (decision 9).

9. **Delivery model — inbox-only.** Audit publishes **no integration events**
   at the first gate; there is no downstream consumer of audit data. It
   registers via `AddCommunityOSEventBusWithInbox<AuditDbContext>` (Search
   pattern, ADR-026 decision 11): MassTransit EF Core receive-endpoint inbox
   for exactly-once consume semantics. Outbox tables are provisioned but
   unused; activating them requires an explicit ADR amendment. No outbox
   behavior is introduced merely because infrastructure supports it.

10. **Idempotency.** Exactly-once persistence keyed on `SourceEventHash`
    (unique constraint) computed deterministically at ingest:

    - duplicate delivery of the same broker message → suppressed by the inbox;
    - semantically identical redelivery under a new broker message (same fact)
      → identical hash → insert treated as success no-op — normal broker
      redelivery can never create a duplicate compliance entry;
    - redelivery after transaction failure → inbox retry, ordinary insert path;
    - same logical event re-published with a different `OccurredOn` → different
      hash → a **new distinct occurrence**, persisted as its own entry (this is
      correct: the producer restated the fact);
    - malformed/unmappable event identity → dead-letter queue + error log,
      never a persisted entry.

11. **Organization scoping.** ADR-016 projection pattern. Entries carry the
    payload's primary `OrganizationUnitId`; additional-scope child rows are
    **not** used at the first gate (no producer payload exports multi-scope
    audit facts). Any-of-scope semantics: access succeeds when the caller's
    grant of `audit.entry.read` is effective at the entry's scope (hierarchy
    coverage resolved by the Authorization service per ADR-011). Per ADR-011,
    data-permission grants behave as follows: national/regional/local grants
    cover descendant units via `/covers` resolution; a Global-scope grant of
    `audit.entry.read` matches checks with **no** organization context — which
    is exactly how **null-scoped entries** (global facts such as break-glass
    requests) become visible only to Global-grant holders. Fail-closed: an
    entry whose scope cannot be established is unreadable. Anti-enumeration:
    single reads return `404` for missing and unauthorized alike; list/query
    results are silently filtered and counts exclude inaccessible rows
    (Records/Search pattern). Sensitive second pass: `Sensitive` entries are
    excluded entirely from standard results and require
    `audit.entry.read.sensitive` to appear anywhere (Search sensitive pattern),
    including export.

12. **Permission matrix (ratified; registration happens at the implementation
    gate — not modified in this prompt):**

    | Permission | Purpose | Typical holder |
    |------------|---------|----------------|
    | `audit.entry.read` | Query/list/read non-sensitive audit entries | auditors / national administrators |
    | `audit.entry.read.sensitive` | See `Sensitive` entries (second pass, additive to read) | higher-privilege auditors / privacy officers |
    | `audit.entry.export` | Run capped synchronous exports (sensitive rows additionally require `.read.sensitive`) | auditors / administrators |
    | `audit.entry.admin` | Place/release holds, execute retention purge batches, view restricted diagnostics | operators |

    `admin` does **not** imply `read`: capability separation mirrors the
    records/workflow override pattern; administrative results still require
    `read`. Role seeds (GlobalAdministrator/NationalAdministrator receive the
    matrix at their scopes; LocalAdministrator receives none — audit is a
    national-level compliance function; PlatformService receives none) are
    registered in `PermissionCatalog`/`AuthorizationSeeder` at the Prompt 12C
    gate.

13. **API surface (minimum first-gate compliance surface; six endpoints):**

    | Method | Path | Capability |
    |--------|------|------------|
    | GET | `/api/v1/audit` | `audit.entry.read` |
    | GET | `/api/v1/audit/{id}` | `audit.entry.read` (+`.sensitive` for sensitive rows) |
    | POST | `/api/v1/audit/export` | `audit.entry.export` (+`.read.sensitive` opt-in) |
    | POST | `/api/v1/audit/holds` | `audit.entry.admin` |
    | POST | `/api/v1/audit/holds/{id}/release` | `audit.entry.admin` |
    | POST | `/api/v1/audit/admin/purge-expired` | `audit.entry.admin` |

    Resource/subject convenience endpoints are deliberately omitted — the query
    filter surface covers them. Query supports filters (source service, event
    type/action, resource type/id, subject, actor, organization unit, occurred-on
    date range), fixed ordering by `OccurredOn` descending (ascending opt-in),
    `limit` default 25 / maximum 100 (clamped) with offset paging. Export is
    **synchronous** with a hard cap (10,000 rows) and streams CSV/NDJSON; an
    asynchronous job system is not justified at this scale. Full contract in
    `docs/api/audit.md`.

14. **Retention / archival / legal hold.** Architecture-level behavior:
    retention classes are configuration (code → ISO-8601 duration) applied at
    ingest to compute `RetentionExpiresOn`; the default class retains
    indefinitely until deployment policy defines durations (deployment-specific
    policy is explicitly **not** invented here — no legal requirements are
    asserted). Expiry detection is a periodic sweep over the partial retention
    index; expiry alone never deletes. Purge is two-step: expiry marks
    eligibility; execution requires the `audit.entry.admin` purge endpoint,
    which processes one bounded batch of expired, unheld entries per call,
    writing a purge-marker entry in the same transaction before deleting under
    the trigger guard (`SET LOCAL app.audit_purge_authorized`). Active holds
    block purge regardless of expiry. Audit may place administrative holds on
    its own entries for investigations; placements/releases are journaled.
    Purge is hard row deletion (cryptographic erasure schemes are not justified
    by any ratified requirement). Archival (cold storage export-and-delete) is
    deferred unless a future ADR requires it.

15. **Audit-of-audit.** Explicit first-gate model:

    - Reading/querying audit entries creates **no** entries (recursion/noise
      prevention).
    - Exports create an entry (direct insert, `SourceService = "audit"`):
      actor, filter-criteria summary, row count, format — never row contents.
    - Administrative mutations (hold place/release, purge batch execution)
      create entries (direct insert).
    - Failed authorization attempts against Audit create **no** entries (the
      Authorization check API remains the enforcement record; journaling denials
      would hand attackers a probe oracle inside the very store being guarded).

    Loop prevention: audit-generated entries are inserted directly within the
    handling request's transaction and are never published onto the bus, so no
    consumer feedback loop can form.

16. **Reconciliation / replay.** Unlike Search, Audit never truncates-and-
    rebuilds: a "reindex" would fabricate or destroy history and is forbidden.
    Replay is nevertheless **safe by construction**: because dedupe keys on
    content hash, re-consuming old messages converges to zero new rows. Replay
    uses the ordinary consumption path (operator requeues from the broker);
    there is no replay/rebuild API at the first gate. History that pre-dates
    Audit activation, or that was lost before a producer's outbox gate, cannot
    be reconstructed — this limitation is accepted and mitigated by the
    guaranteed-delivery prerequisites rather than hidden. Schema evolution is
    forward-only: ingest mappings may change for newly consumed messages;
    existing entries are never reinterpreted retroactively.

17. **Database / infrastructure.** Dedicated database `communityos_audit`,
    schema `audit`, PostgreSQL 16 (ADR-006) on the shared instance — zero new
    infrastructure. EF Core + Npgsql following the existing snake_case/schema
    conventions; migrations via the Infrastructure project. Indexes: PK;
    UNIQUE `source_event_hash`; `(occurred_on)`; `(resource_type, resource_id)`;
    `(subject_id)`; `(actor_id)`; `(organization_unit_id)`; `(sensitivity)`;
    partial `(retention_class, retention_expires_on)` WHERE
    `retention_expires_on IS NOT NULL`. MassTransit inbox tables required;
    outbox tables provisioned but unused (decision 9). Immutability triggers
    ship in the initial migration (decision 2).

18. **Security model.** `AuthorizationGuard` over the HTTP evaluator on every
    exposed operation (ADR-009/010/018); fail-closed everywhere; RS256-only JWT
    validation mirroring the Search validation profile; authenticated subject
    required on every endpoint; HTTPS metadata defaults; no
    `[Authorize(Roles = ...)]`; no local RBAC; no direct Authorization database
    access; no cross-service database access (ADR-018); no source-domain
    secrets or PII in payloads or logs (logs carry entry ids, action, outcome,
    source ids only); sensitive second-pass authorization (decisions 11–12);
    404-equivalent anti-enumeration (decision 11).

19. **ADR numbering and documentation deliverables.** This ADR is numbered
    ADR-027 (next free number after ADR-026). Companion documents ratified at
    this gate: `docs/audit.md` (bounded-context design),
    `docs/api/audit.md` (API contract), `docs/runbooks/audit.md` (operational
    runbook for the future implementation). ADR-017 slot 11 status updated from
    NOT STARTED to RATIFIED.

Superseded decisions: none. ADR-027 extends (does not amend) ADR-015/022/023/
024/025/026 constraints into concrete Audit-gate prerequisites and records the
Documents outbox amendment note inside ADR-022.

## ADR-028 — Correspondence bounded context and communication-lifecycle boundary

**Status:** Accepted (ratified at the Prompt 12H architecture/design gate).

### Context

Correspondence is slot 13 in the ratified implementation sequence (ADR-017).
It owns the institutional letter lifecycle of the community administration:
drafting official letters, confirming them, submitting them as formal acts,
materializing the submitted letters as immutable document artifacts, tracking
dispatch and delivery, cancelling before dispatch, and retaining the history.

Existing ratified documents reference Correspondence only as forward
boundaries: `docs/documents.md` states that a correspondence letter's
draft/confirm/submit/track lifecycle stays in Correspondence while Documents
receives materialized immutable versions; `docs/records.md` states Records may
reference submitted letters as evidence but never owns letter lifecycle;
ADR-022 decision 12 reserves correspondence lifecycle, recipient selection and
institutional routing to Correspondence; ADR-021 positions Knowledge before
Correspondence in the sequence; ADR-027 classifies no correspondence events.
The three `correspondence.letter.*` strings inside
`PermissionCatalog.DocumentedExamples` are explicitly non-normative examples
("NOT enforced here") and carry no ratification weight. No Correspondence
source, tests, contracts, database or configuration exists anywhere in the
repository (verified at Prompt 12G): the context is greenfield.

### Decision

Correspondence is implemented as a standard four-project bounded context
(Domain/Application/Infrastructure/API) following the established house
patterns: PostgreSQL 16 on the shared instance, EF Core + Npgsql with
snake_case naming and a dedicated schema, MassTransit over RabbitMQ via
`CommunityOS.EventBus`, MediatR CQRS with FluentValidation pipeline,
AuthorizationGuard over the Authorization check API, RS256-only JWT, Serilog,
ASP.NET versioning, Testcontainers integration tests. The ratified decisions
follow.

1. **Ownership model.** Correspondence owns: the letter aggregate (identity,
   human reference number, subject, category code, sensitivity classification,
   organization scope), letter content while it is a working record (draft
   bodies and the retained submitted text), recipients as structured
   references plus captured external addressing snapshots, the submission /
   dispatch / delivery state machine and its full transition history, manual
   dispatch and delivery-outcome recordings, external-provider references
   (future), attachments as references to existing Documents artifacts, the
   document-materialization correlation state, the template registry,
   retention state and legal holds on letters, and its own export activity
   journal. Correspondence does **not** own: person or household facts
   (Community), organization-unit facts (Organization), document storage,
   object storage or immutable document versions (Documents — the materialized
   letter version is a Documents artifact from birth), official-record
   registration (Records — Records may reference submitted letters as
   evidence), task/approval workflow (Workflow), notification delivery
   (Notifications), search indices (Search), the compliance journal (Audit),
   AI behavior (no first-gate role). The ratified rule from ADR-022 decision
   12 is preserved verbatim: **Documents never owns the correspondence letter
   lifecycle**, and symmetrically Correspondence never owns document storage
   or version immutability mechanics.

2. **Source-of-truth table.**

   | Fact | Owner | Consumers | Notes |
   |------|-------|-----------|-------|
   | Letter identity & reference number | Correspondence | Audit, Records (evidence refs), Search (deferred) | per-unit yearly sequence, unique |
   | Letter metadata (subject, category, sensitivity, scope) | Correspondence | Audit, Search (deferred) | subject may contain personal names — never exported to events |
   | Recipient identity (person/unit ids) | Community / Organization own the entities; Correspondence owns the *reference* on the letter | Audit (ids only) | resolved at read time through owning APIs |
   | External recipient addressing snapshot | Correspondence | none (never leaves the row except into the materialized artifact) | only for external-kind recipients |
   | Letter content (body) | Correspondence (working + retained copy); Documents owns the immutable materialized version artifact | readers via Correspondence API | duplication is deliberate: working record vs archival artifact |
   | Letter version (submitted snapshot) | Documents (immutable version) with Correspondence holding the DocumentReference | Records evidence, future consumers | one immutable version per submission |
   | Submission act | Correspondence | Audit, Records | compliance core fact |
   | Dispatch recording | Correspondence (manual first gate; provider refs future) | Audit | provider truth once providers exist |
   | Delivery result | Correspondence | Audit | reason codes fixed vocabulary |
   | Approval step | Correspondence (built-in confirm step) | — | no Workflow delegation at this gate |
   | Notification about letters | none at this gate (deferred) | — | Notifications would consume Correspondence events in a future gate |
   | Audit record of letter facts | Audit | auditors | Correspondence remains source of truth for state; Audit is the append-only historical journal |
   | Search representation | none at this gate (Search indexing deferred) | — | requires future ADR-026 amendment |

3. **Letter lifecycle (deterministic state machine).** States:
   `Draft → Confirmed → Submitted → Materialized → Dispatched → Delivered`
   with terminal branches `DeliveryFailed` and `Cancelled`. Legal
   transitions:

   | From | To | Caused by | Actor |
   |------|----|-----------|-------|
   | — | Draft | create command | authorized user |
   | Draft | Confirmed | confirm command | `correspondence.letter.update` |
   | Confirmed | Draft | unconfirm command | `correspondence.letter.update` |
   | Draft/Confirmed | Cancelled | cancel command (reason code) | `correspondence.letter.cancel` |
   | Confirmed | Submitted | submit command (publishes `LetterSubmitted`, assigns reference number) | `correspondence.letter.submit` |
   | Submitted | Materialized | consumption of the correlated materialization event (`DocumentVersionAdded` for this letter) | system |
   | Submitted/Materialized | Cancelled | cancel command pre-dispatch (reason code) | `correspondence.letter.cancel` |
   | Materialized | Dispatched | manual dispatch recording (method code `manual` at first gate; provider codes later) | `correspondence.letter.admin` |
   | Dispatched | Delivered | delivery outcome recording (confirmed) | `correspondence.letter.admin` |
   | Dispatched | DeliveryFailed | delivery outcome recording (failed + reason code) | `correspondence.letter.admin` |

   Invalid transitions throw domain exceptions surfaced as `409 Conflict`.
   Every transition appends an immutable row to `letter_status_history`
   (from, to, actor or system cause, optional reason code, timestamp) — the
   lifecycle is fully retained locally and therefore auditable without
   reconstruction. Content edits are permitted **only** in `Draft`; each save
   bumps an optimistic-concurrency revision. Sensitivity classification is set
   at creation and editable only while `Draft`. Submitted content is
   immutable: corrections after submission are new letters (optionally linked
   via `RelatedLetterId`), mirroring the Records "corrections arrive as new
   records" philosophy. Commands are user-driven HTTP commands; the
   Draft→…→Materialized edge is the sole integration-event reaction;
   provider-callback reactions are future.

4. **Letter numbering.** Each issuing unit owns a yearly sequence:
   `UNIQUE (organization_unit_id, letter_year, letter_sequence)` with
   sequence allocated transactionally at submit; the API composes the display
   reference `{letter_year}-{sequence:D5}` scoped by unit. Numbers are never
   reused, never renumbered, and survive cancellation (cancelled numbers are
   consumed).

5. **Recipients and addressing.** A letter carries one primary recipient
   kind: `person` (Community person id), `unit` (Organization unit id), or
   `external` (captured display/address lines). Person and unit recipients are
   stored as stable identifiers only and resolved at read time through the
   owning services' APIs; free-text display lines are captured **only** for
   `external` recipients (bounded length). Multiple recipients per letter are
   supported (bounded list). Recipient data never appears in logs, published
   events or exports beyond what §9 permits.

6. **Templates.** Correspondence owns a minimal template registry (unique
   code, title, subject/body skeletons, category code, active flag).
   Templates are mutable while referenced by no submitted letter; creating a
   letter from a template copies (snapshots) the skeleton into the draft, so
   later template edits never mutate existing letters. Deactivation is soft.
   No template versioning tables at this gate.

7. **Documents integration (materialization loop).** Submission is an
   event-driven handshake:

   - `POST .../submit` validates the confirmed letter, allocates the reference
     number, persists `Submitted` status + history, journals nothing externally
     yet, and publishes `LetterSubmitted` **through the transactional outbox**
     in the same database transaction (atomicity, ADR-015).
   - A future Documents-side consumer (recorded as the ADR-022 amendment
     requirement below) consumes `LetterSubmitted` and materializes the
     letter's content and recipient snapshot as a new immutable document
     version owned by Documents, publishing the existing
     `DocumentVersionAdded` fact enriched with two new optional fields
     (`SourceContext = "correspondence"`, `SourceEntityId = letterId`) — a
     **future contract addition** identified here, not created in this prompt.
   - Correspondence consumes that correlated `DocumentVersionAdded`, records
     the DocumentReference `(DocumentId, VersionNumber)` in
     `letter_documents`, and transitions the letter to `Materialized`.
   - **Submission itself succeeds independently of materialization; dispatch
     is blocked until the immutable version exists.** A letter stranded in
     `Submitted` past the operator-configured warn threshold is visible to the
     runbook query and repairable via the admin reconciliation endpoint, which
     re-publishes `LetterSubmitted` (safe: Documents-side materialization is
     idempotent per `(SourceContext, SourceEntityId)`).
   - If materialization permanently fails, the letter remains `Submitted`,
     the failure surfaces in the reconciliation listing, and no dispatch path
     exists until repaired. The submission fact is never lost or rewritten.
   - Data crossing the boundary toward Documents: letter id, reference number,
     subject, body text, category/sensitivity codes, organization unit,
     recipient snapshot (ids; display lines only for external recipients),
     submitter id, occurred-on. No credentials, no provider data.
   - Documents remains the sole source of truth for the resulting immutable
     document and version; Correspondence stores only the DocumentReference.
   - Correspondence consumes exactly one Documents event at this gate:
     the correlated `DocumentVersionAdded`. No Correspondence→Documents
     event consumption exists for Documents to subscribe to at this gate;
     whether Documents ever consumes `LetterSubmitted`-derived facts beyond
     materialization is decided by the ADR-022 amendment, not assumed here.

8. **Producer reliability gates (binding).** Mirroring the ADR-027 pattern:

   - **Correspondence outbox (born-with-outbox):** every published
     Correspondence event (`LetterSubmitted`, `LetterDispatched`,
     `LetterDeliveryConfirmed`, `LetterDeliveryFailed`, `LetterCancelled`) is
     transactional-outbox protected from day one, because guaranteed-delivery
     consumers (Audit at minimum) exist by design. Registration uses
     `AddCommunityOSEventBusWithOutbox<CorrespondenceDbContext>`.
   - **Documents outbox gate:** because the materialization loop depends on
     reliably receiving `DocumentVersionAdded`, **Correspondence MUST NOT
     register any Documents-event consumer until Documents upgrades its
     publication to the transactional outbox** (the ADR-022 amendment note
     already anticipates this upgrade; `documents.md` marks it "required
     before Correspondence is implemented"). Until that producer gate
     completes, the `Submitted → Materialized` edge cannot ship; the
     implementation prompt must either complete the Documents outbox upgrade
     first or stage the lifecycle behind the gate exactly as ADR-027 staged
     gated events. This gate is a producer-side change; it is **not** executed
     in this prompt.
   - **Authorization events:** no Authorization-event consumption is
     contemplated; the symmetric Authorization outbox gate (ADR-027 decision
     4) therefore does not apply to Correspondence at this gate.

9. **Integration-event catalog.** Exhaustive classification of every
   contract-relevant event for this context:

   **A. Consumed at first gate (projection-only, 3):**
   Organization `OrganizationUnitCreated`, `OrganizationUnitUpdated`,
   `OrganizationUnitParentChanged` → `organization_unit_references`
   (ADR-016 scoping infrastructure; never journaled as letters). Inbox-
   protected like all consumption.

   **B. Consumed only after the Documents producer reliability gate (1):**
   Documents `DocumentVersionAdded` **with the ratified contract extension**
   (`SourceContext`, `SourceEntityId`) — filtered to
   `SourceContext == "correspondence"`; closes the materialization loop
   (decision 7). The contract extension is a future contract addition; until
   it ships together with the Documents outbox, this consumer must not be
   registered.

   **C. Projection-only:** the Organization trio above (same list; no other
   projection sources).

   **D. Published by Correspondence (5, all outbox-protected):**

   | Event | Required fields (all scalar/codes/ids) | Privacy | Delivery | Idempotency | Audit consumes? | Search consumes? |
   |-------|----------------------------------------|---------|----------|-------------|-----------------|------------------|
   | `LetterSubmitted` | LetterId, LetterNumber(Year+Sequence), OrganizationUnitId, CategoryCode, Sensitivity, RecipientCount, RecipientPersonIds[], RecipientUnitIds[], SubmittedBy, OccurredOn | ids/codes only — never subject/body/display lines | outbox (guaranteed) | dedup on LetterId (single submission per letter) | **Yes** (compliance core) | deferred |
   | `LetterDispatched` | LetterId, LetterNumber, OrganizationUnitId, MethodCode, DispatchedBy, OccurredOn | ids/codes only | outbox | one per dispatch attempt epoch | **Yes** | deferred |
   | `LetterDeliveryConfirmed` | LetterId, LetterNumber, OrganizationUnitId, MethodCode, ConfirmedBy, OccurredOn | ids/codes only | outbox | dedupe per letter terminal state | **Yes** | deferred |
   | `LetterDeliveryFailed` | LetterId, LetterNumber, OrganizationUnitId, MethodCode, ReasonCode, OccurredOn | ids/codes only | outbox | dedupe per letter terminal state | **Yes** | deferred |
   | `LetterCancelled` | LetterId, LetterNumber, OrganizationUnitId, CancelledBy, ReasonCode, OccurredOn | ids/codes only | outbox | single cancellation per letter | **Yes** | deferred |

   **E. Deferred (not subscribed, not published at this gate):**
   template lifecycle events, recipient-change events, read receipts,
   letter-status notifications (a future Notifications-consumes-
   Correspondence direction), `DocumentDeactivated`/`DocumentRestored`
   interplay, Workflow task linkage, provider-specific telemetry.

   **F. Explicitly NOT integration events:** draft creation/edit/confirm/
   unconfirm (local commands), queries, exports (internally journalled as
   `export_activity` rows instead — actor, filter summary, row count, format;
   never letter contents), hold place/release (internal rows +
   `letter_status_history`), purge executions (internal tombstone history
   row), provider callbacks (internal commands with provider-reference
   idempotency).

10. **Audit integration (future ADR-027 amendment).** The five published
    events in decision 9D constitute the correspondence compliance subset:
    Audit **must eventually consume all five** as first-gate-style entries
    (source service `correspondence`). Because Correspondence is born with a
    transactional outbox, these enter ADR-027's catalog as
    **CONSUMED AND PERSISTED — gated only on Correspondence implementation
    itself** (no additional producer reliability gate is required — unlike
    the Documents/Authorization gates). Field-level ingest mappings follow the
    ADR-027 model: stable ids, codes, counts, timestamps, actor/recipient
    **identifiers**; allowlisted scalar metadata only; sensitivity
    `Normal` for all five at this gate (letters classified Sensitive remain
    Normal-sensitivity audit entries — the audit fact is that a letter was
    submitted/dispatched/etc., not its content). Operational/internal facts
    (drafts, edits, confirms, template changes, holds, purges, exports)
    produce **no** audit entries at this gate. Events carrying PII risk
    (anything containing subject/body/display lines) do not exist in the
    catalog by construction — nothing needs excluding. This decision is
    recorded here as the required **future amendment to ADR-027** (extend the
    consumed-and-persisted catalog with the five correspondence events when
    the Correspondence implementation gate lands); ADR-027 itself is not
    modified in this prompt.

11. **Outbox/inbox/delivery guarantees.** Follows ADR-004/015/022 patterns:
    MassTransit EF Core transactional outbox for all publications
    (decision 8); receive-endpoint inbox for all consumption including the
    projection trio; retry via default bus retry policy; poison/unmappable
    messages dead-letter with error logs; handler-level natural-key guards
    (submission uniqueness per letter, single materialization link per
    `(letter, document)`, single cancellation) make redelivery convergent;
    reconciliation is operator-triggered re-publication (decision 7) — there
    is **no truncate-and-rebuild** semantics anywhere (consistent with ADR-027
    decision 16); replay of old events converges to zero side effects because
    every consumer action is keyed on natural identities.

12. **Database and persistence.** Dedicated database `communityos_correspondence`,
    schema `correspondence`, PostgreSQL 16 on the shared instance (ADR-006),
    EF Core + Npgsql snake_case conventions, migrations in Infrastructure.
    Tables:

    - `letters` — PK `id`; `letter_year`, `letter_sequence`;
      `UNIQUE (organization_unit_id, letter_year, letter_sequence)`;
      `subject` (≤200), `category_code` (≤50), `sensitivity` (normal|sensitive),
      `status`, `body` (text, nullable only pre-first-save),
      `related_letter_id` (nullable), `template_id` (nullable, soft ref),
      `revision` (concurrency token), `created_by`, `created_on`,
      `submitted_by`, `submitted_on`, `organization_unit_id`; indexes:
      `(organization_unit_id)`, `(status)`, `(sensitivity)`,
      `(category_code)`, partial `(retention_expires_on)` composite with
      `retention_class`.
    - `letter_recipients` — PK; FK `letter_id`; `kind` (person|unit|external);
      `person_id` / `unit_id` (nullable, exclusive by kind check constraint);
      `display_line` (≤500, external only); index `(letter_id)`.
    - `letter_status_history` — append-only; FK `letter_id`; `from_status`,
      `to_status`, `changed_by` (nullable for system), `cause`
      (command|event|provider|purge-marker), `reason_code` (nullable),
      `changed_on`; index `(letter_id, changed_on)`.
    - `letter_documents` — materialization links; FK `letter_id`;
      `document_id`, `version_number`, `content_hash`, `materialized_on`;
      `UNIQUE (document_id)` and `UNIQUE (letter_id, document_id)`.
    - `letter_attachments` — references to pre-existing Documents artifacts;
      FK `letter_id`; `document_id`, `reference_type`; unique
      `(letter_id, document_id, reference_type)` (ADR-022 tuple shape).
    - `letter_delivery_records` — dispatch/outcome recordings; FK `letter_id`;
      `method_code`, `outcome` (dispatched|confirmed|failed),
      `reason_code` (nullable), `actor_id`, `occurred_on`,
      `provider_reference` (nullable, future); index `(letter_id)`.
    - `templates` — PK; `code` UNIQUE (≤50), `title`, `subject_template`,
      `body_template`, `category_code`, `is_active`, timestamps.
    - `letter_holds` — mirrors the ratified hold shape (Audit pattern):
      `entry_id`=letter FK, `hold_type` (legal|administrative), `placed_by`,
      `placed_on`, nullable `released_by`/`released_on`, `reason_code` from
      fixed set {investigation, legal-request, dispute, regulatory-inquiry,
      other}; partial index on active holds.
    - `export_activity` — internal export journal (actor, filter summary ≤200,
      row count, format, occurred-on).
    - `organization_unit_references` (ADR-016 projection) and MassTransit
      inbox/outbox tables.

    Concurrency: optimistic via `revision` on letters; unique constraints
    back every idempotency claim; no soft-delete of letters (cancellation is
    the terminal state; retention purge is hard delete under holds, §13).
    **AdditionalScopes: not adopted** — letters carry exactly one primary
    organization scope; null-scope is reserved and treated as global-only
    visibility (ADR-011 global-grant semantics). `OrganizationUnitReference`
    projection: adopted (required for scope display and future hierarchy
    needs). Audit-related reference fields on letters: none (Audit references
    letters; not vice versa).

13. **Retention and legal holds.** Retention classes are configuration
    (class code → ISO-8601 duration; empty/missing = indefinite), computed at
    submit into `retention_expires_on`; deployment policy sets actual
    durations (none invented here). Expiry alone never deletes. Purge is
    operator-driven, batch-bounded, excludes actively-held letters, writes a
    tombstone row into `letter_status_history` (`cause=purge-marker`)
    transactionally before deleting the batch, and runs under the same
    SET LOCAL trigger-guard pattern ratified for Audit (native triggers reject
    UPDATE/DELETE on `letters` unless `app.correspondence_purge_authorized='on'`
    is set session-locally by the purge operation). Holds place/release are
    admin capabilities; placements/releases append history rows. History,
    holds, templates and delivery records never expire.

14. **Privacy, PII and logging prohibitions.** Persisted by design: ids,
    codes, subject line (working record only), body text (working + retained
    copy), recipient identifiers, external display lines, statuses,
    timestamps, provider references (future). Never logged (structured logs
    carry letter id, status codes, category/sensitivity codes, counts, actor
    and provider reference ids only): letter bodies, subjects, recipient
    names, addresses, email addresses, phone numbers, credentials, tokens,
    provider secrets. Never published onto the bus: bodies, subjects, display
    lines, addresses — events carry identifiers and codes exclusively
    (decision 9D). What Audit may retain: the identifier/code surface defined
    by the ingest mappings (§10) and nothing more. What Search may index:
    nothing at this gate (§14 posture below). Export restrictions: the export
    endpoint emits a capped metadata/index stream (ids, numbers, codes,
    statuses, timestamps, recipient kinds/counts — **never bodies, subjects
    or external display lines**) and requires the export capability plus the
    sensitive second pass when sensitive letters are included. Encryption: at
    rest via the deployment's PostgreSQL storage policy and TLS in transit;
    no application-layer field encryption at this gate (ratified explicitly —
    no legal requirement is asserted, mirroring ADR-027 decision 14 restraint).
    Anonymization/deletion: retention purge (hard delete) is the only removal
    path; no anonymization scheme at this gate.

15. **Authorization matrix (ratified; registered at the implementation gate —
    not modified in this prompt).** Exact-match ordinal membership semantics;
    no capability implies another unless stated; administrative capability
    does **not** imply read (hold placement additionally requires read-level
    visibility of every target letter, mirroring ADR-027 decision 12):

    | Permission | Purpose |
    |------------|---------|
    | `correspondence.letter.read` | Query/list/read non-sensitive letters |
    | `correspondence.letter.read.sensitive` | Second pass for `Sensitive` letters (additive to read) |
    | `correspondence.letter.create` | Create drafts |
    | `correspondence.letter.update` | Edit drafts, confirm/unconfirm |
    | `correspondence.letter.submit` | Submit letters |
    | `correspondence.letter.cancel` | Cancel pre-dispatch |
    | `correspondence.letter.export` | Run capped metadata exports |
    | `correspondence.letter.admin` | Manual dispatch/delivery recording, holds, retention purge, materialization reconciliation |
    | `correspondence.template.read` | List/get templates |
    | `correspondence.template.manage` | Create/update/deactivate templates |

    Role seeds (registered in `PermissionCatalog`/`AuthorizationSeeder` at the
    implementation gate): **GlobalAdministrator** — all ten, global scope;
    **NationalAdministrator** — all ten, national scope; **LocalAdministrator**
    — `read`, `create`, `update`, `submit`, `cancel`, `template.read` at local
    scope (communities author their own outgoing letters; export/admin/
    sensitive-read/template-management are national functions); **Volunteer**
    and **Guest** — none. Scoping: letters carry the issuing unit's scope;
    access follows ADR-011 any-of/hierarchy semantics resolved by the
    Authorization service; fail-closed everywhere; uniform `404` for missing
    and unauthorized single reads (anti-enumeration); list results silently
    filtered; sensitive letters absent entirely unless the caller opted in
    with the second-pass capability.

16. **API surface (v1; full contract in `docs/api/correspondence.md`).**
    Route root `/api/v{version}/correspondence`. Endpoints (each annotated
    with its capability):

    | Method | Path | Capability | Success |
    |--------|------|------------|---------|
    | GET | `/letters` | read (+`.sensitive` opt-in) | 200 page |
    | GET | `/letters/{id}` | read (+`.sensitive` for sensitive) | 200 / 404 anti-enumerated |
    | POST | `/letters` | create | 201 |
    | PUT | `/letters/{id}/content` | update | 200 (optimistic `expectedRevision`) |
    | POST | `/letters/{id}/confirm` | update | 200 |
    | POST | `/letters/{id}/unconfirm` | update | 200 |
    | POST | `/letters/{id}/submit` | submit | **202 Accepted** (state `Submitted`; materialization asynchronous) |
    | POST | `/letters/{id}/cancel` | cancel | 200 |
    | POST | `/letters/{id}/dispatch` | admin | 200 (manual method first gate) |
    | POST | `/letters/{id}/delivery` | admin | 200 (confirmed / failed+reason) |
    | GET | `/letters/{id}/history` | read (+second pass) | 200 |
    | POST | `/letters/export` | export (+second pass opt-in) | 200 streamed CSV index |
    | GET | `/templates` | template.read | 200 |
    | POST | `/templates` | template.manage | 201 |
    | PUT | `/templates/{id}` | template.manage | 200 |
    | DELETE | `/templates/{id}` | template.manage | 204 (deactivate) |
    | POST | `/admin/reconcile-materializations` | admin | 200 report |
    | POST | `/admin/holds` | admin (+read visibility of targets) | 201 |
    | POST | `/admin/holds/{id}/release` | admin | 200 |
    | POST | `/admin/purge-expired` | admin | 200 |

    Query filters (status, category, sensitivity opt-in, organization unit,
    date range, free-text **reference-number** only — no body/subject search),
    ordering by submitted-on/created-on ascending or descending (deterministic
    tiebreak by id), `limit` default 25 clamped to max 100, offset paging.
    Validation via FluentValidation pipeline → `400` ProblemDetails-shaped
    errors; illegal transitions → `409`; DTO-only responses (EF entities never
    exposed); camelCase JSON; Swagger development-only.

17. **External delivery providers.** First gate supports **manual dispatch
    recording only** (an authorized administrator records that a letter was
    dispatched by conventional means, with a method code `manual` and optional
    reference). Email/postal/SMS provider integrations are **deferred**: the
    boundary is the `IDeliveryProvider` seam (future) — provider adapters
    translate provider callbacks into the same internal delivery-recording
    commands with provider-reference idempotency; internally owned state
    remains the lifecycle statuses and delivery records defined here; provider
    data storage limited to opaque provider references and status/reason
    codes; no SDKs, no HTTP clients, no background pollers in this gate.

18. **Workflow and notifications.** Approval stays **inside** Correspondence
    as the built-in Confirm step; no Workflow task delegation, no hidden
    workflow subsystem. Notifications: no integration in either direction at
    this gate; a future gate may let Notifications consume
    `LetterDispatched`/`LetterDeliveryFailed` to notify authors — recorded as
    deferred, undecided here.

19. **Search.** Letters are **not indexed at this gate**; the searchable
    surface of Correspondence is nil. Rationale: letter subjects/bodies are
    precisely the PII-bearing artifacts the platform keeps out of projections,
    and ADR-026's sensitivity model would require a dedicated classification
    design. Any future indexing requires an explicit **ADR-026 amendment**
    (searchable source type, field allowlist, sensitivity mapping) plus a
    Search-side consumer — recorded here as the future-amendment requirement;
    ADR-026 is not modified in this prompt.

20. **AI.** No first-gate role. Correspondence is fully functional without
    AI. ADR-021's boundary rules apply should AI assistance ever be proposed
    (suggestions only, never authoritative letters); no AI infrastructure is
    introduced by this gate. (Note: ADR-022 decision 12 already assigns "AI
    letter drafting" questions to Correspondence's future discretion, not
    Documents'.)

21. **Reconciliation / replay / duplicates.** Covered by decisions 7 and 11:
    operator-triggered materialization re-request; inbox exactly-once +
    natural-key guards for broker redelivery; duplicated provider callbacks
    (future) deduped on provider reference; restated facts converge; no
    rebuild semantics; forward-only schema evolution.

22. **Test strategy (for the implementation gate; nothing created now).**
    Domain: state-machine legality table exhaustively tested, invalid
    transitions rejected, number allocation/uniqueness/retry, recipient-kind
    exclusivity, hold release semantics, sensitivity immutability post-submit.
    Application: validator coverage per command; authorization matrix per
    capability × role × scope incl. admin-doesn't-imply-read; pagination
    clamp/export cap semantics; optimistic-concurrency conflicts.
    Infrastructure: event mapper purity and allowlisted-metadata conformance
    (mirroring Audit mapper tests); inbox/outbox handlers; materialization
    correlation filtering; purge guard/tombstone transactionality.
    Persistence (Testcontainers, compile-only where Docker unavailable —
    repository-wide established limitation): schema/migration creation,
    triggers, unique constraints, idempotent ingestion, hold-exempt purge,
    released-hold purge progression. Privacy/log-scrub tests asserting the
    §14 prohibitions. Provider-seam tests for the manual dispatcher.

23. **Documentation deliverables and implementation prerequisites.**
    Companion documents ratified at this gate: `docs/correspondence.md`
    (bounded-context design), `docs/api/correspondence.md` (API contract),
    `docs/runbooks/correspondence.md` (operational runbook) — each headed
    `STATUS: RATIFIED — NOT IMPLEMENTED (Prompt 12H)`. Implementation
    prerequisites recorded for the next gates: (a) the **Documents
    transactional-outbox upgrade** plus the `DocumentVersionAdded` contract
    extension (decision 7/8) — producer-side work; (b) registration of the
    ten permissions and role seeds (decision 15); (c) docker-init database
    entry; (d) ADR-027 catalog amendment consuming the five letter events
    (decision 10). None of these are executed in this prompt.

Future amendments explicitly identified by this ADR (to be executed at their
own gates, not silently): **ADR-022** — Documents consumes `LetterSubmitted`
and materializes correspondence letters; `DocumentVersionAdded` gains
`SourceContext`/`SourceEntityId`. **ADR-026** — if letters are ever indexed.
**ADR-027** — extend the consumed-and-persisted catalog with the five
correspondence events at the Correspondence implementation gate.

Superseded decisions: none. ADR-028 extends (does not amend) ADR-004/006/009/
010/011/015/016/017/020/021/022/023/024/025/026/027 constraints into concrete
Correspondence-gate decisions and preserves every forward-boundary statement
found in `docs/documents.md`, `docs/records.md` and ADR-021/022 as ratified
constraints.
