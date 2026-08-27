# Finance Service Runbook

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 16U).**
> Operational notes for the Finance service (ADR-032 Accepted,
> `docs/finance.md` ratified). Implementation completed in Prompt 16U.

## Services

- **Finance API** — `src/Services/Finance/CommunityOS.Finance.API`
  (`CommunityOS.Finance.API`).

## Prerequisites

- .NET 9 SDK (this machine runs .NET 10; prefix dotnet commands with
  `$env:DOTNET_ROLL_FORWARD="Major"`).
- Docker with PostgreSQL 16 and RabbitMQ (see Local development). **No Docker is
  available on the development host** — see "Environment limitation" below.
- The Authorization service (permission checks) and Identity (JWT) must be
  reachable; the guard is fail-closed.

## Implementation status (Prompt 16U)

All implementation-time steps are complete:

1. Service projects exist under `src/Services/Finance/` and are registered in
   `CommunityOS.sln` (Domain / Application / Infrastructure / API) plus the
   unit and integration test projects.
2. `CREATE DATABASE communityos_finance;` is present in
   `docker/init/01-create-databases.sql`.
3. Initial EF Core migration `InitialCreateFinance` created (`funds`,
   `transactions`, `organization_unit_references` + the MassTransit outbox
   tables in the `finance` schema).
4. `AddCommunityOSEventBus` (transactional outbox, committed atomically with
   the ledger) + `ConfigureAuthorizationService` +
   `HttpAuthorizationEvaluator` + `AuthorizationGuard` are wired following the
   existing pattern. The open-generic `INotificationHandler<>` publisher
   forwards `FinanceTransactionRecorded`.
5. The ratified `finance.*` permission matrix (6 permissions) is registered in
   the Authorization permission catalog and seeded to **both**
   `GlobalAdministrator` and `NationalAdministrator` in the development seed.
6. The `OrganizationUnitCreated/Updated` consumer projects into
   `organization_unit_references` (ADR-016 pattern; a flat projection, so
   `OrganizationUnitParentChanged` is not consumed — see docs/finance.md
   Deviations).
7. `appsettings.json` / `appsettings.Development.json` ship with the API
   (`FinanceDb`, JWT, RabbitMq, AuthorizationService sections).
8. Unit tests (77, no external dependencies) and offline model integration
   tests (4) are green.

## Outbox gate (ADR-015)

The transactional outbox is **enabled at the Finance gate** (ADR-032 eventing
posture): the first-gate consumer — Organization unit events into the read-model
projection — requires guaranteed delivery. `FinanceTransactionRecorded` is
forwarded through the MassTransit EF Core outbox and commits **atomically with
the ledger row**; the in-process publisher runs before the single DB transaction
commits. This supercedes any earlier deferral and is the approved posture for
Finance.

## Local development

1. Start infrastructure:

   ```sh
   docker compose -f docker/docker-compose.yml up -d
   ```

   On first boot the Postgres entrypoint runs `docker/init/01-create-databases.sql`,
   which includes `communityos_finance`. If the volume already exists, run
   `docker compose down -v` once to re-run the init scripts.

2. Run the Finance API (from the repo root):

   ```sh
   dotnet run --project src/Services/Finance/CommunityOS.Finance.API
   ```

   In development the pipeline automatically migrates the `finance` schema to
   the latest migration. Swagger is available at `/swagger`.

> **Environment limitation (Prompt 16U):** the development host has no Docker.
> The Finance integration tests build the EF model **offline** (no database) —
> they verify mappings, the schema default and the outbox registration without
> a live PostgreSQL. A real PostgreSQL round-trip (Testcontainers) remains a
> pending CI step; the UTF-8 / Npgsql plan is to enable it in CI where Docker
> exists. This is reported as an environment limitation, not a test gap in code
> under the Finance gate.

## Database

- Database: `communityos_finance`; schema `finance`.
- Connection string default:
  `Host=localhost;Port=5432;Database=communityos_finance;Username=communityos;Password=communityos`
  (key `ConnectionStrings:FinanceDb`).

### Adding or changing the schema

The schema is managed with EF Core migrations. The migration assembly lives in
the Infrastructure project; a `FinanceDbContextFactory` provides the
design-time factory.

```sh
dotnet ef migrations add <Name> \
  --project src/Services/Finance/CommunityOS.Finance.Infrastructure \
  --startup-project src/Services/Finance/CommunityOS.Finance.Infrastructure \
  --output-dir Persistence/Migrations \
  --namespace CommunityOS.Persistence.Migrations
```

Verify there are no pending model changes before shipping:

```sh
dotnet ef migrations has-pending-model-changes \
  --project src/Services/Finance/CommunityOS.Finance.Infrastructure \
  --startup-project src/Services/Finance/CommunityOS.Finance.Infrastructure
```

> Note: `dotnet ef` requires the EF Core tools; this repo pins `dotnet-ef`
> 10.0.7 in `dotnet-tools.json` (`dotnet tool restore`). Run commands with
> `$env:DOTNET_ROLL_FORWARD="Major"`.

## Configuration (ratified)

| Section | Key | Example | Notes |
|---------|-----|---------|-------|
| `ConnectionStrings:FinanceDb` | | `Host=localhost;Port=5432;Database=communityos_finance;...` | PostgreSQL |
| `Jwt:Authority` / `Jwt:MetadataAddress` | | `http://localhost:5001` | Identity OIDC discovery (JWKS for RS256 validation) |
| `Jwt:Issuer` / `Jwt:Audience` | | `http://localhost:5001` / `CommunityOS` | Bearer token issuer/audience |
| `Jwt:RequireHttpsMetadata` | | `false` *(local)* / `true` *(prod)* | Identity discovery over HTTPS in prod |
| `RabbitMq:Host` / `Port` / `Username` / `Password` | | `localhost` / `5672` / `guest` / `guest` | MassTransit bus |
| `AuthorizationService:BaseUrl` | | `http://localhost:5007` | Authorization check API base URL |
| `AuthorizationService:AccessToken` | | *(empty in dev)* | Service-principal bearer token |
| `AuthorizationService:ClientId` | | `communityos-finance` | Audit identifier |
| `Finance:InternalClientId` | | `communityos-authorization` | Trusted service-principal identifier |

The Finance service never reads the Authorization or Organization databases. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed).

## Health and operations

- Health: the Finance API registers no dedicated health probe endpoint (only
  Organization and Authorization register `GET /health`). Observe availability
  through Serilog console/Seq logs and container/infrastructure probes.
- Logs: Serilog to console (Seq endpoint when configured). **Never log amounts,
  payee details, descriptions, secrets or names** — structured logs carry only
  stable ids, action, actor and outcome.

## Testing

Unit and security regression tests do not require Docker (domain invariants,
fail-closed authorization, denied-command-persists-nothing, read-denial as
not-found, fail-closed list filtering, unknown-status fail-soft passthrough,
permission catalog alignment, integration-event payload boundary, JWT
RS256-only validation, log-template token analysis):

```sh
dotnet test tests/Unit/CommunityOS.Finance.Tests
```

Integration tests build the EF model offline and require no Docker:

```sh
dotnet test tests/Integration/CommunityOS.Finance.IntegrationTests
```

Build the full solution (the three pre-existing NU1605 test projects —
Community/Documents/Knowledge — are a known, out-of-scope trio):

```sh
dotnet build CommunityOS.sln
```

## Troubleshooting

- **`403 Forbidden` on all endpoints** — check `AuthorizationService:BaseUrl`
  and the service token; the guard is fail-closed.
- **`400` creating a fund** — the `organizationUnitId` is not in the
  read-model references yet (the Organization consumer has not projected the
  unit), or `currency` is lowercase/non-ISO-4217.
- **`400` recording a transfer** — `transferDestinationFundId` is missing,
  equals the source fund, or belongs to a different currency context; transfers
  require a distinct destination.
- **`409` approving a transaction** — the actor is the same subject that
  recorded the entry (separation of duties), or the transaction is not
  `PendingApproval`.
- **`409` recording on a closed fund** — close the fund is terminal; a new fund
  must be created.
- **`GET /funds/{id}` returns `404` for a fund the caller created** — the read
  capability (`finance.fund.read`) is evaluated at the fund's unit scope;
  holding only `finance.fund.manage` does not imply read.
- **`Failed to create database` on startup** — ensure the Postgres container is
  up and `communityos_finance` exists (init script only runs on a fresh volume).
- **`dotnet ef` fails with a .NET 9 runtime error** — run with
  `$env:DOTNET_ROLL_FORWARD="Major"` after `dotnet tool restore`.
- **Outbox messages not draining** — confirm RabbitMQ is reachable and the bus
  is healthy; outbox delivery is required for the Organization unit consumer
  and future subscriptions.