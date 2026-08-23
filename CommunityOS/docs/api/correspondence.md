# Correspondence API Contract

> **STATUS: IMPLEMENTED (Prompt 16).** Contract for the
> Correspondence service API, ratified by ADR-028 at the Prompt 12H gate.
> All endpoints described here exist in `CommunityOS.Correspondence.API`;
> this document remains the authoritative contract.

Base URL route root: `/api/v{version}/correspondence` (ASP.NET versioning,
default `1.0`). JSON is camelCase. Authentication: Identity-issued RS256
bearer tokens; a `sub` claim is mandatory on every endpoint. All
authorization is capability-based via the Authorization service (exact-match
permission membership); fail-closed; missing and unauthorized single resources
are indistinguishable (`404` anti-enumeration). Errors use ProblemDetails-
shaped bodies produced by the shared validation pipeline and exception
middleware: `400` validation, `401` unauthenticated, `403` forbidden,
`404` missing/unauthorized, `409` illegal lifecycle transition or conflict,
`500` unexpected.

## Capabilities referenced below

`correspondence.letter.read` · `.read.sensitive` · `.create` · `.update` ·
`.submit` · `.cancel` · `.export` · `.admin` ·
`correspondence.template.read` · `correspondence.template.manage`

No capability implies another. Hold placement additionally requires read-level
visibility (including the sensitive second pass) of every target letter.

## Letters

### GET /letters

Query/list letters visible to the caller. Capability:
`correspondence.letter.read`; when `includeSensitive=true`, additionally
requires `correspondence.letter.read.sensitive` (sensitive letters are absent
entirely otherwise).

Query parameters: `status`, `category`, `organizationUnitId`, `submittedFrom`,
`submittedTo`, `reference` (exact reference-number match only — there is no
subject or body search), `order` (`asc`|`desc`, default `desc`, ordered by
submission/creation time with deterministic id tiebreak), `limit`
(default 25, clamped to [1,100]), `offset` (≥0), `includeSensitive` (default
false).

`200 OK` → page DTO:

```json
{
  "items": [ { "id": "...", "reference": "2026-00042", "subject": null,
               "category": "official", "sensitivity": "normal",
               "status": "dispatched", "recipientCount": 1,
               "organizationUnitId": "...", "createdOn": "...",
               "submittedOn": "..." } ],
  "limit": 25, "offset": 0, "returned": 1
}
```

List items are metadata-only — subjects and bodies are never included in list
results.

### GET /letters/{id}

Single letter including content. Capability: `correspondence.letter.read`;
sensitive letters additionally require `.read.sensitive`.
`200 OK` → full letter DTO (id, reference, subject, body, category,
sensitivity, status, revision, organizationUnitId, recipients [{kind,
personId?/unitId?/displayLine?}], documentReference?, relatedLetterId?,
audit timestamps) — or `404` for missing and unauthorized alike.

### POST /letters

Create a draft. Capability: `correspondence.letter.create`.
Body: `{ category, sensitivity ("normal"|"sensitive"), subject (≤200),
body, organizationUnitId, recipients: [...], templateId?, relatedLetterId? }`.
Recipients: kind `person` requires `personId`; kind `unit` requires
`unitId`; kind `external` requires bounded display lines; person/unit ids are
validated non-empty; external display lines are required and length-bounded.
`201 Created` → letter DTO with `status: "draft"`.

### PUT /letters/{id}/content

Edit draft content. Capability: `correspondence.letter.update`.
Body: `{ subject, body, expectedRevision }`. Allowed only while `Draft`;
revision mismatch → `409 Conflict`. `200 OK` → updated letter DTO (revision
incremented).

### POST /letters/{id}/confirm · POST /letters/{id}/unconfirm

Capability: `correspondence.letter.update`. Confirm moves Draft→Confirmed;
unconfirm reverts Confirmed→Draft. Illegal current state → `409`.

### POST /letters/{id}/submit

Submit the confirmed letter. Capability: `correspondence.letter.submit`.
Allocates the per-unit yearly reference number, persists the immutable
submission snapshot, appends history, and publishes `LetterSubmitted` through
the transactional outbox atomically. **Returns `202 Accepted`** — submission
is complete; materialization by Documents is asynchronous and dispatch remains
blocked until it confirms. Re-submission of an already-submitted letter →
`409`.

### POST /letters/{id}/cancel

Cancel pre-dispatch. Capability: `correspondence.letter.cancel`.
Body: `{ reasonCode }` from the ratified set {draft-error, superseded,
withdrawn-by-institution, other}. Legal from Draft/Confirmed/Submitted/
Materialized; `409` once Dispatched or already terminal. The reference number
is consumed permanently.

### POST /letters/{id}/dispatch

Record a manual dispatch (first gate has no providers). Capability:
`correspondence.letter.admin`. Body: `{ methodCode }` (`manual` at this gate)
plus optional free-reference-free note fields limited to codes.
Legal from Materialized → status `Dispatched`; `409` otherwise.

### POST /letters/{id}/delivery

Record a delivery outcome. Capability: `correspondence.letter.admin`.
Body: `{ outcome: "confirmed" | "failed", reasonCode? }` — reason codes from
{bad-address, refused, unclaimed, returned-to-sender, provider-error, other}.
Legal from Dispatched → terminal `Delivered` / `DeliveryFailed`; `409`
otherwise.

### GET /letters/{id}/history

Transition log. Capability: `correspondence.letter.read` (+second pass for
sensitive letters). Ordered chronologically: from/to statuses, cause
(command|event|provider|purge-marker), actor or system, optional reason code,
timestamp.

### POST /letters/export

Capped synchronous export. Capability: `correspondence.letter.export`;
`includeSensitive=true` additionally requires `.read.sensitive`.
Body mirrors Audit's export shape: `{ format ("csv"|"ndjson"), filters,
maxRows? }` — `maxRows` outside [1, `Correspondence:ExportMaxRows`] is
rejected with `400` (never clamped). Streams an attachment
(`text/csv` or `application/x-ndjson`) containing the **metadata index only**:
ids, reference numbers, codes, statuses, timestamps, recipient kinds/counts.
Bodies, subjects and external display lines are never exported.

## Templates

- `GET /templates` — `correspondence.template.read` — active templates
  (code, title, category, timestamps). `200 OK`.
- `POST /templates` — `correspondence.template.manage` — create
  `{ code (unique), title, subjectTemplate, bodyTemplate, category }`.
  `201 Created`; duplicate code → `409`.
- `PUT /templates/{id}` — `correspondence.template.manage` — update fields /
  reactivate. `200 OK`. Creating a letter from a template snapshots its
  skeleton; edits never mutate existing letters.
- `DELETE /templates/{id}` — `correspondence.template.manage` — soft
  deactivation. `204 No Content`.

## Administration

- `POST /admin/reconcile-materializations` — `correspondence.letter.admin` —
  lists letters stuck in `Submitted` beyond
  `Correspondence:MaterializationWarnMinutes` and re-publishes their
  `LetterSubmitted` facts (safe: Documents-side materialization is idempotent
  per source context+entity). `200 OK` → report `{ inspected, republished }`.
- `POST /admin/holds` — `correspondence.letter.admin` (+read visibility of
  every target incl. second pass) — place legal/administrative holds
  `{ entryIds[], holdType, reasonCode }`; fixed reason-code set; whole-batch
  atomicity; conflict on existing active hold → `409`. `201 Created`.
- `POST /admin/holds/{id}/release` — `correspondence.letter.admin` — release;
  unknown → `404`; already released → `409`. `200 OK`.
- `POST /admin/purge-expired` — `correspondence.letter.admin` — one bounded
  retention-purge batch of expired unheld letters behind the database trigger
  guard; body `{ maxBatchSize? }` (omitted/zero → configured default; above
  maximum clamped; negative rejected `400`). `200 OK` →
  `{ purgedCount, remainingExpiredEstimate }`.

## Pagination, sorting, concurrency summary

- Page size default 25, clamp ceiling 100; offset paging; deterministic order.
- Export cap rejected-not-clamped; default = configured maximum.
- Optimistic concurrency on draft content edits via `expectedRevision`.
- Idempotency: state-machine guards plus unique constraints (one submission
  per letter, one materialization link per document, single cancellation);
  broker-level exactly-once via the MassTransit inbox.

## Endpoint inventory

Exactly twenty endpoints are ratified (twelve letter routes, four template
routes, four admin routes). Any additional surface requires an ADR amendment.
