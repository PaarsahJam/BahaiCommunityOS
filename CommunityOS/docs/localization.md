# Localization Bounded Context

> **STATUS: IMPLEMENTED (Prompt 16F).** Design for the
> Localization bounded context (ADR-017 slot 14), ratified at the Prompt 16E
> gate by ADR-029 and implemented as the Localization service
> (`CommunityOS.Localization.{Domain,Application,Infrastructure,API}`).
> This document remains the authoritative specification; deviations are noted
> in the Prompt 16F implementation report.

Localization owns the community's multilingual resource catalog: locale
lifecycle, resource namespaces and string entries with per-culture review
workflows (draft → in review → approved / rejected, approved revisions
immutable and superseded verbatim by later approvals), entity translations
for Community/Organization records keyed by `(sourceContext, entityType,
entityId, field)`, a translation-suggestion intake that can never publish on
its own, and versioned export bundles consumed by client applications.

## Position in the platform

- **Sequence:** ADR-017 slot 14 — after Correspondence (implemented,
  Prompt 16). It is the next bounded context to be implemented.
- **Authoritative contract:** `docs/architecture/ADR.md` → ADR-029
  (Localization bounded context and multilingual-resource boundary).
- **Greenfield:** no source projects existed before Prompt 16F (verified
  exhaustively at Prompt 16D).

## Ownership

Localization owns:

- locales: BCP-47 identity, activation lifecycle, exactly one default at all
  times; only `en` is seeded (active + default), `fa`/`ar` are created as
  inactive candidates by operators, never by migration;
- the resource catalog: namespaces (`[a-z0-9.]`, reserved `library.` /
  `knowledge.` prefixes rejected — the ADR-021 Knowledge/Library boundary),
  entries (`[a-z0-9._-]`, ≤200 chars) and per-culture revision history;
- entity translations: per-field translation records for entities owned by
  other services, same review workflow, reserved source contexts enforced;
- suggestions: external/agent-proposed drafts stored as provenance-tagged
  proposals; accepting one into review composes propose+submit atomically
  under the acceptor's authority; machine-generated provenance is preserved
  (`machine:<provider>`); acceptance never publishes;
- catalog versioning: a singleton monotonic bundle version bumped inside the
  save transaction of every publishing change, exposed as deterministic ETags.

Localization does NOT own: authoritative Writings content or their
authoritative translations (Knowledge/Library — hard boundary, decision 18),
the entities being translated (Community/Organization keep resolution), UI
rendering concerns, or any consumer of catalog-change events.

## Architecture

Four-project service following the house pattern
(`Domain` / `Application` / `Infrastructure` / `API`):

- **Domain:** aggregates (`Locale`, `ResourceNamespace`, `ResourceEntry`
  + `ResourceRevision`, `EntityTranslation` + `EntityTranslationRevision`,
  `TranslationSuggestion`) enforcing every invariant centrally: BCP-47
  validation, key charset/length bounds, value length bounds (≤2000),
  legal transition matrix (illegal transitions throw
  `LocalizationConflictException`; missing things throw
  `LocalizationNotFoundException`), approve-supersedes-verbatim, deprecated
  aggregates refuse new revisions, reserved-prefix rejection. Publishing
  changes raise `CatalogChangedDomainEvent(CatalogContext, Namespace?,
  Culture, BundleVersion)`.
- **Application:** MediatR handlers behind exactly five permissions
  (`localization.locale.read|manage`, `localization.resource.read|propose|review`;
  exact-match, no implication chains, evaluated via `AuthorizationGuard`),
  FluentValidation on every request, keyset pagination for entry walks,
  fail-visible export resolution, disabled-by-default
  `IMachineTranslationSuggestionSource` seam.
- **Infrastructure:** EF Core (`Npgsql`, schema `localization`, database
  `communityos_localization`), snake_case mappings, partial unique indexes
  enforcing "at most one approved revision per culture", `xmin` concurrency
  tokens, MassTransit transactional outbox born into the context. The journal
  publishes catalog changes atomically: bump version → save → emit domain
  event → second save flushes outbox rows → commit; concurrent writers get
  `DbUpdateConcurrencyException` surfaced as `409`. The reader is split from
  the journal so queries never take write paths.
- **API:** controllers under `/api/v{version}/localization/*`, JWT RS256
  bearer auth with mandatory `sub`, ProblemDetails-shaped errors, Swagger in
  development.

## Integration events

Born with the transactional outbox but with **zero consumers** at this gate:
the only published contract is `LocalizationCatalogChanged(BundleVersion,
CatalogContext, Culture, Namespace?, OccurredOn)` (payload allowlist is
asserted by tests). Nothing else may be added without an ADR.

## Configuration

`appsettings.json` section `Localization`: `MaxPageSize` (100),
`DefaultPageSize` (25), `MaxBatchUpsert` (200), `MaxExportKeys` (10000),
`MaxValueLength` (2000). Connection string `LocalizationDb`; RabbitMQ bus
settings under `RabbitMq`; Authorization service base URL + client id under
`AuthorizationService`.

## Testing

- **Unit** (`tests/Unit/CommunityOS.Localization.Tests`): domain invariants
  (BCP-47 theory data, lifecycle transitions, supersede semantics, value/key
  bounds, reserved contexts/prefixes, suggestion decisions), authorization
  (exact permission asserted per handler, empty actor rejected before any
  evaluation), bundle resolver fallback/fail-visible/ETag determinism,
  integration-event payload allowlist.
- **Integration** (`tests/Integration/CommunityOS.Localization.IntegrationTests`,
  Testcontainers Postgres, executed in CI): migration seeds exactly one
  active default locale + version 0, atomic version-bump + outbox capture,
  rollback discards mutation and outbox row alike, concurrent approval of the
  same aggregate surfaces as conflict, reserved contexts rejected before any
  storage, end-to-end entity-translation upsert/query flow.
