# Finance Service

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 16U).** ADR-032 (Accepted at Prompt
> 16T) ratified Finance as a dedicated bounded context and the financial-truth
> boundary. The service has been implemented in Prompt 16U (Domain, Application,
> Infrastructure, API, permissions, EF migration, Database, unit tests, and
> compile-only integration tests). Decisions are binding unless a later
> ratified ADR amends them.

Finance (Slot 17 in the ratified implementation sequence, ADR-017) owns the
community's **monetary facts**: organization-scoped **funds**, an **immutable,
append-only ledger** of contributions / expenses / transfers, and **derived
balances** computed from the ledger stream — never stored as authoritative
state. It enforces the financial invariants no other context owns: append-only
ledger entries, separation of duties (recorder ≠ approver), and financial
confidentiality (amounts and attribution never appear in events, logs or audit
exports).

It is not — and must never become — the owner of document artifacts (ADR-022),
official records (ADR-023), the compliance/audit journal (ADR-027), approval
task orchestration (ADR-024), person/identity facts, or the organizational
hierarchy (ADR-016). Financial **approval authority** is an explicit human
domain decision; **AI never authorizes financial transactions** (ADR-030).

## What Finance owns (and does not)

| Concept | Owned by | Relationship to Finance |
|---------|----------|-------------------------|
| **Fund** | Finance | Organization-scoped financial account, `Active → Closed` lifecycle. |
| **FinancialTransaction** | Finance | Append-only ledger entry (Contribution / Expense / Disbursement / Reimbursement / Transfer). |
| **Balance** | Finance | Derived read projection over the approved transaction stream; never stored. |
| **Financial approval authority** | Finance | Approve/reject is a guarded human decision with a dedicated capability. |
| **Budget** | Finance | *Conceptually owned; implementation deferred to a later gate.* |
| **Document artifacts** | Documents | Finance references via `DocumentReference` (reserved `finance.*` SourceContext) only; no binary storage. |
| **Official records** | Records | Records never attaches financial meaning; Finance references verified records later (future). |
| **Compliance/audit journal** | Audit | Finance owns its ledger; Audit owns the compliance journal. Finance raises no shadow audit trail. |
| **Approval task orchestration** | Workflow | Task state stays in Workflow (future); financial approval authority stays in Finance. |
| **Person/identity facts** | Community/Identity | Finance stores stable ids only; names resolve through the Community API at read time. |
| **Organizational hierarchy** | Organization | ADR-016 read-model projection; no second hierarchy. |
| **Cross-domain analytics** | Analytics (future) | Finance owns finance-scoped reporting only; no enterprise aggregation. |
| **AI decisions** | AI Platform | Advisory only; hard-prohibited from financial authority (ADR-030). |
| **Payment-provider adapters** | Finance | Seam owned (ADR-031); no provider selected or activated at this gate. |

## Model

### Fund (aggregate root)

- **Identity** — `Guid Id`, platform-generated, referenced by every other
  context as an id-typed reference (no cross-context FK, ADR-018).
- **Organization scope** — `OrganizationUnitId` + cached
  `OrganizationUnitDisplayName` from the read-model projection (ADR-016); a
  fund belongs to exactly one unit.
- **Name** — display name, max 200 chars, required.
- **Currency** — the fund's currency, uppercase ISO-4217 three-letter code,
  immutable per fund. Every ledger entry on the fund must match it
  (`TransactionCurrencyMismatchException`).
- **Status** — `Active → Closed` (soft close). A closed fund never accepts new
  ledger entries but its history and derived balance remain readable.
- **Lifecycle provenance** — `CreatedOn` / `UpdatedOn`, optimistic concurrency
  token `Revision`.
- **Balance** — never stored; computed from the approved transaction stream on
  read (`LedgerBalanceCalculator`).

### FinancialTransaction (aggregate root)

Immutable, append-only ledger entry. **No in-place mutation ever**; corrections
append a reversal entry in a future gate.

- **Identity** — `Guid Id`.
- **Fund / scope** — `FundId` (the owning fund), plus snapped
  `OrganizationUnitId` / `OrganizationUnitDisplayName` of the owning fund at
  record time.
- **Amount** — positive monetary value (`Money`, minor units + uppercase
  ISO-4217 code). Direction is derived from the type.
- **Type (closed vocabulary)** — `contribution` (inflow) | `expense` |
  `reimbursement` | `disbursement` (outflow) | `transfer` (outflow). A transfer
  **requires** a `TransferDestinationFundId` that differs from the source fund
  (`TransferReferenceRequiredException` / `InvalidTransferReferenceException`).
- **Status** — `Recorded → PendingApproval → Approved | Rejected`. Recorded is
  the terminal-eligible baseline; `Submit` moves to `PendingApproval`;
  `Approve`/`Reject` require a subject **different from the recorder**
  (`TransactionApprovalConflictException`) and raise no integration events.
- **Provenance** — `RecordedBy` / `OccurredOn` (required), `SubmittedBy` /
  `SubmittedOn`, `ApprovedBy` / `ApprovedOn`, `RejectedBy` / `RejectedOn`,
  optional `RejectionReason`.
- **Description** — optional free text (max 500); it **never** leaves Finance
  through events.

### Money (value object)

Positive minor units + uppercase three-letter ISO-4217 code (`Money.Create`);
invalid codes are rejected at the boundary (`InvalidCurrencyCodeException`),
both in the API validators and in the domain.

### LedgerBalanceCalculator

- Balance = sum over **approved** entries of `+MinorUnits` for inflows and
  `−MinorUnits` for outflows, evaluated only for entries whose `FundId == fund`
  (or transfer destination). Contributions/expenses/reimbursements/disbursements
  count on the **source fund**; transfers count as an outflow on the source fund
  and an inflow on the destination fund (via the destination reference).
- Non-approved statuses (Recorded, PendingApproval, Rejected) contribute
  nothing.
- The ledger is closed per fund: an entry can never influence a fund's balance
  through any cross-context path — the calculator is bounded strictly by
  `FundId` and transfer destination.

### OrganizationUnitReference (read model)

Finance mirrors the Community/Knowledge/Documents/Records pattern: it consumes
`OrganizationUnitCreated` and `OrganizationUnitUpdated` into
`organization_unit_references` (ADR-016) so fund scoping never depends on a live
Organization query. The projection is intentionally flat (unit id + current
display name) and stores no parent/hierarchy, so `OrganizationUnitParentChanged`
is **not** consumed — display names are carried by `Created`/`Updated` only
(see Deviations). It never introduces a second hierarchy and never influences
authorization decisions (authorization is resolved live by the Authorization
service).

## Key rules

- **The ledger is append-only.** Financial transactions are never mutated or
  hard-deleted through normal operations; there is no destructive purge
  (ADR-032 OQ-5). A closed fund keeps its history.
- **The balance is derived, never stored.** Every read computes the approved
  balance from the entry stream; there is no authoritative balance column.
- **A fund has one currency.** Every entry denomination must match the fund's
  currency or the entry is rejected.
- **A transfer requires a distinct destination fund**; a transfer never
  force-dangles — recording one without a destination is rejected.
- **Separation of duties.** The subject who records a transaction cannot be the
  subject who approves or rejects it (mirrors ADR-023). Approval/decision
  authority is never delegated to AI (ADR-030).
- **Approvals carry no event.** `Approve`/`Reject` change status without
  raising integration events; only recording emits the outbox event.
- **Closed funds stop the ledger.** New entries on a closed fund are rejected;
  reads remain available.
- **Unauthorized enumeration is prevented.** List reads return only funds the
  caller may read (fail-closed batch filtering at the query boundary);
  `GET /funds/{id}` and `GET /transactions/{id}` return `404` for both *missing*
  and *not readable* resources — no existence oracle.
- **Read-denial is indistinguishable from not-found.** Denied confirmed reads
  surface as `FundNotFoundException` / `FinancialTransactionNotFoundException`
  (`404`), never `403` plus existence leakage.
- **Unknown status filters are fail-soft.** `GET /funds/{id}/transactions?status=`
  with an unrecognized status never errors. *Implementation note:* the current
  behavior passes a null filter (all entries for the fund are returned); the
  `ResolveStatus` doc-comment ("match nothing") is reconciled below
  (Prompt 16U deviations).

## Privacy and permissions

Finance executes every guarded operation through the Authorization service
`AuthorizationGuard` bound to the shared `HttpAuthorizationEvaluator` (ADR-031)
— fail-closed (ADR-010). No `[Authorize(Roles = "...")]`, no local RBAC, no
direct Authorization database access. Resource-level authorization is
mandatory: every guarded operation evaluates against the owning fund's
context `(OrganizationUnitId, "fund", FundId)`; transactions are evaluated at
the context of the fund that holds them.

### Permission matrix (ratified, ADR-032)

| Permission | Purpose | Read/write | Notes |
|------------|---------|-----------|-------|
| `finance.fund.read` | View fund metadata and derived balances | read | required for `GET /funds` and `GET /funds/{id}` |
| `finance.fund.manage` | Create/close fund accounts | write | does NOT imply transaction read/record |
| `finance.transaction.read` | View ledger entries (non-confidential fields) | read | also required for `GET /funds/{id}/balance` |
| `finance.transaction.record` | Record a contribution/expense/transfer; submit for approval | write | does NOT imply approve |
| `finance.transaction.approve` | Approve/reject a transaction (human authority) | write (decision) | does NOT imply record |
| `finance.transaction.admin` | Administrative override for correction/restoration | write (admin) | reserved; does NOT imply read/record |

Deferred (defined in ADR-032; **not registered**):
`finance.transaction.read.anonymous` (confidential contributor attribution),
`finance.budget.read`/`finance.budget.manage`, `finance.report.read`.

Seeding follows ADR-011 scope semantics: the six permissions are registered in
`PermissionCatalog` and seeded to both `GlobalAdministrator` and
`NationalAdministrator` in the development seed. A grant at a national scope
covers descendant units through Authorization hierarchy resolution; a global
grant applies only to checks with no organization context. Administration never
implies operational access; approval never implies recording.

### Metadata exposure

| Surface | What may appear |
|---------|-----------------|
| **API responses** | Fund metadata, currency, status, derived balance (minor units + currency code); ledger entry metadata, type, direction, status, stable ids, provenance. Attribution is **not** stored in this gate (ADR-032 OQ-2). |
| **Logs** | Fund/transaction ids, action, actor, outcome. **Never** amounts, payee details, descriptions, secrets or names. |
| **Integration events** | Stable ids + lifecycle metadata only (see contract). **Never** amounts, currency, descriptions, attribution. |
| **Audit** | Finance integration events enter the Audit catalog only through the recorded ADR-027 amendment path at a future gate. |
| **Search / AI** | Not consumed in this gate; any future subscription is confidentiality-constrained and AI is advisory only (ADR-030). |

## HTTP API

All endpoints are versioned under `/api/v1/funds` and `/api/v1/transactions`,
require a valid access token, and return DTOs — **EF entities are never
exposed**. See `docs/api/finance.md` for the full endpoint reference, request
bodies and error mapping.

## Integration

- **Events** — domain events are forwarded through the **transactional outbox**
  (MassTransit EF Core outbox, ADR-015) committed **atomically with the ledger
  row** — the Finance outbox is enabled at this gate because the first-gate
  consumer (Organization unit events) requires guaranteed delivery (ADR-032
  eventing posture). In-process, the `FinanceTransactionRecorded` domain event
  is published before the single DB transaction commits via the open-generic
  `DomainEventPublisher`; the forwarded integration event and the ledger entry
  commit together.
- **Organization** — Finance consumes `OrganizationUnitCreated/Updated`
  (flat projection; `OrganizationUnitParentChanged` is not consumed — see
  Deviations) into `organization_unit_references` (ADR-016 pattern); it never
  reads the Organization database.
- **Authorization** — Finance never reads the Authorization database;
  `AuthorizationGuard` is bound to the shared `HttpAuthorizationEvaluator`
  (ADR-031, service-principal, ADR-018/019).
- **Future consumers (subscribe only after the outbox gate)** — Audit (via the
  recorded ADR-027 amendment path), Notifications (approval alerts), Search
  (metadata indexing), Workflow (approval-task reconciliation). No consumer
  beyond Organization is ratified at this gate.

### Integration event (`FinanceTransactionRecorded`)

The sole baseline event, raised when a ledger entry is **recorded** (status
`Recorded`), and published through the outbox. Payload boundary mirrors Records
and ADR-032: stable ids and lifecycle metadata only.

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `FinanceTransactionRecorded` | A transaction is recorded | `TransactionId`, `FundId`, `TransactionType`, `Direction`, `Status`, `RecordedBy`, `OccurredOn` |

Deliberately **not exported**: amounts/currency, descriptions, payee details,
contributor attribution, rejection reasons, and names. `Approve`/`Reject`
status transitions raise **no** event. `FundCreated`/`FundClosed` are future
baseline events (deferred).

## Dependency map

```
Organization ──►  Finance  ──►  (future consumers: Audit / Notifications /
Authorization ─┘    ▲  │           Search / Workflow)
Identity (authN)    │  └── Outbox-protected ──►  RabbitMQ (future consumers)
                    │  ◄── consumes OrganizationUnitCreated/Updated
                    │      (read-model projection, ADR-016; not ParentChanged)
                    └── never reads Organization/Authorization databases
```

- **Depends on (existing):** Identity (JWT), Authorization (AuthorizationGuard
  over the check API), Organization (unit events → scope references),
  Shared Integration Infrastructure (`HttpAuthorizationEvaluator`, ADR-031).
- **Does not depend on (yet):** Community, Documents, Records, Audit, Workflow,
  Notifications, Search, AI. Finance is a leaf service today — its consumers
  are future.
- **Provides for (future):** Audit (compliance journal via ADR-027 amendment),
  Notifications (approval alerts), Search (metadata indexing), Workflow
  (approval-task reconciliation), Analytics (cross-domain aggregation),
  AI Platform (non-authoritative suggestions only, ADR-030-gated).

## Data

- Database: `communityos_finance` (PostgreSQL; `docker/init/01-create-databases.sql`),
  schema `finance`.
- Tables (as migrated, Prompt 16U): `funds`, `transactions`,
  `organization_unit_references`, plus the MassTransit outbox tables
  (`InboxState`, `OutboxMessage`, `OutboxState`) committed atomically with the
  ledger (`__EFMigrationsHistory` managing the schema).
- `transactions` is append-only; the ledger relates to `funds` with
  `DeleteBehavior.Restrict` (nothing cascade-deletes treasure).
- Schema managed by EF Core migrations (initial migration
  `InitialCreateFinance`, Prompt 16U).

## Storage

- **Metadata storage** — PostgreSQL (`communityos_finance`). Authoritative.
- **No binary storage** — Finance never stores file bytes; document evidence is
  referenced only (reserved `DocumentReference` seam, future).
- **Backup / restore / DR** — PostgreSQL dump/PITR; the ledger is the
  authoritative financial record and must be protected with the same posture as
  Audit (compliance journal).

## Configuration (ratified)

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `FinanceDb` | `Host=localhost;Port=5432;Database=communityos_finance;...` | PostgreSQL connection string |
| `Jwt` | `Issuer` / `Audience` / `MetadataAddress` / `RequireHttpsMetadata` | *(local)* | Token validation (RS256-only validator) |
| `RabbitMq` | `Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Message bus |
| `AuthorizationService` | `BaseUrl` / `AccessToken` / `ClientId` | `communityos-finance` | Authorization check API configuration |
| `Finance` | `InternalClientId` | `communityos-authorization` | Trusted service-principal identifier |

The Finance service never reads the Authorization or Organization databases. If
`AuthorizationService:BaseUrl` or the presented token is misconfigured, every
guarded endpoint returns `403 Forbidden` (fail-closed).

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Finance.Tests`, 77 tests) — domain
  invariants (money/fund/transaction invariants, lifecycle transitions,
  closed-fund behavior, derived balance, cross-fund closure) and security
  regression tests (fail-closed authorization, denied-command-persists-nothing,
  unreachable-evaluator fail-closed, read-denial as not-found, fail-closed list
  filtering, unknown-status fail-soft passthrough, permission catalog
  alignment, integration-event payload boundary, JWT RS256-only validation,
  log-template token analysis) through the MediatR pipeline with substitute
  persistence.
- **Integration tests** (`tests/Integration/CommunityOS.Finance.IntegrationTests`)
  — EF model mapping assertions built **offline** (no Docker): `finance` schema
  default, fund/transaction/unit table/column/conversion/delete mappings, and
  the MassTransit outbox registration.

## Deviations

Two ratified implementation choices are recorded here and are binding unless a
later ratified ADR amends them:

1. **Unknown `status` filters are currently fail-soft to "no filter".** The
   ADR-032 doc-comment on `ResolveStatus` describes "match nothing"; the
   implemented behavior passes a null filter through to the repository so all
   fund entries are returned. The behavior is locked by a regression test.
   Reconcile the doc-comment and the reported behavior in a future gate
   (prefer the implemented fail-soft read semantics).
2. **`GET /funds/{id}/balance` requires `finance.transaction.read`** (ledger
   read), while `GET /funds/{id}` requires `finance.fund.read`. This honors
   ADR-032's "derived balance reads" pairing with the ledger without making one
   capability imply another.
3. The `FluentValidation` guards and the domain reject non-uppercase currency
   codes at entry; the domain `ToUpperInvariant` normalization is defensive only
   (uppercase is enforced, not normalized, at the boundary).
4. **The Organization read-model consumes `OrganizationUnitCreated` and
   `OrganizationUnitUpdated` only.** ADR-032 lists the unit-event stream as
   `Created/Updated/ParentChanged`; because the projection is flat (unit id +
   current display name, no parent/hierarchy, display names carried by
   `Created`/`Updated`), `OrganizationUnitParentChanged` is deliberately not
   consumed and no `IConsumer<OrganizationUnitParentChanged>` exists. Recorded
   at the Prompt 16V closure verification to correct a claim of functionality
   that did not exist.