# CommunityOS

CommunityOS is a modular, privacy-conscious community management and knowledge
platform designed to provide a secure foundation for community organizations,
their members, volunteers, committees, and administrators.

The project is being developed with a strong emphasis on:

- security and least privilege
- privacy and data minimization
- modular service boundaries
- auditable operations
- reliable asynchronous processing
- explicit architectural decisions
- fail-closed security behavior
- maintainable, testable .NET services

> **Project status:** Active development / pre-release.
>
> CommunityOS is not currently presented as a production-ready or ratified
> platform. Architectural decisions and implementation details continue to
> undergo review.

---

## Vision

CommunityOS aims to provide a common technical foundation for community
organizations that need secure management of people, activities, knowledge,
documents, correspondence, records, notifications, workflows, and related
services.

The architecture is intentionally modular so that individual capabilities
can evolve independently while sharing common security, identity, messaging,
audit, and infrastructure conventions.

---

## Current Architecture

CommunityOS is built around a modular service architecture using:

- **ASP.NET Core / .NET**
- **Entity Framework Core**
- **PostgreSQL**
- **RabbitMQ**
- **MassTransit**
- **Transactional outbox/inbox patterns**
- **JWT-based authentication**
- **RSA-signed access tokens**
- **Clean Architecture principles**
- **REST APIs**
- **Automated unit and integration testing**

The repository contains multiple domain services together with shared
building blocks and host infrastructure.

### Major areas

- Identity and authentication
- Authorization
- Community management
- Correspondence
- Documents
- Finance
- Knowledge
- Localization
- Notifications
- Organization
- Records
- Search
- Workflow
- AI services
- API Gateway / host infrastructure

Service boundaries are designed so that services own their own persistence
and do not directly access another service's database.

---

## Security Architecture

Security is treated as a cross-cutting architectural concern.

Current design principles include:

### Authentication

Access tokens are JWTs signed using RSA-based signing keys.

Access tokens currently have a limited lifetime, with refresh sessions used
for longer-lived authentication.

JWT validation is performed at the appropriate service authentication
boundaries rather than relying on the API Gateway as the security authority.

### Authorization

Authorization follows least-privilege and fail-closed principles.

Permissions are preferred over hard-coded role checks where the architecture
requires fine-grained authorization.

Security-sensitive operations are intended to be explicit, auditable, and
subject to authorization controls.

### Auditability

Security-sensitive operations are designed to integrate with the project's
audit architecture.

The system favors durable, transactional records over best-effort logging for
security-relevant state changes.

### Session and Token Security

The Identity service contains infrastructure for account-scoped session
revocation epochs, developed together with transactional refresh-session
rotation and emergency session invalidation.

The corresponding architectural decision record, ADR-036 ("Session Revocation
and Access-Token Validity Model"), remains **Proposed**. It is not ratified.

One narrowly scoped part of it has been decided and implemented. The
revocation epoch is keyed per account, and the Identity `UserAccount` row is
protected by a PostgreSQL `xmin` whole-row concurrency token, so a stale write
cannot overwrite a newer committed revocation epoch. A stale epoch is rejected
rather than clamped, and is not automatically retried.

The broader architecture is still undecided. Open questions remain on the
revocation-state store, propagation and consistency guarantees, fail-open
versus fail-closed behavior, required invalidation latency, scope and event
mapping, gateway versus downstream enforcement, and rollout strategy.

Related material remains provisional or unresolved: the refresh-session
epoch-at-issue binding is provisional, and the provenance of the existing
prototype migrations is not yet resolved. The prototype code, migrations, and
tests are working evidence rather than ratified decisions.

---

## Reliability and Messaging

CommunityOS uses asynchronous messaging where appropriate.

MassTransit and RabbitMQ provide the messaging infrastructure, while
transactional outbox/inbox patterns are used to preserve consistency between
database state changes and published integration events.

The architecture favors:

- atomic database state transitions
- at-least-once delivery
- idempotent consumers
- monotonic state application where applicable
- explicit failure handling
- avoidance of distributed database transactions

Messaging is not treated as a substitute for service ownership or database
consistency.

---

## API Gateway

The API Gateway is intentionally designed as a transparent forwarding layer.

It does not serve as the authoritative Identity security boundary and does
not perform per-request token introspection or session-revocation decisions.

Authentication and authorization remain responsibilities of the appropriate
services.

This separation prevents the Gateway from becoming a centralized dependency
for domain-specific security state.

---

## Architecture Decisions

Architecture is documented through Architecture Decision Records (ADRs).

The ADR collection documents decisions concerning subjects such as:

- authentication and authorization
- service boundaries
- API Gateway behavior
- audit and retention
- messaging and transactional outbox
- session and token validity
- infrastructure responsibilities

ADRs are treated as controlled architectural artifacts rather than informal
documentation.

An ADR marked **Proposed** must not be interpreted as a ratified architectural
decision.

---

## Testing

The repository contains extensive automated tests across the implemented
services.

Testing includes:

- domain/unit tests
- application-layer tests
- infrastructure tests
- authentication and authorization tests
- integration tests
- PostgreSQL-backed persistence tests
- transaction/concurrency tests
- messaging/outbox tests

Security-sensitive concurrency behavior is tested against real PostgreSQL
where database locking and transaction semantics are part of the behavior
under test.

---

## Development Status

CommunityOS is currently under active development.

Implemented and verified areas include substantial portions of:

- Identity
- authentication and JWT validation
- authorization
- session management
- transactional refresh rotation
- account-scoped session-revocation state
- emergency session invalidation
- integration-event publication through the transactional outbox
- multiple domain services and shared infrastructure

Some architectural areas remain subject to formal review before additional
implementation is authorized.

Implemented and verified behavior is not the same as ratified architecture. In
session-revocation work, only the per-account epoch keying and the `UserAccount`
concurrency protection are decided; the remaining questions are still open, and
ADR-036 stays **Proposed** until the project owner answers them.

Session-revocation propagation and reconciliation architecture is undergoing an
adversarial architecture review before the related ADR is amended and ratified.

---

## Repository Structure

A simplified repository structure is:

```text
CommunityOS/
├── src/
│   ├── BuildingBlocks/
│   ├── Host/
│   └── Services/
│       ├── AI/
│       ├── Community/
│       ├── Correspondence/
│       ├── Documents/
│       ├── Finance/
│       ├── Identity/
│       ├── Knowledge/
│       ├── Localization/
│       ├── Notifications/
│       ├── Organization/
│       ├── Records/
│       ├── Search/
│       └── Workflow/
│
├── tests/
│   ├── Unit/
│   └── Integration/
│
├── docs/
│   └── architecture/
│
└── CommunityOS.sln