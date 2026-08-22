# Audit Service API

> **STATUS: RATIFIED — NOT IMPLEMENTED (Prompt 12B).** Contract for the Audit
> service, aligned with ratified ADR-027 and `docs/audit.md`. This is the API
> surface the Prompt 12C implementation gate must realize.

All endpoints are versioned under `/api/v1/audit`, require a valid access
token (`[Authorize]`), and return DTOs — **EF entities are never exposed**.
Every guarded operation is evaluated against the Authorization service through
`AuthorizationGuard` (fail-closed; ADR-009/010/011/018/019); there is no local
RBAC and no `[Authorize(Roles = ...)]`. The journal is read-mostly: the only
mutations are administrative holds, retention purges, and the journal's own
records of those acts.

## Endpoints — first gate (six)

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/audit` | `audit.entry.read` | Query the journal (paged, filtered) |
| GET | `/audit/{id}` | `audit.entry.read` | Get one entry by id |
| POST | `/audit/export` | `audit.entry.export` | Synchronous capped export (CSV or NDJSON stream) |
| POST | `/audit/holds` | `audit.entry.admin` | Place a legal/administrative hold on entries |
| POST | `/audit/holds/{id}/release` | `audit.entry.admin` | Release an active hold |
| POST | `/audit/admin/purge-expired` | `audit.entry.admin` | Execute one retention-purge batch |

Resource-oriented and subject-oriented convenience endpoints are deliberately
omitted at the first gate; the query filter surface covers them. No endpoint
creates or updates a historical entry — corrections arrive as new producer
events.

## Query — `GET /audit`

Filters (all optional, combinable):

| Parameter | Type | Semantics |
|-----------|------|-----------|
| `sourceService` | string | Exact match (`records`, `workflow`, `notifications`, …) |
| `eventType` | string | Exact `SourceEventType` match (e.g. `RecordVerified`) |
| `action` | string | Exact action-code match (e.g. `record-verified`) |
| `resourceType` | string | Exact match (`record`, `workflow-task`, …) |
| `resourceId` | Guid | Exact primary-resource match |
| `subjectId` | Guid | Exact subject match |
| `actorId` | Guid | Exact actor match |
| `organizationUnitId` | Guid | Scope filter; results are limited to entries whose scope the caller can see regardless of this filter |
| `occurredFrom` / `occurredTo` | datetime | Inclusive range over `OccurredOn` |
| `order` | `desc` \| `asc` | Ordering over `OccurredOn`; default `desc` |
| `limit` | int | Default `25`; values above `100` are clamped to `100` (Search clamp pattern) |
| `offset` | int | Default `0` |
| `includeSensitive` | bool | Default `false`. Requires `audit.entry.read.sensitive`; requesting `true` without that permission is rejected (`403`) rather than ignored |

Standard results always exclude `Sensitive` entries entirely — they are not
masked, they are absent (Search sensitive pattern), including from counts.
Only a caller holding both `audit.entry.read` and
`audit.entry.read.sensitive` may pass `includeSensitive=true`.

**Response envelope** (no grand total is returned — see anti-enumeration):

```json
{
  "items": [ { ...entry dto... } ],
  "limit": 25,
  "offset": 0,
  "returned": 7
}
```

**Entry DTO:**

```json
{
  "id": "0f0e0d0c-0000-0000-0000-000000000001",
  "occurredOn": "2026-08-22T10:15:00Z",
  "ingestedOn": "2026-08-22T10:15:04Z",
  "sourceService": "records",
  "sourceEventType": "RecordVerified",
  "action": "record-verified",
  "outcome": null,
  "resourceType": "record",
  "resourceId": "a1000000-0000-0000-0000-000000000002",
  "secondaryResourceId": null,
  "subjectId": null,
  "actorId": "b2000000-0000-0000-0000-000000000003",
  "organizationUnitId": "c3000000-0000-0000-0000-000000000004",
  "sensitivity": "normal",
  "correlationId": null,
  "causationId": null,
  "metadata": {},
  "retentionClass": "standard-7y",
  "retentionExpiresOn": "2033-08-22T00:00:00Z"
}
```

`correlationId`/`causationId` are reserved fields and are always `null` at the
first gate (ADR-027 decision 7). `metadata` contains only allowlisted scalar
keys validated at ingest; unknown keys cannot occur in responses because they
could not have been persisted. `retentionExpiresOn` is `null` when the entry's
class retains indefinitely. Sensitive DTOs are identical in shape; sensitivity
governs *visibility*, never field masking.

## Read one — `GET /audit/{id}`

Returns `404` for missing **and** unauthorized entries alike (no enumeration
oracle). A `Sensitive` entry additionally returns `404` to any caller lacking
`audit.entry.read.sensitive` — existence itself is not disclosed across the
sensitivity boundary.

## Export — `POST /audit/export`

Synchronous bounded export. There is no asynchronous job system at the first
gate; scale does not justify one yet.

**Body:**

```json
{
  "format": "ndjson",
  "filters": {
    "sourceService": "records",
    "resourceType": "record",
    "resourceId": "a1000000-0000-0000-0000-000000000002",
    "organizationUnitId": null,
    "subjectId": null,
    "actorId": null,
    "occurredFrom": "2026-01-01T00:00:00Z",
    "occurredTo": null
  },
  "includeSensitive": false,
  "maxRows": 10000
}
```

Semantics:

- `format`: `"csv"` or `"ndjson"`; response is streamed with
  `Content-Disposition: attachment`.
- Filter keys mirror the query parameters; empty/null filters mean unfiltered.
- Hard cap: `maxRows` above `10000` is rejected with `400` (deliberate hard
  boundary for compliance tooling — exports do not silently clamp).
- `includeSensitive=true` requires `audit.entry.read.sensitive` in addition to
  `audit.entry.export`; without it the request is rejected `403`.
- Every successful export writes an audit-of-audit entry (`SourceService =
  "audit"`) recording actor, filter-criteria summary, row count and format —
  never row contents (ADR-027 decision 15).
- Exports are themselves journaled facts and therefore appear in later queries;
  they carry no special exemption from scoping or sensitivity rules.

## Holds — `POST /audit/holds`

Places a legal/administrative hold exempting entries from retention purge
regardless of expiry. Hold state lives in `audit_entry_holds`; the immutable
entries themselves are never touched.

**Body:**

```json
{
  "entryIds": ["0f0e0d0c-0000-0000-0000-000000000001"],
  "holdType": "administrative",
  "reasonCode": "investigation"
}
```

Validation:

- `holdType`: `legal` | `administrative`.
- `reasonCode`: short code from the fixed set (`investigation`,
  `legal-request`, `dispute`, other ratified codes) — never free text.
- Every referenced entry must be visible to the caller under
  `audit.entry.read` (including the sensitivity second pass for `Sensitive`
  rows); otherwise the whole request fails `404` without disclosing which id
  was problematic.
- An entry with an existing active hold rejects with `409` (one active hold per
  entry at the first gate).
- Placement is journaled as an audit-of-audit entry.

## Hold release — `POST /audit/holds/{id}/release`

Releases an active hold; actor recorded from the token. Releasing an already-
released hold returns `409`. Release is journaled as an audit-of-audit entry.
No body.

## Retention purge — `POST /audit/admin/purge-expired`

Executes **one** bounded purge batch: expired entries (`RetentionExpiresOn`
past) that carry no active hold. Expiry alone never deletes; this endpoint is
the explicit authorized execution step (ADR-027 decision 14).

**Body:** `{ "maxBatchSize": 500 }` — default `500`, maximum `5000`.

Behavior:

1. Select up to `maxBatchSize` expired, unheld entries.
2. Write a purge-marker entry (`SourceService = "audit"`) recording the batch
   count and retention classes — **in the same transaction**, before deletion,
   under the database trigger guard (`SET LOCAL app.audit_purge_authorized`).
3. Delete the batch rows.

**Response:**

```json
{
  "purgedCount": 412,
  "purgeMarkerEntryId": "d4000000-0000-0000-0000-000000000005",
  "remainingExpiredEstimate": 87
}
```

Held-but-expired entries are skipped silently by selection and simply remain;
they appear again in later invocations only after their holds are released.
Repeated invocation drains the backlog. Purge is hard row deletion.

## Error behavior

| Status | Meaning |
|--------|---------|
| `400` | Validation failure (bad filter shape, `maxRows` above cap, unknown `reasonCode`) |
| `401` | Missing/invalid token (RS256-only validation profile) |
| `403` | Capability missing on an operation endpoint, or sensitive data requested without `audit.entry.read.sensitive` |
| `404` | Single-entry read: missing **or** unauthorized (no oracle); hold referencing inaccessible entries |
| `409` | Conflict states: duplicate active hold, release of a released hold |

Problem-details bodies follow the platform convention; error text never echoes
payload field values.

## Anti-enumeration rules

- Single reads return identical `404`s for missing and unauthorized resources.
- List/query results are filtered at the scope boundary before pagination;
  `returned` reflects only visible rows and no exhaustive total is exposed.
- `Sensitive` entries are absent — not masked — from every surface without the
  second-pass permission.
- Failed authorization attempts are **not** journaled (no probe oracle inside
  the guarded store; ADR-027 decision 15).

## Permission-to-endpoint equality

| Endpoint | Required permissions |
|----------|----------------------|
| `GET /audit` | `audit.entry.read` (+ `.read.sensitive` iff `includeSensitive=true`) |
| `GET /audit/{id}` | `audit.entry.read` (+ `.read.sensitive` for `Sensitive` rows) |
| `POST /audit/export` | `audit.entry.export` (+ `.read.sensitive` iff `includeSensitive=true`) |
| `POST /audit/holds` | `audit.entry.admin` (+ `.read` visibility of targets) |
| `POST /audit/holds/{id}/release` | `audit.entry.admin` |
| `POST /audit/admin/purge-expired` | `audit.entry.admin` |

`audit.entry.admin` never implies read access; hold placement still requires
read-level visibility of its targets. The full matrix is ratified in ADR-027
decision 12 and registered in the Authorization permission catalog at the
Prompt 12C gate — no permission code changes in this gate.
