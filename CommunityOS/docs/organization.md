# Organization Service

The Organization bounded context is the operational administrative backbone of
a Faith institution (e.g. a National Spiritual Assembly, a Regional Council or
a Local Spiritual Assembly). It owns the organizational hierarchy — the
organizations, their units, the committees, the institutions, the
appointments and the delegation facts that carry administrative authority.
Person identity is owned by the Community service; the Organization service
only carries stable person ids.

## Model

- **Organization** — a Faith institution that operates within the community
  and owns the units that form the hierarchy. Has a name, type, jurisdiction,
  lifecycle status (`active` / `suspended` / `dissolved`) and optional
  established/dissolved dates.
- **OrganizationUnit** — a node in the organizational hierarchy (a cluster, a
  local unit, an institute, a team). Units belong to an organization and keep
  an effective-dated history of parents so hierarchy changes are auditable and
  the hierarchy can be reconstructed at any point in time.
- **Committee** — a committee within an organization. Committees carry an
  effective-dated membership roster and operate within a jurisdiction. They may
  be attached to a specific organization unit or span an organization.
- **Institution** — a canonical record of a Faith institution (e.g. the
  Universal House of Justice, an Auxiliary Board). Institutions are the bodies
  the Faith formally recognizes.
- **Appointment** — an appointment of a person to a role within an organization
  unit. Appointments are effective-dated and non-overlapping per person / unit /
  type.
- **DelegationFact** — a fact recording that a subject has delegated authority
  to another subject within the scope of an organization unit. Delegation facts
  are effective-dated, revocable and auditable.
- **Jurisdiction** — the geographic / administrative scope of an organization,
  committee or unit (`Global`, `National`, `Regional`, `Local`,
  `OrganizationUnit`, `Committee`), mirroring the scope taxonomy of the
  Authorization service so organization facts can participate in scope-aware
  authorization.
- **EffectivePeriod** — an inclusive half-open effective window used across all
  organization facts (hierarchy links, appointments, committee membership,
  delegation facts).

## Key rules

- A unit's parent is **effective-dated**: `CurrentParent(moment)` reconstructs
  the parent in force at a given moment, and reparenting writes a new link
  instead of mutating history.
- Reparenting a unit into its own subtree is rejected (`HierarchyCycleException`).
  Cycle detection against the whole hierarchy is performed by the application
  layer using the repository's `IsDescendantAsync`.
- Appointments of the same type for a person within the same unit must not
  overlap (`OverlappingAppointmentException`). Ending is a one-way transition:
  an ended appointment cannot be ended again.
- Delegation facts cannot be granted to oneself (`SelfDelegationFactException`),
  cannot be revoked twice, and are only active within their effective window.
- Organization facts are effective-dated, so temporal queries (current,
  historical, upcoming, ended) are first-class.

## HTTP API

All endpoints are versioned under `/api/v1/organizations` and require a valid
access token. See `docs/api/organization.md` for the full endpoint reference.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (see
  `CommunityOS.Organization.Infrastructure.Integration.OrganizationIntegrationEventPublisher`).
  Consumers (e.g. the Community service, which keeps a read-model of
  organization facts) subscribe through `CommunityOS.Contracts.Organization`.
- **Community** — the Community service never duplicates the organizational
  hierarchy. It consumes Organization integration events
  (`OrganizationCreated`, `OrganizationUpdated`, `OrganizationUnitCreated`,
  `OrganizationUnitUpdated`, `OrganizationUnitParentChanged`) into
  `organization_references` / `organization_unit_references` tables via
  `OrganizationIntegrationEventConsumer` so Community can reference
  organization facts without owning them (`ADR-016`).
- **Authorization** — the Organization service never reads the Authorization
  database. `AuthorizationGuard` is bound to an HTTP evaluator
  (`HttpAuthorizationEvaluator`) that calls the Authorization service's check
  API as a service principal, configured by the `AuthorizationService` section
  (`ADR-018`). Jurisdictions participate in hierarchy-aware authorization via
  the `OrganizationUnitCoverageDto` fact query, which the Authorization service
  calls over `/api/v1/orgunits/{id}/covers` as `communityos-authorization`.

## Data

- Database: `communityos_organization` (PostgreSQL), schema `organization`.
- Tables: `organizations`, `organization_units`, `organization_unit_parents`,
  `appointments`, `committees`, `committee_members`, `institutions`,
  `delegation_facts`.
- Schema is managed by EF Core migrations
  (`InitialCreateOrganization`).
- Development seeding creates a minimal national hierarchy when the
  organization table is empty.

### Consumed tables

The Organization service does not own a database schema on the consumer side.
Consumers project its events:

- Community: `organization_references`, `organization_unit_references` (schema
  `community`).

## Configuration

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `OrganizationDb` | *(local)* | PostgreSQL connection string |
| `AuthorizationService` | `BaseUrl` | `http://localhost:5007` | Base URL of the Authorization service check API |
| `AuthorizationService` | `AccessToken` | *(empty)* | Bearer token presented to Authorization as service principal |
| `AuthorizationService` | `ClientId` | `communityos-organization` | Audit/tracing identifier |
| `Organization` | `InternalClientId` | `communityos-authorization` | Trusted in-process caller for internal fact queries |
| `Jwt` | `Issuer` / `Audience` | *(local)* | Token validation for the API |

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Organization.Tests`) — domain
  invariants (effective periods, jurisdictions, hierarchy rules, appointment
  non-overlap, delegation revocation) and application service behaviour through
  the MediatR pipeline with substitute persistence.
- **Integration tests** (`tests/Integration/CommunityOS.Organization.IntegrationTests`)
  — EF mapping and migrations against a real PostgreSQL via Testcontainers
  (requires Docker).
