# Prompt 09G — Non-Blocking Finding Remediation Report

**Date:** 2026-08-19
**Scope:** Remediation of exactly the three NON-BLOCKING findings from the
Prompt 09F final verification report
(`docs/architecture/prompt-09f-final-verification-report.md`). No BLOCKING or
INFORMATIONAL findings were addressed — none of the INFORMATIONAL items exposed
a concrete correctness issue. No new architecture; no `/health` endpoint added.

## G1 — Request cancellation token in the sensitive authorization gate

**Finding:** `WorkflowQueries.cs:129,131` passed `CancellationToken.None` to both
`HasForTaskAsync` checks in `GetWorkflowTaskSensitiveFieldsQueryHandler`,
ignoring request cancellation during the authorization round-trips.

**Fix:** Replaced both `CancellationToken.None` arguments with the handler's
request `ct` (`WorkflowQueries.cs:129,131`). The handler already received `ct`
from the pipeline; no other change. Runtime semantics otherwise unchanged —
denied/missing sensitive reads still surface as 404 (no enumeration oracle).

## G2 — Stale Prompt 09C idempotent-create/409 limitation removed

**Finding:** `docs/runbooks/workflow.md:66-69` still carried the historical
"Known limitations at Prompt 09C" bullet describing the idempotent-create vs
409-troubleshooting discrepancy that was already resolved (09C handler behavior
ratified; 09D F5 / 09E corrected `docs/api/workflow.md` and the runbook
troubleshooting entry).

**Fix:** Deleted that bullet. The remaining "Known limitations at Prompt 09C"
bullet (Docker/Testcontainers compile-only integration suite) is retained — it is
still accurate. The runbook's troubleshooting section (`:213-220`) is now
internally consistent: duplicate create returns the existing open task and 409
is the invalid-transition case.

## G3 — `/health` documentation reconciled to the actual API surface

**Finding:** The Workflow runbook claimed parity "(consistent with the Records
service)" while `docs/runbooks/records.md:161` still documented `GET /health`;
Documents/Knowledge/Community runbooks did the same. No `/health` endpoint
exists in those APIs.

**Verification first (read-only):** grepped all `src/Services/**` for
`MapHealthChecks|/health|AddHealthChecks`. Result: **only Organization**
(`CommunityOS.Organization.API/Extensions/WebApplicationExtensions.cs:24`,
`ServiceCollectionExtensions.cs:54`) and **Authorization**
(`CommunityOS.Authorization.API/Extensions/WebApplicationExtensions.cs:24`,
`ServiceCollectionExtensions.cs:51`) register `GET /health`. Workflow, Records,
Documents, Knowledge and Community expose none.

**Fixes (documentation only — no endpoint added anywhere):**
- `docs/runbooks/workflow.md:171-174` — cross-reference corrected: Workflow
  exposes no dedicated health probe endpoint; Records/Documents/Knowledge/
  Community likewise expose none; only Organization and Authorization register
  `GET /health`.
- `docs/runbooks/records.md:161` — corrected from `Health probe: GET /health` to
  "the Records API exposes no dedicated health probe endpoint (only Organization
  and Authorization register `GET /health`)".
- `docs/runbooks/documents.md:145`, `docs/runbooks/knowledge.md:92`,
  `docs/runbooks/community.md:91` — same correction for the other three APIs
  (verified stale; none of these services registers `/health`).
- `docs/runbooks/organization.md:94` — **unchanged** (accurate: Organization API
  does register `GET /health`).

## Verification

- **Workflow unit/security tests:** `dotnet test tests/Unit/CommunityOS.Workflow.Tests`
  → **57/57 passed**.
- **All active cross-service unit tests:** Authorization 118/118, Community
  106/106, Organization 79/79, Records 73/73, Documents 56/56, Knowledge 49/49,
  Identity 26/26 — all green. (Content/Enrollment/Events/Notifications/Reporting
  contain no test discoverers — pre-existing empty projects.)
- **Full solution build:** `dotnet build CommunityOS.sln` → **0 warnings,
  0 errors**.
- **EF pending-model-change:** `dotnet ef migrations has-pending-model-changes`
  (Workflow) → **no pending model changes** (G1 is a token-passing change; no
  persistence shape change).
- **Git diff / scope review:** exactly six files changed, all intended —
  `src/.../Application/Queries/WorkflowQueries.cs` (G1), `docs/runbooks/workflow.md`
  (G2 + G3), `docs/runbooks/records.md`, `docs/runbooks/documents.md`,
  `docs/runbooks/knowledge.md`, `docs/runbooks/community.md` (G3). No migration,
  configuration, infrastructure, other service source, or test file changed;
  `organization.md` intentionally untouched.

## Environment limitation

Docker/Testcontainers remain unavailable on this machine; the Workflow
integration suite is **compile-only** and its tests were **not executed** and
are **not claimed to have passed**. It must run in CI before production use.

## Result

All three NON-BLOCKING findings remediated with minimal, precisely scoped
changes. Build clean, EF in sync, all active unit suites green.

**Prompt 09G: COMPLETE. Prompt 09H is not begun.**