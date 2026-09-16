# Identity Service API

> **STATUS: RATIFIED IN PART — SELF-SERVICE SESSION MANAGEMENT (Prompt 46),
> MFA ENROLLMENT-COMPLETION OWNERSHIP (Prompt 49), AND SELF-SERVICE MFA
> METHOD REMOVAL (Prompt 52).**
> This document covers the authenticated **self-service session management**
> surface (`/api/v1/me/sessions`), the corrected **MFA enrollment
> completion** ownership contract (`/api/v1/mfa/enroll/complete`), and the
> **self-service MFA method removal** contract (`DELETE /api/v1/mfa/{methodId}`).
> The broader authentication contract (login, tokens, MFA setup) is owned by
> the ratified Identity/Authorization arrangements and is not restated here.

The session-management surface is versioned under `/api/v1/me/sessions`,
requires a valid access token (`[Authorize]`), and is **self-scoped**: the
actor is always the JWT `sub` subject. There is **no client-supplied
`memberId`, `userAccountId`, or token family parameter** anywhere on this
surface, and no organization membership or role inference. The persisted
ownership of the session row is the sole server-side authorization
(fail-closed).

## Sessions — `/me/sessions`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/me/sessions` | List the caller's own sessions (id, device id, created/expires/last-used times, active flag, current flag, device name/platform) |
| POST | `/me/sessions/{sessionId}/revoke` | Revoke a single one of the caller's own sessions; idempotent |
| POST | `/me/sessions/revoke-others` | Revoke every one of the caller's other logical sessions, preserving the current session's family |

`GET /me/sessions` returns only the authenticated actor's sessions. The
`SessionDto` items expose `id`, `deviceId`, `createdOn`, `expiresOn`,
`lastUsedOn` and `isActive`; no refresh-token material, no token-family
metadata, and no other account's sessions.

Starting with the device-metadata release, each item also exposes two
**optional** `deviceName`/`devicePlatform` strings that resolve from the
actor's own owned `Device` rows at read time:

- **Nullable** — a value is present only when the actor's account carries an
  owned device row with a matching `deviceId`. A session whose `deviceId` does
  not resolve, or whose device row carries no name/platform, serializes `null`
  for the corresponding field. Absent fields are never fabricated server-side.
- **Self-scoped** — resolution can only ever use the actor's own `Device`
  rows; a `deviceId` referencing another account's device yields `null`. This
  is display metadata derived from already-encrypted cross-service device
  records; it is **not** an IP address, user-agent string, or network/geographic
  identifier.
- **Read-only** — the fields change nothing about authorization, revocation,
  or the route contract; the response shape for older responses with the
  fields absent remains valid (client treats them as nullable).

Starting with the current-session identification release, each `SessionDto`
item also includes a non-nullable **`isCurrent`** boolean that indicates
whether the server considers that row the currently-active session of the
authenticated caller:

- **Server-authoritative** — `isCurrent` is derived from the signed `sid`
  claim on the caller's access token. The claim carries the issuing session's
  logical token-family id; a row is marked current when its own
  `TokenFamilyId` matches the claimed value. The claim is signed and
  immutable; no client-supplied parameter influences the result.
- **Legacy / absent claim** — tokens issued before this feature carry no `sid`
  claim; in this case, `isCurrent` is `false` for every row. A malformed or
  non-GUID `sid` value is treated identically (null correlation → all false).
- **Family-level semantics** — multiple rows in the same token family may all
  be marked `true` simultaneously (e.g., a superseded row that shares the same
  logical session id). The list never collapses families, removes superseded
  rows, or applies a latest-row-only rule; each row is evaluated independently
  within its own family.
- **No token-family exposure** — neither the token-family id nor the `sid`
  claim value is returned in the response; only the derived boolean is
  exposed.
- **No Gateway change** — the existing transparent pass-through of `/me` to
  the Identity service is unchanged.

`POST /me/sessions/{sessionId}/revoke` revokes **exactly one** session row by
its id. Request body: none. The `sessionId` is a server-side id retrieved from
the caller's own session list — the caller cannot supply a different account's
id because the server discards any non-owned value.

Semantics:

- **Self-scoping (no oracle)** — if the session does not exist, **or** belongs
  to a different account, the server returns `404` — the same response for both
  cases. Cross-account existence cannot be probed through this endpoint.
- **Idempotent** — revoking an already-revoked, or expired-but-owned, session
  is a successful no-op (`204`). `Session.Revoke` is idempotent by contract:
  an already-revoked row records the original reason and timestamp unchanged.
- **Scope of effect** — only the single requested row is revoked, never the
  actor's whole token family or account. No `RevokeAllForUser` path is reachable
  through this endpoint.
- **Auth binding** — the actor id is resolved exclusively from the `sub` claim
  of the access token; the endpoint takes no identity parameter.
- **Route constraint** — `{sessionId:guid}`; a non-GUID value is not routable
  and yields `404`.

## Revoke-others — `POST /me/sessions/revoke-others`

`POST /me/sessions/revoke-others` revokes every session row the caller owns
**except** those belonging to the caller's current logical session family.
Request body: **none**. Success returns `204 No Content`.

Authentication and identity derivation:

- Requires a valid access token (`[Authorize]`).
- The current user account is resolved **exclusively** from the JWT `sub`
  claim; the current logical session family is resolved exclusively from the
  signed `sid` claim. No client-supplied `userAccountId`, family id, session id,
  or request body is accepted anywhere on the endpoint.
- A missing or malformed (non-GUID) `sid` claim yields `400 Bad Request`
  (problem-details) and the operation is never dispatched.
- The repository operation is scoped by the authenticated `userAccountId`;
  a missing `UserAccountId` ownership predicate would be a security bug.

Semantics:

- **Family-level revocation** — a logical session family may own many
  historical rows (refresh-token rotation creates a sibling row per rotation).
  Every **non-revoked** row belonging to every other family owned by the user
  is revoked in one operation, including rows that are already expired but
  never revoked.
- **Current family preserved** — no row in the current logical family
  (`TokenFamilyId == sid` value) is modified, including superseded historical
  rows within that family.
- **Other accounts untouched** — rows belonging to another account are never
  selected or modified.
- **No-op safety** — when there are no other non-revoked rows, the operation
  succeeds with `204` and nothing is persisted.
- **Idempotence of rows** — already-revoked rows are never re-recorded;
  `Session.Revoke` is idempotent and preserves the original reason and
  timestamp.
- **Access-token caveat** — revoking another session prevents **future
  refresh** from that session. Already-issued short-lived access tokens from
  those sessions may remain valid until their normal expiration (currently
  15 minutes). No access-token blacklisting, introspection, or session-state
  validation is performed by this feature.
- **No data returned** — the `204` response body is empty. No family ids,
  session ids, token data, or counts of revoked sessions are ever returned.
- **No security event** — this operation emits no security event and introduces
  no security-event policy.

| HTTP status | Meaning |
|-------------|---------|
| 400 | Missing or malformed `sid` claim (validation failure) |
| 401 | Missing / invalid access token |
| 204 | (success) all other logical sessions revoked, current session preserved |

## Error handling

Errors are returned as JSON problem-details. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure (empty route values), or missing/malformed `sid` claim on `revoke-others` |
| 401 | Missing / invalid access token |
| 404 | Session not found **or** not owned by the caller (uniform, no existence oracle); non-GUID session id not routable |
| 204 | (success) session revoked |

## Auth notes

The endpoint is covered by the existing router-facing `/api/v1/me` surface
(the API Gateway maps the `me` segment to the Identity service; no new gateway
segment was introduced). Session rows carry no claim/policy data — revocation
is a row-level `RevokedOn` transition; downstream token validation
(`IsActive = not revoked && not expired`) continues to reject revoked sessions.
Future work (rate limiting, current-session
guards) is deferred by ADR-035 and tracked separately; none is implemented by
this contract.

## MFA enrollment completion — `/api/v1/mfa/enroll/complete`

`POST /mfa/enroll/complete` completes a pending TOTP enrollment. It requires a
valid access token (`[Authorize]`) and is **self-scoped**: the acting user
account is resolved exclusively from the JWT `sub` subject. The request body
carries only the MFA-method identifier and the six-digit verification code.

Request body:

| Field | Type | Description |
|-------|------|-------------|
| `mfaMethodId` | GUID | Identifier of an MFA method enrolled for the caller |
| `code` | string | Six-digit verification code |

Semantics:

- **Authenticated-sub binding** — the account is derived solely from the `sub`
  claim; the endpoint accepts **no** client-supplied `userAccountId`,
  `personId`, `membershipId`, `organizationUnitId`, or any other account
  identity.
- **Ownership scope** — the `mfaMethodId` is a selector within the
  authenticated account's own MFA methods only. A method belonging to another
  account cannot be found or completed from this endpoint.
- **Uniform non-existence (no oracle)** — an unknown method id and a method
  belonging to another account return the same `404`, with identical semantics
  and no information about the target account or method.
- **Invalid code** — a well-formed but incorrect code for the caller's own
  method returns `401` (unchanged).
- **Success** — completing the caller's own pending method returns `204 No
  Content` (unchanged).

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure (missing/empty identifiers or malformed code) |
| 401 | Missing / invalid access token, or invalid verification code |
| 404 | MFA method not found **or** not owned by the caller (uniform, no existence oracle) |
| 204 | (success) enrollment completed |

## MFA method removal — `DELETE /api/v1/mfa/{methodId}`

`DELETE /mfa/{methodId}` removes one of the caller's own verified MFA methods.
It requires a valid access token (`[Authorize]`) and is **self-scoped**: the
acting user account is resolved exclusively from the JWT `sub` subject. The
route carries only the MFA-method identifier; there is no request body and no
client-supplied account identity.

Semantics:

- **Authenticated-sub binding** — the account is derived solely from the `sub`
  claim; the endpoint accepts **no** client-supplied `userAccountId`,
  `personId`, `membershipId`, or any other account identity.
- **Ownership scope** — the `{methodId}` is a selector within the
  authenticated account's own MFA methods only. A method belonging to another
  account cannot be found or removed from this endpoint. There is no global
  MFA-method lookup.
- **Uniform non-existence (no oracle)** — an unknown method id, a method
  belonging to another account, and an already-removed method all return the
  same `404`, with identical semantics and no information about the target
  account or method.
- **Final verified MFA method guard** — a user **must** keep at least one
  verified MFA method. Removing the final verified method returns `409
  Conflict`; the account is unchanged and no sessions are revoked.
- **Session revocation** — a successful removal revokes **all** of the actor's
  sessions via the existing `RevokeAllForUserAsync` path. The Flutter client
  returns control to the centralized session-expiry/re-authentication path.
- **Non-idempotent** — removal is a single-use mutation. A second request for
  the same already-removed method returns `404`.
- **No secret or provisioning URI returned** — the response body is empty on
  success; MFA secrets and provisioning URIs are never exposed after the
  one-time enrollment flow.
- **No step-up authentication in this contract** — a current authenticated
  session is sufficient for this slice. Step-up authentication is a future
  architectural concern and is not part of this endpoint.

| HTTP status | Meaning |
|-------------|---------|
| 401 | Missing / invalid access token |
| 404 | MFA method not found, not owned by the caller, or already removed (uniform, no existence oracle); non-GUID method id not routable |
| 409 | Removal would eliminate the final verified MFA method |
| 204 | (success) MFA method removed and all sessions revoked |