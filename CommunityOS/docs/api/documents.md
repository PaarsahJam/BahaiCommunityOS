# Documents Service API

> **STATUS: RATIFIED (Prompt 07A-R).** Contract for the future Documents service,
> aligned with ratified ADR-022 and `docs/documents.md`. Not implemented;
> implementation proceeds in Prompt 07B.

All endpoints are versioned under `/api/v1/documents`, require a valid access
token (`[Authorize]`), and return DTOs — **EF entities are never exposed**.
Every guarded operation is evaluated against the Authorization service
(fail-closed). Metadata access and binary content access are separate
endpoints with separate permissions. Content is streamed; it never appears in a
JSON response.

## Documents — `/documents`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/documents?organizationUnitId=&ownerType=&ownerId=&status=&isSensitive=&query=` | `documents.document.read` | List accessible document metadata |
| GET | `/documents/{id}` | `documents.document.read` | Get document metadata (not content) |
| POST | `/documents` | `documents.document.create` | Create a document (metadata, optional initial content) |
| PUT | `/documents/{id}/metadata` | `documents.document.metadata.update` | Update title / description |
| POST | `/documents/{id}/classify` | `documents.document.classify` | Set classification, sensitive flag, retention category, hold references |
| POST | `/documents/{id}/scopes` | `documents.document.scope.manage` | Add an organization scope |
| DELETE | `/documents/{id}/scopes/{organizationUnitId}` | `documents.document.scope.manage` | Remove an organization scope |
| POST | `/documents/{id}/archive` | `documents.document.archive` | Transition `Active → Archived` |
| POST | `/documents/{id}/deactivate` | `documents.document.deactivate` | Transition to `Deactivated` (soft-delete) |
| POST | `/documents/{id}/restore` | `documents.document.restore` | Restore from `Archived`/`Deactivated` |

**Create** body (metadata only; content via a subsequent version upload):

```json
{
  "title": "Nineteen Day Feast Agenda",
  "description": "Draft agenda for the feast consultation.",
  "owner": { "ownerType": "organizationUnit", "ownerId": "00000000-0000-0000-0000-000000000001" },
  "organizationUnitId": "00000000-0000-0000-0000-000000000001",
  "isSensitive": false
}
```

**Classify** body:

```json
{
  "classificationCode": "internal",
  "isSensitive": true,
  "retentionCategory": "administrative-7y",
  "legalHoldReference": null,
  "administrativeHoldReference": null
}
```

`classificationCode`, `retentionCategory` and hold references are reserved
strings pending the ratified classification/Records models. A document with an
active hold reference cannot be deactivated (`409`) except by an
administrative override.

## Versions — `/documents/{id}/versions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/documents/{id}/versions` | `documents.document.read` | List version metadata |
| GET | `/documents/{id}/versions/{versionNumber}` | `documents.document.read` | Get version metadata |
| POST | `/documents/{id}/versions` | `documents.document.version.create` | Upload a new version (multipart) |

**Upload** — `multipart/form-data` with a `file` part. The service computes the
SHA-256, stores the content under the content-addressed key, creates an
immutable version, and moves the current-version pointer.

- Same document + same content hash → returns the existing version (idempotent),
  not a duplicate.
- Exceeding `MaxFileSizeBytes` → `413 Payload Too Large`.
- Disallowed MIME type → `415 Unsupported Media Type`.

## Content — `/documents/{id}/content`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/documents/{id}/content` | `documents.document.content.read` (+ `documents.document.content.read.sensitive` for sensitive documents) | Stream the current version |
| GET | `/documents/{id}/versions/{versionNumber}/content` | same | Stream a specific version |

Responses are streamed with `Content-Type` (from version MIME), `Content-Length`
(from version size) and `Content-Disposition: attachment; filename=...`. When
scanning is enabled, versions that are `Scanning`, `Quarantined` or `Rejected`
are **not** downloadable (fail closed). Sensitive downloads emit a
`DocumentContentDownloaded` audit event (ratified; targeted at future security/
audit consumers; never binary content, secrets or unnecessary personal
information).

## References — `/documents/{id}/references`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/documents/{id}/references` | `documents.document.reference.read` | List references (which contexts attach this document) |
| POST | `/documents/{id}/references` | `documents.document.reference` | Create a reference |

**Create reference** body:

```json
{
  "sourceContext": "records.record",
  "sourceEntityId": "00000000-0000-0000-0000-000000000002",
  "referenceType": "evidence"
}
```

The referencing entity's lifecycle stays with its owning context; the reference
is a Documents-side convenience for enumerating attachments.

## DTOs (conceptual)

- `DocumentDto` — id, title, description, status, owner, primary org unit,
  additional scopes, classification (code, sensitive, retention category,
  holds), current version descriptor, created/updated provenance.
- `DocumentSummaryDto` — id, title, status, classification, primary scope,
  current version number, updated timestamp (for lists).
- `DocumentVersionDto` — id, versionNumber, mimeType, sizeBytes, fileName,
  contentHash, uploadedBy, uploadedOn, source, scanStatus.
- `DocumentReferenceDto` — id, sourceContext, sourceEntityId, referenceType,
  createdBy, createdOn.
- Request records — `CreateDocumentRequest`, `UpdateDocumentMetadataRequest`,
  `ClassifyDocumentRequest`, `AddScopeRequest`, `CreateReferenceRequest`.

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure, invalid owner/scope reference |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) or content not downloadable (scan state) |
| 404 | Document / version not found |
| 409 | Invalid transition, duplicate content, deactivation blocked by hold |
| 413 | Payload exceeds the maximum file size |
| 415 | Disallowed MIME type |

## Service-to-service notes

Documents exposes no cross-service fact endpoint yet (unlike Organization
`/covers` or Knowledge citation resolution); no current consumer requires one.
If the future Records/Workflow integration needs an internal fact query, it
will follow the established `X-Client-Id` internal-header pattern and be
documented here.