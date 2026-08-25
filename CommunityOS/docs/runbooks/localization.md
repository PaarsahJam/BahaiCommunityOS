# Localization Service Runbook

Operational reference for the Localization service
(`CommunityOS.Localization.{Domain,Application,Infrastructure,API}`).
Authoritative specification: ADR-029 (`docs/architecture/ADR.md`);
contract: `docs/api/localization.md`; context overview:
`docs/localization.md`. This runbook describes the implementation as it
exists — nothing here is aspirational.

## Database

- PostgreSQL; database `communityos_localization`; schema `localization`;
  connection string name `LocalizationDb`.
- Migration: `InitialCreateLocalization` (single migration; created with
  `dotnet ef migrations add InitialCreateLocalization` from the API project).
- Aggregates: `locales`, `resource_namespaces`, `resource_entries`,
  `resource_revisions`, `entity_translations`,
  `entity_translation_revisions`, `translation_suggestions`, plus the
  singleton `catalog_state` row (starts at version 0) and the MassTransit
  outbox tables born into the same context.
- Integrity enforced by partial unique indexes: at most one **approved**
  revision per culture per resource entry and per entity translation.
  Concurrency uses `xmin` tokens — a lost race surfaces as
  `DbUpdateConcurrencyException` → HTTP `409`.
- Soft delete only: deprecation flags (`is_deprecated`) on entries and
  entity translations. Nothing is ever hard-deleted.
- Apply migrations manually in shared environments:

  ```
  dotnet ef database update --project src/Services/Localization/CommunityOS.Localization.Infrastructure \
      --startup-project src/Services/Localization/CommunityOS.Localization.API
  ```

  In Development the API also applies migrations on startup.

## Seed policy

The migration seeds **exactly one locale**: `en`, active, default,
catalog version 0. `fa`/`ar` are activation candidates — an operator must
create them via `POST /locales` and activate them explicitly. The seed
policy is asserted by the integration suite.

## Configuration

`appsettings.json` section `Localization`:

| Key               | Default | Meaning                                   |
|-------------------|---------|-------------------------------------------|
| `MaxPageSize`     | 100     | Walk page-size clamp                      |
| `DefaultPageSize` | 25      | Page size when `limit` is omitted or 0    |
| `MaxBatchUpsert`  | 200     | Entity-translation batch cap              |
| `MaxExportKeys`   | 10000   | Hard cap on keys per export bundle        |
| `MaxValueLength`  | 2000    | Revision value length bound               |

Also required: `AuthorizationService` base URL + client credentials, and
`RabbitMq` bus settings (the outbox persists into PostgreSQL regardless of
broker availability).

## Permissions

Exactly five capabilities, exact-match ordinal strings, no implication
chains (admin does NOT imply read; review does NOT imply propose):

- `localization.locale.read`
- `localization.locale.manage`
- `localization.resource.read`
- `localization.resource.propose`
- `localization.resource.review`

Role mapping: Global/National Administrator hold all five;
LocalAdministrator holds `locale.read` + `resource.read` +
`resource.propose`; CommitteeMember and Member hold `locale.read` +
`resource.read`; Volunteer/Guest hold none. Grants are seeded by the
Authorization service's seeder and evaluated fail-closed per request via
the AuthorizationGuard.

## Catalog lifecycle

- Revisions move draft → in_review → approved/rejected. Approving
  supersedes the previous approved revision for that culture verbatim
  (history preserved). Approved revisions are immutable. Deprecated
  aggregates refuse new revisions; deprecation is terminal.
- Every approval (and every publishing act) bumps the singleton catalog
  version inside the save transaction and emits
  `LocalizationCatalogChanged(BundleVersion, CatalogContext, Culture,
  Namespace?, OccurredOn)` through the transactional outbox — one atomic
  commit for mutation, version bump and event. Payloads carry codes and
  versions only, never translation values (asserted by tests).
- The event currently has **zero consumers**; it exists so downstream
  services can subscribe to bundle-version changes later without a
  contract change.

## Exports

`GET /api/v1/localization/export/bundles?culture=X&namespace=Y` resolves
the BCP-47 fallback chain requested → language → default locale
(deduplicated), fails visibly (keys without an approved value anywhere in
the chain are omitted; clients render the key identifier), caps output at
`MaxExportKeys` and returns deterministic ETags `"loc-v{version}"`.

## Entry walks and pagination

`GET /resources/entries` is a keyset walk ordered by namespace name, key,
id. Responses are metadata-only (no revision values). `nextCursor` is an
opaque continuation token, null on the final page. State filters
(`draft`, `in_review`, `approved`, `rejected`, `superseded`,
`deprecated`) apply inside the SQL query before paging — filtered walks
are complete at any page size.

## Knowledge/Library boundary

Namespaces beginning `library.` or `knowledge.` are rejected, as are
entity-translation source contexts `library` and `knowledge`. Authoritative
Writings content never enters this store.

## AI / machine translation

The `IMachineTranslationSuggestionSource` seam exists but is disabled by
default; no provider ships at this gate. Suggestions enter via the human
suggestion intake and can only reach review by explicit acceptance — they
can never publish directly.

## Local development notes

- Tests requiring PostgreSQL use Testcontainers and therefore need a
  container runtime; on hosts without Docker they compile but are skipped/
  not executed locally — CI runs them. Unit tests run anywhere:
  `dotnet test tests/Unit/CommunityOS.Localization.Tests`.
- Health-check endpoints are not part of this gate's implementation.
