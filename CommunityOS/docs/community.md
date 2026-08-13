# Community Service

The Community bounded context owns the community itself: the people, the
households they live in, the family relationships between them, their community
membership, and the community life that sustains them — activities, events,
meetings and participation. It owns the person profile (a Person is a real
individual, **not** an Identity account). The Organization service owns the
administrative hierarchy; Community only keeps a read-model of organization
facts so its records can reference organizational context without owning it
(`ADR-016`).

## Model

- **Person** — a real individual known to the community. A person may exist
  without a login, and an Identity account may be linked to a person
  (auditable, reversible). Carries a preferred/formal name, preferred language,
  optional date of birth, contact methods and privacy preferences. A person is
  never hard-deleted: deactivation is a lifecycle state that preserves history.
- **ContactMethod** — an email, phone or postal contact on a person record.
  Structurally validated (emails are regex-checked), at most one is preferred,
  values are never duplicated. Visibility is gated by the application layer.
- **PrivacyPreferences** — profile / contact / date-of-birth visibility. Privacy
  is a Community concern; actual exposure is further gated by application-layer
  permissions.
- **Household** — a cohabitation unit (members need not be related and need not
  share an address). Members carry a role (`head` / `adult` / `child` /
  `dependent`) and an effective-dated membership.
- **FamilyRelationship** — a directional relationship between two persons
  (`parent` → `child`, `spouse`, `sibling`, …). Types are controlled, the data
  is sensitive (reads require an explicit grant), and self-relationships are
  rejected. Never encoded inside the Organization hierarchy.
- **Membership** — community membership of a person. A lifecycle (status +
  effective window) with a period history supports effective membership queries
  and preserves history. Never represented by an authorization role or an
  organization appointment.
- **Activity** — a community activity (gathering, study circle, devotional
  meeting, service project, …). Categories are configurable free text. Carries a
  schedule window, visibility, lifecycle status, optional organizer and an
  organization-unit reference.
- **CommunityEvent** — a community event (commemoration, holy day, festival, …).
  Registration may be open or closed; capacity is optional.
- **Meeting** — a meeting supporting the community workflow: participants,
  agenda items, action items, attendance and draft minutes. Minutes are
  operational drafts; formally declared records belong to the future Records
  service.
- **Participation** — a single participation record. One abstraction covers
  activity, event, meeting and volunteer/service participation. A volunteer
  assignment is not an organization appointment unless explicitly created as
  one in the Organization service.
- **EffectivePeriod / DateTimeRange** — effective windows and scheduling ranges
  shared across community facts (household membership, family relationships,
  membership, participation, activity/event schedules).
- **Community / LocalUnit / Cluster** — the deprecated Community-owned hierarchy
  (premature implementation). Tracked for data preservation only; the hierarchy
  is being relocated to the Organization service (`ADR-016`).

## Key rules

- A person is never hard-deleted; deactivation preserves historical
  participation.
- A person may be linked to an Identity account only once at a time; linking
  and unlinking are auditable events.
- At most one contact method is preferred; duplicate contact values are
  rejected.
- A household member cannot be added twice; roles are mutable.
- Family relationships are directional, cannot reference oneself, and duplicate
  active relationships of the same type are rejected by the application layer.
  Ending is one-way.
- A person has at most one membership; status transitions append period history
  and cannot move backwards in time (`InvalidEffectivePeriodException`).
- Activities, events and meetings require an ordered time range
  (`InvalidTimeRangeException`); rescheduling preserves the ordering invariant.
- A participation record for the same person/target is unique
  (`DuplicateParticipationException`); cancelled records are terminal.

## Privacy and permissions

Sensitive data classes carry their own permissions, so reading a profile never
implies reading all personal data:

- Contact details require `community.person.contact.read`.
- Date of birth and the identity link require `community.person.sensitive.read`.
- Family relationships require `community.family.read`.
- Identity linking/unlinking requires `community.person.identity.link`.

Privilege escalation is denied by fail-closed evaluation: if the Authorization
service is unreachable or the permission is not granted, the endpoint returns
`403 Forbidden`.

## HTTP API

All endpoints are versioned under `/api/v1` and require a valid access token.
See `docs/api/community.md` for the full endpoint reference.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (see
  `CommunityOS.Community.Infrastructure.Integration.CommunityIntegrationEventPublisher`).
  Contracts live in `CommunityOS.Contracts.Community`.
- **Organization** — Community never duplicates the organizational hierarchy. It
  consumes Organization integration events (`OrganizationCreated`,
  `OrganizationUnitCreated`, …) into `organization_references` /
  `organization_unit_references` via `OrganizationIntegrationEventConsumer`, so
  activities, events and meetings can carry an `organizationUnitId` reference
  without owning organization facts (`ADR-016`). The projection is eventually
  consistent and never influences authorization decisions.
- **Identity** — Community owns the person profile; Identity owns accounts. The
  link between them is explicit and reversible. Community never stores
  passwords or session state; Identity never owns the person profile.
- **Authorization** — the Community service never reads the Authorization
  database. `AuthorizationGuard` is bound to an HTTP evaluator
  (`HttpAuthorizationEvaluator`) that calls the Authorization service's check
  API as a service principal, configured by the `AuthorizationService` section
  (`ADR-018`/`ADR-019`).

## Data

- Database: `communityos_community` (PostgreSQL), schema `community`.
- Tables: `persons`, `contact_methods`, `households`, `household_members`,
  `family_relationships`, `memberships`, `membership_periods`, `activities`,
  `community_events`, `meetings`, `meeting_participants`, `meeting_agenda_items`,
  `meeting_actions`, `participations` (plus the deprecated hierarchy tables
  `communities`, `local_units`, `clusters`, `organization_references`,
  `organization_unit_references`).
- Schema is managed by EF Core migrations (`InitialCreateCommunity`,
  `AddCommunityLife`).

## Configuration

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `CommunityDb` | `Host=localhost;Port=5432;Database=communityos_community;Username=communityos;Password=communityos` | PostgreSQL connection string |
| `Jwt` | `Issuer` / `Audience` / `Secret` | *(local)* | Token validation for the API |
| `RabbitMq` | `Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Message bus for integration events |
| `AuthorizationService` | `BaseUrl` / `AccessToken` / `ClientId` | *(see runbook)* | Authorization check API configuration |

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Community.Tests`) — domain invariants
  (contact uniqueness, identity link lifecycle, household/family/membership
  rules, activity/event/meeting scheduling and status transitions,
  participation) and application service behaviour through the MediatR pipeline
  with substitute persistence.
- **Security regression tests** — fail-closed authorization, privacy-scoped
  reads (contact/date-of-birth/identity masking), identity-link permission and
  family-read grants.
- **Integration tests** (`tests/Integration/CommunityOS.Community.IntegrationTests`)
  — EF mapping and migrations against a real PostgreSQL via Testcontainers
  (requires Docker).
