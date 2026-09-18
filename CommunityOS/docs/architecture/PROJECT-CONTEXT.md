# PROJECT-CONTEXT.md — AI-Agent Handoff Record

> This document is a persistent AI-agent handoff record. It must be updated whenever a
> major architectural decision, implementation milestone, validation result, or change
> in next-step plan occurs.

This file is a **continuation/handoff document for future AI-agent sessions**, not
ordinary project documentation. A fresh agent session should be able to open this one
file and reconstruct the current architectural work without relying on past chat
history. Facts below are labeled **[verified this session]**, **[verified previously]**,
or **[unverified — inspect repo]** as appropriate.

---

## 1. Project and repository state

- **Project:** CommunityOS — Bahá'í Community OS Platform.
- **Repository path:** `CommunityOS/` under
  `https://github.com/PaarsahJam/BahaiCommunityOS.git` (remote `origin`).
- **Current branch:** `master` **[verified this session]**.
- **Current commit:** `349bfbb` — `feat: revoke all other sessions`
  **[verified this session]**.
- **Git status (at time of writing)** **[verified this session]**:
  - `M CommunityOS/docs/architecture/ADR.md` (the six applied ADR-036 wording
    fixes, uncommitted).
  - `?? CommunityOS/docs/architecture/PROJECT-CONTEXT.md` (this untracked
    handoff file).
  - No source code is modified. `bin/`, `obj/`, `artifacts/`, and Flutter/Dart
    build outputs are git-ignored.
- **Working-tree change:** the ADR.md modification is **uncommitted** (and
  therefore unpushed). Nothing related to the current ADR work is committed or
  pushed **[verified this session: `git status` shows only the two entries above]**.
- **Push state:** `HEAD == origin/master == 349bfbb` **[verified this session]`,
  so the last feature commit is already on the remote; only the docs work is
  ahead of (and not committed to) the repository.

## 2. Completed feature — `feat: revoke all other sessions`

Recorded at commit `349bfbb` (HEAD/origin/master). Endpoint:

`POST /api/v1/me/sessions/revoke-others`

Behavior **[verified this session against `MeController.cs`, `RevokeOthersCommand.cs`, `SessionRepository.cs`, `docs/api/identity.md`]**:

- **Identity:** actor account derived from the `sub` claim only
  (`GetUserAccountId`); no client-supplied account id accepted.
- **Current token family:** from the **signed `sid`** claim
  (`GetSessionFamilyId`); no client-supplied family id accepted.
- **Scope:** revokes other **non-revoked session rows whose token family differs
  from the caller's `sid`** (`RevokeAllExceptFamilyForUserAsync`), never another
  account.
- **Preserves the current family:** all rows with `TokenFamilyId == sid` are left
  untouched, including superseded historical rows.
- **Success:** returns **HTTP 204 No Content** (empty body) when other sessions
  were revoked; it is also a successful no-op when there is nothing else to
  revoke.
- **Missing/malformed `sid`:** `GetSessionFamilyId()` returns `null` for absent or
  non-GUID `sid`; revoke-others then fails validation → **HTTP 400**. (Revoke-single
  session does not require `sid`.)
- **Access tokens:** this feature **does not invalidate already-issued JWT access
  tokens**; revocation affects the refresh path only.
- **JWT validation:** **unchanged** by this feature; no session-state/`sid`
  lookup was added to validation.

## 3. Validation already completed

Results are labeled by how they were obtained:

| Check | Result | How verified |
|-------|--------|--------------|
| Backend full-solution build | **Not green.** `dotnet build CommunityOS.sln` (Debug, SDK 9.0.318) reported 10 errors in projects **unrelated** to this work: `GatewayForwarder.cs:124` (CS1998), `CorrespondenceConfigurations.cs:28,93` (CS0853 x2), `LocalizationConfigurations.cs:21,96,166,202` (CS0853 x4), `AssistInvocationIntegrationTests.cs:24,33` (CS1998 x2), `FinanceIntegrationEventSecurityTests.cs:86` (CS1998). These look like pre-existing analyzer-hygiene failures (async-without-await / named-argument-in-expression-tree) in files untouched by the current docs-only change. | **[verified this session]** — ran the build; no file related to Identity/revoke-others/ADR failed; `CommunityOS.Identity.API`, `CommunityOS.Identity.Tests`, and `CommunityOS.Identity.IntegrationTests` compiled successfully. |
| Backend unit tests — Identity | **Passed: 122/122** across `CommunityOS.Identity.Tests`. | **[verified this session]** — `dotnet test tests/Unit/CommunityOS.Identity.Tests ... --no-build`. |
| Focused revoke-others unit tests | **Passed: 19/19** (`FullyQualifiedName~RevokeOthers`), covering `RevokeOthersCommandTests` and contract tests. | **[verified this session]** — same project, filtered run. |
| Revoke-others HTTP contract tests | **Passed** — `MeSessionRevokeOthersHttpContractTests` is included in the 19-test focused run. | **[verified this session]** — same filtered run. |
| Flutter analysis (`flutter analyze`) | **Not run / not verified** — no Flutter toolchain (`flutter` or `fvm`) is installed on this machine. | **[unverified]** — inspect on a machine with the Flutter SDK; member-portal lives at `apps/member-portal`. |
| Flutter focused tests | **Not run / not verified** — same reason. | **[unverified]**. |
| Environmental/cache issue | **None encountered this session.** Restore completed from the local NuGet cache without network or cache errors during the build/test runs. No prior cache issue is recorded in this handoff. | **[verified this session]** — build/test ran offline from `~/.nuget/packages` (cache present). |
| Working tree contains only intended changes | **Yes.** Only `docs/architecture/ADR.md` (M) and this untracked `PROJECT-CONTEXT.md` are present; no source code changed. | **[verified this session]** — `git status --porcelain`. |

Note: build/test command equivalents are `dotnet build CommunityOS.sln` and
`dotnet test tests/Unit/CommunityOS.Identity.Tests/CommunityOS.Identity.Tests.csproj`
(run via `~/.dotnet/dotnet` in this environment; `dotnet` is not on PATH by default).

## 4. Authentication and token-lifecycle facts

Verified model at commit `349bfbb` (labels as defined at the top):

- **Access tokens:** self-contained **RS256 JWTs** issued by Identity
  `JwtTokenService` (`RsaSigningKeyProvider.Algorithm = RsaSha256`). **[verified this session]**.
- **Access-token lifetime:** **hard-coded 15 minutes**
  (`AccessTokenLifetime = TimeSpan.FromMinutes(15)`); echoed in login/refresh
  `TokenDto`. **[verified this session]**.
- **JWT claims:** `sub` (user account id), `email`, `jti` (unique per token),
  and `sid` (the issuing session's token-family id). **[verified this session]**.
- **Refresh tokens:** opaque, **64 random bytes** (`RandomNumberGenerator.GetBytes(64)`),
  Base64 serialized, **rotating**. **[verified this session]**.
- **Refresh-token storage:** only a **SHA-256 hash** is persisted on the
  `sessions.refresh_token_hash` column (max 64 chars); raw value never persisted.
  **[verified this session]**.
- **Refresh-token lifetime:** **30 days** (`RefreshTokenLifetime = TimeSpan.FromDays(30)`
  in login, refresh, and OAuth token commands). **[verified this session]**.
- **Token-family semantics:** multiple session rows share one `sid`/family id;
  the family is the logical session. Rotation creates a new row in the same
  family and marks the previous row as used (`Session.Rotate`). **[verified this session]**.
- **Refresh reuse detection:** reusing a rotated token marks the session reused
  (`Session.MarkReused`) and `RefreshSessionCommand` revokes the **whole token
  family** (private `RevokeFamilyAsync` via `GetByFamilyAsync`).
  **[verified this session]**.
- **Single-session revocation:** `POST /me/sessions/{sessionId}/revoke` revokes a
  single owned row, idempotent (`Session.Revoke` no-ops if already revoked), actor
  from `sub`. **[verified this session]**.
- **Revoke-others:** as documented in section 2. **[verified this session]**.
- **Logout:** `LogoutCommand` revokes the single row matching the presented
  refresh-token hash. **[verified this session]**.
- **Password change / reset / MFA removal:** account-wide refresh invalidation via
  `RevokeAllForUserAsync` (ChangePassword, ResetPassword, RemoveMfa). **[verified this session]**.
- **Per-service JWT validation:** each protected service runs its own `JwtBearer`
  validation (signature/issuer/audience/lifetime). Service inventory **[verified this session]**:
  **15 services call `AddJwtBearer`**: AI, Audit, Authorization, Community,
  Correspondence, Documents, Finance, Identity, Knowledge, Localization,
  Notifications, Organization, Records, Search, Workflow.
  (Content, Enrollment, Events, Reporting exist but are not yet token consumers.)
- **Session/revocation consulted during validation?:** **No.** No service performs
  a session-state, `sid`, family, or revocation lookup during token validation.
  Identity sets `ValidateTokenReplay = true` but registers no `TokenReplayCache`,
  so replay validation is inert. **[verified this session]**.
  (Note: **not all** services assert subject presence in a validation handler; only
  some (e.g., AI, Audit, Correspondence, Localization, Search) add an
  `OnTokenValidated` subject check. The durable fact is the absence of any
  session/revocation lookup.) **[verified this session]**.
- **API Gateway:** transparent forwarder (ADR-035), preserves the caller's
  `Authorization` header verbatim, strips hop-by-hop and internal-identity
  headers, does not authenticate/authorize, introspect, or call Identity session
  endpoints. **[verified this session against `GatewayForwarder.cs`]**.
- **Redis:** present in `docker-compose.yml` (`redis:7-alpine`) and package
  references (`StackExchange.Redis` 2.8.16; `Microsoft.Extensions.Caching.StackExchangeRedis`
  9.0.0, referenced by `CommunityOS.Infrastructure.Common`), but **no C# code uses
  a Redis client**. Redis is not an active mechanism. **[verified this session]**.
- **Residual access-token validity:** after any revocation, a revoked session's
  already-issued access token remains accepted until its JWT (15-minute) expiry.
  Revocation affects the refresh path only. **[verified this session]**.
- **Session rows are never hard-deleted**; superseded rotated rows persist and
  remain listed (session list never collapses families). **[verified this session]**.

## 5. Critical architectural distinction

> **Refresh-token revocation is not the same as invalidation of already-issued
> access tokens.**

- Revocation today stops **future refresh operations** for the affected session
  rows/families (immediate refresh revocation).
- It does **not** invalidate access tokens already handed out; those remain
  usable until JWT expiry.

| Term | Meaning |
|------|---------|
| **Session row** | A single `sessions` DB row (one refresh-token record). |
| **Logical token family (`sid`)** | The group of rows sharing a `sid`; the logical session. |
| **Single-session revocation** | Revokes one session row (self-service endpoint). |
| **Revoke-all-other-sessions** | Revokes every non-revoked row/family except the caller's current `sid` family. |
| **Account-wide security invalidation** | Invalidates every family/session of the account (password change/reset, MFA removal). |

## 6. ADR-036 current status

- **File:** `docs/architecture/ADR.md` (ADR-036 near end of file).
- **Title:** **ADR-036 — Session Revocation and Access-Token Validity Model**.
- **Status:** **Proposed** (not ratified).
- **Decision:** **Not yet decided** — no architectural option selected, ranked,
  scored, approved, or ratified.
- **Implementation:** **None**.

History:

1. ADR-036 was drafted (documents the current model and alternatives for
   project-owner review).
2. It underwent an architecture review of the actual text (cross-checked against
   the repository).
3. Six review findings were identified.
4. **All six corrections have been applied to `docs/architecture/ADR.md` and
   verified** — this is no longer pending; the ADR was re-read and validated
   (terminology consistent, decision-neutral, status still Proposed).
5. No implementation has been performed; nothing has been committed or pushed.

The six completed corrections:

1. **Session-row vs token-family clarification** — added a "Row vs family" note
   under the Section 6 scope table (single-session endpoint revokes one row, not
   the whole family).
2. **Corrected repository/family-level revocation description** — Section 3 now
   covers `RevokeAllForUserAsync`, `RevokeAllExceptFamilyForUserAsync`, and the
   refresh handler's private family-revocation path (reuse detection).
3. **Removed wording implying approval** — Section 16 uses "continues to apply" /
   "owner-approved" instead of "accepted posture" / "sanctioned".
4. **Removed steering wording** — Section 12 no longer says "or, more likely, be
   enforced at each service's own validation boundary"; the Gateway-vs-service
   question stays open.
5. **Aligned Option C terminology** — Section 10 table reads "Near-immediate or
   immediate (propagation-dependent)" to match the Option C prose.
6. **Consistent defined term** — Section 13 ties "refresh revocation is immediate"
   to the Section 4 defined term.

Follow-up alignment applied during the final consistency review (ADR-036 §2):
the service inventory now includes **Knowledge** (15 services), and the
validation-handler description was aligned with the verified facts — not all
services assert a subject claim; only some add an `OnTokenValidated` check.

## 7. Architectural options under consideration

Presented for project-owner review — **not recommended or ranked**:

- **A.** Retain the current refresh-only revocation model.
- **B.** Shorten access-token lifetime.
- **C.** Add shared revocation state (e.g., Redis) checked by each service.
- **D.** Use an account/session epoch or token-version mechanism with shared state.
- **E.** Perform per-request Identity/database/introspection checks.
- **F.** Rotate signing keys.

These remain **alternatives** until the project owner decides.

## 8. Defined terminology (preserve these; do not weaken or replace)

- **Immediate refresh revocation:** the next refresh attempt is rejected after the
  revocation transaction commits. This is the behavior of the current model for
  every revocation operation.
- **Near-immediate access revocation:** access-token rejection occurs after
  revocation state propagates or becomes available through the revocation store or
  cache; a bounded propagation window may still accept a token.
- **Immediate access revocation:** previously issued access tokens are no longer
  accepted after the authoritative revocation decision, subject to stated
  propagation and failure guarantees ("immediate" relative to the authoritative
  decision record).

A design that provides one does not automatically provide another.

## 9. Remaining project-owner decisions

These are **project-owner architectural decisions**; the AI agent must not decide
them unilaterally:

1. Is the residual access-token validity window (up to 15 minutes for already-issued
   tokens of a revoked session) acceptable?
2. How deep should account-wide security events (password change/reset, MFA removal)
   invalidate sessions — refresh-only, or also already-issued access tokens?
3. What revocation granularity is required — session/family-granular,
   account-granular, or both?
4. What shared store/propagation mechanism, if any, should be used?
5. What should happen when revocation state is unavailable — **fail-open or
   fail-closed** (and under what outage/incident conditions)?
6. What rollout/feature-flag policy is acceptable before any mechanism is enabled
   in production?
7. Should the API Gateway role change (centralized revocation enforcement, amending
   ADR-035), or should enforcement remain at each service's validation boundary?

## 10. Prohibited work before architectural approval

Until ADR-036 is approved, do **not**:

- Implement Redis revocation.
- Implement token introspection.
- Implement access-token blacklists.
- Implement token epochs/versioning.
- Change JWT validation.
- Shorten the JWT lifetime solely to resolve this unresolved decision.
- Change API Gateway authentication responsibilities.
- Add migrations, packages, endpoints, or configuration for an unapproved approach.
- Commit or push implementation changes without explicit instruction.

## 11. Recommended continuation plan

1. Preserve project context in `PROJECT-CONTEXT.md` (this document).
2. Treat ADR-036 as the current decision document (Proposed, not yet decided).
3. Obtain project-owner decisions on the Section 9 questions.
4. Create a detailed implementation plan **only after** an approach is approved.
5. Implement the approved design in small, auditable stages with tests.
6. Validate backend, service-level security behavior, Flutter compatibility, and
   failure modes.
7. Address session-list semantics, superseded/expired rows, and cleanup as a
   **separate concern** unless the approved design explicitly combines them.

## 12. Instructions for a fresh AI-agent session

1. Read `docs/architecture/PROJECT-CONTEXT.md` first.
2. Read `docs/architecture/ADR.md` and locate **ADR-036**.
3. Check Git branch, status, and current commit (`git status`, `git log --oneline -5`).
4. Inspect the actual repository before relying on assumptions.
5. Continue from the documented state (do not restart the analysis).
6. Treat ADR-036 as **Proposed** unless a later explicit decision record says otherwise.
7. **Never implement an architectural option without project-owner approval.**
8. Keep architectural analysis separate from implementation.
9. Update this handoff document after major milestones, decisions, validation
   results, or changes in the next-step plan.

---

> This document is a persistent AI-agent handoff record. It must be updated whenever a
> major architectural decision, implementation milestone, validation result, or change
> in next-step plan occurs.