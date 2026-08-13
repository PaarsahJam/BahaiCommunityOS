# Architecture Decision Records

This directory contains ADRs for CommunityOS.

## Index

| ID      | Title                                     | Status   |
|---------|-------------------------------------------|----------|
| ADR-001 | Modular Monolith as initial deployment    | Accepted |
| ADR-002 | Clean Architecture per bounded context    | Accepted |
| ADR-003 | MediatR for in-process CQRS              | Accepted |
| ADR-004 | Transport-independent event bus (MassTransit + RabbitMQ; NATS/Kafka future) | Accepted |
| ADR-005 | Flutter for all client applications       | Accepted |
| ADR-006 | PostgreSQL as primary data store          | Accepted |
| ADR-007 | Central Package Management (CPM)          | Accepted |
| ADR-008 | Testcontainers for integration tests      | Accepted |
| ADR-009 | Authorization as a bounded context        | Accepted |
| ADR-010 | Fail-closed permission evaluation         | Accepted |
| ADR-011 | Scoped grants with exact-match fallback   | Accepted |
| ADR-012 | Relationship tuples for per-object access | Accepted |
| ADR-013 | Delegation constrained to held authority  | Accepted |
| ADR-014 | Break-glass requires approval and expiry  | Accepted |
| ADR-015 | Reliable event publication via transactional outbox | Accepted |
| ADR-016 | Organization bounded context and Community boundary | Accepted |
| ADR-017 | Bounded context implementation sequence | Accepted |
| ADR-018 | Authentication and authorization ownership boundary | Accepted |
| ADR-019 | Community security corrections | Accepted |
| ADR-020 | Repository specifications as source of truth | Accepted |
| ADR-021 | Knowledge bounded context and Library boundary | Accepted |

## ADR-004 — Transport-independent event bus (MassTransit + RabbitMQ; NATS/Kafka future)

**Status:** Accepted (ratified at architecture reconciliation).

CommunityOS architecture is transport-independent. Domain and integration
contracts are transport-agnostic and must never depend on a specific broker,
queueing topology or serialization technology.

- **Current implementation:** MassTransit with the RabbitMQ transport
  (`CommunityOS.EventBus`). RabbitMQ remains the implementation transport for
  the current development phase.
- **NATS** is not required to be introduced immediately. It remains an
  evaluated future transport option.
- **Kafka** remains a future option for high-scale and event-analytics
  requirements.
- Transport selection must remain isolated to the event-bus registration and
  configuration; changing transport must not require changes to contracts or
  handlers.

This supersedes the earlier "MassTransit + RabbitMQ for async events" decision.

## ADR-009 — Authorization as a bounded context

**Status:** Accepted

Authorization is its own bounded context (`CommunityOS.Authorization.*`) owning
roles, assignments, scopes, delegations, break-glass and relationship tuples.
No other service embeds authorization decisions; they call the Authorization
service's API (or, in-process, the evaluator). Clients only ever supply context
(organization unit, resource, attributes) — never roles, permissions or
decisions.

## ADR-010 — Fail-closed permission evaluation

**Status:** Accepted

The evaluator is fail-closed: any subject, permission, scope or data state that
cannot be safely established results in a Deny. Missing subjects, invalid
permission names, dangling role references, expired or revoked grants, and
unknown hierarchy relationships all deny. There is no allow-by-default path.

## ADR-011 — Scoped grants with exact-match fallback

**Status:** Accepted

Every grant carries an `AuthorizationScope` (`Global`, `National`, `Regional`,
`Local`, `OrganizationUnit`, `Committee`, `Resource`). A global grant of an
administration permission (`authz.*`) is authority at every scope; a global
grant of a data permission never grants resource access. Organizational
hierarchy awareness comes exclusively from the Organization service through
`IOrganizationContextProvider`, which defaults to exact-match only and fails
closed until that integration exists.

## ADR-012 — Relationship tuples for per-object access

**Status:** Accepted

Relationship-based access uses persisted tuples (`subject`, `relation`,
`objectType`, `objectId`, optional permissions). Tuples are read-through on
every check (no caching) so revocation is immediately effective. Permission
grants must match the requested resource exactly.

## ADR-013 — Delegation constrained to held authority

**Status:** Accepted

Delegations are always scoped, time-limited, revocable and audited. A delegator
must hold `authz.delegation.grant` and each delegated permission at the
delegation's scope (privilege-escalation protection). Self-delegation and
global delegations are forbidden.

## ADR-014 — Break-glass requires approval and expiry

**Status:** Accepted

Emergency access is an explicit, reason-required, narrowly-scoped request that
must be approved by a different subject, cannot be global, and automatically
expires. Break-glass grants never silently bypass normal authorization and emit
high-priority audit events.

## ADR-015 — Reliable event publication via transactional outbox

**Status:** Accepted (documented decision; implementation deferred).

CommunityOS requires reliable event publication. Any domain change that raises
domain/integration events must not lose those events if the message broker is
unavailable or the publish fails. The intended architecture:

```
Database transaction
        ↓
Domain state + Outbox message
        ↓
Commit
        ↓
Outbox dispatcher
        ↓
MassTransit
        ↓
RabbitMQ
```

Domain state and the outbox message commit in the same database transaction; a
dispatcher reads committed outbox rows and publishes them onto the bus via
MassTransit. Implementation is deferred until the first cross-service consumers
require guaranteed delivery; the MassTransit EF Core outbox is the intended
mechanism. Until then, publishers remain best-effort in-process delivery, and
this limitation is tracked for removal.

## ADR-016 — Organization bounded context and Community boundary

**Status:** Accepted (ratified at architecture reconciliation).

The Organization bounded context owns:

- organizations
- organization units
- organizational hierarchy
- institutions
- committees
- teams
- appointments
- appointment terms
- jurisdiction
- organizational delegation facts
- effective-dated organizational history

The Community bounded context owns:

- persons
- households
- family relationships
- contact information
- community membership
- activities
- events
- meetings
- participation

The existing Community organizational hierarchy (National / Regional / Cluster /
Local Unit) is considered premature implementation. It is not deleted; it will
be relocated and refactored as part of the Organization implementation
(Prompt 04).

Organization will own its own database, `communityos_organization`. There is no
shared database with Community, Identity or Authorization.

## ADR-017 — Bounded context implementation sequence

**Status:** Accepted (ratified at architecture reconciliation).

The ratified implementation sequence for CommunityOS is:

1. Solution Foundation
2. Identity
3. Authorization
4. Organization
5. Community
6. Documents
7. Records
8. Workflow
9. Notifications
10. Search
11. Audit
12. Knowledge
13. Correspondence
14. Localization
15. AI Platform
16. Integrations
17. Finance
18. Communications / VoIP
19. Analytics

## ADR-018 — Authentication and authorization ownership boundary

**Status:** Accepted (ratified at architecture reconciliation; extends ADR-009).

The Identity bounded context owns authentication: accounts, credentials, MFA,
sessions, devices, and recovery.

The Authorization bounded context owns authorization: roles, permissions,
policies, relationship tuples, authorization decisions, authorization
delegation, and break-glass authorization.

The Organization bounded context owns organizational facts. The Community
bounded context owns person/community facts.

No service may access another service's database. All cross-service data access
is via the owning service's API or integration events.

## ADR-019 — Community security corrections

**Status:** Accepted (recorded; implementation deferred to the Community
re-scope).

The following Community service issues are recorded for correction during the
Community re-scope:

- HMAC JWT validation must be replaced by validation of Identity-issued RS256
  tokens using the appropriate public key / JWKS mechanism.
- `[Authorize(Roles = "...")]` must not be used for Community authorization
  decisions.
- Community must use the existing Authorization service/guard mechanism for
  authorization decisions.

These corrections are not implemented during the reconciliation phase; they are
recorded to prevent regression.

## ADR-020 — Repository specifications as source of truth

**Status:** Accepted (ratified at architecture reconciliation).

The `/specifications` directory in this repository is the authoritative
architectural baseline for CommunityOS implementation work. Files marked
PLACEHOLDER are pending import of their authoritative source documents; they do
not invent requirements and must be replaced before being relied upon.
Ratified decisions are recorded in this ADR document.

## ADR-021 — Knowledge bounded context and Library boundary

**Status:** Accepted (ratified at architecture reconciliation; positions the
Knowledge service in the ADR-017 sequence).

The Knowledge bounded context owns:

- the Library: authoritative source material (Works, Editions, Passages) with
  provenance, verification status and multilingual translations
- community questions (a lifecycle: Draft → Submitted → Published → Under
  Review → Merged / Canonicalized / Archived)
- answers and structured discussions attached to questions
- references and citations from community content to Library passages
- categories / topics / tags used to organize questions and answers
- moderation flags and review state
- AI-assist boundaries: AI-generated suggestions are first-class, clearly
  marked, non-authoritative artifacts that require human review before
  publication (see ADR-021 boundary rules below)

The Knowledge service is positioned in the implementation sequence (ADR-017)
after Search and before Correspondence. It depends on the platform services
Authorization, Search, AI, Documents, Workflow and Notifications; it never owns
persons, organization units, search indices, AI model behavior or notification
delivery — it only references them through APIs and integration events.

The following boundary rules are mandatory:

- Authoritative religious text must never be produced, edited or arbitrated by
  AI. AI-generated content is always a *suggestion* artifact with a distinct
  provenance, never a Library entry and never the canonical answer.
- The Library is the single source of truth for citation text. Community
  answers and discussions cite Library passages by stable passage id; they never
  embed authoritative text as their own content.
- Knowledge owns its own database, `communityos_knowledge`. There is no shared
  database with Community, Organization, Identity, Authorization or AI.
- All cross-service access (person authors, organization-unit scoping, AI
  suggestions, search indexing, notifications, workflow reviews) is via the
  owning service's API or integration events (`ADR-018`).
