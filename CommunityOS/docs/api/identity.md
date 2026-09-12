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
| GET | `/me/sessions` | List the caller's own sessions (id, device id, created/expires/last-used times, active flag, device name/platform) |
| POST | `/me/sessions/{sessionId}/revoke` | Revoke a single one of the caller's own sessions; idempotent |

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

## Error handling

Errors are returned as JSON problem-details. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure (empty route values) |
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