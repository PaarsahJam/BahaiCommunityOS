# Workflow Service Runbook

> **STATUS: RATIFIED (Prompt 09B).** Operational notes for the future Workflow
> service (`ADR-024` Accepted, `docs/workflow.md` ratified). **Not implemented.**
> These notes are the operational contract to be followed during Prompt 09C.

## Services

- **Workflow API** — to be created at `src/Services/Workflow/CommunityOS.Workflow.API`.
  Project name `CommunityOS.Workflow.API` (proposed; follow the Records project
  layout: Domain / Application / Infrastructure / API + unit and integration
  test projects).

## Prerequisites

- .NET 9 SDK.
- Docker with PostgreSQL 16 and RabbitMQ (see Implementation steps).
- The **transactional outbox** (ADR-015) must be enabled at the Workflow
  integration gate before any Workflow integration event is published or any
  Records/Knowledge consumer is registered in a shared environment. See "Outbox
  gate" below.

## Implementation status (Prompt 09B)

Not implemented. Ratified architecture and contracts exist in `ADR-024`,
`docs/workflow.md` and `docs/api/workflow.md`. Prompt 09C is the implementation
gate.

Planned implementation steps (Prompt 09C):

1. Create service projects under `src/Services/Workflow/` and reference them in
   `CommunityOS.sln` (Domain / Application / Infrastructure / API), plus the
   unit and integration test projects.
2. Add `CREATE DATABASE communityos_workflow;` to
   `docker/init/01-create-databases.sql`.
3. Create the initial EF Core migration `InitialCreateWorkflow`
   (`task_definitions`, `workflow_tasks`, `task_assignments`, `task_activity`,
   `organization_unit_references`, plus the MassTransit outbox tables in the
   `workflow` schema).
4. Wire `AddCommunityOSEventBusWithOutbox<WorkflowDbContext>`,
   `ConfigureAuthorizationService` + `HttpAuthorizationEvaluator` +
   `AuthorizationGuard`, and the open-generic `INotificationHandler<>`
   integration publisher following the existing pattern.
5. Register the ratified `workflow.*` permission matrix in the Authorization
   permission catalog and the development role seeds.
6. Register the `OrganizationUnitCreated/Updated/ParentChanged` consumer
   (ADR-016 projection), the Records
   `RecordSubmitted/RecordVerified/RecordRejected` consumer, and the Knowledge
   `QuestionFlagged/QuestionUnderReview/QuestionMerged/QuestionArchived/
   AiSuggestionRequested/AiSuggestionReviewed` consumer.
7. Configure the `AuthorizationService`, `DocumentsService` and `CommunityService`
   sections (service principals) and the event-bus/outbox wiring.
8. Seed the baseline task-definition catalog idempotently at migration time
   (`WorkflowCatalogSeeder`: `record-review`, `document-review`,
   `knowledge-moderation`, `knowledge-ai-review`, `general`).
9. Ship `appsettings.json` / `appsettings.Development.json` /
   `appsettings.Production.json` (`WorkflowDb`, JWT, RabbitMq,
   AuthorizationService, DocumentsService, CommunityService sections;
   `Jwt:RequireHttpsMetadata: "true"` in Production).
10. Implement the API surface in `docs/api/workflow.md` and the documentation in
    `docs/workflow.md`.

## Outbox gate (ADR-015)

Best-effort publication is **not acceptable** for any Workflow integration
event at implementation time:

- Workflow is itself a guaranteed-delivery **consumer** of Records events
  (`RecordSubmitted`/`RecordVerified`), which the Records outbox already
  protects; Workflow must consume with receive-endpoint outbox semantics to
  reconcile exactly-once.
- Workflow's compliance-significant events
  (`WorkflowTaskCreated/Completed/Cancelled/Escalated` for record review) target
  Notifications (slot 9), Search (slot 10) and Audit (slot 11) consumers and
  must be protected before those consumers subscribe.

The MassTransit EF Core outbox is the intended mechanism (ADR-015, amended at
Prompt 08A-R2; Records gate at Prompt 08B). The outbox-protected set is
**non-exhaustive by design**: any Workflow event consumed by a
guaranteed-delivery consumer is protected whether or not it is listed in
`docs/workflow.md`; never publish a compliance-critical event best-effort merely
because its name is absent from the table.

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which must include `communityos_workflow`. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Workflow API (from the repo root):

   ```sh
   dotnet run --project src/Services/Workflow/CommunityOS.Workflow.API
   ```

   In development the pipeline automatically migrates the `workflow` schema to
   the latest migration. Swagger is available at `/swagger`.

> Note: this machine currently has no .NET 9 runtime installed. When running
> tooling against the net9.0 targets, prefix with
> `$env:DOTNET_ROLL_FORWARD="Major"` so the .NET 10 runtime is used.

## Database

- Database: `communityos_workflow`; schema `workflow`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_workflow;Username=communityos;Password=communityos`
  (key `ConnectionStrings:WorkflowDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `WorkflowDbContextFactory` provides the
design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Workflow/CommunityOS.Workflow.Infrastructure \
  --startup-project src/Services/Workflow/CommunityOS.Workflow.Infrastructure \
  --output-dir Persistence/Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Workflow/CommunityOS.Workflow.Infrastructure \
  --startup-project src/Services/Workflow/CommunityOS.Workflow.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `CommunityOS/dotnet-tools.json` (`dotnet tool restore`).

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:WorkflowDb` | | `Host=localhost;Port=5432;Database=communityos_workflow;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod (`appsettings.Production.json`) |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-workflow` | Audit identifier |
| `DocumentsService:BaseUrl` | | *(empty in dev — must be configured for document references)* | Documents API base URL (task document references); unset, the Documents client fails closed with a clear message |
| `DocumentsService:AccessToken` | | *(empty in dev)* | Service token presented to Documents |
| `DocumentsService:ClientId` | | `communityos-workflow` | `X-Client-Id` sent to Documents |
| `CommunityService:BaseUrl` | | *(empty in dev)* | Community API base URL (assignee resolution at read time) |
| `CommunityService:AccessToken` | | *(empty in dev)* | Service token presented to Community |
| `CommunityService:ClientId` | | `communityos-workflow` | `X-Client-Id` sent to Community |
| `Workflow:InternalClientId` | | *(reserved)* | Trusted in-process caller for future fact queries |

The Workflow service never reads the Authorization, Organization, Community,
Documents, Records or Knowledge databases. If `AuthorizationService:BaseUrl` or
the presented token is misconfigured, every guarded endpoint returns
`403 Forbidden` (fail-closed).

## Health and operations

- Health probe: `GET /health`.
- Logs: Serilog to console (Seq endpoint when configured). Never log task
  notes, sensitive fields, secrets or names.

## Testing

Unit and security regression tests should not require Docker (domain invariants,
lifecycle transitions, multi-scope authorization, fail-closed persistence,
no-PII integration events, task-creation idempotency, Records/Knowledge
reconcile consumers with substitute persistence):

```sh
dotnet test tests/Unit/CommunityOS.Workflow.Tests
```

Integration tests boot PostgreSQL via Testcontainers and require Docker; they
cover EF mapping, migrations, and the Records/Knowledge reconcile consumers.
These tests are enabled only once the transactional outbox is in place:

```sh
dotnet test tests/Integration/CommunityOS.Workflow.IntegrationTests
```

> Note: this environment has no Docker, so integration tests are expected to
> compile only and must be reported honestly as not executed.

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`InvalidOperationException: DocumentsService:BaseUrl is not configured`** —
  set `DocumentsService:BaseUrl` to the Documents API base URL; Workflow only
  needs it when creating task document references.
- **`InvalidOperationException: CommunityService:BaseUrl is not configured`** —
  set `CommunityService:BaseUrl` to the Community API base URL; Workflow only
  needs it to resolve assignee names at read time.
- **`409` on create for an open task** — task creation is idempotent per
  (definition, domain entity); the domain entity already has an open task of
  that definition.
- **`409` on complete for a `Created`/`Assigned` task** — complete is only
  legal from `In Progress`; start the task first (`POST /tasks/{id}/start`).
- **Reconcile consumer missing events** — check that the transactional outbox
  is enabled (best-effort delivery loses events).
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_workflow` exists (init script only runs on a fresh
  volume).
- **`has-pending-model-changes` reports changes after a migration** — ensure the
  snapshot was regenerated and the build is up to date (warnings are treated as
  errors in this repo).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.