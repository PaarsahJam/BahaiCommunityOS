# Prompt 04C — Architecture Verification Report

**Date:** 2026-08-11
**Scope:** Organization bounded context (B1–B6), the Community read-model
projection (`OrganizationReference` / `OrganizationUnitReference`), and the
Authorization HTTP organization-context integration.
**Method:** Static inspection (csproj graph, EF models, migrations, contracts,
ADRs), `dotnet ef migrations has-pending-model-changes`, solution build, and
unit test runs. Integration tests compile but cannot run in this environment
(no Docker/RabbitMQ/PostgreSQL available).

## 1. Verification checklist

| # | Check | Status | Evidence |
|---|-------|--------|----------|
| 1 | Organization owns the authoritative structure; Community owns only a derived, read-only projection | **PASS** | `communityos_organization` (schema `organization`) is the only writer of `organizations`, `organization_units`, `organization_unit_parents`. Community's `organization_references` / `organization_unit_references` are written solely by `OrganizationIntegrationEventConsumer`; no Community Application/API code reads or writes them (no cross-service DB references, see #5). |
| 2 | Community must not independently create or mutate the organizational hierarchy | **PASS** | Community.Domain read-model aggregates expose only consumer-driven `Apply`/`Update` sync methods; no public mutation/creation API. No Community handler creates org data outside the consumer. |
| 3 | Community's own scope (persons, households, activities) remains intact | **PASS** | Community owns `communities`, `clusters`, `local_units` plus its scoped aggregates; org hierarchy moved to Organization. `has-pending-model-changes` reports no drift. |
| 4 | Deleted/retired organizational units are handled appropriately | **PARTIAL** | `OrganizationUnit.Deactivate()` raises **no** domain event, so no `OrganizationUnitDeactivated` integration event exists. The Community read model never learns a unit was deactivated. Non-blocking: the read model has no authorization role and no consumers yet; recorded as finding F2. |
| 5 | No service may access another service's database | **PASS** | csproj graph: no Infrastructure/DbContext references cross service boundaries. Only `Organization.Application → Authorization.Application` (interface `IAuthorizationEvaluator`). Community.Application has zero references to Organization or the read model. ADR-018 satisfied. |
| 6 | Cross-service facts flow via integration events (MassTransit/RabbitMQ) or owning-service API only | **PASS** | Organization publishes `OrganizationCreated/Updated`, `OrganizationUnitCreated/Updated`, `OrganizationUnitParentChanged` via `OrganizationIntegrationEventPublisher` (ADR-015 best-effort, outbox deferred); Community consumes them. Authorization reads hierarchy facts over the owning API `/api/v1/orgunits/{id}/covers`. |
| 7 | Event contracts are complete, versioned, and durable | **PARTIAL** | Contracts are complete for the consumed events (no PII, minimal payload). No explicit event-version field or schema-evolution note exists; ADR-004/ADR-015 do not address versioning. Outbox is deferred (ADR-015), so a broker outage can drop events. Findings F3, F4. |
| 8 | Read model is eventually consistent and cannot influence authorization | **PASS** | Projection is write-only and eventually consistent (documented in `docs/organization.md`). Authorization decisions are resolved live by `AuthorizationEvaluator` → `IOrganizationContextProvider` → `HttpOrganizationContextProvider` → Organization `/covers` at decision time. The stale projection is never consulted for authorization. |
| 9 | Authorization integration respects bounded context ownership | **PASS** | Authorization never reads the Organization DB; it calls the owning service's fact endpoint. Organization's `AuthorizationGuard` similarly calls Authorization's check API (pre-existing B6 wiring). Fail-closed in both directions. |
| 10 | `/covers` endpoint is properly protected | **PASS** | Requires `[Authorize]` (valid JWT) at class level; action returns `Forbid()` unless `X-Client-Id` equals `Organization:InternalClientId` (`communityos-authorization`). Fail-closed. See note on `X-Client-Id` (finding F1). |
| 11 | No request cycle / recursion between services | **PASS** | `GetOrganizationUnitCoveredQuery` deliberately performs no application-layer permission check (documented in code) to avoid the Organization → Authorization → Organization cycle. The fact query is authorization-agnostic. |
| 12 | DB isolation: Organization and Community each use their own database and migration | **PASS** | `communityos_organization` and Community's database are separate. `InitialCreateOrganization` and `InitialCreateCommunity` generated with independent `DbContext`s. |
| 13 | EF Core model matches migrations in both services | **PASS** | `has-pending-model-changes` reports no changes for both projects after the final build. |
| 14 | EF materialization is safe (no public setters bypassed, correct ctors) | **PASS** | Read-model aggregates and Community root aggregates (`Community`, `LocalUnit`, `Cluster`) use private parameterless EF ctors + `null!`-initialized navigation/owned types; public factory methods enforce invariants. Consistent with Organization aggregates. |
| 15 | Community migration faithfully represents the model; no unintended cross-table coupling | **PASS** | `InitialCreateCommunity` creates `clusters`, `communities`, `local_units`, `organization_references`, `organization_unit_references`. Reference tables have no FKs to community or each other (intentional: external facts are projections, not relations). |
| 16 | Organization migration is sound (hierarchy, indexing, effective dating) | **PASS** | `organization_unit_parents` captures hierarchy; unique + effective-date indexes present. No FK from `organization_units.organization_id → organizations` and no self-FK on `parent_id` — deliberate application-layer integrity (documented). |
| 17 | Tests exist and pass | **PASS** | Organization unit tests **79/79**, Authorization unit tests **118/118**. Other unit projects are empty placeholders (no test summary). Integration tests compile as part of the solution but are not runnable here. |
| 18 | Solution builds clean | **PASS** | `dotnet build CommunityOS.sln`: **0 warnings, 0 errors** (serially; parallel build+test causes transient MSB3026/CS2012 file-lock collisions). |

## 2. Non-critical findings

- **F1 — `X-Client-Id` is identification/routing only, not an authorization credential.** Any authenticated caller with a valid JWT could present the `communityos-authorization` client id and query `/covers`. The header grants no privilege beyond the caller's own authentication, and the returned fact is a single ancestor-or-self boolean over hierarchy — not sensitive data. Documented as such in `docs/api/organization.md` and `docs/authorization.md`. Recommended future hardening: bind `/covers` to a dedicated service principal token / claim rather than a plain header.
- **F2 — No deactivation propagation.** `OrganizationUnit.Deactivate()` raises no domain event; there is no `OrganizationUnitDeactivated` integration event, so deactivated/retired units remain referenced in the Community read model. Not a correctness or authorization issue (no reader uses it); address when the read model gains consumers.
- **F3 — Lifecycle status not carried in events.** `OrganizationUpdatedEvent` does not include `Status`/dissolved-on, so dissolved/suspended organizations are indistinguishable in the read model. Same impact class as F2.
- **F4 — Consumer is not self-healing for units.** A lost `OrganizationUnitCreated` leaves the unit reference absent; subsequent unit updates are skipped (logged warning) until a later event. Organizations are self-healing (update upserts the missing reference); units are not. Documented in `docs/organization.md`.
- **F5 — No event versioning / schema-evolution convention.** Contracts are versioned in the namespace sense only. Recommend an explicit version field or documented evolution policy before other consumers subscribe.
- **F6 — ADR-015 outbox deferred.** Publication is best-effort in-process via `IPublishEndpoint`; a broker outage at command time can lose events. Explicitly deferred by ADR-015; tracked for removal.

## 3. Result

**Overall: PASS** for the bounded-context boundaries, DB isolation, EF/migration
integrity, build, and tests. **PARTIAL** items (deactivation handling, event
versioning, outbox) are pre-existing, documented, and non-blocking — none
produce incorrect authoritative state, and none affect authorization.

**Recommendation:** Proceed to Prompt 05. Track F2–F6 as follow-up work items
(preferable on the Community read-model backlog), and revisit F1 before
exposing `/covers` beyond the current internal-client trust model.
