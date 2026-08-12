# CommunityOS Specifications — Source of Truth

> **Status statement**: This directory is the authoritative architectural baseline
> for CommunityOS implementation work. It is maintained in lockstep with the
> ratified Architecture Decision Records in `docs/architecture/ADR.md`.

Files in this directory are the architectural requirements. If a file is marked
**PLACEHOLDER**, the authoritative source document was not available in the
workspace at the time this baseline was established; the placeholder records the
document's scope only and does **not** invent requirements. The placeholder must
be replaced by the authoritative source document before it is relied upon for
implementation.

## Index

| File | Document | Status |
|------|----------|--------|
| `00-CommunityOS-Master-Prompt.md` | CommunityOS Master Prompt | PLACEHOLDER |
| `01-Architecture-Overview.md` | Architecture Overview | PLACEHOLDER |
| `03-Service-Boundaries.md` | Service Boundaries | PLACEHOLDER |
| `05-Security-Architecture.md` | Security Architecture | PLACEHOLDER |
| `06-Data-Classification-Model.md` | Data Classification Model | PLACEHOLDER |
| `07-Authorization-Model-and-Permission-Matrix.md` | Authorization Model & Permission Matrix | PLACEHOLDER |
| `08-Data-Architecture.md` | Data Architecture | PLACEHOLDER |
| `09-Event-Architecture.md` | Event Architecture | PLACEHOLDER |
| `10-AI-Governance.md` | AI Governance | PLACEHOLDER |

Document numbering is intentionally non-contiguous (02, 04 absent) to match the
referenced specification set; gaps are not filled with invented documents.

## Ratified decisions

Decisions ratified at the architecture reconciliation (Prompt 04A) are recorded
in `docs/architecture/ADR.md`:

- ADR-004 — Transport-independent event bus (MassTransit + RabbitMQ; NATS/Kafka future)
- ADR-015 — Reliable event publication via transactional outbox
- ADR-016 — Organization bounded context and Community boundary
- ADR-017 — Bounded context implementation sequence
- ADR-018 — Authentication and authorization ownership boundary
- ADR-019 — Community security corrections
- ADR-020 — Repository specifications as source of truth
