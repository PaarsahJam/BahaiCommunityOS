# Authorization Service

The Authorization bounded context answers one question: *may this subject
perform this action in this scope?* It provides role-based access control
(RBAC), scoped grants, delegations, break-glass emergency access and
relationship-based per-object permissions. It is fail-closed: anything that
cannot be safely established is a Deny.

## Model

- **Permission** — a protected capability named `domain.entity.action`
  (e.g. `records.record.read`, `authz.role.assign`). The full catalog is in
  `CommunityOS.Authorization.Application.Permissions.PermissionCatalog`.
- **Role** — a named, configurable collection of permissions. Roles are
  deployment configuration; business logic never hard-codes a decision around
  a specific role code.
- **Scope** — where a grant applies: `Global`, `National`, `Regional`, `Local`,
  `OrganizationUnit`, `Committee` or `Resource`.
- **Role assignment** — grants a role to a subject within a scope. Assignments
  are revocable and effective-dated.
- **Delegation** — a time-limited grant of a subset of the delegator's own
  permissions to another subject, within a scope.
- **Break-glass request** — a controlled emergency access grant requiring a
  reason, approval by a different subject, a narrow scope and an expiry.
- **Relationship tuple** — `(subject, relation, objectType, objectId,
  permissions)` used for per-object access (e.g. `has_permission`).

## Decision rules

The evaluator (`IAuthorizationEvaluator`) combines RBAC assignments,
delegations, break-glass grants and relationship tuples. A check is allowed
only when a grant is effective, carries the requested permission, and applies
to the requested scope. Deny reasons are coarse and non-sensitive:
`denied_by_default`, `missing_subject`, `invalid_permission`, `no_permission`,
`scope_mismatch`.

Key rules:

- A **global** grant of an administration permission (`authz.*`) is authority
  at every scope.
- A **global** grant of a **data** permission only applies to checks with no
  organization or resource context — it never grants access to a specific
  resource.
- Organization hierarchy (an ancestor grant covering a descendant org unit) is
  supplied by `IOrganizationContextProvider`; the provider is bound to the
  Organization service's HTTP covers endpoint and fails closed on any error.
- A delegator cannot delegate more than it holds, and cannot escape its scope.
- A break-glass request cannot be global, cannot be self-approved, and expires.

## HTTP API (versioned, under `/api/v1/authz`)

All endpoints require a valid access token.

| Method | Path | Capability required |
|--------|------|---------------------|
| POST | `/authz/check` | `authz.check` (only when checking another subject) |
| POST | `/authz/batch-check` | `authz.check` (only when any subject != caller) |
| POST | `/authz/relationships/write` | `authz.relationship.write` |
| POST | `/authz/relationships/read` | `authz.relationship.read` |
| POST | `/authz/delegations/grant` | `authz.delegation.grant` + each delegated permission |
| POST | `/authz/delegations/revoke` | delegator, or `authz.delegation.revoke` |
| POST | `/authz/breakglass/request` | self-service |
| POST | `/authz/breakglass/approve` | `authz.breakglass.approve` |
| POST | `/authz/breakglass/reject` | `authz.breakglass.approve` |
| POST | `/authz/breakglass/revoke` | requester, or `authz.breakglass.revoke` |
| GET | `/authz/breakglass` | own requests; all when `authz.breakglass.list` |
| GET | `/authz/roles` | `authz.role.list` |
| POST | `/authz/roles` | `authz.role.create` |
| PUT | `/authz/roles/{id}` | `authz.role.update` |
| POST | `/authz/roles/{id}/assign` | `authz.role.assign` |
| POST | `/authz/roles/assignments/{id}/revoke` | `authz.role.revoke` |

Clients never send roles or permissions in a check; they send only context
(`organizationUnitId`, `resourceType`, `resourceId`, optional attributes).

## Integration

- **Events** — domain events are published as integration events on RabbitMQ
  via MassTransit (`RoleAssigned`, `RoleRevoked`, `DelegationGranted`,
  `DelegationRevoked`, `BreakGlassRequested`, `BreakGlassApproved`,
  `BreakGlassRevoked`).
- **Organization** — the Organization service owns hierarchy; Authorization
  resolves organization-scoped grants by calling the Organization service's
  `/api/v1/orgunits/{id}/covers` endpoint over HTTP
  (`HttpOrganizationContextProvider`), presented as the internal client
  `communityos-authorization`. The exact-match-only default is used only when
  the HTTP integration is not registered. Fail-closed on any error.
- **Identity** — access tokens are validated with the Identity service's
  signing key (`Jwt:SigningPrivateKey` / `Jwt:SigningKeyXml` /
  `Jwt:SigningKeyBase64`). Without configuration a dev-only ephemeral key is
  generated so the service boots.

## Data

- Database: `communityos_authorization` (PostgreSQL), schema `authorization`.
- Tables: `roles`, `role_assignments`, `delegations`, `break_glass_requests`,
  `authorization_relationships`.
- Schema is managed by EF Core migrations
  (`AddAuthorizationInfrastructure`).
- Development seeding creates the role catalog and an optional bootstrap global
  administrator when `Authorization:BootstrapGlobalAdminSubjectId` is set.

## Configuration (`Authorization` section)

| Key | Default | Description |
|-----|---------|-------------|
| `MaxBreakGlassDurationMinutes` | 60 | Max break-glass window |
| `MaxBreakGlassPermissions` | 10 | Max permissions per break-glass request |
| `MaxDelegationDurationDays` | 30 | Max delegation length |
| `MaxDelegatedPermissions` | 25 | Max permissions per delegation |
| `BootstrapGlobalAdminSubjectId` | *(empty)* | Subject to bootstrap as GlobalAdministrator |

### Configuration (`OrganizationService` section)

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | *(required)* | Base URL of the Organization service API |
| `AccessToken` | *(empty)* | Service token presented to the Organization service |
| `ClientId` | `communityos-authorization` | `X-Client-Id` header sent to the covers endpoint |

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Authorization.Tests`) — domain
  invariants, the evaluator decision matrix, guard behaviour and
  privilege-escalation regressions through the full MediatR pipeline.
- **Integration tests** (`tests/Integration/CommunityOS.Authorization.IntegrationTests`)
  — EF mapping and migrations against a real PostgreSQL via Testcontainers
  (requires Docker).
