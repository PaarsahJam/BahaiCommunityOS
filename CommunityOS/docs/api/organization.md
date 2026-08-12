# Organization Service API

All endpoints are versioned under `/api/v1`, require a valid access token
(`[Authorize]`), and return the organization entity's DTOs. Jurisdiction types
are `Global`, `National`, `Regional`, `Local`, `OrganizationUnit`, `Committee`.

## Organizations — `/organizations`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/organizations` | `organization.org.read` | List organizations |
| POST | `/organizations` | `organization.org.create` | Create an organization |
| GET | `/organizations/{id}` | `organization.org.read` | Get an organization |
| PUT | `/organizations/{id}` | `organization.org.update` | Update name and jurisdiction |
| POST | `/organizations/{id}/dissolve` | `organization.org.dissolve` | Dissolve an organization |

**Create** body:

```json
{
  "name": "Ridvan Cluster",
  "organizationType": "LocalSpiritualAssembly",
  "jurisdictionType": "Local",
  "jurisdictionScopeId": "00000000-0000-0000-0000-000000000000",
  "establishedOn": null
}
```

## Organization units — `/orgunits`

Units belong to an organization and keep an effective-dated history of parents.
Temporal endpoints accept an optional `asOf` query parameter (defaults to now).

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/orgunits?organizationId={id}&asOf=` | `organization.unit.read` | List units (optional `asOf`) |
| POST | `/orgunits` | `organization.unit.create` | Create a unit (optionally with a parent) |
| GET | `/orgunits/{id}?asOf=` | `organization.unit.read` | Get unit detail + parent history + children |
| PATCH | `/orgunits/{id}` | `organization.unit.update` | Update name / unit type |
| PATCH | `/orgunits/{id}/parent` | `organization.unit.reparent` | Change the effective-dated parent |
| POST | `/orgunits/{id}/deactivate` | `organization.unit.deactivate` | Deactivate a unit |
| GET | `/orgunits/{id}/children?asOf=` | `organization.unit.read` | List effective children |
| GET | `/orgunits/{id}/ancestors?asOf=` | `organization.unit.read` | List effective ancestors |
| GET | `/orgunits/{id}/descendants?asOf=` | `organization.unit.read` | List effective descendants |
| GET | `/orgunits/{id}/covers?ancestorId={id}&asOf=` | *service-only* | Fact query: is `ancestorId` ancestor-or-self of the unit (requires `X-Client-Id`) |

**Create** body:

```json
{
  "organizationId": "00000000-0000-0000-0000-000000000000",
  "name": "Cluster 1",
  "unitType": "Cluster",
  "parentId": null,
  "effectiveFrom": "2026-08-11T00:00:00Z"
}
```

**Change parent** body:

```json
{
  "parentId": "00000000-0000-0000-0000-000000000000",
  "effectiveFrom": "2026-08-11T00:00:00Z",
  "effectiveUntil": null
}
```

Reparenting a unit into its own subtree returns `409 Conflict`.

## Appointments — `/appointments`

Appointments are effective-dated and non-overlapping per person / unit / type.

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/appointments?personId=&organizationUnitId=&asOf=&view=` | `organization.appointment.read` (+ `organization.appointment.read.person` for person-scoped) | List appointments (view: `Current`, `Historical`, `Upcoming`, `Ended`, `All`) |
| POST | `/appointments` | `organization.appointment.assign` | Assign an appointment |
| GET | `/appointments/{id}` | `organization.appointment.read` | Get an appointment |
| POST | `/appointments/{id}/end` | `organization.appointment.end` | End an appointment |

**Assign** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "organizationUnitId": "00000000-0000-0000-0000-000000000000",
  "appointmentType": "Treasurer",
  "effectiveFrom": "2026-08-11T00:00:00Z",
  "effectiveUntil": null,
  "reason": null
}
```

Overlapping appointments of the same type return `409 Conflict`. The `Reason`
field is masked unless the caller holds `organization.appointment.reason.read`.

## Committees — `/committees`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/committees?organizationId=&asOf=` | `organization.committee.read` | List committees |
| POST | `/committees` | `organization.committee.create` | Create a committee |
| GET | `/committees/{id}?asOf=` | `organization.committee.read` | Get a committee |
| PUT | `/committees/{id}` | `organization.committee.update` | Update name and jurisdiction |
| POST | `/committees/{id}/members` | `organization.committee.member.add` | Add an effective-dated member |
| DELETE | `/committees/{id}/members/{personId}?roleCode=` | `organization.committee.member.remove` | Remove a member |
| POST | `/committees/{id}/deactivate` | `organization.committee.deactivate` | Deactivate a committee |

**Add member** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "roleCode": "convener",
  "effectiveFrom": "2026-08-11T00:00:00Z",
  "effectiveUntil": null
}
```

## Institutions — `/institutions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/institutions` | `organization.institution.read` | List institutions |
| POST | `/institutions` | `organization.institution.create` | Create an institution |
| GET | `/institutions/{id}` | `organization.institution.read` | Get an institution |
| PUT | `/institutions/{id}` | `organization.institution.update` | Update name and jurisdiction |

## Delegations — `/delegations`

Delegation facts record that one subject delegated authority to another within
the scope of an organization unit.

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/delegations?delegatorId=&delegateId=&organizationUnitId=` | `organization.delegation.read` (+ `organization.delegation.read.person` for person-scoped) | List delegation facts |
| POST | `/delegations` | `organization.delegation.grant` | Grant a delegation fact |
| GET | `/delegations/{id}` | `organization.delegation.read` | Get a delegation fact |
| POST | `/delegations/{id}/revoke` | `organization.delegation.revoke` | Revoke a delegation fact |

**Grant** body:

```json
{
  "delegatorId": "00000000-0000-0000-0000-000000000000",
  "delegateId": "00000000-0000-0000-0000-000000000000",
  "organizationUnitId": "00000000-0000-0000-0000-000000000000",
  "delegationType": "SigningAuthority",
  "effectiveFrom": "2026-08-11T00:00:00Z",
  "effectiveUntil": null,
  "reason": null
}
```

Granting to oneself returns `400 Bad Request`; revoking twice returns
`409 Conflict`. The `Reason` field is masked unless the caller holds
`organization.delegation.reason.read`.

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure or invalid jurisdiction type |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability |
| 404 | Entity not found |
| 409 | State conflict (overlap, cycle, double end/revoke) |

## Service-only fact endpoint

`GET /orgunits/{id}/covers` is a narrow internal endpoint consumed by the
Authorization service's organization context provider. It performs no
application-layer permission check (avoiding a request cycle with
Authorization) and is protected by the `X-Client-Id` header matching
`Organization:InternalClientId`. Unknown clients are rejected with
`403 Forbidden`.

The `X-Client-Id` header is an **identification/routing signal, not an
authorization credential** (it is a plain, spoofable header). The endpoint is
still behind `[Authorize]`, so a valid bearer token is always required. The
header only tells the Organization service *which* trusted caller is invoking
the fact endpoint; it grants no privileges beyond that already granted by
authentication.
