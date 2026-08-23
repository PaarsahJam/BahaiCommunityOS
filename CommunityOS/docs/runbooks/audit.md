# Audit Service Runbook

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 12C).** Operational runbook for
> the Audit bounded context (`ADR-027`, ADR-017 slot 11). The service is
> implemented at `src/Services/Audit/CommunityOS.Audit.API` with unit and
> integration test projects; configuration and operational procedures below
> describe the implemented state.

## Services

- **Audit API/consumer host** — implemented at
  `src/Services/Audit/CommunityOS.Audit.API` with Domain / Application /
  Infrastructure projects plus unit and integration test projects, mirroring the
  Records/Workflow/Notifications/Search layout. Single deployable hosts both
  the query API and the MassTransit consumers (Search pattern).

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see `docker/docker-compose.yml`).
- Producer delivery gates before enabling gated consumers:

  | Producer | Requirement before its Audit consumers register |
  |----------|--------------------------------------------------|
  | Records, Workflow, Notifications | None — already outbox-protected |
  | Organization | None — projection feed only |
  | Documents | **Documents outbox gate**: upgrade Documents to transactional outbox publication for the compliance subset (ADR-022 amendment, ADR-027 decision 4) |
  | Authorization | **Authorization outbox gate**: upgrade Authorization to outbox publication for the security subset (break-glass is ADR-014-mandatory) |

## Ratified implementation steps (Prompt 12C)

1. Create the four service projects under `src/Services/Audit/` and reference
   them in `CommunityOS.sln`, plus unit and integration test projects. There is
   no scaffold — everything is new (ADR-027 decision 3).
2. Add `CREATE DATABASE communityos_audit;` to
   `docker/init/01-create-databases.sql`.
3. Generate the initial EF Core migration `InitialCreateAudit` containing:
   `audit_entries` (with UNIQUE `source_event_hash` and all ratified indexes),
   `audit_entry_holds`, `organization_unit_references`, the MassTransit
   `InboxState`/`OutboxMessage`/`OutboxState` tables in the `audit` schema, and
   **the immutability triggers** rejecting `UPDATE`/`DELETE` on `audit_entries`
   unless `app.audit_purge_authorized = 'on'` (ADR-027 decisions 2 and 17).
4. Wire `AddCommunityOSEventBusWithInbox<AuditDbContext>` (inbox-only;
   ADR-027 decision 9) and register exactly the first-gate consumers:
   - Records ×16 (all lifecycle/compliance events),
   - Workflow ×5 (`WorkflowTaskCreated/Assigned/Completed/Cancelled/Escalated`),
   - Notifications ×1 (`NotificationDispatched`),
   - Organization ×3 (projection into `organization_unit_references` only).
   Do **not** register Documents or Authorization consumers until their
   producer gates complete; do not register any deferred or not-classified
   event (ADR-027 decision 5).
5. Register the ratified `audit.*` permissions in `PermissionCatalog.cs`
   (`audit.entry.read`, `audit.entry.read.sensitive`, `audit.entry.export`,
   `audit.entry.admin`) and add them to the GlobalAdministrator and
   NationalAdministrator development role seeds in `AuthorizationSeeder.cs`.
   LocalAdministrator and PlatformService receive none.
6. Implement the six-endpoint API surface exactly as contracted in
   `docs/api/audit.md`; verify permission-to-endpoint equality at the gate.
7. Configure `AuditDb`, `RabbitMq`, `AuthorizationService` (service principal
   `communityos-audit`) sections; ship `appsettings.json` /
   `appsettings.Development.json`.
8. Implement the ingest mapping layer with the per-event-type metadata
   allowlists, deterministic sensitivity rules, retention-class application and
   `SourceEventHash` computation; unmappable input must dead-letter, never
   partially persist (ADR-027 decisions 6 and 10).
9. Implement the two-step purge operation and hold management (ADR-027
   decision 14); ensure the purge path is the only code that ever sets the
   trigger-guard setting, via `SET LOCAL`. Expiry detection is evaluated on
   demand against the partial retention index at each purge invocation (the
   batch selection and its remaining-expired estimate) — there is no
   background sweep worker at this gate; expiry alone never deletes.
10. Ship unit tests (ingest mappings, sensitivity derivation, hash stability,
    allowlist enforcement) and Testcontainers integration tests (migration +
    trigger immutability + idempotent redelivery). See the environment
    limitation below.

## Configuration

| Key | Example/default | Purpose |
|-----|-----------------|---------|
| `ConnectionStrings:AuditDb` | `Host=localhost;Port=5432;Database=communityos_audit;Username=communityos;Password=communityos` | Journal database connection |
| `Jwt:Authority` / `Jwt:MetadataAddress` / `Jwt:Issuer` / `Jwt:Audience` | `http://localhost:5001` / `…/openid-configuration` / `http://localhost:5001` / `CommunityOS` | RS256 bearer-token validation profile |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Bus transport |
| `AuthorizationService:BaseUrl` | `http://localhost:5007` | Check API base URL for the guard |
| `AuthorizationService:ClientId` | `communityos-audit` | Service-principal identifier (audit/tracing) |
| `AuthorizationService:AccessToken` | — | Service-principal token used by `HttpAuthorizationEvaluator` |
| `Audit:MaxPageSize` | `100` | Query clamp ceiling (`limit` values above it are clamped, not rejected) |
| `Audit:DefaultPageSize` | `25` | Page size when no `limit` is supplied |
| `Audit:ExportMaxRows` | `10000` | Hard export cap (`maxRows` above it is rejected with `400`) |
| `Audit:PurgeDefaultBatchSize` | `500` | Batch size when `maxBatchSize` is omitted on the purge endpoint |
| `Audit:PurgeMaxBatchSize` | `5000` | Purge batch upper bound (`maxBatchSize` above it is clamped to this value; negative values are rejected with `400`) |
| `Audit:HoldMaxBatchSize` | `500` | Maximum entries addressable by one hold-placement request |
| `Audit:JournalClass` | `audit-journal` | Retention class of audit-of-audit entries (exports, holds, purge markers) |
| `Audit:Retention:DefaultClass` | `default` | Class assigned to entries whose event type has no explicit mapping |
| `Audit:Retention:Classes:{Code}` | ISO-8601 duration or empty (= indefinite) | Deployment retention policy per class code |
| `Audit:Retention:EventClasses:{EventType}` | class code | Optional event-type → retention-class override |

There is no sweep-interval setting: expiry detection runs inside the purge
operation itself (see Retention / archival operations below).

Retention durations are **deployment policy**, deliberately unspecified by the
architecture; the default class retains indefinitely until configured
(ADR-027 decision 14).

## Database

- Database `communityos_audit`, schema `audit`. Tables: `audit_entries`,
  `audit_entry_holds`, `organization_unit_references`, `InboxState`,
  `OutboxMessage`, `OutboxState` (outbox tables provisioned, unused).
- Indexes: PK; UNIQUE `(source_event_hash)`; `(occurred_on)`;
  `(resource_type, resource_id)`; `(subject_id)`; `(actor_id)`;
  `(organization_unit_id)`; `(sensitivity)`; partial
  `(retention_class, retention_expires_on)` WHERE
  `retention_expires_on IS NOT NULL`.
- **Immutability triggers**: any `UPDATE`/`DELETE` against `audit_entries`
  outside the guarded purge transaction fails. Operators must never set
  `app.audit_purge_authorized` manually; legitimate history removal happens
  only through the purge endpoint, which writes its marker entry first.
- Backups follow the shared-instance policy; restored copies inherit trigger
  protection automatically (triggers live in the schema).

## Retention / archival operations

1. Expired entries are identified on demand by the purge operation against the
   partial retention index; nothing deletes automatically and no background
   sweep worker exists.
2. Purge via `POST /api/v1/audit/admin/purge-expired` (one bounded batch per
   call; repeat to drain). Each invocation writes its purge-marker entry before
   deleting.
3. Place/release investigation holds via the holds endpoints; active holds
   always override expiry.
4. Archival (cold-storage export-and-delete) is **not implemented** at this
   gate; if a future requirement demands it, it needs an ADR amendment.

## Event consumption

- Consumers are exactly-once via the MassTransit inbox; broker redelivery of
  the same message is suppressed, semantically identical re-publication is
  absorbed by the `source_event_hash` unique constraint (no duplicate entries
  are possible through normal broker behavior).
- Malformed/unmappable messages dead-letter with an error log — investigate the
  producer mapping before manual replay; replay is safe because dedupe is
  content-addressed.
- Delivery-latency signal: monitor the `OccurredOn` → `IngestedOn` gap; growth
  indicates bus/backlog problems rather than journal faults.
- Gated producers: if Documents or Authorization events appear missing, confirm
  whether that producer's outbox gate has completed — absence before the gate
  is expected behavior, not data loss.

## Security / authorization

- Service principal `communityos-audit` calls the Authorization check API; no
  direct Authorization database access, no cross-service database access
  (ADR-018).
- JWT validation is RS256-only, mirroring the Search validation profile; every
  endpoint requires an authenticated subject.
- All authorization is fail-closed; sensitive rows need the second-pass
  permission everywhere including export; single reads return 404 for missing
  and unauthorized alike.
- Reads are not journaled; exports and administrative mutations are; failed
  authorization attempts are never journaled (ADR-027 decision 15).

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Entries missing for a producer | Consumer not registered (producer gate pending) or producer outage | Check gate table above; inspect producer logs/outbox |
| Dead-letter queue growing | Ingest mapping cannot conform (unknown field shape, metadata allowlist violation) | Inspect DLQ message; fix mapping; replay safely (hash dedupe prevents duplication) |
| Trigger violation errors in logs | Non-ratified mutation attempt against `audit_entries` | Treat as a defect: some code path is trying to update/delete history; fix the code, never relax the trigger |
| Purge returns small counts while expiry backlog grows | Active holds exempting entries | Review `audit_entry_holds`; release holds that are no longer needed |
| Query returns fewer rows than expected | Scope/sensitivity filtering working as designed | Verify caller's grant scopes and second-pass permission |
| Large `OccurredOn`→`IngestedOn` gaps | Bus backlog or consumer downtime | Check RabbitMQ queues and consumer health; catch-up is automatic |

## Environment limitation

Docker/Testcontainers is unavailable in the current implementation environment;
integration tests compile as part of the solution but execute only in CI where
a container runtime exists. This mirrors the recorded limitation for Workflow,
Notifications and Search (ADR-017 status block) and applies equally to the
Prompt 12C gate.
