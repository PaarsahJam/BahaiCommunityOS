# Prompt 08C — Records Architecture & Security Verification Report

> **STATUS: COMPLETED (Prompt 08C).** Read-only verification of the Prompt 08B
> Records implementation (ADR-023) against the ratified contract (ADR-015/
> 016/017/018/019/022/023; `docs/records.md`, `docs/api/records.md`,
> `docs/runbooks/records.md`; `docs/documents.md`, `docs/api/documents.md`,
> `docs/runbooks/documents.md`; `docs/authorization.md`) and the implementation
> and its tests.
>
> **Scope of work:** read-only. No source, test, migration, configuration or
> infrastructure files were modified. The sole deliverable of Prompt 08C is this
> report.
>
> **Method:** source inspection (Domain / Application / Infrastructure / API /
> BuildingBlocks), configuration inspection, EF model check
> (`has-pending-model-changes`), full solution build, and execution of every unit
> test suite. Records integration tests (Testcontainers) compile but could **not**
> be executed in this environment (Docker unavailable) — see Area 20.
>
> **Verdict summary:** **PASS WITH FINDINGS.** No BLOCKING finding was
> identified. 3 NON-BLOCKING findings (silent no-op search parameter; category
> default-retention doc/implementation gap; `Jwt:RequireHttpsMetadata=false` in
> the base `appsettings.json` with no production override) and 9 INFORMATIONAL
> findings plus 1 ENVIRONMENT LIMITATION are recorded in the Findings Register.
> Prompt 08D remediation is appropriate (recommendations in the Conclusion).

## Verification environment

| Item | Value |
|------|-------|
| Repo root | `CommunityOS/` |
| Runtime | .NET 10 (targets net9.0; commands prefixed with `DOTNET_ROLL_FORWARD=Major`) |
| Build | `dotnet build CommunityOS.sln` → **Build succeeded, 0 warnings, 0 errors** |
| EF model | `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration." |
| Unit suites | Records **64/64**, Authorization **118/118**, Organization **79/79**, Community **106/106**, Knowledge **49/49**, Documents **56/56** |
| Integration | Records Testcontainers suite compiles only — **ENVIRONMENT LIMITATION** (no Docker) |

---

## Area-by-area verification

### Area 1 — Architecture & ownership — PASS

Ownership boundaries are respected:

- Records owns official records, retention schedules/rules, hold lifecycle and
  the category catalog. Documents owns binary artifacts (referenced by Records,
  never stored by Records). Organization owns hierarchy (events only). Policy is
  owned by Authorization; authentication by Identity.
- Project dependency graph is clean:
  - `Domain` → `SharedKernel` only;
  - `Application` → `Domain`, `Contracts`, `Authorization.Application` (interface
    only, for `AuthorizationGuard`);
  - `Infrastructure` → `Application`, `Infra.Common`, `EventBus`;
  - `API` → `Application`, `Infrastructure`.
- `RecordsDbContext` is the only DbContext; schema `records`, database
  `communityos_records`. No cross-service database reads. Evidence/hold
  references touch Documents only through `HttpDocumentsServiceClient` (never
  the Documents database).

### Area 2 — Lifecycle — PASS

`Draft → Submitted → Under Review → Verified → Archived | Deactivated`, plus
`Rejected` (from Under Review) — matches the ratified lifecycle.

- `Verified` is the authoritative state; field changes after verification go
  through `Correct` (new superseding version), never in-place mutation.
- `UpdateFields` throws for `Verified`/`Archived`/`Deactivated`; `Verify` freezes
  the working fields; `Correct` appends an immutable superseding version with a
  required change reason (max 2000 chars).
- `Rejected` records remain editable but **cannot** be resubmitted (intentional,
  asserted in `RecordLifecycleTests`).
- `Deactivate` is blocked by an active hold unless
  `records.record.admin` + reason; `Archive` requires `Verified`; `Restore`
  returns from `Archived`/`Deactivated`.
- `SetClassification`/`FlagRetentionExpired` block only `Deactivated` (not
  `Archived`) — intentional, so classification/retention-review metadata can be
  adjusted on archived records; the contract docs are silent and the behavior is
  consistent with the retention-review flow (not a defect).

### Area 3 — Versioning / immutability — PASS

- `RecordVersion` is immutable once written; `SupersedesVersionNumber` chains
  corrections; prior versions are never mutated by any command.
- Evidence references pin `DocumentId` + `VersionNumber` (documents are
  immutable-versioned in the Documents service).
- No API path can mutate a historical version's fields.

### Area 4 — Authorization (17-permission matrix) — PASS

- All 17 ratified permissions exist and are used for the correct operations:
  `records.record.create/read/read.sensitive/update/submit/verify/correct/
  archive/deactivate/restore/classify/scope.manage/evidence.manage/admin`,
  `records.retention.manage`, `records.hold.manage`, `records.category.manage`.
- No `[Authorize(Roles = "...")]` anywhere in Records; no local RBAC; policy is
  evaluated only by the Authorization service via `AuthorizationGuard` (fail-closed).
- Every mutating command authorizes **before** mutation + save; denied reads
  surface as `404` (no existence oracle).
- `PermissionCatalog` registers all 17 permissions; `AuthorizationSeeder`
  seeds the developer roles with all 17.

### Area 5 — Multi-scope authorization — PASS

- `RecordAuthorization.ContextsFor` produces one context per unique
  organization scope, or a single global resource context when the record has no
  scopes (global grants apply only to context-free checks — ADR-011 semantics).
- `HasForRecordAsync` is **ANY-of** across scopes: access succeeds when the
  permission is effective at any of the record's scopes.
- List/batch filtering is any-scope with no leak: only records readable by the
  caller are returned, and nothing reveals the existence/count of unreadable
  records (fail-closed at the query boundary).
- Empty-scope records fail closed via the global resource context; denied
  individual reads return `404`.

### Area 6 — Organization boundary — PASS

- `OrganizationIntegrationEventConsumer` consumes
  `OrganizationUnitCreated/Updated/ParentChanged`, is idempotent, and stores
  only the unit id + occurred-on in `organization_unit_references`.
- The read model is **never** used for authorization decisions; authz is resolved
  live by the Authorization service. Hierarchy changes therefore cannot broaden
  access.
- Records never reads the Organization database (ADR-016 pattern).

### Area 7 — Documents boundary — PASS

- Evidence/hold references are commanded onto documents exclusively through the
  Documents API: `HttpDocumentsServiceClient` posts `X-Client-Id:
  communityos-records` to `POST /api/v1/documents/{id}/references` and
  `POST /api/v1/documents/{id}/classify` (both endpoints confirmed to exist in
  `DocumentsController.cs` and to match `docs/api/documents.md`).
- `POST /documents/{id}/classify` is a **full-replacement** operation;
  `HttpDocumentsServiceClient` reads the current classification via
  `GET /api/v1/documents/{id}` and resubmits all five fields
  (`ClassificationCode`, `IsSensitive`, `RetentionCategory`,
  `LegalHoldReference`, `AdministrativeHoldReference`) with the changed hold
  reference — full-replacement semantics respected.
- The client is fail-closed on any non-success/transport failure (empty
  `BaseUrl` throws), and `DocumentsServiceOptions` carries no secret material.
- `DocumentLifecycleIntegrationEventConsumer` reconciles
  `DocumentDeactivated`/`DocumentRestored` by flagging/clearing evidence
  references; it is idempotent and registered only when the outbox gate is on
  (`AddCommunityOSEventBusWithOutbox`).
- Records never touches the Documents database.

### Area 8 — Hold semantics — PASS

- `HoldType` is `legal` | `administrative`; the reason is sensitive and never
  exported in events/logs.
- Separation of duties: `RecordHold.Release` requires the releaser to differ from
  the placer (`held-record` conflict → `409`).
- Active holds freeze disposition: a held record cannot be deactivated
  (`409`) unless `records.record.admin` override + reason; document-level hold
  references are written onto the document so Documents enforces its own
  deactivation-protection rule.
- `RecordHoldPlaced`/`RecordHoldReleased` deliberately carry no document
  references (consumers resolve coverage via `GET /holds/{id}`), matching the
  ratified contract.
- See F-07 (double-release audit overwrite) and F-08 (partial-failure edge).

### Area 9 — Retention — PASS

- `RetentionPeriod` and `MaximumPeriod` are validated as ISO-8601 durations
  (regex + dedicated tests); `maximum_period` nullable `varchar(30)` added by
  the `AddRetentionRuleMaximumPeriod` migration — correct.
- Duplicate retention rules for the same category are rejected (`409`).
- Disposition in this prompt is `review` only; retention expiry **never destroys
  data** — `FlagRetentionExpired` flags the record for a disposition review and
  raises `RecordRetentionExpired` (authorized with `records.record.classify`,
  consistent with it operating on classification metadata).
- See F-06 (no automated trigger wired yet) and F-11 (free-form triggers).

### Area 10 — Classification — PASS

- Classification uses stable string codes (never a fixed enum), with the
  `IsSensitive` operational gate; the 7-code baseline catalog (`birth`,
  `marriage`, `death`, `membership`, `appointment`, `official-community`,
  `administrative`) is seeded idempotently by `RecordsCatalogSeeder`
  (BaselineActorId `00000000-0000-0000-0000-000000000001`) at migration time.
- `GET /records/{id}/sensitive` and version-sensitive endpoints require
  `records.record.read.sensitive` in addition to `records.record.read`;
  ordinary reads never expose sensitive field values.
- Category management (`records.category.manage`) verified.
- See F-02 (default sensitivity / default retention on categories not
  implemented).

### Area 11 — Integration events (16) — PASS

All 16 ratified integration events exist in `RecordsIntegrationEvents.cs`
(exactly 16), carry minimal identifier-only payloads with trailing
`DateTime OccurredOn`, and are mapped 1:1 by
`RecordsIntegrationEventPublisher`:

`RecordCreated`, `RecordSubmitted`, `RecordUnderReview`, `RecordVerified`,
`RecordRejected`, `RecordCorrected`, `RecordArchived`, `RecordDeactivated`,
`RecordRestored`, `RecordClassified`, `RecordHoldPlaced`, `RecordHoldReleased`,
`RecordRetentionChanged`, `RecordEvidenceAttached`, `RecordEvidenceRemoved`,
`RecordRetentionExpired`.

No PII, no field values, no hold reasons, no filenames, no object keys appear in
any payload (asserted by `RecordsIntegrationEventSecurityTests`).

### Area 12 — Outbox (CRITICAL) — PASS

The transactional outbox is genuinely wired (not just a package reference):

- API wiring uses `AddCommunityOSEventBusWithOutbox<RecordsDbContext>`
  (MassTransit EF Core outbox, receive-endpoint outbox for exactly-once
  consumption).
- Outbox/inbox entities are owned by `RecordsDbContext`
  (`AddInboxStateEntity`/`AddOutboxMessageEntity`/`AddOutboxStateEntity`) in the
  `records` schema, so outbox rows commit atomically with record changes.
- Domain events are published **before** `SaveChanges` (`DomainEventPublisher`),
  guaranteeing the record state and its outbox message commit in the same
  transaction.
- Consumers (`OrganizationIntegrationEventConsumer`,
  `DocumentLifecycleIntegrationEventConsumer`) are registered; the Documents
  consumer is gated on the outbox being enabled, per the ADR-015 amendment
  (Prompt 08A-R2) and the ADR-023 Records integration gate.

### Area 13 — Failure behavior — PASS

- `HttpAuthorizationEvaluator` denies on empty subject, non-success status,
  transport failure, or null response (fail-closed).
- `HttpDocumentsServiceClient` throws on transport failure / empty `BaseUrl`
  (fail-closed).
- Denied reads return `404` (indistinguishable from missing), so no oracle is
  exposed.
- No persistence on deny (asserted by `RecordsSecurityRegressionTests`).
- See F-08 (place/release hold partial-failure edge, informational).

### Area 14 — JWT security — PASS

- `RecordsJwtValidation` restricts algorithms to **RS256 only**
  (`ValidAlgorithms = [RsaSha256]`); JWKS discovery via
  `Jwt:MetadataAddress` (`/api/v1/.well-known/openid-configuration`, which
  matches the Identity `DiscoveryController` route and JWKS at
  `/api/v1/.well-known/jwks`).
- Issuer, audience, lifetime and signing key are validated; no
  HMAC/`SymmetricSecurityKey`/`Jwt:Secret` anywhere in Records.
- `RecordsJwtSecurityTests` reject a wrong signing key, wrong issuer and wrong
  audience; `RecordsSecurityRegressionTests` verify the RS256-only gate.
- See F-03 (`RequireHttpsMetadata=false` in the base config).

### Area 15 — API contract — PASS WITH FINDINGS

- Routes, methods, DTOs and capabilities match `docs/api/records.md` exactly
  (list, get, sensitive, create, fields, submit, under-review, verify, reject,
  correct, classify, archive, deactivate, restore, scopes, versions, evidence,
  holds, retention schedules, categories).
- Status-code contract in `ExceptionHandlingMiddleware` matches the documented
  contract: `400` validation/invalid refs; `403` fail-closed deny; `404`
  not-found (also for unauthorized reads); `409` invalid transition /
  verified-field update / creator-as-verifier / hold release by placer / held
  deactivation / duplicate evidence / duplicate category or schedule.
- Sensitive behavior matches: separate `read.sensitive` endpoints; hold reasons
  only under `records.hold.manage`; `query=` documented.
- Findings: **F-01** (`query=` search parameter accepted but ignored — silent
  no-op) and **F-02** (`PUT /categories/{code}` "default retention" not
  implemented).

### Area 16 — Persistence / EF — PASS

- Unique indexes verified: category code, schedule code, `record_versions`
  `(record_id, version_number)`, `record_scopes`
  `(record_id, organization_unit_id)`, evidence references
  `(record_id, document_id, version_number, reference_type)`,
  `organization_unit_references`.
- No cross-service tables; `records` schema only; migrations consistent;
  `has-pending-model-changes` clean.
- See F-10 (working-fields uniqueness is aggregate-enforced only, no unique DB
  index — informational).

### Area 17 — Tests — PASS

Audited and executed:

- `RecordsSecurityRegressionTests` — multi-scope allow/deny, fail-closed
  evaluator, no persistence on deny, denied read → `404`, list filtering
  (any-scope, no leak), sensitive-field gating.
- `RecordsJwtSecurityTests` — RS256-only; wrong key/issuer/audience rejected.
- `RecordsIntegrationEventSecurityTests` — events carry no field values / hold
  reasons.
- `RecordLifecycleTests` — full lifecycle, separation of duties (creator cannot
  verify), `Rejected` editable but not resubmittable, correction appends
  versions.
- `RetentionScheduleTests` — ISO-8601 period + `MaximumPeriod`, duplicate
  category rule rejection.
- `RecordsCatalogSeederTests` — 7 baseline codes seeded idempotently.
- `DocumentLifecycleConsumerTests` — deactivate/restore evidence reconciliation.
- All Records unit tests pass (64/64). Integration tests compile only — see
  Area 20.

### Area 18 — Documentation — PASS WITH FINDINGS

- `ADR.md` positions 15/16/17/18/19/22/23 verified consistent with the
  implementation (outbox-at-gate, boundaries, 17-permission matrix, 16-event
  catalog, RS256/JWKS/no-role-authz).
- `docs/records.md`, `docs/api/records.md`, `docs/runbooks/records.md`,
  `docs/documents.md`, `docs/api/documents.md`, `docs/runbooks/documents.md`,
  `docs/authorization.md` compared against the implementation.
- Findings: **F-04** (stale outbox phrasing in `docs/records.md`), **F-05**
  (method name in `docs/runbooks/records.md`), **F-02** (category default
  retention overreach).

### Area 19 — Cross-service regression — PASS

All six unit suites pass against the current build (see environment table):
Records, Authorization, Organization, Community, Knowledge, Documents. The
Authorization check/batch-check API surface (`/api/v1/authz/check`,
`/api/v1/authz/batch-check`) consumed by Records' `HttpAuthorizationEvaluator`
exists in `AuthorizationController` and is covered by the Authorization suite.

### Area 20 — Environment limitations — ENVIRONMENT LIMITATION

- Docker is not available in this environment. The Records Testcontainers
  integration suite
  (`tests/Integration/CommunityOS.Records.IntegrationTests/Persistence/RecordsPersistenceTests.cs`)
  **compiles** but was **not executed**; its EF/outbox round-trip assertions are
  therefore unverified at runtime. This is recorded as an environment limitation,
  not a pass.

### Area 21 — Final verdict — PASS WITH FINDINGS

No BLOCKING defect was found. The Prompt 08B Records implementation conforms to
the ratified contract across architecture, lifecycle, versioning, the 17
permissions, multi-scope authorization, both service boundaries, holds,
retention, classification, the 16-event catalog, the transactional outbox,
failure behavior, JWT security, the API contract, EF persistence and tests.
Three NON-BLOCKING findings and nine INFORMATIONAL findings are recorded below.

---

## Findings register

| # | Severity | Finding | Evidence |
|---|----------|---------|----------|
| F-01 | **NON-BLOCKING** | `GET /api/v1/records` accepts `query=` (documented at `docs/api/records.md:19`) and `ListRecordsQuery` carries `Query`, but the handler never uses it — free-text search is a **silent no-op**. | `RecordsController.cs:30`, `RecordQueries.cs:22`, handler filter at `RecordQueries.cs:41-51` |
| F-02 | **NON-BLOCKING** | Category default sensitivity / default retention (`RetentionScheduleCode`) are ratified in `docs/records.md:99-100` and `docs/api/records.md:201` ("default retention") but **not implemented**: `RecordCategory` has no such fields and `PUT /api/v1/categories/{code}` only accepts `DisplayName`/`Description`. Classification/retention must be set per record. | `RecordCategory.cs:28-39`, `CategoriesController.cs:56-58` |
| F-03 | **NON-BLOCKING** (security hygiene) | `Jwt:RequireHttpsMetadata: "false"` is set in the **base** `appsettings.json` (not a Development-only override) and no production override file exists; `docs/runbooks/records.md` specifies `true` in prod. If the same file ships to prod, JWKS discovery metadata is fetched over HTTP. | `appsettings.json:14-20` |
| F-04 | INFORMATIONAL | `docs/records.md:378` ("Publication is best-effort in-process today (ADR-015 outbox deferred)") is stale relative to the implementation, which **enables** the transactional outbox at the Records gate (exceeding the ratified minimum). Doc refresh recommended; not a compliance gap. | `records.md:378`, `ServiceCollectionExtensions.cs` (`AddCommunityOSEventBusWithOutbox`) |
| F-05 | INFORMATIONAL | `docs/runbooks/records.md:31` names `AddCommunityOSEventBus`; the implemented method is `AddCommunityOSEventBusWithOutbox`. | `runbooks/records.md:31`, `EventBusServiceExtensions.cs` |
| F-06 | INFORMATIONAL | `FlagRecordRetentionExpiredCommand` (handler `RecordCommands.cs:485-501`, authorized with `records.record.classify`) is implemented but **not wired** to any endpoint or scheduled job — today it is invocable only in-process via MediatR. Consistent with the contract, which defers the automated disposition-review process; the domain capability and `RecordRetentionExpired` event exist. | `RecordCommands.cs:480-501`, no controller/hosted-service references |
| F-07 | INFORMATIONAL | `RecordHold.Release` does not guard a double release: calling release on an already-released hold overwrites the `ReleasedBy`/`ReleasedOn` audit fields. No functional/security impact (release is idempotent); a guard would preserve audit integrity. | `RecordHold.cs` (`Release`) |
| F-08 | INFORMATIONAL | `PlaceHold`/`ReleaseHold` write hold references to Documents via sequential HTTP calls **after** mutating the Records aggregate but **before** `SaveChanges`; a mid-loop failure rolls back the Records-side mutation but leaves earlier document references already written — no saga/compensation. Orphan/stale hold references are recoverable (re-apply, or reconcile via `GET /holds/{id}`). | `RecordCommands.cs:440-442, 469-473` |
| F-09 | INFORMATIONAL | `HttpAuthorizationEvaluator.EvaluateBatchAsync` issues **N sequential HTTP round-trips** even though the Authorization API exposes `POST /api/v1/authz/batch-check`. Perf-only; no correctness impact. | `HttpAuthorizationEvaluator.cs`, `AuthorizationController.cs` |
| F-10 | INFORMATIONAL | No unique DB index on record working fields `(record_id, field_key)`; uniqueness is aggregate-enforced only. Low risk given single-writer aggregate semantics. | `RecordsConfigurations.cs` |
| F-11 | INFORMATIONAL | `RetentionRule.StartTrigger`/`Disposition` are free-form strings (only the periods are ISO-8601-validated); no enum validation. No destructive path exists (disposition is `review` only) and `Records:Retention:ReviewDispositionEnabled` is bound but never consumed. | `RetentionSchedule.cs`, `RecordsOptions.cs` |
| F-12 | INFORMATIONAL | `Records:InternalClientId` is present in `appsettings.json` but not bound (Records has no internal fact endpoint yet — consistent with `docs/api/records.md:240-246`). | `appsettings.json:38`, `RecordsApiOptions.cs` |
| F-13 | **ENVIRONMENT LIMITATION** | Docker unavailable → Records Testcontainers integration tests compile only and were not executed. | `tests/Integration/CommunityOS.Records.IntegrationTests` |

---

## Conclusion and Prompt 08D recommendation

**No BLOCKING finding.** The Prompt 08B Records implementation is verified
conformant to the ratified contract (PASS WITH FINDINGS across the 21 areas).

Prompt 08D remediation is appropriate. Recommended priority:

1. **F-01** — either implement free-text search in `ListRecordsQuery` or remove
   the `query=` parameter from the controller and both docs. A documented but
   silently ignored parameter is a correctness trap for consumers.
2. **F-02** — align `docs/records.md:99-100` and `docs/api/records.md:201` with
   the implemented category model (drop "default retention"/"default
   sensitivity"), or add the fields to `RecordCategory` if the ergonomics are
   wanted.
3. **F-03** — move `Jwt:RequireHttpsMetadata=false` to a Development-only
   override and add an explicit production override file (`true`).
4. Informational items (F-04…F-12) may be addressed opportunistically; none
   blocks production readiness of the Records service itself, but F-08 and F-07
   are worth a follow-up design note before a hold-heavy workload lands.

Records integration tests (F-13) must be executed in a Docker-capable
environment before the Records integration gate is declared production-ready.