# Notifications Service Runbook

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 10C).** Operational notes for the
> Notifications service (`ADR-025` Accepted, `docs/notifications.md` ratified).

## Services

- **Notifications API** — implemented at
  `src/Services/Notifications/CommunityOS.Notifications.API`. Four projects:
  Domain / Application / Infrastructure / API, plus unit and integration test
  projects. Per ADR-025 decision 2 the original scaffold was superseded at the
  Prompt 10C gate; the current source is the authoritative implementation.

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see Implementation steps).
- The **transactional outbox/inbox** (ADR-015) must be enabled at the
  Notifications integration gate before any Notifications integration event is
  published or any Workflow consumer is registered in a shared environment.
  See "Outbox gate" below.

## Implementation status (Prompt 10C)

**Complete.** Service projects, database, migration, wiring, consumers and API
surface are implemented. The following steps are the ratified implementation
plan that was executed at the Prompt 10C gate:

1. Finalize service projects under `src/Services/Notifications/` and reference
   them in `CommunityOS.sln` (Domain / Application / Infrastructure / API),
   plus unit and integration test projects. Apply ADR-025 decision 2: retain or
   supersede each scaffold file explicitly.
2. Add `CREATE DATABASE communityos_notifications;` to
   `docker/init/01-create-databases.sql`.
3. Generate the initial EF Core migration `InitialCreateNotifications`
   (`notifications`, `notification_recipients`, `notification_types`,
   `notification_preferences`, `organization_unit_references`, plus the
   MassTransit outbox tables in the `notifications` schema).
4. Wire `AddCommunityOSEventBusWithOutbox<NotificationsDbContext>` with the
   first-gate consumers, `ConfigureAuthorizationService` +
   `HttpAuthorizationEvaluator` + `AuthorizationGuard`, and the
   `NotificationsIntegrationEventPublisher` following the existing pattern.
5. Register the ratified `notifications.*` permission matrix in the
   Authorization permission catalog and the development role seeds.
6. Register consumers: `OrganizationUnitCreated/Updated/ParentChanged`
   (ADR-016 projection) and Workflow
   `WorkflowTaskAssigned/WorkflowTaskEscalated` (first gate). Deferred
   consumers (Workflow `TaskCompleted`/`TaskCancelled`, Records
   `Verified`/`Rejected`/`HoldPlaced`/`HoldReleased`, Knowledge
   `QuestionFlagged`/`QuestionUnderReview`, Community activity/event/meeting)
   are **not** enabled at Prompt 10C; each requires its recipient-resolution
   dependency (ADR-025 consumed-events catalog).
7. Configure `AuthorizationService` and `CommunityService` sections (service
   principals) and the event-bus/outbox wiring.
8. Seed the notification-type/template catalog idempotently at migration time
   (`NotificationCatalogSeeder`: `task-assigned`, `task-escalated`,
   `task-completed`, `task-cancelled`, `record-verified`, `record-rejected`,
   `record-hold`, `question-flagged`, `community-activity`, `community-event`,
   `community-meeting`, `general`).
9. Ship `appsettings.json` / `appsettings.Development.json` /
   `appsettings.Production.json` (`NotificationsDb`, JWT, RabbitMq,
   AuthorizationService, CommunityService sections;
   `Jwt:RequireHttpsMetadata: "true"` in Production).
10. Implement the API surface in `docs/api/notifications.md` and the
    documentation in `docs/notifications.md` (completed; see
    `docs/architecture/prompt-10c-completion-report.md`).

## Outbox gate (ADR-015)

Best-effort publication is **not acceptable** for any Notifications integration
event at implementation time:

- Notifications is itself a guaranteed-delivery **consumer** of Workflow events
  (`WorkflowTaskAssigned`/`WorkflowTaskEscalated`), which the Workflow outbox
  already protects; Notifications must consume with receive-endpoint outbox
  semantics to reconcile exactly-once.
- Notifications' `NotificationDispatched` targets Audit (slot 11) and Analytics
  (slot 19) and must be protected before those consumers subscribe.

The MassTransit EF Core outbox is the intended mechanism (ADR-015, amended at
Prompt 08A-R2; Records gate at Prompt 08B, Workflow gate at Prompt 09C). The
outbox-protected set is **non-exhaustive by design**: any Notifications event
consumed by a guaranteed-delivery consumer is protected whether or not it is
listed in `docs/notifications.md`; never publish a compliance-critical event
best-effort merely because its name is absent from the table.

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which must include `communityos_notifications`. If the volume already
   exists, run `docker compose down -v` once to re-run the init scripts.

2. Run the Notifications API (from the repo root):

   ```sh
   dotnet run --project src/Services/Notifications/CommunityOS.Notifications.API
   ```

   In development the pipeline automatically migrates the `notifications`
   schema to the latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used.

## Database

- Database: `communityos_notifications`; schema `notifications`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_notifications;Username=communityos;Password=communityos`
  (key `ConnectionStrings:NotificationsDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `NotificationsDbContextFactory` provides the
design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Notifications/CommunityOS.Notifications.Infrastructure \
  --startup-project src/Services/Notifications/CommunityOS.Notifications.Infrastructure \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Notifications/CommunityOS.Notifications.Infrastructure \
  --startup-project src/Services/Notifications/CommunityOS.Notifications.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:NotificationsDb` | | `Host=localhost;Port=5432;Database=communityos_notifications;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod (`appsettings.Production.json`) |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-notifications` | Audit identifier |
| `CommunityService:BaseUrl` | | *(empty in dev)* | Community API base URL (channel-destination resolution at dispatch time) |
| `CommunityService:AccessToken` | | *(empty in dev)* | Service token presented to Community |
| `CommunityService:ClientId` | | `communityos-notifications` | `X-Client-Id` sent to Community |
| `NotificationRecipient.MaxRetryCount` | `5` | *(domain constant)* | Fixed provider-retry bound enforced by the domain entity; not configurable at runtime |
| `Notifications:RetentionWindow` | | `P180D` | Review-flagged retention disposition window |
| `Notifications:InternalClientId` | | *(reserved)* | Trusted in-process caller for future fact queries |

The Notifications service never reads the Authorization, Organization,
Community, Records, Workflow or Knowledge databases. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed). If
`CommunityService:BaseUrl` is unset, InApp dispatch still works (no destination
resolution needed); Email/SMS/Push dispatch fails closed at dispatch time.

## Health and operations

- Health: the Notifications API will expose **no dedicated health probe
  endpoint** (Records, Documents, Knowledge, Community and Workflow likewise
  expose none; only Organization and Authorization register `GET /health`).
  Observe availability through Serilog console/Seq logs and
  container/infrastructure probes.
- Logs: Serilog to console (Seq endpoint when configured). Never log
  notification bodies, names, destinations, failure reasons beyond the code.

## Testing

Unit and security regression tests should not require Docker (domain
invariants, terminal-state guards, multi-scope authorization, fail-closed
persistence, no-PII integration events, notification-creation idempotency,
Workflow reconcile consumers with substitute persistence):

```sh
dotnet test tests/Unit/CommunityOS.Notifications.Tests
```

Integration tests boot PostgreSQL via Testcontainers and require Docker; they
cover EF mapping, migrations, and the Workflow reconcile consumers. These tests
are enabled only once the transactional outbox is in place:

```sh
dotnet test tests/Integration/CommunityOS.Notifications.IntegrationTests
```

> Note: this environment has no Docker, so integration tests are expected to
> compile only and must be reported honestly as not executed.

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`InvalidOperationException: CommunityService:BaseUrl is not configured`** —
  set `CommunityService:BaseUrl` to the Community API base URL; Notifications
  only needs it to resolve non-InApp channel destinations at dispatch time.
  InApp delivery continues to work without it.
- **Recipient stuck `Failed` with `provider-not-configured`** — an Email/SMS/
  Push provider is not configured; this is the ratified fail-closed behavior
  (ADR-025 decision 5). The recipient is terminal; enabling a provider later
  requires a new notification.
- **`409` on dispatch of a `Dispatched` notification** — dispatch is only legal
  from `Queued`; the aggregate is terminal and immutable.
- **`409` on mark-read of an undelivered/failed notification** — read is only
  legal from `Delivered`.
- **Duplicate create for a source fact** — notification creation is idempotent
  per (type, source type, source id, channel); a duplicate create returns the
  existing notification (200), never a new row. The uniqueness constraint is
  the concurrency backstop.
- **Reconcile consumer missing events** — check that the transactional outbox
  is enabled (best-effort delivery loses events).
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_notifications` exists (init script only runs on a fresh
  volume).
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated and the build is up to date (warnings are treated as
  errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.