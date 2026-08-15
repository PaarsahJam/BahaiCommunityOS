# Records Service API

> **STATUS: RATIFIED (Prompt 08A-R).** Contract for the future Records service,
> aligned with ratified ADR-023 and `docs/records.md`. Not implemented;
> implementation proceeds in Prompt 08B.

All endpoints are versioned under `/api/v1/records`, require a valid access
token (`[Authorize]`), and return DTOs — **EF entities are never exposed**.
Every guarded operation is evaluated against the Authorization service
(fail-closed). Metadata access and sensitive-field access are separate
capabilities with separate permissions. Records stores no binary content;
evidence is referenced by `DocumentId`/`VersionNumber` and served by the
Documents API.

## Records — `/records`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/records?category=&status=&organizationUnitId=&personId=&householdId=&isSensitive=&query=` | `records.record.read` | List accessible record metadata (non-sensitive) |
| GET | `/records/{id}` | `records.record.read` | Get record metadata, current version descriptors, evidence and hold references (non-sensitive fields) |
| GET | `/records/{id}/sensitive` | `records.record.read.sensitive` | Get the sensitive fields of the record |
| POST | `/records` | `records.record.create` | Create a Draft record |
| PUT | `/records/{id}/fields` | `records.record.update` | Update non-authoritative fields of a Draft/Submitted/Under-Review record |
| POST | `/records/{id}/submit` | `records.record.submit` | Transition `Draft → Submitted` |
| POST | `/records/{id}/under-review` | `records.record.verify` | Transition `Submitted → Under Review` |
| POST | `/records/{id}/verify` | `records.record.verify` | Transition `Under Review → Verified` (creator excluded) |
| POST | `/records/{id}/reject` | `records.record.verify` | Transition `Under Review → Rejected` (creator excluded) |
| POST | `/records/{id}/correct` | `records.record.correct` | Apply a post-verification correction (new superseding version) |
| POST | `/records/{id}/classify` | `records.record.classify` | Set classification, sensitive flag, retention schedule reference |
| POST | `/records/{id}/archive` | `records.record.archive` | Transition `Verified → Archived` |
| POST | `/records/{id}/deactivate` | `records.record.deactivate` | Transition to `Deactivated` (soft-delete; blocked by active holds) |
| POST | `/records/{id}/restore` | `records.record.restore` | Restore from `Archived`/`Deactivated` |
| POST | `/records/{id}/scopes` | `records.record.scope.manage` | Add an organization scope |
| DELETE | `/records/{id}/scopes/{organizationUnitId}` | `records.record.scope.manage` | Remove an organization scope |

**Create** body:

```json
{
  "category": "birth",
  "subject": { "subjectType": "person", "subjectId": "00000000-0000-0000-0000-000000000001" },
  "organizationUnitId": "00000000-0000-0000-0000-000000000002",
  "fields": [
    { "fieldKey": "name", "value": "...", "isSensitive": false },
    { "fieldKey": "dateOfBirth", "value": "1970-01-01", "isSensitive": false }
  ],
  "isSensitive": false
}
```

The record is created in `Draft`. Non-authoritative fields remain editable in
`Draft`, `Submitted` and `Under Review` (`records.record.update`); a `Verified`
record requires the `correct` endpoint, which appends a superseding version and
never mutates the authoritative baseline in place.

**Classify** body:

```json
{
  "classificationCode": "internal",
  "isSensitive": true,
  "retentionScheduleCode": "administrative-7y"
}
```

`classificationCode` and `retentionScheduleCode` are reserved strings pending
the ratified classification/Records models.

## Versions — `/records/{id}/versions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/records/{id}/versions` | `records.record.read` | List version descriptors |
| GET | `/records/{id}/versions/{versionNumber}` | `records.record.read` | Get a version (fields gated by sensitivity) |
| GET | `/records/{id}/versions/{versionNumber}/sensitive` | `records.record.read.sensitive` | Get a version's sensitive fields |

Versions are immutable once written. The first `Verified` transition freezes
the working field set into the authoritative baseline
(`VersionNumber = 1`); each post-verification correction appends a new
superseding version. Version numbers are assigned by the service, never by the
client.

**Correct** body:

```json
{
  "fields": [
    { "fieldKey": "dateOfBirth", "value": "1970-01-01", "isSensitive": false }
  ],
  "changeReason": "Corrected the recorded birth date per official certificate"
}
```

`changeReason` is required for post-verification corrections and is never
exported in integration events.

## Evidence — `/records/{id}/evidence`

Evidence links a record to specific document versions (the Documents-side mirror
is `DocumentReference` with `SourceContext = records.record`).

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/records/{id}/evidence` | `records.record.read` | List evidence references |
| POST | `/records/{id}/evidence` | `records.record.evidence.manage` | Attach a document version as evidence |
| DELETE | `/records/{id}/evidence/{evidenceId}` | `records.record.evidence.manage` | Remove an evidence reference |

**Attach** body:

```json
{
  "documentId": "00000000-0000-0000-0000-000000000003",
  "versionNumber": 2,
  "referenceType": "evidence"
}
```

Attachment calls the Documents reference surface as the `communityos-records`
service principal; Records never reads the Documents database. Evidence on a
deactivated document is flagged for reconciliation via
`DocumentDeactivated`/`DocumentRestored`.

## Holds — `/holds`

Holds freeze disposition. An active hold on a record blocks deactivation, and a
hold targeting a document is mirrored onto the document so Documents enforces
its own deactivation-protection rule.

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/holds?recordId=&holdType=&status=` | `records.record.read` | List holds |
| GET | `/holds/{id}` | `records.record.read` | Get a hold (reason is restricted) |
| POST | `/holds` | `records.hold.manage` | Place a legal/administrative hold |
| POST | `/holds/{id}/release` | `records.hold.manage` | Release a hold (releaser must differ from placer) |

**Place** body:

```json
{
  "holdType": "legal",
  "reason": "Pending court determination re: recorded parentage",
  "recordId": "00000000-0000-0000-0000-000000000004",
  "documentReferences": [
    { "documentId": "00000000-0000-0000-0000-000000000003", "versionNumber": 2 }
  ]
}
```

`reason` is stored and visible only to authorized holders of
`records.hold.manage`; it is never exported in events or logs. Deactivating a
record with an active hold returns `409` unless the actor holds
`records.record.admin` and supplies a reason.

`RecordHoldPlaced`/`RecordHoldReleased` intentionally carry no document
references. Consumers needing document-level hold coverage resolve it through
this endpoint (`GET /holds/{id}`), not through the integration events.

## Retention — `/retention`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/retention/schedules` | `records.record.read` | List retention schedules |
| GET | `/retention/schedules/{code}` | `records.record.read` | Get a retention schedule with its rules |
| POST | `/retention/schedules` | `records.retention.manage` | Create a retention schedule |
| PUT | `/retention/schedules/{code}` | `records.retention.manage` | Update a retention schedule |
| DELETE | `/retention/schedules/{code}` | `records.retention.manage` | Retire a retention schedule (only when unused) |

**Create schedule** body:

```json
{
  "code": "administrative-7y",
  "categoryCodes": ["administrative"],
  "rules": [
    { "retentionPeriod": "P7Y", "startTrigger": "recordDate", "disposition": "review" }
  ]
}
```

`disposition` is `review` only in this prompt: retention expiry never destroys
data; it flags the record for a ratified disposition review and raises
`RecordRetentionExpired`.

## Categories — `/categories`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/categories` | `records.record.read` | List the category catalog |
| POST | `/categories` | `records.category.manage` | Add a category code |
| PUT | `/categories/{code}` | `records.category.manage` | Update category metadata / default retention |

The baseline catalog is `birth`, `marriage`, `death`, `membership`,
`appointment`, `official-community`, `administrative`. Codes are stable strings,
extensible via configuration.

## DTOs (conceptual)

- `RecordDto` — id, category, status, subject, primary org unit, additional
  scopes, classification (code, sensitive, retention schedule), current version
  descriptor, evidence summary, hold summary, created/updated/verified
  provenance.
- `RecordSummaryDto` — id, category, status, subject type, primary scope,
  current version number, updated timestamp (for lists).
- `RecordVersionDto` — id, versionNumber, supersedesVersionNumber, appliedBy,
  appliedOn, changeReason, fieldCount (fields streamed separately).
- `RecordFieldDto` — fieldKey, value, isSensitive.
- `RecordEvidenceReferenceDto` — id, documentId, versionNumber, referenceType,
  attachedBy, attachedOn.
- `RecordHoldDto` — id, holdType, status, recordId, placedBy, placedOn,
  releasedBy, releasedOn (reason exposed only under `records.hold.manage`).
- Request records — `CreateRecordRequest`, `UpdateRecordFieldsRequest`,
  `VerifyRecordRequest`, `CorrectRecordRequest`, `ClassifyRecordRequest`,
  `AttachEvidenceRequest`, `PlaceHoldRequest`, `CreateRetentionScheduleRequest`.

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure, invalid subject/scope/category reference |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Record / version / evidence / hold / schedule not found (also for unauthorized reads) |
| 409 | Invalid transition, field update on a Verified record, creator-as-verifier, hold release by placer, deactivation blocked by hold, duplicate evidence |

## Service-to-service notes

Records exposes no cross-service fact endpoint yet (like Documents). If a future
consumer (e.g. Workflow or Finance) needs an internal fact query — for example
to resolve a record's authoritative baseline or retention state — it will follow
the established `X-Client-Id` internal-header pattern (`Records:InternalClientId`)
and be documented here. The `DocumentDeactivated`/`DocumentRestored` consumer is
an inbound integration, not an exposed endpoint; it requires the transactional
outbox gate (ADR-015) to be enabled before production use.