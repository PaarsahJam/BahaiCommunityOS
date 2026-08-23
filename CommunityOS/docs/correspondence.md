# Correspondence Bounded Context

> **STATUS: RATIFIED — NOT IMPLEMENTED (Prompt 12H).** Design for the
> Correspondence bounded context (ADR-017 slot 13), ratified at the Prompt 12H
> gate by ADR-028. Nothing in this document describes existing code; it is the
> authoritative specification the future implementation gate must satisfy.

Correspondence owns the institutional letter lifecycle of the community
administration: drafting official letters, confirming them, submitting them as
formal acts of correspondence, materializing submitted letters as immutable
document artifacts (owned by Documents), recording dispatch and delivery
outcomes, cancelling pre-dispatch, and retaining full history under retention
policy and legal holds.

## Position in the platform

- **Sequence:** ADR-017 slot 13 — after Audit (implemented and closed,
  Prompt 12F) and Knowledge (implemented early, out of sequence). It is the
  next bounded context to be implemented.
- **Authoritative contract:** `docs/architecture/ADR.md` → ADR-028
  (Correspondence bounded context and communication-lifecycle boundary).
- **Greenfield:** no source projects, tests, contracts, database, schema,
  permissions or configuration exist today (verified at Prompt 12G).

## Ownership

Correspondence owns:

- the letter aggregate: identity, per-unit yearly reference number, subject,
  category code, sensitivity classification, organization scope;
- letter content as a working record (draft bodies and the retained submitted
  text);
- recipients as structured references (person id / unit id) plus captured
  external addressing snapshots;
- the submission / dispatch / delivery state machine and its complete
  transition history;
- manual dispatch and delivery-outcome recordings (external providers are a
  deferred seam);
- attachments as references to existing Documents artifacts;
- document-materialization correlation state (DocumentReference per letter);
- the template registry;
- retention state and legal holds on letters;
- its internal export activity journal.

Correspondence does **not** own person or household facts (Community),
organization-unit facts (Organization), document storage or immutable document
versions (Documents), official-record registration (Records), task/approval
workflow (Workflow), notification delivery (Notifications), search indices
(Search), the compliance journal (Audit), or AI behavior. The ADR-022 rule is
preserved verbatim: Documents never owns the correspondence letter lifecycle —
and Correspondence never owns document storage or version-immutability
mechanics.

## Letter lifecycle

Deterministic state machine (full transition table in ADR-028 decision 3):

```
            create        confirm                 submit
   ─────► Draft ─────► Confirmed ──────────────► Submitted
          ▲  │           │  │                     │     │
 unconfirm│  │ cancel    │  │ cancel      materialized   │ cancel
          └──┘           │  │             (event) ▼     │
                         └──┘──────────► Materialized    │
                                              │ dispatch │
                                              ▼          │
                                         Dispatched ◄────┘? no —
                                              │          Cancelled is
                              delivery confirmed│         reachable from
                                              ▼          Draft/Confirmed/
                                          Delivered      Submitted/Materialized

   Dispatched ──delivery failed──► DeliveryFailed
```

- Content edits only while `Draft`; each save bumps an optimistic-concurrency
  revision.
- Submit allocates the reference number, publishes `LetterSubmitted` through
  the transactional outbox atomically with the state change, and returns
  `202 Accepted`.
- Dispatch is blocked until Documents confirms materialization
  (`Submitted → Materialized` on the correlated `DocumentVersionAdded`);
  stranded submissions are operator-repairable via the reconciliation
  endpoint.
- Corrections after submission are new letters (`RelatedLetterId`); history
  is never rewritten.
- Every transition appends an immutable `letter_status_history` row — locally
  auditable without reconstruction.

## Source-of-truth highlights

| Fact | Owner |
|------|-------|
| Letter identity / reference number | Correspondence |
| Recipient entities | Community (persons) / Organization (units); Correspondence holds references |
| External addressing snapshot | Correspondence (external-kind recipients only) |
| Submitted content snapshot | Documents (immutable version artifact); Correspondence retains the working copy + DocumentReference |
| Submission / dispatch / delivery facts | Correspondence |
| Audit journal of those facts | Audit |

## Integration events

Published by Correspondence (all transactional-outbox protected from day one):
`LetterSubmitted`, `LetterDispatched`, `LetterDeliveryConfirmed`,
`LetterDeliveryFailed`, `LetterCancelled` — identifiers, codes, counts and
timestamps only; never subjects, bodies or display lines.

Consumed at first gate: Organization unit-created/updated/parent-changed
(projection-only into `organization_unit_references`; never journaled).

Consumed after the **Documents producer reliability gate**: the correlated
`DocumentVersionAdded` (with the future `SourceContext`/`SourceEntityId`
contract extension). **Correspondence must not register any Documents-event
consumer until Documents upgrades to the transactional outbox** — this is the
same binding producer-gate pattern ADR-027 established for Audit.

## Audit interaction

The five published events are the compliance subset. ADR-028 records the
required **future amendment to ADR-027**: extend the consumed-and-persisted
catalog with these five events when Correspondence is implemented (no extra
producer gate is needed because Correspondence is born outbox-protected).
Ingest mappings carry ids/codes/counts only; all five entries are Normal
sensitivity; drafts/edits/holds/purges/exports produce no audit entries.

## Authorization

Ten capabilities (exact-match semantics; admin implies nothing):

`correspondence.letter.read` · `.read.sensitive` · `.create` · `.update` ·
`.submit` · `.cancel` · `.export` · `.admin` · `correspondence.template.read`
· `correspondence.template.manage`

Role seeds ratified for registration at the implementation gate:
GlobalAdministrator — all ten (global); NationalAdministrator — all ten
(national); LocalAdministrator — read/create/update/submit/cancel/template.read
(local); Volunteer/Guest — none. Fail-closed everywhere; uniform 404
anti-enumeration on single reads; sensitive letters require the second pass in
query, single read, export and hold placement visibility.

The three strings previously present in `PermissionCatalog.DocumentedExamples`
were non-normative examples only; the matrix above supersedes them.

## Database

Dedicated database `communityos_correspondence`, schema `correspondence`,
PostgreSQL 16 on the shared instance, EF Core + Npgsql snake_case. Tables:
`letters`, `letter_recipients`, `letter_status_history`, `letter_documents`,
`letter_attachments`, `letter_delivery_records`, `templates`, `letter_holds`,
`export_activity`, `organization_unit_references`, MassTransit inbox/outbox.
Immutability triggers guard `letters` against UPDATE/DELETE except through the
SET LOCAL-guarded retention purge (Audit pattern). Full column/index/constraint
specification: ADR-028 decision 12.

## Privacy posture

Persisted: ids, codes, subject/body (working record), recipient identifiers,
external display lines, statuses, timestamps. Never logged: bodies, subjects,
recipient names, addresses, emails, phone numbers, credentials, tokens,
provider secrets. Never published: bodies, subjects, display lines, addresses.
Exports are capped metadata/index streams that never include bodies, subjects
or display lines. Retention classes are deployment configuration (default
indefinite); expiry alone never deletes; active holds override expiry; purge
is batch-bounded behind the trigger guard with a tombstone history row.
Search indexing: none at this gate (future ADR-026 amendment required if ever
ratified). AI: none; Correspondence is fully functional without AI.

## Dependencies

| Service | Relationship |
|---------|--------------|
| Identity | authentication (RS256 JWKS profile) — required |
| Authorization | guard over check API — required |
| Organization | unit scoping + projection events — required |
| Community | person-id resolution at read time — optional/read-time |
| Documents | letter materialization — required; **gated on the Documents outbox upgrade** |
| Records | future evidence references to submitted letters — deferred |
| Workflow | none at this gate (built-in Confirm step) |
| Notifications | none at this gate (deferred direction) |
| Search | none at this gate (indexing deferred) |
| Audit | consumes the five letter events once Correspondence ships — requires ADR-027 amendment recorded in ADR-028 |
| Knowledge | no dependency found |

## Implementation prerequisites (not executed by Prompt 12H)

1. Documents transactional-outbox upgrade + `DocumentVersionAdded` contract
   extension (producer-side work; ADR-022 amendment).
2. Permission catalog registration + role seeds (ten capabilities above).
3. `docker/init/01-create-databases.sql` entry for
   `communityos_correspondence`.
4. ADR-027 catalog amendment (five correspondence events).

Until these land, no implementation may begin beyond what its own gate
ratifies.
