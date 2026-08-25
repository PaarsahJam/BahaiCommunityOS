# AI Platform

## Overview

The AI Platform bounded context (Slot 15, ADR-030) provides a provider-neutral
abstraction for AI-assisted capabilities. At this implementation gate, the
platform ships with a **disabled/no-op provider** — no external AI provider is
enabled, no data is transmitted externally, and all AI operations fail visibly.

## Architecture

- **Stateless first gate**: No database, no persistence, no migrations.
- **Zero events**: No integration events published or consumed.
- **Provider-neutral seam**: `IAiModelGateway` interface with
  `DisabledAiModelGateway` as the sole implementation.
- **Fail-visible**: All AI operations return typed failures when the provider
  is disabled.

## Governance Status

| Question | Status |
|---|---|
| OQ-3 (data-transmission classification) | **UNRESOLVED** — external providers remain prohibited |
| OQ-8 (provider selection) | **DEFERRED** — no concrete provider selected |

## Permissions

| Permission | Grant |
|---|---|
| `ai.assist.invoke` | GlobalAdministrator, NationalAdministrator, LocalAdministrator, CommitteeMember, Member |
| `ai.platform.manage` | GlobalAdministrator, NationalAdministrator |

Exact-match semantics. `ai.platform.manage` does NOT imply `ai.assist.invoke`.

## Data Handling

- **Default-deny**: No repository or consumer data is transmitted externally.
- **Logging**: Only operational metadata (correlation ids, subject ids,
  capability names, outcome codes). Never prompts, completions, or generated
  content.

## Human-Review Invariant

AI output is **never authoritative**. There is no auto-publish path from AI
results to Knowledge, Library, Records, Localization, or any other authoritative
context. Any future AI suggestion must remain non-authoritative and
provenance-bearing.

## What Is Implemented

- `IAiModelGateway` provider-neutral seam
- `DisabledAiModelGateway` (fail-visible, no content generated)
- `POST /api/v1/ai/assist/{capability}` (returns 503 — provider disabled)
- `GET /api/v1/ai/capabilities` (returns empty list)
- `GET /api/v1/ai/admin/providers` (returns disabled provider)
- `GET /api/v1/ai/admin/models` (returns empty list)
- `GET /api/v1/ai/admin/templates` (returns empty list)
- `GET /api/v1/ai/admin/usage` (returns zeroed summary)
- Two permissions (`ai.assist.invoke`, `ai.platform.manage`)
- Comprehensive unit and integration tests

## What Is NOT Implemented

- External AI providers (OQ-3 unresolved, OQ-8 deferred)
- Database / persistence
- Integration events
- Consumer integrations (Localization, Knowledge, Documents, etc.)
- RAG / embeddings / vector storage
- Prompt template management
- Usage persistence / accounting
- Health endpoint

## ADR Reference

- ADR-030: AI Platform bounded context and assistance boundary
- ADR-021: Knowledge/Library AI constraints
- ADR-029: Localization machine-translation seam (deferred)
