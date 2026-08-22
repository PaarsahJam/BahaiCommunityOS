# Search Service API

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 11C).** Contract for the Search
> service, aligned with ratified ADR-026 and `docs/search.md`.

All endpoints are versioned under `/api/v1/search`, require a valid access
token (`[Authorize]`), and return DTOs — **EF entities are never exposed**.
Every guarded operation is evaluated against the Authorization service
(fail-closed). Standard and sensitive search results are separate capabilities
with separate permissions. The Search service stores no PII, no names, no field
values, no contact information, and no document filenames; only stable
identifiers and safe display codes appear in projections.

## Search queries — `/search`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/search?q=&types=&organizationUnitId=&status=&sensitive=false&limit=25&offset=0` | `search.result.read` | Execute a full-text query across all indexed source types |
| GET | `/search/{sourceType}?q=&organizationUnitId=&status=&sensitive=false&limit=25&offset=0` | `search.result.read` | Execute a full-text query scoped to a single source type |

### Query parameters

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `q` | `string` | *(required)* | Full-text query string; passed to `websearch_to_tsquery` (`english`). Min 1 char. |
| `types` | `string[]` | all | Comma-separated or repeated source-type codes to include. Allowed: `record`, `document`, `workflow-task`, `knowledge-question`. Unknown codes return `400`. |
| `organizationUnitId` | `Guid?` | *(caller's granted scopes)* | Restrict results to a specific organization scope. If supplied and the caller lacks `search.result.read` at that scope, returns `403`. |
| `status` | `string[]` | *(exclude terminal)* | Include results with these status values. Terminal statuses (`Deactivated`, `Archived`, `Merged`, `Cancelled`) are excluded by default. |
| `sensitive` | `bool` | `false` | When `true`, requires `search.result.read.sensitive`; results include `IsSensitive = true` rows. When `false`, sensitive rows are excluded silently (no enumeration oracle). |
| `limit` | `int` | `25` | Page size. Max `50` (`Search:MaxResultsPerPage`). Values above the max are clamped to the max. |
| `offset` | `int` | `0` | Zero-based result offset for pagination. |

Results are ordered by `ts_rank` (descending). Ties are broken by `IndexedOn`
(descending). The caller receives only results their authorization grants allow;
results they cannot see are silently absent — there is no count or marker of
inaccessible hits (no enumeration oracle).

`GET /search/{sourceType}` returns `400` when `{sourceType}` is not a
recognized type code in the first-gate taxonomy.

### Response — `SearchResultPageDto`

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "sourceType": "record",
      "sourceId": "00000000-0000-0000-0000-000000000002",
      "displayTitle": "Birth Registration",
      "typeCode": "birth-registration",
      "status": "Active",
      "isSensitive": false,
      "organizationUnitId": "00000000-0000-0000-0000-000000000003",
      "indexedOn": "2026-01-15T09:00:00Z",
      "createdOn": "2026-01-10T08:30:00Z",
      "rank": 0.7563
    }
  ],
  "totalCount": 1,
  "limit": 25,
  "offset": 0
}
```

`displayTitle` is the safe display code only. The full domain record is
retrieved from the source service using `sourceId`. `rank` is the PostgreSQL
`ts_rank` score. `isSensitive = true` items appear only when the caller holds
`search.result.read.sensitive`.

## Index administration — `/search/admin`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/admin/health` | `search.index.manage` | Return index health: document counts per source type, last-event timestamp per type, lag indicator |
| POST | `/admin/reindex` | `search.index.manage` | Trigger a full rebuild of the search index (truncate + re-consume from source APIs) |
| POST | `/admin/reindex/{sourceType}` | `search.index.manage` | Trigger a partial rebuild for one source type only |

`POST /admin/reindex` is idempotent. Concurrent calls are serialized by a
database-level advisory lock. The rebuild runs synchronously in the first gate;
a background job variant may be introduced at a later gate.

`POST /admin/reindex/{sourceType}` returns `400` when the source type is not
recognized.

### `GET /admin/health` response — `SearchIndexHealthDto`

```json
{
  "sources": [
    {
      "sourceType": "record",
      "documentCount": 1420,
      "lastEventOccurredOn": "2026-01-15T08:59:00Z",
      "indexedCount": 1420
    },
    {
      "sourceType": "document",
      "documentCount": 830,
      "lastEventOccurredOn": "2026-01-15T08:57:00Z",
      "indexedCount": 830
    }
  ],
  "generatedOn": "2026-01-15T09:01:00Z"
}
```

## DTOs (conceptual)

- `SearchResultPageDto` — `items: SearchResultItemDto[]`, `totalCount`, `limit`, `offset`.
- `SearchResultItemDto` — `id`, `sourceType`, `sourceId`, `displayTitle`, `typeCode`,
  `status`, `isSensitive`, `organizationUnitId`, `indexedOn`, `createdOn`, `rank`.
- `SearchIndexHealthDto` — `sources: SearchSourceHealthDto[]`, `generatedOn`.
- `SearchSourceHealthDto` — `sourceType`, `documentCount`, `lastEventOccurredOn`, `indexedCount`.

No request bodies are needed for query endpoints (all parameters are query-string).
`POST /admin/reindex` and `POST /admin/reindex/{sourceType}` accept no request body.

## Integration events consumed

The Search service is a **consumer only** at the first gate. It publishes no
integration events. All consumers use MassTransit EF Core inbox for
exactly-once semantics (idempotent upsert by `(SourceType, SourceId)`).

| Producer | Events consumed | Idempotency key |
|----------|-----------------|-----------------|
| `CommunityOS.Contracts.Records` | `RecordCreated`, `RecordClassified`, `RecordArchived`, `RecordDeactivated`, `RecordRestored` | `("record", RecordId)` |
| `CommunityOS.Contracts.Documents` | `DocumentCreated`, `DocumentMetadataUpdated`, `DocumentClassified`, `DocumentArchived`, `DocumentDeactivated`, `DocumentRestored` | `("document", DocumentId)` |
| `CommunityOS.Contracts.Workflow` | `WorkflowTaskCreated`, `WorkflowTaskAssigned`, `WorkflowTaskStarted`, `WorkflowTaskCompleted`, `WorkflowTaskCancelled` | `("workflow-task", TaskId)` |
| `CommunityOS.Contracts.Knowledge` | `QuestionSubmitted`, `QuestionPublished`, `QuestionFlagged`, `QuestionUnderReview`, `QuestionMerged`, `QuestionArchived` | `("knowledge-question", QuestionId)` |
| `CommunityOS.Contracts.Organization` | `OrganizationUnitCreated`, `OrganizationUnitUpdated`, `OrganizationUnitParentChanged` | `("org-unit-ref", OrganizationUnitId)` — ADR-016 read model |

### Consumer field contracts

**Records** (`RecordCreated` / `RecordClassified` / `RecordArchived` / `RecordDeactivated` / `RecordRestored`):

| Consumed field | Projected to |
|---------------|--------------|
| `RecordId` | `SourceId` |
| `CategoryCode` | `DisplayTitle` and `TypeCode` |
| `OrganizationUnitId` | `OrganizationUnitId` |
| `IsSensitive` | `IsSensitive` |
| `Status` | `Status` |
| `OccurredOn` | `IndexedOn` (and `CreatedOn` on `RecordCreated`) |

**Documents** (`DocumentCreated` / `DocumentMetadataUpdated` / `DocumentClassified` / `DocumentArchived` / `DocumentDeactivated` / `DocumentRestored`):

| Consumed field | Projected to |
|---------------|--------------|
| `DocumentId` | `SourceId` |
| `Title` | `DisplayTitle` |
| `OwnerType` | `TypeCode` |
| `OrganizationUnitId` | `OrganizationUnitId` |
| `IsSensitive` | `IsSensitive` |
| `Status` | `Status` |
| `OccurredOn` | `IndexedOn` (and `CreatedOn` on `DocumentCreated`) |

> `Title` is the document title from its metadata — never a filename. If title
> is absent (blank document), `DisplayTitle` is set to `TypeCode` only.

**Workflow tasks** (`WorkflowTaskCreated` / `WorkflowTaskAssigned` / `WorkflowTaskStarted` / `WorkflowTaskCompleted` / `WorkflowTaskCancelled`):

| Consumed field | Projected to |
|---------------|--------------|
| `TaskId` | `SourceId` |
| `DefinitionCode` | `DisplayTitle` and `TypeCode` |
| `OrganizationUnitId` | `OrganizationUnitId` |
| `Status` | `Status` |
| `OccurredOn` | `IndexedOn` (and `CreatedOn` on `WorkflowTaskCreated`) |

`IsSensitive` is always `false` for workflow tasks (task notes are not indexed).

**Knowledge questions** (`QuestionSubmitted` / `QuestionPublished` / `QuestionFlagged` / `QuestionUnderReview` / `QuestionMerged` / `QuestionArchived`):

| Consumed field | Projected to |
|---------------|--------------|
| `QuestionId` | `SourceId` |
| *(literal)* `"Question"` | `DisplayTitle` |
| *(literal)* `"question"` | `TypeCode` |
| `OrganizationUnitId` | `OrganizationUnitId` |
| `Status` | `Status` |
| `OccurredOn` | `IndexedOn` (and `CreatedOn` on `QuestionSubmitted`) |

`DisplayTitle` is always the literal string `"Question"` for knowledge
questions. The actual question text is never stored in the projection (privacy
gate deferred to the second Knowledge–Search integration gate).

**Organization units** (ADR-016 read-model; same pattern as Records, Workflow, Notifications):

| Consumed field | Projected to |
|---------------|--------------|
| `OrganizationUnitId` | `OrganizationUnitReference.OrganizationUnitId` |
| `Name` | *(not projected into search documents)* |
| `ParentOrganizationUnitId` | `OrganizationUnitReference.ParentOrganizationUnitId` |
| `OccurredOn` | `OrganizationUnitReference.LastUpdatedOn` |

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Invalid `q` (empty/missing), unrecognized `sourceType` or `types` value, `limit` < 1 |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed); also when `organizationUnitId` is supplied and the caller lacks scope access |
| 404 | Not applicable for query endpoints; used only for admin `/{sourceType}` with an unknown type code (returned as 400 per above) |
| 409 | Not applicable at first gate |

## Permission matrix

| Permission | Purpose |
|------------|---------|
| `search.result.read` | Execute queries, receive non-sensitive results |
| `search.result.read.sensitive` | Receive `IsSensitive = true` results in query output |
| `search.index.manage` | Trigger reindex, view index health |

## Service-to-service notes

Search is a projection consumer. It calls no peer-service APIs during query
execution. The rebuild (`POST /admin/reindex`) will call source-service read
APIs in a future gate; at first gate, projections are built exclusively from
consumed integration events. Authorization check calls are made to the
Authorization service for every guarded request (fail-closed; ADR-009/010/019).
