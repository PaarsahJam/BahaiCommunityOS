# Documents Service

> **STATUS: RATIFIED (Prompt 07A-R).** The architectural decisions for the
> Documents bounded context have been ratified in ADR-022 (Accepted) and
> recorded in this document. The service itself is **not yet implemented**;
> implementation proceeds in Prompt 07B. Decisions are binding unless a later
> ratified ADR amends them.

The Documents bounded context owns document **artifacts** and their metadata:
the named, versioned, immutable binary content that other bounded contexts
reference but never store (`ADR-022`, position 6 of `ADR-017`). Documents is
the foundational artifact service for Records, Workflow, Correspondence,
Finance and Administration/Ticketing. It is not — and must never become — the
owner of official records, workflow state, correspondence lifecycle, financial
meaning, organizational hierarchy, member identity, authorization policy,
notification delivery or AI decisions.

## What a "Document" is (and is not)

**Document** — the aggregate root. A named, ordered, append-only sequence of
immutable content snapshots (*versions*) plus the metadata that governs their
security, classification, organization scope, retention and lifecycle.

| Concept | Owned by | Relationship to Documents |
|---------|----------|---------------------------|
| **Document artifact** | Documents | The bytes and immutable version records. |
| **Official Record** | Records (future) | Records *references* documents as evidence/attachments via `DocumentReference`. Documents never owns record lifecycle, retention schedules or holds. |
| **Workflow attachment** | Workflow (future) | Workflow *references* documents as artifacts under review. Documents never owns workflow/task state. |
| **Correspondence letter** | Correspondence (future) | Correspondence *materializes* its submitted letters as immutable document versions and references them. The draft/confirm/submit/track lifecycle stays in Correspondence. |
| **Knowledge passage / source** | Knowledge | Knowledge remains the authority for Library passages and citation text; it may reference a source document (e.g. a scanned tablet). Documents never becomes the Knowledge authority. |
| **Financial document** | Finance (future) | Finance references invoices/receipts/supporting documents. Documents never attaches financial meaning. |
| **Ticket attachment** | Administration/Ticketing (future) | Tickets attach documents. Documents never owns ticket lifecycle. |

Documents carries **retention/hold references** and enforces a
deactivation-protection rule, but it never invents retention schedules or legal
policy — those belong to Records and the ratified classification model.

## Model

### Document

- **Identity** — `Guid Id`, stable, referenced by every other context.
- **Title / Description** — mutable metadata.
- **Lifecycle** — `Draft → Active → Archived | Deactivated`.
  - `Draft` — created, content not yet required.
  - `Active` — has at least one version and is in normal use (default when the
    first version is uploaded).
  - `Archived` — read-only retention; content remains downloadable by
    authorized readers; no new versions, no metadata edits except holds.
  - `Deactivated` — soft-deleted; content not downloadable; preserved for
    compliance. Reversible only by `documents.document.restore` (or an
    administrative override). Never hard-deleted.
- **Ownership** — `OwnerReference` (value object): `OwnerType` (`Person` |
  `OrganizationUnit`) and `OwnerId`. The governing principal of the document.
- **Organization scope** — `OrganizationUnitId` (primary scope, nullable
  reference) plus an optional set of additional scopes
  (`DocumentOrganizationScope`). A document may belong to multiple organization
  scopes. Person-owned documents carry no organization scope.
- **Security metadata** — see Classification below.
- **CurrentVersionId** — pointer to the current `DocumentVersion`.
- **Creator / updater** — `CreatedBy`/`UpdatedBy` (stable person ids resolved
  through the Community API; never names/PII stored locally).

### DocumentVersion (entity)

Immutable content snapshot. **Every field is immutable once written except
`ScanStatus`.**

- **Identity** — `Guid Id`.
- **VersionNumber** — 1-based, monotonically increasing per document, assigned
  by the service (never client-supplied).
- **ContentHash** — SHA-256 of the bytes (hex).
- **ObjectKey** — content-addressed storage key `documents/{sha256}`.
- **MimeType / SizeBytes / FileName** — content descriptors.
- **UploadedBy / UploadedOn** — creator reference and UTC timestamp.
- **Source** — `member` | `system` | `import`.
- **ScanStatus** — `NotScanned | Quarantined | Scanning | Clean | Rejected`
  (the only mutable field; see Malware / Content safety).

### DocumentClassificationMetadata

Security metadata carried by every document. Designed so the ratified
Data Classification Model (`specifications/06`, currently PLACEHOLDER) can be
consumed **without schema redesign**: classification is stored as a string
code, not a fixed enum.

- **ClassificationCode** — nullable string reserved for the ratified
  classification level code. No fixed allowed set until the model is ratified.
- **IsSensitive** — `bool` (default `false`). Operational gate: when `true`,
  content downloads require `documents.document.content.read.sensitive` and
  emit the `DocumentContentDownloaded` audit event (ratified). This is an
  operational primitive, not a classification level.
- **RetentionCategory** — nullable string reference to a future Records
  retention category. Documents does not enforce schedules.
- **LegalHoldReference / AdministrativeHoldReference** — nullable references to
  future hold records in Records. When set, deactivation is protected (see
  Retention and legal hold).
- **ClassifiedBy / ClassifiedOn** — audit provenance for classification changes.

### DocumentOrganizationScope (entity)

`(DocumentId, OrganizationUnitId)` rows for additional organization scopes.
Primary scope lives on `Document.OrganizationUnitId`. Unit ids are references
to the Organization read-model projection — never FKs into the Organization
database (`ADR-016`).

### DocumentReference (entity)

The attachment/evidence mechanism for all other contexts.

- **Identity** — `Guid Id`.
- **DocumentId** — the referenced document.
- **SourceContext** — e.g. `workflow.task`, `records.record`,
  `correspondence.letter`, `finance.invoice`, `ticket`.
- **SourceEntityId** — stable id of the referencing entity.
- **ReferenceType** — e.g. `attachment`, `evidence`, `letter`, `receipt`.
- **CreatedBy / CreatedOn** — provenance.
- Unique per `(SourceContext, SourceEntityId, DocumentId, ReferenceType)`.

The lifecycle of the referencing entity stays with its owning context; the
reference is a Documents-side convenience for enumerating what a document is
attached to.

### OrganizationUnitReference (read model)

Documents mirrors the Community/Knowledge pattern: it consumes
`OrganizationUnitCreated/Updated/ParentChanged` into
`organization_unit_references` so document scoping never depends on a live
Organization query (`ADR-016`). It never introduces a second organizational
hierarchy and never influences authorization decisions (authorization is
resolved live by the Authorization service).

## Key rules

- A document version is **immutable**. A new upload creates a new version and
  moves the current-version pointer; existing versions are never mutated
  (scan status is the only allowed transition).
- **Metadata changes do not create versions.** Only binary content creates
  versions. Metadata (title, description, classification, scope, retention,
  holds) mutates on the document and is audited through events. The immutable
  artifact is the versioned bytes; the governed state is metadata.
- A document may belong to **multiple organization scopes**; access is granted
  when the caller holds the permission at **any** of the document's scopes.
- **Deletion is never hard deletion** during normal operation. `Deactivated`
  preserves content; restoration is a first-class operation.
- Content and metadata access are **separate capabilities**; reading metadata
  never implies reading bytes.
- Duplicate upload of the same content for the same document returns the
  existing version (idempotent), never a duplicate row.
- Versions reference the content-addressed object key `documents/{sha256}`;
  the database is the authority for the version → object mapping.
- **Binary content never passes through the API as a payload.** Uploads arrive
  as `multipart/form-data` streams; downloads are streamed byte responses.
  Binary content never appears in a JSON request/response, an integration
  event, a log line, a search index, or an AI pipeline input.
- **Unauthorized enumeration is prevented.** List/search returns only
  documents the caller may read (fail-closed filtering at the query boundary);
  `GET /documents/{id}` returns `404` for both *missing* and *not readable*
  documents — no existence oracle is exposed to callers who lack read access.
- **Resource-level authorization is explicit.** Every guarded operation passes
  `resourceType = "document"` and the document id as resource context to the
  Authorization check API, so per-document grants (Authorization relationship
  tuples) and organization-scoped grants compose exactly as in
  Community/Organization/Knowledge.

## Privacy and permissions

Documents content can be highly sensitive. Security metadata follows the
convention below; every guarded operation is evaluated by the Authorization
service through `AuthorizationGuard` (fail-closed). No `[Authorize(Roles =
"...")]`, no local RBAC, no direct Authorization database access
(`ADR-009/018/019`).

### Permission matrix (ratified)

| Permission | Purpose | Typical holder |
|------------|---------|----------------|
| `documents.document.create` | Create a document (metadata, optionally with initial content) | members / services |
| `documents.document.read` | List and read metadata (never content) | all authorized |
| `documents.document.metadata.update` | Update non-security metadata (title, description) | owner / editors |
| `documents.document.scope.manage` | Add/remove organization scopes (changes effective audience) | administrators |
| `documents.document.content.read` | Download content of non-sensitive documents | authorized readers |
| `documents.document.content.read.sensitive` | Download content of sensitive documents | higher-privilege readers |
| `documents.document.version.create` | Upload a new version | editors / services |
| `documents.document.classify` | Set classification code, sensitive flag, retention category, hold references | classifiers / Records service |
| `documents.document.archive` | Transition to `Archived` | administrators |
| `documents.document.deactivate` | Transition to `Deactivated` (soft-delete) | administrators |
| `documents.document.restore` | Restore from `Archived`/`Deactivated` | administrators |
| `documents.document.reference` | Create a document reference (attach) | services / members |
| `documents.document.reference.read` | List references | authorized readers |
| `documents.document.admin` | Administrative overrides (hold override, forensics) | operators |

Scope semantics follow `ADR-011`: data permissions are organization-scoped; a
grant at a national scope covers descendant units via the Authorization service
hierarchy resolution (Organization `/covers`). A global grant of a data
permission only applies to checks with no organization context. Per-document
grants use Authorization relationship tuples; Documents never evaluates them
itself.

### Resource-level authorization

- Every guarded operation calls the Authorization check API with
  `resourceType = "document"`, the document id as `resourceId`, and the
  effective organization-unit scope (primary scope by default; access succeeds
  when the permission is effective at **any** of the document's scopes).
- Organization-scoped grants and per-document relationship-tuple grants compose
  through the Authorization service — Documents implements neither.
- Fail-closed: any inability to establish the grant (unreachable Authorization
  service, unknown scope, missing tuple) is a Deny (`403`).

### Unauthorized enumeration

- List/search applies fail-closed read filtering: the query returns only rows
  the caller is authorized to read; it never returns a count or marker of
  documents the caller cannot read.
- `GET /documents/{id}` and all version/content lookups return `404` for
  missing **and** unauthorized documents alike (no existence oracle).
- Download attempts on sensitive documents without
  `documents.document.content.read.sensitive` are denied without revealing that
  the document exists.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Metadata, version descriptors (id, number, MIME, size, hash, uploader ref, scan status), classification, scopes, references. **Never binary content.** |
| **Logs** | Document/version ids, action, actor, outcome. Never content, never secrets, never filenames. |
| **Audit events** | Ids, actor, action, classification before/after, scope/reference changes, sensitive-download records. Never content. |
| **Integration events** | Identifiers and minimal metadata only. Never binary, never secrets, never filenames, never names. |
| **Search indexes** | Metadata and (future) extracted text, gated by classification. Never secrets. |
| **AI pipelines** | Metadata and (future) extracted text only, gated by classification and the AI Governance model. Binary is never sent raw to AI. |

## HTTP API

All endpoints are versioned under `/api/v1/documents` and require a valid
access token. Metadata access and binary content access are separate endpoints.
See `docs/api/documents.md` for the full endpoint reference. No EF entities are
exposed; DTOs are returned.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (`CommunityOS.Contracts.Documents`), following the open-generic
  publisher pattern used by every existing service. See below for the contract.
- **Organization** — Documents consumes `OrganizationUnitCreated/Updated/
  ParentChanged` into `organization_unit_references` (ADR-016 pattern). It never
  reads the Organization database.
- **Community** — creators/owners are stable person ids; Documents resolves
  names through the Community API at read time and never stores person data.
- **Authorization** — Documents never reads the Authorization database.
  `AuthorizationGuard` is bound to the same HTTP evaluator
  (`HttpAuthorizationEvaluator`) as Community/Organization/Knowledge, calling
  the Authorization check API as a service principal (`ADR-018/019`).
- **Future consumers** — Audit, Search, Workflow, Notifications, Correspondence
  and Records subscribe to `CommunityOS.Contracts.Documents` events; none cross
  the Documents database boundary.

### Planned integration events (`CommunityOS.Contracts.Documents`)

Names and payloads below are the **proposed** baseline. Each record carries a
trailing `DateTime OccurredOn`. Only stable ids and minimal metadata are
exported — no binary content, no secrets, no filenames, no names.

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `DocumentCreated` | A document is created | `DocumentId`, `Title`, `Status`, `OrganizationUnitId`, `OwnerType`, `OwnerId`, `CreatedBy` |
| `DocumentMetadataUpdated` | Non-security metadata or scope changes | `DocumentId`, `Status`, `OrganizationUnitId` |
| `DocumentVersionAdded` | A new version is added | `DocumentId`, `VersionId`, `VersionNumber`, `MimeType`, `SizeBytes`, `ContentHash`, `UploadedBy` |
| `DocumentClassified` | Classification/sensitive/retention/holds change | `DocumentId`, `ClassificationCode`, `IsSensitive` |
| `DocumentArchived` | `Active → Archived` | `DocumentId` |
| `DocumentDeactivated` | `Active/Archived → Deactivated` | `DocumentId` |
| `DocumentRestored` | `Deactivated/Archived → Active` | `DocumentId`, `Status` |
| `DocumentScanCompleted` | A scan finishes (only when scanning enabled) | `DocumentId`, `VersionId`, `ScanStatus` |
| `DocumentContentDownloaded` | Ratified. Sensitive content downloaded | `DocumentId`, `VersionId`, `ActorId` |

Deliberately **not exported** as separate events: file name changes (filename is
sensitive metadata; consumers resolve it through the API) and per-title edits.

### Event delivery and outbox readiness

Publication is best-effort in-process today (ADR-015 outbox deferred). For
Documents this is **safe for every event at implementation time**, because no
Documents event has a live consumer yet — a dropped event today can only delay
a future read model, never corrupt authoritative state.

| Consumer | Guaranteed delivery required | When |
|----------|------------------------------|------|
| Search indexing | No — best-effort; re-index reconciles | always safe |
| Workflow task reconciliation | No (recommended later) — workflow can re-query the Documents API | safe best-effort now |
| Notifications | No — notification loss is tolerable | safe best-effort now |
| **Audit (compliance trail)** | **Yes** — `DocumentClassified`, `DocumentDeactivated`, `DocumentRestored`, `DocumentContentDownloaded` (sensitive reads), `DocumentScanCompleted` (rejected scans) must not be lost | required before Audit is implemented (outbox gate) |
| **Records (hold/retention reconciliation)** | **Yes** — `DocumentDeactivated`/`DocumentRestored` protect held documents | required before Records integration |
| **Correspondence (letter submission)** | **Yes** — the immutable submitted-letter version event must be reliable at submission time | required before Correspondence is implemented |

Conclusion: no Documents event requires guaranteed delivery for the service
itself; the outbox (ADR-015) becomes mandatory at the Correspondence/Audit
gate, consistent with the platform roadmap.

## Dependency map

```
Community ──┐   (person refs: creators/owners; never owns persons)
Organization ─┼──►  Documents  ──►  (event consumers, future)
Authorization ─┘     ▲  │              │
Identity (authN)     │  └── Events ──►  Audit / Search / Workflow /
                     │                   Notifications / Records / Correspondence
                     │  ◄── consumes OrganizationUnitCreated/Updated/ParentChanged
                     │      (read-model projection, ADR-016)
                     └── resolves names via Community API (read time)
```

- **Depends on (existing):** Identity (authentication), Authorization
  (AuthorizationGuard over the check API), Organization (unit events + scope
  references), Community (person-id references resolved at read time).
- **Does not depend on (yet):** Knowledge, Search, Workflow, Notifications,
  Records, Correspondence, Finance, Administration, AI — Documents is a leaf
  service today.
- **Provides for (future):** Records (evidence via `DocumentReference`),
  Workflow (artifacts under review), Correspondence (immutable letter
  versions), Finance/Ticketing (receipts/attachments), Audit (event stream),
  Search (indexing), AI Platform (metadata + extracted text only, never
  binary, never authoritative).

## Data

- Database: `communityos_documents` (PostgreSQL), schema `documents`.
- Tables: `documents`, `document_versions`, `document_scopes`,
  `document_references`, `organization_unit_references`.
- Schema is managed by EF Core migrations (created at implementation time).
- Binary content is **not** stored in the database; it lives in object storage
  (see Storage).

## Storage

- **Metadata storage** — PostgreSQL (`communityos_documents`). Authoritative.
- **Binary content storage** — S3-compatible object storage behind an
  `IDocumentObjectStorage` abstraction. MinIO is the concrete self-hosted
  deployment; any S3-compatible provider is a configuration change
  (ADR-022). The concrete client is **AWSSDK.S3**; application/domain layers
  depend only on `IDocumentObjectStorage`, never on the SDK.
- **Object keys** — content-addressed: `documents/{sha256}`.
- **Integrity** — SHA-256 computed on upload and stored; verified on download
  when configured (default on).
- **Encryption at rest** — handled at the storage layer (SSE); the service
  records whether storage-layer encryption is enabled. Not a per-document
  decision.
- **Upload flow** — stream to object storage under the content hash, then write
  the immutable version row; orphaned objects are cleaned up if the row write
  fails.
- **Download flow** — resolve current (or requested) version → object key →
  stream with content-type/length; verify hash on read when configured
  (default on).
- **Limits (ratified)** — maximum file size 50 MiB (configurable); MIME allowlist
  configurable (default: `application/pdf`, `text/plain`, `text/markdown`,
  `text/csv`, `application/json`, `application/msword`,
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document`,
  `application/zip`, `image/png`, `image/jpeg`, `image/gif`, `image/webp`).
- **Backup / restore / DR** — PostgreSQL dump/PITR; object-store bucket
  versioning and replication (MinIO site replication or provider-native CRR);
  restore both from backup; an offline integrity-audit job reconciles DB rows
  against objects by content hash.

## Malware / content safety

Versions carry a scan lifecycle:

```
Uploaded → Quarantined → Scanning → Clean | Rejected
```

- The scanner is an **extension point**, not a pretend feature: the application
  layer depends on an `IDocumentScanService`; when no scanner is configured
  (`Documents:MalwareScanning:Enabled=false`) versions are created with
  `ScanStatus = NotScanned` — an explicit opt-out, never an illusion that
  scanning happened. A no-op development implementation ships with the service
  and is replaceable by a real scanner later.
- **Active-before-scan (explicit):** a document may become `Active` while its
  versions are still `NotScanned`/`Scanning`. Scanning does not gate the
  document lifecycle; it gates **download** only.
- **Download policy (fail closed):** when scanning is enabled, content whose
  version is `Scanning`, `Quarantined` or `Rejected` is **not downloadable**;
  only `Clean` (or `NotScanned` when scanning is disabled) is downloadable.
- When a scan completes, `DocumentScanCompleted` is raised.

## Retention and legal hold

Retention and hold **semantics belong to Records** (future). Documents provides
hooks only, without inventing legal policy:

- **RetentionCategory** — a nullable reference to a future Records retention
  category. Documents does not enforce schedules.
- **LegalHoldReference / AdministrativeHoldReference** — nullable references to
  future hold records.
- **Deactivation protection (ratified):** when any hold reference is present,
  deactivating the document is rejected (`409`) unless the actor holds
  `documents.document.admin` and supplies a reason. Releasing a hold happens in
  Records and updates the reference through `documents.document.classify`.
- **Who can override deletion (ratified):** `documents.document.admin` holders
  and, in future, the Records service principal.
- Relationship to Records: Records creates a record snapshot that references
  specific document versions; Records owns the retention schedule and hold
  lifecycle; Documents only honors the references.

## Configuration (ratified)

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `DocumentsDb` | `Host=localhost;Port=5432;Database=communityos_documents;...` | PostgreSQL connection string |
| `Jwt` | `Issuer` / `Audience` / `MetadataAddress` | *(local)* | Token validation for the API |
| `RabbitMq` | `Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Message bus |
| `AuthorizationService` | `BaseUrl` / `AccessToken` / `ClientId` | *(see runbook)* | Authorization check API configuration |
| `Documents` | `InternalClientId` | `communityos-authorization` | Trusted in-process caller (reserved) |
| `Documents:Storage` | `Endpoint` / `AccessKey` / `SecretKey` / `Bucket` / `UseHttp` | MinIO local | S3-compatible object storage |
| `Documents:Storage` | `EncryptionAtRest` | `true` | AES256 server-side encryption on write |
| `Documents:Upload` | `MaxFileSizeBytes` | 52428800 (50 MiB) | Maximum upload size |
| `Documents:Upload` | `AllowedMimeTypes` | *(allowlist above)* | Allowed content types |
| `Documents:Integrity` | `VerifyHashOnRead` | `true` | Recompute SHA-256 on download |
| `Documents:MalwareScanning` | `Enabled` | `false` | Enable the scan extension point |

## Testing

- **Unit tests** — domain invariants (version immutability, lifecycle
  transitions, content-addressed keys, multi-scope access, duplicate/idempotent
  upload, deactivation protection, classification changes) and security
  regression tests (fail-closed authorization, sensitive-content gating,
  metadata/content permission separation, JWT RS256-only validation) through
  the MediatR pipeline with substitute persistence and a substitute
  `IDocumentObjectStorage`.
- **Integration tests** — EF mapping, migrations, and object-storage round-trips
  against PostgreSQL and MinIO via Testcontainers (requires Docker).

## Deviations

No deviations yet. This specification and ADR-022 were ratified at the Prompt
07A-R gate; future deviations require a ratified ADR amendment. The
`DocumentContentDownloaded` read-audit event and the `documents.*` permission
matrix, previously flagged, are now part of the ratified baseline.