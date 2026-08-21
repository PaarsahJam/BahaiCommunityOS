# Search Service

> **STATUS: RATIFIED, NOT IMPLEMENTED (Prompt 11B).** Architectural design for
> the Search bounded context (`ADR-026`), positioned at ADR-017 slot 10.

The Search bounded context owns **search index projections and the cross-domain
query surface**. It is the platform-wide full-text and metadata search service,
consuming integration events from producer contexts to build and maintain
read-only index projections over which callers execute queries. Search never
owns the domain facts it indexes; every producer service remains the source of
truth for its own state.

The service must preserve the boundary rules of `ADR-026`: Search is a
projection-only service (never a source of truth), no cross-service database
access, authorization is centralized and fail-closed, indexed projections
exclude PII and sensitive content, and index synchronization is best-effort with
full-rebuild reconciliation.

## Technology

**Selected: PostgreSQL full-text search (`tsvector` / `tsquery`)**, using the
existing `postgres:16-alpine` instance that every CommunityOS service already
depends on.

### Rationale

| Criterion | Decision |
|-----------|----------|
| Infrastructure dependency | Zero new services; PostgreSQL is already required by all 9 implemented contexts |
| NuGet stack | Npgsql.EntityFrameworkCore.PostgreSQL — already the project-wide ORM; no new package |
| Docker compose | No new entry needed; Testcontainers uses `Testcontainers.PostgreSql` already used by integration tests |
| Index model | `tsvector` column per indexed projection table; `GIN` index for fast full-text query |
| Query model | `tsquery` / `websearch_to_tsquery` supporting multi-word, phrase and prefix search; EF Core translates to SQL `@@` operator |
| Ranking | `ts_rank` / `ts_rank_cd` for relevance ordering |
| Highlighting | `ts_headline` for snippet generation |
| Multi-language | PostgreSQL text-search dictionaries (`english` default; extendable to `arabic`, `french`, `spanish`, etc.) |
| Organization scoping | Organization unit ids stored as columns on projection rows; WHERE clause filters; identical to Records/Workflow pattern |
| Authorization | Row-level filtering via `AuthorizationGuard` + scope columns; no index-engine ACL required |
| Operational complexity | Managed by the existing PostgreSQL instance; no separate engine, no separate backup strategy |
| Rejected alternatives | Elasticsearch/OpenSearch: no existing package dependency, requires new compose service, new NuGet client, new Testcontainers image, large operational footprint. Meilisearch/Typesense: same new-dependency concerns, no established pattern in this codebase. |

### Deployment model

Search uses the shared PostgreSQL instance under a dedicated database
`communityos_search` and schema `search`. Index projection tables live in that
database alongside EF Core MassTransit inbox tables (for consumer idempotency).
No separate search engine process is needed.

### Full-text vector update model

Each projection table has a generated `tsvector` column (or an application-
maintained `search_vector` column populated on insert/update) covering the
indexed textual fields. A `GIN` index on that column enables fast `@@` operator
queries. Projection rows are upserted idempotently on each consumed event.

## Model

### SearchDocument (projection entity)

The central projection. One row per indexed domain object. Source-of-truth
services own the underlying data; Search owns only this projection.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Synthetic row id (UUID v4) |
| `SourceType` | `string(50)` | Stable domain type code. First-gate values: `record`, `document`, `workflow-task`, `knowledge-question`. Future-gate values (deferred): `knowledge-work`, `knowledge-passage`. |
| `SourceId` | `Guid` | Stable id of the domain object in its owner service |
| `OrganizationUnitId` | `Guid?` | Primary organization scope (nullable for global/unscoped) |
| `Status` | `string(50)` | Lifecycle status string from the producer (for filter purposes only) |
| `IsSensitive` | `bool` | True when the source object carries a sensitive classification; sensitive projections are excluded from standard search results |
| `DisplayTitle` | `string(500)` | Human-readable title/subject — the only displayable text field in the projection |
| `TypeCode` | `string(100)` | Source-type-specific subtype code (e.g. category code for records, definition code for tasks, work type for Knowledge works) |
| `IndexedOn` | `DateTime` | UTC timestamp of last projection update |
| `CreatedOn` | `DateTime` | UTC timestamp of original source creation (from event `OccurredOn` at `*Created` event) |
| `SearchVector` | `NpgsqlTsVector` | Maintained `tsvector` from `DisplayTitle` + `TypeCode`; `GIN`-indexed |

### AdditionalScope (owned child)

One row per additional organization-unit scope attached to a `SearchDocument`.
Enables any-of-scope authorization (matching Records/Workflow/Notifications
pattern).

| Field | Type | Notes |
|-------|------|-------|
| `SearchDocumentId` | `Guid` (FK) | Parent projection row |
| `OrganizationUnitId` | `Guid` | Additional scope unit id |

### SearchIndexLog (optional infrastructure table)

Tracks the last-processed `OccurredOn` per source type for reconciliation
diagnostics. Not a guaranteed-delivery checkpoint — that is handled by the
MassTransit inbox.

| Field | Type | Notes |
|-------|------|-------|
| `SourceType` | `string(50)` | PK |
| `LastEventOccurredOn` | `DateTime` | UTC timestamp |
| `IndexedCount` | `long` | Cumulative indexed document count |

## Lifecycle and Synchronization

### Normal path

1. Producer publishes an integration event (e.g. `RecordCreated`).
2. MassTransit delivers the event to the Search consumer via RabbitMQ.
3. The consumer upserts the `SearchDocument` projection in a single database
   transaction (inbox row + upsert = atomic, idempotent).
4. The `SearchVector` column is updated to reflect the new textual content.

### Idempotency

Every consumer upserts by `(SourceType, SourceId)` unique key. Duplicate
delivery of the same event re-applies the same projection update — safe and
idempotent. The `IndexedOn` field is always overwritten with the current
`OccurredOn` of the processed event, so late re-delivery of an older event
does not regress a newer projection (consumers compare `OccurredOn` to the
current `IndexedOn` value and skip if the incoming event is older).

### Soft-delete / deactivation

When a source object is archived, deactivated, or merged/archived (terminal
states), the corresponding `SearchDocument` row is **soft-deleted** (its
`Status` is updated to the terminal status string). The row is retained for
audit/diagnostics but is **excluded from all search queries** via a
`WHERE Status NOT IN ('Deactivated','Archived','Merged','Cancelled')` filter.
Hard deletion from the projection table is never performed through normal
operations — it requires an admin rebuild.

### Rebuild / reconciliation

Full rebuild: an admin triggers a re-index via `POST /api/v1/search/admin/reindex`.
The rebuild truncates the `search_documents` table and replays source events
(by calling source-service read APIs if needed, or re-consuming from the
event log). Re-indexing is idempotent by design; the unique `(SourceType,
SourceId)` index prevents duplicates during concurrent rebuilds.

### Missed events

If an event is missed (consumer failure, broker downtime), the projection falls
out of sync. This is acceptable because:
- Search is best-effort (explicitly ratified in `docs/records.md` and aligned
  with ADR-026 decision 12).
- The `POST /admin/reindex` endpoint corrects drift.
- The inbox table prevents duplicate processing of successfully delivered events.

### Best-effort delivery rationale

Search is a **read projection** service. A missed index event means a document
is temporarily absent from search results — it remains fully accessible through
each source service's own API. This is fundamentally different from Workflow or
Audit consumers (which have compliance obligations). ADR-026 therefore does not
require the transactional outbox on the **producer** side for Search-targeted
events; producers' existing outbox protection (present for Workflow and Records)
is sufficient but not relied upon by Search's correctness contract.

## First-Gate Index Taxonomy

### Included at Prompt 11C (first gate)

| Source type | SourceType code | Triggering events consumed | Projection fields |
|------------|-----------------|---------------------------|-------------------|
| **Records** | `record` | `RecordCreated`, `RecordClassified`, `RecordArchived`, `RecordDeactivated`, `RecordRestored` | `SourceId`=RecordId, `DisplayTitle`=Category (human-readable), `TypeCode`=Category, `OrganizationUnitId`, `Status`, `IsSensitive` |
| **Documents** | `document` | `DocumentCreated`, `DocumentMetadataUpdated`, `DocumentClassified`, `DocumentArchived`, `DocumentDeactivated`, `DocumentRestored` | `SourceId`=DocumentId, `DisplayTitle`=Title, `TypeCode`=OwnerType, `OrganizationUnitId`, `Status`, `IsSensitive` |
| **Workflow tasks** | `workflow-task` | `WorkflowTaskCreated`, `WorkflowTaskAssigned`, `WorkflowTaskStarted`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled` | `SourceId`=TaskId, `DisplayTitle`=DefinitionCode (human-readable), `TypeCode`=DefinitionCode, `OrganizationUnitId`, `Status`, `IsSensitive`=false |
| **Knowledge questions** | `knowledge-question` | `QuestionSubmitted`, `QuestionPublished`, `QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`, `QuestionArchived` | `SourceId`=QuestionId, `DisplayTitle`=`"Question"` (title resolved via API), `TypeCode`=`"question"`, `OrganizationUnitId`, `Status` |

### Deferred (not at first gate)

| Source | Reason deferred |
|--------|----------------|
| Knowledge Library (Works, Editions, Passages) | High-volume corpus; requires language-specific dictionaries and a dedicated Library search surface. Deferred to a second gate alongside the Knowledge–Search integration. |
| Knowledge answers | Dependent on question indexing being stable first. Deferred. |
| Community persons / households | Strong PII concerns; name/contact indexing requires a dedicated privacy gate and explicit ratification. Deferred. |
| Community events / meetings / activities | Lower priority for cross-domain search. Deferred. |
| Organization units / committees | Lookup endpoints on the Organization API are sufficient for the first gate. Deferred. |
| Notifications | Notification metadata has limited cross-user discoverability value; sensitive content rules make indexing complex. Deferred. |

### Privacy / exclusion rules (mandatory for all sources)

- `IsSensitive = true` records/documents **are indexed** (so their existence is tracked) but are **excluded from standard search query results**. Only callers holding `search.result.read.sensitive` at the relevant scope may receive sensitive hits.
- Recipient ids, names, contact details, field values, hold reasons, task notes, passage text bodies, and document filenames are **never stored in any projection field**.
- `DisplayTitle` for records is the **category code** only (never a person's name or any sensitive field value). Callers resolve the actual subject by calling the Records API with the `SourceId`.
- `DisplayTitle` for Knowledge questions is the literal string `"Question"` at index time; the question text itself is resolved through the Knowledge API. (Revisit at the second gate with explicit Knowledge–Search API integration.)

## Consumed Integration Events

See `docs/api/search.md` for the full consumer catalog. Summary:

| Producer contract | Events consumed | Idempotency key |
|------------------|-----------------|-----------------|
| `CommunityOS.Contracts.Records` | `RecordCreated`, `RecordClassified`, `RecordArchived`, `RecordDeactivated`, `RecordRestored` | `("record", RecordId)` |
| `CommunityOS.Contracts.Documents` | `DocumentCreated`, `DocumentMetadataUpdated`, `DocumentClassified`, `DocumentArchived`, `DocumentDeactivated`, `DocumentRestored` | `("document", DocumentId)` |
| `CommunityOS.Contracts.Workflow` | `WorkflowTaskCreated`, `WorkflowTaskAssigned`, `WorkflowTaskStarted`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled` | `("workflow-task", TaskId)` |
| `CommunityOS.Contracts.Knowledge` | `QuestionSubmitted`, `QuestionPublished`, `QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`, `QuestionArchived` | `("knowledge-question", QuestionId)` |
| `CommunityOS.Contracts.Organization` | `OrganizationUnitCreated`, `OrganizationUnitUpdated`, `OrganizationUnitParentChanged` | `("org-unit-ref", OrganizationUnitId)` — ADR-016 read-model |

## Organization Scoping

Follows ADR-016. Search consumes `OrganizationUnitCreated/Updated/ParentChanged`
to maintain a local `OrganizationUnitReference` read model (identical pattern to
Records, Workflow, Notifications, Knowledge).

Each `SearchDocument` projection row carries:
- `OrganizationUnitId` — the primary scope of the indexed object (nullable for
  globally-scoped objects).
- Zero or more `AdditionalScope` child rows for objects that span multiple scopes
  (matching the Records/Workflow pattern).

**Any-of scope semantics:** A caller is authorized to see a search result when
they hold `search.result.read` at **any** of the result's scopes (primary or
additional). This mirrors the Records/Workflow/Notifications any-of-grant
pattern (ADR-011). The query layer evaluates this as:
`OrganizationUnitId IN (<caller's granted scopes>) OR AdditionalScopes.OrganizationUnitId IN (<caller's granted scopes>)`.

Global results (null `OrganizationUnitId`, no additional scopes) are returned
when the caller holds `search.result.read` at global scope.

## Authorization

Every guarded operation calls the Authorization service through
`AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019). No
`[Authorize(Roles = "...")]`, no local RBAC, no direct Authorization database
access. Resource-level authorization uses `resourceType = "search.result"`.

Search result filtering is fail-closed: queries return only results the caller
is authorized to read. An unauthorized result is simply absent from the result
set — never a 404 or a 403 (no enumeration oracle applies only to direct
single-item lookups, not to result sets).

## Permission Matrix

| Permission | Purpose | Typical holder |
|------------|---------|----------------|
| `search.result.read` | Execute search queries and receive non-sensitive results | all authorized users |
| `search.result.read.sensitive` | Receive sensitive-flagged results in search output | higher-privilege / privacy roles |
| `search.index.manage` | Trigger reindex, view index health, manage index configuration | administrators / operators |

## Security and Privacy Rules

- No cross-service database access (ADR-018).
- `DisplayTitle` never contains names, contact data, field values, hold reasons,
  task notes, passage text, or document filenames.
- Sensitive projections (`IsSensitive = true`) are excluded from standard results;
  only callers with `search.result.read.sensitive` at the appropriate scope see them.
- Logs: only indexed source type, source id, operation, and outcome. Never
  display titles, never any field value that could contain PII.
- JWT: RS256/JWKS-only (ADR-019 pattern, same as all implemented services).
- Authorization must occur before result delivery, not post-filter on an
  unbounded result set.

## Outbox / Inbox

Search is **exclusively a consumer** at the first gate. It publishes no
integration events.

- **Inbox**: The MassTransit EF Core inbox is enabled on the Search `DbContext`
  so that each consumer runs with exactly-once semantics (deduplication of
  redelivered messages at the consume endpoint). This prevents a redelivered
  event from creating a duplicate projection upsert that could corrupt
  `IndexedOn` ordering.
- **Outbox**: Not required. Search emits no integration events at the first gate.
  If future gates introduce a `SearchIndexed` or similar audit event, the outbox
  will be enabled at that gate (ADR-015 amendment pattern).
- **No transactional outbox required on producers**: Search explicitly does not
  require guaranteed delivery from producers. The inbox alone provides
  idempotency at the consume side.

## Data / Persistence

- **Database**: `communityos_search` (PostgreSQL 16)
- **Schema**: `search`
- **Tables** (EF Core migrations, owned by `SearchDbContext`):
  - `search.search_documents` — primary projection table
  - `search.search_document_scopes` — additional org-unit scopes (owned child)
  - `search.organization_unit_references` — ADR-016 read-model projection
  - `search.search_index_log` — reconciliation diagnostics
  - `search.InboxState`, `search.OutboxMessage`, `search.OutboxState` — MassTransit inbox (outbox tables provisioned but unused at first gate)
- **Indexes**:
  - `GIN` index on `search_documents.search_vector` — full-text query performance
  - Unique index on `(source_type, source_id)` — idempotency / upsert key
  - Index on `organization_unit_id` — scope filtering
  - Unique index on `organization_unit_references.organization_unit_id`

## Configuration

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:SearchDb` | | `Host=localhost;Port=5432;Database=communityos_search;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | RS256/JWKS identity discovery |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Token validation |
| `Jwt:RequireHttpsMetadata` | | `false` (local) / `true` (prod) | Fails closed when absent |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-search` | Audit identifier |
| `Search:DefaultLanguage` | | `english` | PostgreSQL text-search dictionary |
| `Search:MaxResultsPerPage` | | `50` | Hard upper limit on `limit` |
| `Search:InternalClientId` | | *(reserved)* | Trusted in-process caller for rebuild |

## Dependencies

- **Identity** — RS256 JWT authentication of actors.
- **Authorization** — `search.*` permission evaluation via `AuthorizationGuard`.
- **Organization** — org-unit scoping via ADR-016 read-model projection.
- **Records** — consumes `CommunityOS.Contracts.Records` events for record projections.
- **Documents** — consumes `CommunityOS.Contracts.Documents` events for document projections.
- **Workflow** — consumes `CommunityOS.Contracts.Workflow` events for task projections.
- **Knowledge** — consumes `CommunityOS.Contracts.Knowledge` events for question projections.
- **Does not depend on (yet)**: Community (person search deferred), Notifications, AI Platform, Audit.

## Runbook

See `docs/runbooks/search.md` for the implementation-stage operational plan.
