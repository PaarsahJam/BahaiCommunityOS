# Identity Service API

> **STATUS: RATIFIED IN PART — SELF-SERVICE SESSION MANAGEMENT (Prompt 46).**
> This document covers the authenticated **self-service session management**
> surface of the Identity service (`/api/v1/me/sessions`). The broader
> authentication contract (login, tokens, MFA) is owned by the ratified
> Identity/Authorization arrangements and is not restated here.

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
| GET | `/me/sessions` | List the caller's own sessions (id, device id, created/expires/last-used times, active flag) |
| POST | `/me/sessions/{sessionId}/revoke` | Revoke a single one of the caller's own sessions; idempotent |

`GET /me/sessions` returns only the authenticated actor's sessions. The
`SessionDto` items expose `id`, `deviceId`, `createdOn`, `expiresOn`,
`lastUsedOn` and `isActive`; no refresh-token material, no token-family
metadata, and no other account's sessions.

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
Future work (MFA enrollment ownership binding, rate limiting, current-session
guards) is deferred by ADR-035 and tracked separately; none is implemented by
this contract.