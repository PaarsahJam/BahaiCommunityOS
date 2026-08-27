# Finance Service API

> **STATUS: RATIFIED AND IMPLEMENTED (Prompt 16U).** Contract for the Finance
> service, aligned with ratified ADR-032 and `docs/finance.md`. Implemented in
> Prompt 16U (Domain, Application, Infrastructure, API, EF migration, tests).

All endpoints are versioned (`/api/v1`), require a valid access token
(`[Authorize]`), and return DTOs — **EF entities are never exposed**. Every
guarded operation is evaluated against the Authorization service at the owning
**fund** context `(OrganizationUnitId, "fund", FundId)` (fail-closed; denied
reads are indistinguishable from not-found). Amounts are denominated in **minor
units** (smallest currency unit) with an uppercase ISO-4217 currency code; the
ledger is append-only and balances are derived on read.

## Funds — `/funds`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/funds?organizationUnitId=` | `finance.fund.read` | List accessible funds, filtered fail-closed to readable funds, with derived balances |
| GET | `/funds/{fundId}` | `finance.fund.read` | Get fund metadata (status, currency, revision) with the derived approved balance |
| GET | `/funds/{fundId}/balance` | `finance.transaction.read` | Get the derived approved balance (currency + minor units) |
| POST | `/funds` | `finance.fund.manage` | Create an active fund scoped to an organization unit |
| POST | `/funds/{fundId}/close` | `finance.fund.manage` | Close the fund (soft); a closed fund accepts no new ledger entries |

**Create** body:

```json
{
  "organizationUnitId": "00000000-0000-0000-0000-000000000001",
  "name": "Sacred Fund",
  "currency": "USD"
}
```

`currency` must be an uppercase ISO-4217 three-letter code and is immutable per
fund. The requested `organizationUnitId` must exist in the read-model
references (`InvalidFundScopeException`, `400`) — a fund is never created on an
unknown unit.

**Create response** (`201 Created` with `Location: /api/v1/funds/{id}`):

```json
{
  "id": "00000000-0000-0000-0000-00000000000a",
  "organizationUnitId": "00000000-0000-0000-0000-000000000001",
  "organizationUnitDisplayName": "House of Justice",
  "name": "Sacred Fund",
  "currency": "USD",
  "status": "active",
  "revision": 1,
  "balanceMinorUnits": 0,
  "createdOn": "2026-08-27T10:00:00Z",
  "updatedOn": "2026-08-27T10:00:00Z"
}
```

**List** returns `FundSummaryDto[]` (id, unit scope + display name, name,
currency, status, derived `balanceMinorUnits`, `updatedOn`) ordered by
`updatedOn` descending. A caller never sees the existence or count of funds it
cannot read (fail-closed batch filtering).

**Close** marks the fund `closed`. Closing a fund with a non-zero balance is
**not** blocked in this gate (the derived history remains readable); a closed
fund rejects new `Record` calls with `409`.

## Transactions — `/transactions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/transactions/{transactionId}` | `finance.transaction.read` | Get a ledger entry (404 for missing **and** unreadable) |
| POST | `/transactions/{transactionId}/submit` | `finance.transaction.record` | `Recorded → PendingApproval` |
| POST | `/transactions/{transactionId}/approve` | `finance.transaction.approve` | `PendingApproval → Approved` (approver must differ from recorder) |
| POST | `/transactions/{transactionId}/reject` | `finance.transaction.approve` | `PendingApproval → Rejected` (rejecter must differ from recorder) |

## Fund transactions — `/funds/{fundId}/transactions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/funds/{fundId}/transactions?status=` | `finance.transaction.read` | List ledger entries for a fund (optional status filter), newest first |
| POST | `/funds/{fundId}/transactions` | `finance.transaction.record` | Record a contribution / expense / reimbursement / disbursement / transfer |

**Record** body:

```json
{
  "type": "contribution",
  "currency": "USD",
  "minorUnits": 10000,
  "description": "Monthly donation",
  "transferDestinationFundId": null
}
```

- `type` is a closed vocabulary: `contribution` (inflow) | `expense` |
  `reimbursement` | `disbursement` (outflow) | `transfer` (outflow).
- `minorUnits` must be positive; `currency` must be uppercase ISO-4217 and must
  match the fund's currency.
- `transferDestinationFundId` is **required for `transfer`** and must differ
  from the source fund.
- A closed fund rejects recording (`409`).
- The fund the transaction is recorded against must be readable at its scope:
  the recorder holds `finance.transaction.record` at the **fund's**
  organization unit.

**Record response** (`201 Created`) — full transaction DTO:

```json
{
  "id": "00000000-0000-0000-0000-00000000000b",
  "fundId": "00000000-0000-0000-0000-00000000000a",
  "organizationUnitId": "00000000-0000-0000-0000-000000000001",
  "organizationUnitDisplayName": "House of Justice",
  "amount": { "currency": "USD", "minorUnits": 10000 },
  "type": "contribution",
  "direction": "inflow",
  "status": "recorded",
  "description": "Monthly donation",
  "transferDestinationFundId": null,
  "recordedBy": "00000000-0000-0000-0000-0000000000aa",
  "occurredOn": "2026-08-27T10:05:00Z",
  "submittedBy": null,
  "submittedOn": null,
  "approvedBy": null,
  "approvedOn": null,
  "rejectedBy": null,
  "rejectedOn": null,
  "rejectionReason": null
}
```

**List** — `?status=` is case-insensitive over the status vocabulary
(`recorded`, `pendingApproval`, `approved`, `rejected`). An unrecognized status
value is fail-soft and never errors (see `docs/finance.md` deviations). Reads
require `finance.transaction.read` at the fund's scope; a denied read returns
`404`.

**Submit / Approve / Reject** — each requires its own capability; approval and
rejection additionally require the actor to **differ from `recordedBy`**
(`409` otherwise). The lifecycle is `Recorded → PendingApproval → Approved |
Rejected`; an out-of-order transition returns `409`. Approve/Reject raise no
integration events.

**Reject** body:

```json
{
  "reason": "Duplicate entry — recorded twice"
}
```

`reason` is optional (max 500 chars), stored locally, and **never exported** in
events or logs.

## DTOs (conceptual)

- `FundSummaryDto` — id, unit scope + display name, name, currency, status,
  derived `balanceMinorUnits`, `updatedOn`.
- `FundDto` — summary fields + `revision`, `createdOn`.
- `FundBalanceDto` — `currency`, `balanceMinorUnits`.
- `MoneyDto` — `currency`, `minorUnits`.
- `FinancialTransactionDto` — full ledger entry (see response above).
- Request records — `CreateFundRequest`, `RecordTransactionRequest`,
  `RejectTransactionRequest`.

## Error handling

Errors are returned as problem-details JSON. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure; unknown unit scope; invalid/inconsistent currency; unknown transaction type; transfer without destination or to the same fund; non-positive amount; invalid rejection reason |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Fund or transaction not found — **also for denied reads** (no existence oracle) |
| 409 | Closed-fund entry, invalid lifecycle transition, recorder-as-approver, close/reopen conflict |

## Service-to-service notes

Finance exposes no cross-service endpoint in this gate. Future consumers
(Audit, Notifications, Search, Analytics) subscribe to
`FinanceTransactionRecorded` through the bus — they never read the Finance
database. Any future internal fact query will follow the established
`X-Client-Id` internal-header pattern. Amounts and attribution never appear in
integration events; consumers resolve amounts only through this API under
`finance.transaction.read`.