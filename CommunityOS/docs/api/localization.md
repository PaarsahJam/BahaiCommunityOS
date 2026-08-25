# Localization API Contract

> **STATUS: IMPLEMENTED (Prompt 16F).** Contract for the
> Localization service API, ratified by ADR-029 at the Prompt 16E gate.
> All endpoints described here exist in `CommunityOS.Localization.API`;
> this document remains the authoritative contract.

Base URL route root: `/api/v{version}/localization` (ASP.NET versioning,
default `1.0`). JSON is camelCase. Authentication: Identity-issued RS256
bearer tokens; a `sub` claim is mandatory on every endpoint. All
authorization is capability-based via the Authorization service (exact-match
permission membership); fail-closed; missing and unauthorized single
resources are indistinguishable (`404` anti-enumeration). Errors use
ProblemDetails-shaped bodies produced by the shared validation pipeline and
exception middleware: `400` validation, `401` unauthenticated, `403`
forbidden, `404` missing/unauthorized, `409` illegal lifecycle transition or
conflict, `500` unexpected.

## Capabilities referenced below

`localization.locale.read` → `.locale.manage` →
`localization.resource.read` → `.resource.propose` → `.resource.review`

No capability implies another. The mapping ratified in ADR-029 (decision 11,
as amended): GlobalAdministrator and NationalAdministrator hold all five;
LocalAdministrator holds `locale.read` + `resource.read` +
`resource.propose`; CommitteeMember and Member hold `locale.read` +
`resource.read`; Volunteer/Guest/PlatformServicePrincipal hold none.

## Locales

### GET /locales — `localization.locale.read`
All locales with their activation state and default flag.

### POST /locales — `localization.locale.manage`
Create a locale candidate (BCP-47 validated; duplicates → `409`). New
locales start inactive.

### POST /locales/{code}/activate — `localization.locale.manage`
Activate a locale. Activating a locale that is already active is idempotent.

### POST /locales/{code}/deactivate — `localization.locale.manage`
Deactivate a locale. Deactivating the default is rejected (`409`): exactly
one default exists at all times.

## Namespaces

### GET /resources/namespaces — `localization.resource.read`
All namespaces with descriptions.

### POST /namespaces — `localization.resource.propose`
Create a namespace. Body: `{ "name", "description" }`. Names are unique,
lower-cased dotted identifiers; reserved prefixes (`library.`,
`knowledge.`) are rejected (`409`) per the Knowledge/Library boundary.

## Resource entries

### GET /resources/entries — `localization.resource.read`
Deterministic keyset walk of the catalog. Query: `namespaceId`, `state`
(whitelist: draft, in_review, approved, rejected, superseded, deprecated),
`search` (key substring), `cursor`, `limit` (omitted/0 → default 25;
clamped to max 100). List responses are **metadata-only** — revision values
are never included (ADR-029 decision 10). Response shape:
`{ "items", "nextCursor" }`; `nextCursor` is an opaque continuation token
and is `null` on the final page — keep walking while it is non-null to
retrieve every match exactly once. State filtering happens inside the page
query, so filtered walks are complete regardless of page size.

### POST /resources/entries — `localization.resource.propose`
Create an entry with its first draft revision. Namespace via
`?namespaceId=`. Body: `{ "key", "culture", "value" }`. Keys are unique per
namespace (case-sensitive ordinal comparison), limited to ASCII letters and
digits plus `.`, `_`, `-` and `:` with a maximum length of 200. The target
culture must be an active locale.

### POST /resources/entries/{id}/revisions — `localization.resource.propose`
Add a draft revision for a culture. Body: `{ "culture", "value" }`.

### POST /resources/entries/{id}/revisions/{revisionId}/submit — `localization.resource.propose`
Move a draft to review. Only drafts may be submitted.

### POST /resources/entries/{id}/revisions/{revisionId}/approve — `localization.resource.review`
Approve a revision. Approving supersedes any previously approved revision
for the same culture verbatim (history preserved). Every approval bumps the
catalog bundle version and publishes `LocalizationCatalogChanged`.

### POST /resources/entries/{id}/revisions/{revisionId}/reject — `localization.resource.review`
Reject a revision from review.

### DELETE /resources/entries/{id} — `localization.resource.review`
Deprecate an entry (soft delete; nothing is ever hard-deleted). Publishes
with culture `"*"`.

## Entity translations

### POST /entity-translations/query — `localization.resource.read`
Resolve translations. Body: `{ "sourceContext", "entityType", "entityIds",
"field", "culture", "includePending?" }`. Approved values win; with
`includePending`, latest in-review/draft candidates are returned separately.
Reserved source contexts are rejected before any lookup.

### PUT /entity-translations/batch — `localization.resource.propose`
Upsert up to 200 translations in one call. Items are deduplicated by
`(sourceContext, entityType, entityId, field, culture)` within the batch;
each upsert adds a draft revision to the existing record or creates the
record. Deprecated records refuse new revisions (`409`) — the batch is
rejected as a unit; deprecation is terminal.

### POST /entity-translations/{id}/submit · /approve · /reject — propose / review
Same transition semantics as resource revisions; approval publishes the
catalog-change event with the translation's context.

### DELETE /entity-translations/{id} — `localization.resource.review`
Deprecate (soft).

## Suggestions

### GET /suggestions — `localization.resource.review`
List suggestions. Query: `status` (pending, accepted_into_review, rejected),
`limit`.

### POST /suggestions/{id}/accept-into-review — `localization.resource.propose`
Accept a pending suggestion into the standard review workflow of its target
(resource entry or entity translation). Performed atomically as one save:
the suggestion transitions to `accepted_into_review` while a new InReview
revision appears on the target. The acceptor acts as proposer; original
provenance (human or `machine:<provider>`) is preserved verbatim. Acceptance
never publishes. Suggestions targeting entity translations must match the
target's culture.

### POST /suggestions/{id}/reject — `localization.resource.review`
Reject a pending suggestion.

## Export

### GET /export/bundles — `localization.resource.read`
Export a versioned bundle for client consumption. Query: `culture`,
`namespace?`. Resolution walks the fallback chain
(requested → language → default locale) and fails visibly: keys without an
approved value anywhere in the chain are omitted, and the client renders the
key identifier itself (fail-visible, never silently substituted). Hard cap
10 000 keys per export. Responses carry a deterministic ETag
(`"loc-v{bundleVersion}"`).
