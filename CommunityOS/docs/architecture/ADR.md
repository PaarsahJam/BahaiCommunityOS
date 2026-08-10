# Architecture Decision Records

This directory contains ADRs for CommunityOS.

## Index

| ID      | Title                                     | Status   |
|---------|-------------------------------------------|----------|
| ADR-001 | Modular Monolith as initial deployment    | Accepted |
| ADR-002 | Clean Architecture per bounded context    | Accepted |
| ADR-003 | MediatR for in-process CQRS              | Accepted |
| ADR-004 | MassTransit + RabbitMQ for async events   | Accepted |
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
