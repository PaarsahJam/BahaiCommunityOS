# Correspondence Operations Runbook

> **STATUS: IMPLEMENTED (Prompt 16).** Operational runbook for
> the Correspondence service, ratified by ADR-028 at the Prompt 12H gate.
> The commands and endpoints below are live in the running service.

## Service profile (planned)

| Aspect | Value |
|--------|-------|
| Database | `communityos_correspondence` (PostgreSQL 16, shared instance), schema `correspondence` |
| Transport | RabbitMQ via MassTransit; transactional outbox for all publications; inbox for all consumption |
| Authorization service | `AuthorizationService:BaseUrl` check API (`HttpAuthorizationEvaluator`, fail-closed) |
| JWT | Identity-issued RS256 only; subject claim mandatory; HTTPS metadata default |
| Configuration root | `Correspondence` section (limits below) + `ConnectionStrings:CorrespondenceDb`, `Jwt:*`, `RabbitMq:*`, `AuthorizationService:*` |

## Planned configuration keys

| Key | Default | Meaning |
|-----|---------|---------|
| `Correspondence:MaxPageSize` | `100` | Query clamp ceiling (`limit` clamped, not rejected) |
| `Correspondence:DefaultPageSize` | `25` | Page size when no `limit` supplied |
| `Correspondence:ExportMaxRows` | `10000` | Hard export cap (above it rejected with `400`) |
| `Correspondence:PurgeDefaultBatchSize` | `500` | Purge batch when `maxBatchSize` omitted |
| `Correspondence:PurgeMaxBatchSize` | `5000` | Purge batch upper bound (above clamped; negative rejected) |
| `Correspondence:HoldMaxBatchSize` | `200` | Maximum letters addressable per hold request |
| `Correspondence:MaterializationWarnMinutes` | `60` | Age beyond which a letter in `Submitted` is flagged for reconciliation |
| `Correspondence:Retention:DefaultClass` | `default` | Class for letters without an explicit mapping |
| `Correspondence:Retention:Classes:{Code}` | — | ISO-8601 duration or empty (= indefinite) |
| `Correspondence:Retention:EventClasses:{EventType}` | — | Optional event-type → class override |

Defaults are ratified with ADR-028; deployment may override values but never
semantics.

## Startup / database

- Migrations apply automatically in Development only (`MigrateAsync` behind
  the environment check); production applies migrations out of band.
- The initial migration provisions schema `correspondence`, all tables listed
  in ADR-028 decision 12, the immutability triggers on `letters`
  (`app.correspondence_purge_authorized` guard) and the MassTransit
  inbox/outbox tables.
- Docker initialization adds `CREATE DATABASE communityos_correspondence;` to
  `docker/init/01-create-databases.sql` **at the implementation gate**.

## Event flow

Published (outbox, guaranteed): `LetterSubmitted`, `LetterDispatched`,
`LetterDeliveryConfirmed`, `LetterDeliveryFailed`, `LetterCancelled`.
Consumed first gate: Organization unit projections (inbox). Consumed after the
**Documents outbox gate**: correlated `DocumentVersionAdded`
(`SourceContext="correspondence"`) closing materialization. Until that producer
gate completes, no Documents consumer may be registered and letters will
remain in `Submitted` after submission — this is expected behavior under the
gate, not an incident.

## Standard operating procedures

### Reconcile stuck materializations

Symptom: letters remain `Submitted` longer than
`MaterializationWarnMinutes` while the Documents gate is complete.

1. `POST /api/v1/correspondence/admin/reconcile-materializations`
   → `{ inspected, republished }`.
2. Verify Documents consumed the re-published facts (outbox/inbox dashboards).
3. Escalate to Documents owners if `republished > 0` repeatedly — indicates a
   consumer-side fault, not a Correspondence fault.

### Retention purge

Expiry alone never deletes. Run manually or scheduled:

```
POST /api/v1/correspondence/admin/purge-expired   { "maxBatchSize": 500 }
```

Each call removes one bounded batch of expired unheld letters inside one
transaction: tombstone history row first, then deletion under the trigger
guard. Repeat until `purgedCount` is 0. Active holds always override expiry.

### Holds

Place: `POST /admin/holds { entryIds, holdType: "legal"|"administrative",
reasonCode }` — reason codes fixed: investigation, legal-request, dispute,
regulatory-inquiry, other. Release: `POST /admin/holds/{id}/release`. Every
placement/release appends to letter status history. Hold placement requires
read visibility of every target (admin does not imply read).

### Exports

`POST /letters/export { format, filters, maxRows? }` — metadata index only;
never bodies/subjects/display lines. Requires `.export` (+`.read.sensitive`
when including sensitive letters). Large exports: iterate with narrower date
filters rather than raising `ExportMaxRows`.

## Observability

Structured logs carry identifiers and codes only (letter id, statuses,
category/sensitivity codes, counts, actor/provider-reference ids). Letter
bodies, subjects, recipient names, addresses, emails, phone numbers,
credentials and provider secrets must never appear in any log line — the log-
scrub test suite enforces this. Health: standard house health endpoints;
bus health via RabbitMQ management; database via connection + migration checks.

## Failure playbook

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Letters stuck in `Submitted` | Documents outbox gate incomplete, or Documents consumer down | If gate incomplete: expected. Otherwise run reconcile SOP |
| `409` storms on submit | Client retrying already-submitted letters | Treat as success; fetch letter state |
| Purge returns `purgedCount` 0 forever | All expired letters held | Review holds; holds intentionally block purge |
| Dead-letter queue growth | Unmappable events (contract drift) | Inspect DLQ payload shapes; fix mapping behind an amendment, replay safely (natural-key guards make redelivery convergent) |
| Authorization check failures spike | Authorization service outage | Service fails closed (403); restore Authorization; no local bypass exists by design |

## Implementation prerequisites checklist

- [ ] Documents transactional outbox upgrade + `DocumentVersionAdded`
      contract extension (ADR-022 amendment)
- [ ] Permission catalog + role seeds registered (ten capabilities)
- [ ] Docker init entry for `communityos_correspondence`
- [ ] ADR-027 catalog amendment (five correspondence events)
