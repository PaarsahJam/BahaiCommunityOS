# Prompt 08D — Remediation Completion Report

> **STATUS: COMPLETED (Prompt 08D).** Remediation of the three NON-BLOCKING
> findings identified in the Prompt 08C verification (F-01, F-02, F-03).
> Scope was strictly limited to those three findings. No Prompt 09 / other
> bounded context work was started, no ADR decision was changed, and no
> unrelated service was modified.

## Scope

- **F-01 — Query parameter no-op** — `GET /api/v1/records?query=` was accepted
  and documented but silently ignored.
- **F-02 — Category default retention** — `docs/records.md` and
  `docs/api/records.md` claimed a "default sensitivity" / "default retention"
  (`RetentionScheduleCode`) on record categories that the implementation does
  not provide.
- **F-03 — JWT `RequireHttpsMetadata`** — the Records API base
  `appsettings.json` set `Jwt:RequireHttpsMetadata: "false"` with no Production
  override, unlike the established Community/Documents/Knowledge pattern.

Preserved throughout: the fail-closed authorization model, RS256/JWKS
validation, multi-scope authorization, the Records/Documents boundary, and the
transactional outbox (none of these files changed).

---

## F-01 — Query parameter no-op

### Root cause

`ListRecordsQuery` declared a `Query` member (`RecordQueries.cs:22`) and
`RecordsController.List` bound `[FromQuery] string? query`, but the handler's
filter chain (`RecordQueries.cs:41-51`) filtered only on `Category`, `Status`,
`IsSensitive`, `PersonId` and `HouseholdId` — the free-text parameter was never
consumed.

### Contract intent

`docs/api/records.md:19` ratifies `query=` as a list parameter. The repository
convention for free-text list search is a case-insensitive substring match over
the content text: Knowledge (`KnowledgeQueries.cs:36-38` matches question
Title/Body) and Documents (`DocumentQueries.cs:47-49` matches Title/Description).
Records has no Title/Body; its content is the typed record field values, and the
list endpoint exposes non-sensitive metadata only. The intended behavior is a
case-insensitive substring match over the record's current **non-sensitive**
field values, with sensitive values never searched (fail-closed — a search can
never reveal sensitive data through the non-sensitive list).

### Remediation

Implemented in
`src/Services/Records/CommunityOS.Records.Application/Queries/RecordQueries.cs`:

- Added a `query=` filter to the candidate chain (before the fail-closed
  authorization filter, so only matching records are authorized/batch-checked).
- Added `MatchesQuery`: matches against the record's current effective field set
  (`CurrentVersion.Fields` once verified, otherwise `WorkingFields`), excluding
  `IsSensitive` fields, using `StringComparison.OrdinalIgnoreCase`. Null,
  empty or whitespace queries are treated as no filter.
- Documented the exact semantics in `docs/api/records.md` (right after the
  records endpoint table), removing all ambiguity about the parameter.

### Tests added

`tests/Unit/CommunityOS.Records.Tests/Queries/ListRecordsQuerySearchTests.cs` (4
tests):

1. `Query_filters_by_non_sensitive_field_value_case_insensitively` — the search
   matches non-sensitive field values case-insensitively.
2. `Query_never_matches_sensitive_field_values` — a record matching only through
   a sensitive field is never returned (fail-closed).
3. `Null_or_whitespace_query_returns_all_readable_records` — no filter when the
   parameter is empty/whitespace.
4. `Query_matches_the_current_verified_version_not_stale_working_fields` — after
   a post-verification correction, the search reflects the current superseding
   version, not the stale working field set.

### Verification results

Records unit suite: **73/73 passed** (64 existing + 9 new — 4 search tests + 5
config tests).

---

## F-02 — Category default retention

### Root cause

`docs/records.md:99-100` stated "Each category may carry a default sensitivity
and a default `RetentionScheduleCode`" and `docs/api/records.md:201` listed
`PUT /categories/{code}` as "Update category metadata / default retention".
The implemented `RecordCategory` (`RecordCategory.cs`) carries only
`Code`/`DisplayName`/`Description`/`IsRetired`/provenance, and
`CategoriesController.Update` accepts only `DisplayName`/`Description`. No code
path ever derives classification or retention from a category.

### Contract intent

ADR-023 (the authoritative decision record, `ADR.md:588-591`) ratifies the
category catalog as "stable string codes … configuration, never a hard enum".
It does **not** ratify default sensitivity or default retention on categories.
Classification and retention are set per record via `records.record.classify`
(ADR-023 permission matrix, `ADR.md:646`). The category default-retention claims
were documentation overreach beyond the ratified decision.

### Remediation

Corrected the documentation to match the ratified ADR-023 taxonomy and the
implemented model (no architecture was invented):

- `docs/records.md:96-102` — replaced the default-sensitivity/default-retention
  sentence with the accurate statement: categories carry
  `DisplayName`/`Description` metadata, can be retired (soft state) but are
  never deleted, and classification/retention are set per record
  (`records.record.classify`), never derived from the category.
- `docs/api/records.md:201` — `PUT /categories/{code}` description changed to
  "Update category metadata (display name, description)".

No code or migration changes were required.

### Tests added

None required — this is a documentation correction; the implemented behavior is
already covered by the existing catalog and classification tests.

### Verification results

No behavior change; full build and all suites pass (see below).

---

## F-03 — JWT `RequireHttpsMetadata`

### Root cause

The Records API base `appsettings.json` set
`Jwt:RequireHttpsMetadata: "false"` (intentional for local HTTP development,
matching the repository-wide convention), but unlike Community, Documents and
Knowledge, Records shipped **no** `appsettings.Production.json` override, so a
Production deployment using the shipped file would fetch OIDC discovery metadata
over HTTP. The runbook (`docs/runbooks/records.md:144`) already specified
`true` in prod — the enforcement file was simply missing.

### Remediation

Followed the established Community/Documents/Knowledge pattern exactly:

- Added `src/Services/Records/CommunityOS.Records.API/appsettings.Production.json`
  with `Jwt:RequireHttpsMetadata: "true"` and HTTPS placeholder Identity
  discovery/issuer URLs (`https://PLACEHOLDER-identity.example.com`),
  mirroring `Knowledge.API/appsettings.Production.json` byte-for-byte in shape.
  The base `appsettings.json` intentionally keeps `false` for local development
  (repo convention; `RecordsJwtValidation.Configure` also fails closed to
  `true` when the setting is absent).
- The file is auto-loaded by `WebApplication.CreateBuilder(args)` under the
  `Production` environment (`appsettings.{Environment}.json`).

### Tests added

`tests/Unit/CommunityOS.Records.Tests/Security/RecordsJwtConfigurationTests.cs`
(5 tests), with the API's `appsettings.json` and
`appsettings.Production.json` linked into the test output under `Config/` via
the test project file:

1. `Base_configuration_uses_the_intentional_local_development_https_opt_out` —
   base config is `false` (documents the deliberate dev default).
2. `Production_configuration_forces_https_metadata_discovery` — Production
   config is `true` and `Authority`/`MetadataAddress`/`Issuer` are `https://`.
3. `Configure_fails_closed_when_RequireHttpsMetadata_is_absent` —
   `RecordsJwtValidation.Configure` defaults to `true` when unset.
4. `Configure_under_production_configuration_requires_https_metadata` — the
   resulting `JwtBearerOptions.RequireHttpsMetadata` is `true` with an `https://`
   metadata address under the Production config.
5. `Configure_under_base_configuration_allows_http_for_local_development` — the
   base config intentionally yields `false`/`http://` for local use.

### Verification results

Records unit suite: **73/73 passed** (includes the 5 new config tests). The
linked `Config/appsettings.json` + `Config/appsettings.Production.json` were
confirmed copied to the test output and loaded correctly.

---

## Verification results (summary)

| Check | Result |
|-------|--------|
| `dotnet build CommunityOS.sln` | **Build succeeded, 0 warnings, 0 errors** |
| `dotnet ef migrations has-pending-model-changes` (Records Infrastructure) | "No changes have been made to the model since the last migration." |
| Records unit tests | **73/73 passed** (64 existing + 9 new) |
| Authorization unit tests | **118/118 passed** |
| Organization unit tests | **79/79 passed** |
| Community unit tests | **106/106 passed** |
| Knowledge unit tests | **49/49 passed** |
| Documents unit tests | **56/56 passed** |
| Git diff review | Only the 7 intended files changed (see below); no unrelated service or ADR touched |

### Files changed

- `src/Services/Records/CommunityOS.Records.Application/Queries/RecordQueries.cs` — F-01 implementation.
- `src/Services/Records/CommunityOS.Records.API/appsettings.Production.json` — F-03 (new).
- `tests/Unit/CommunityOS.Records.Tests/CommunityOS.Records.Tests.csproj` — F-03 (link config files into test output).
- `tests/Unit/CommunityOS.Records.Tests/Queries/ListRecordsQuerySearchTests.cs` — F-01 tests (new).
- `tests/Unit/CommunityOS.Records.Tests/Security/RecordsJwtConfigurationTests.cs` — F-03 tests (new).
- `docs/api/records.md` — F-01 search semantics + F-02 category description.
- `docs/records.md` — F-02 category description.

## Remaining environment limitations

- Docker is unavailable in this environment. The Records Testcontainers
  integration suite
  (`tests/Integration/CommunityOS.Records.IntegrationTests`) still **compiles
  but is not executed**; its assertions remain unverified at runtime and are
  reported honestly as unexecuted, not passed. None of the 08D changes affect
  EF mappings or persistence, so this limitation does not gate the 08D verdict.
- `Jwt:Authority`/`MetadataAddress`/`Issuer` in the new Production file are
  placeholders (`https://PLACEHOLDER-identity.example.com`) and must be
  substituted with the real Identity endpoint at deployment time, exactly as in
  the existing Community/Documents/Knowledge Production files.

## Final verdict

**PASS.**

- F-01 — resolved by implementing the documented `query=` search (fail-closed
  over non-sensitive field values) with regression tests.
- F-02 — resolved by correcting the documentation to the ratified ADR-023
  taxonomy (no invention of behavior).
- F-03 — resolved by adding the Production configuration override per the
  established repository convention, with configuration regression tests
  proving Production can no longer silently run with
  `RequireHttpsMetadata=false`.

Prompt 08D is complete. **Stopping here per instructions — Prompt 09 is not
started.**