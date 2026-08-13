# Knowledge Service

The Knowledge bounded context owns the community's knowledge base: the Library
of authoritative source material (the Baha'i Writings and their translations),
and the community knowledge built on top of it — questions, answers,
discussions, and the references that connect them to the Library
(`ADR-021`). Knowledge is positioned in the implementation sequence after
Search and before Correspondence (`ADR-017`).

The service must preserve the boundary rules of `ADR-021`: the Library is the
single source of truth for citation text, AI-generated content is always a
clearly-marked non-authoritative *suggestion*, and authoritative religious text
is never produced or arbitrated by AI.

## Model

### Library

- **Work** — an authoritative literary work (a book, tablet, compilation).
  Carries a stable identifier, title and canonical attribution. Works are
  versioned by Editions.
- **Edition** — a concrete published edition or translation of a Work (a
  language, a translator, a publisher, an edition year). Carries a
  verification state: references may only target *verified* Editions.
- **Passage** — a quotable unit of text within an Edition (a paragraph, a
  tablet opening, a section). Carries the edition, the original-language
  reference path, and the published translation text. Passages are immutable;
  corrections create a new revision record.
- **Translation** — multilingual text attached to a Work/Edition/Passage.
  Always carries its source edition so translations are attributable and
  verifiable.
- **Category / Topic / Tag** — controlled and free-form organization of
  questions and answers. Categories are configurable; tags are user-assigned.

### Community knowledge

- **Question** — a community question. Lifecycle:
  `Draft → Submitted → Published → Under Review → Merged | Archived`.
  A `Merged` question is canonicalized onto a sibling; a `Canonicalized`
  question is the surviving target of one or more merges. Archival is
  terminal but preserves history.
- **Answer** — a structured answer to a Question. Answers are revisioned,
  can carry an author, and may cite Library passages. One answer may be
  marked *accepted* for a Question by a moderator.
- **Discussion / Comment** — lighter-weight commentary attached to a Question
  or Answer. Not a first-class citation carrier.
- **Reference** — a citation from a Question/Answer/Comment/Discussion to a
  verified Library Passage. References are the only allowed way for community
  content to carry authoritative text.
- **ModerationFlags / ReviewState** — user flags and moderator-driven state
  that gate promotion (e.g., a `Submitted` question to `Published`).
- **AiSuggestion** — an AI-generated draft answer or summary attached to a
  Question. Always carries `source = "ai"`, a model identifier, a review
  state (`suggested` / `accepted` / `rejected`), and is never published as an
  accepted answer until a human moderator accepts it.

## Key rules

- A passage is immutable; text corrections produce a new revision, never an
  in-place mutation.
- References may only target *verified* editions; unverified editions cannot be
  cited.
- Authoritative text is never embedded as community content — community content
  carries passage ids and the reading UI resolves the authoritative text from
  the Library.
- AI output is always an `AiSuggestion` with explicit provenance and review
  state; it is never a Library entry and never an authoritative answer.
- A question transitions forward along its lifecycle; `Merged` and `Archived`
  are terminal.
- Canonicalization is immutable once applied: a `Canonicalized` question's
  target cannot change (a moderator may reverse it only by a new moderation
  event).
- Only one accepted answer per question.
- Referencing an unverified edition or a nonexistent passage is rejected
  (`InvalidReferenceException`).
- AI suggestions may not be made authoritative by the AI itself; acceptance
  requires a human moderator action (`AiAssistedAnswerRequiresReviewException`).

## Privacy and permissions

Knowledge content is community content. It is not PII-bearing by design, but
authors (person ids) are treated as references and resolved through the
Community service; no person data is stored locally. Permissions follow the
`knowledge.entity.action` naming:

- Reading the Library requires `knowledge.library.read`.
- Publishing authoritative Library content requires moderation capabilities
  (`knowledge.library.import`, `knowledge.library.verify`).
- Authoring questions/answers requires `knowledge.question.create` /
  `knowledge.answer.create`; publishing and canonicalization require
  moderator capabilities (`knowledge.moderation.review` etc., see
  `docs/api/knowledge.md`).
- AI suggestion acceptance requires a moderator grant
  (`knowledge.ai.review`); the AI itself holds no grant and is never a subject.

Privilege escalation is denied by fail-closed evaluation: if the Authorization
service is unreachable or the permission is not granted, the endpoint returns
`403 Forbidden`.

## HTTP API

All endpoints are versioned under `/api/v1` and require a valid access token.
See `docs/api/knowledge.md` for the full endpoint reference.

## Integration

- **Events** — domain events are forwarded as integration events onto RabbitMQ
  via MassTransit (`CommunityOS.Knowledge.Infrastructure.Integration`).
  Contracts live in `CommunityOS.Contracts.Knowledge`.
- **Community** — person authors are stable ids; Knowledge never reads or
  writes Community data. Person names are resolved through the Community API
  at read time.
- **Organization** — questions can be scoped to a `organizationUnitId` (a
  reference, not ownership). Knowledge consumes Organization integration
  events into `organization_unit_references`, mirroring the Community pattern,
  so scoping never depends on a live Organization query (`ADR-016`).
- **Authorization** — the Knowledge service never reads the Authorization
  database. `AuthorizationGuard` is bound to the same HTTP evaluator as
  Community and Organization, calling the Authorization service's check API as
  a service principal (`ADR-018`/`ADR-019`).
- **AI / Documents / Workflow / Notifications / Search** — Knowledge consumes
  and emits events for AI suggestions, document import, review workflows,
  notification digests and search indexing; none of these cross the service
  database boundary. Details are deferred to the implementation sequence.

### Planned integration events (`CommunityOS.Contracts.Knowledge`)

Names and payloads below are the ratified baseline for the implementation
phase; each record carries a trailing `DateTime OccurredOn`. Only stable ids
and lifecycle state are exported — no author PII; consumers resolve authors
through the Community API.

| Event | Raised when | Key fields |
|-------|-------------|-----------|
| `WorkImported` | A work is imported | `WorkId`, `Title`, `WorkType` |
| `EditionImported` | An edition is imported | `EditionId`, `WorkId`, `Language`, `Verified` |
| `EditionVerified` | An edition becomes citable | `EditionId`, `WorkId` |
| `PassageImported` | A passage is imported | `PassageId`, `EditionId`, `ReferencePath` |
| `PassageCorrected` | A passage text correction is published | `PassageId`, `EditionId`, `Revision` |
| `QuestionSubmitted` | Draft → Submitted | `QuestionId`, `AuthorId`, `OrganizationUnitId` |
| `QuestionPublished` | Submitted → Published | `QuestionId`, `OrganizationUnitId` |
| `QuestionFlagged` | A question is flagged for review | `QuestionId`, `FlagReason` |
| `QuestionUnderReview` | Published → Under Review | `QuestionId` |
| `QuestionMerged` | A question is canonicalized onto a target | `QuestionId`, `TargetQuestionId` |
| `QuestionArchived` | A question is archived (terminal) | `QuestionId` |
| `AnswerAdded` | An answer is added | `AnswerId`, `QuestionId`, `AuthorId`, `Source` |
| `AnswerUpdated` | An answer gets a new revision | `AnswerId`, `QuestionId`, `Revision` |
| `AnswerAccepted` | An answer is marked accepted | `AnswerId`, `QuestionId` |
| `AiSuggestionRequested` | An AI draft is requested | `SuggestionId`, `QuestionId`, `ModelId` |
| `AiSuggestionReviewed` | A suggestion is accepted/rejected | `SuggestionId`, `QuestionId`, `Outcome` |
| `CategoryCreated` / `CategoryUpdated` | Category managed | `CategoryId`, `Name` |

Consumers in the ADR-017 sequence (Workflow reviews, Notifications digests,
Search indexing, Documents import, AI) subscribe by contract name and never by
database.

## Data

- Database: `communityos_knowledge` (PostgreSQL), schema `knowledge`.
- Tables: `works`, `editions`, `passages`, `passage_revisions`,
  `translations`, `categories`, `topics`, `tags`, `questions`,
  `question_lifecycle_events`, `answers`, `answer_revisions`,
  `discussions`, `comments`, `references`, `moderation_flags`,
  `ai_suggestions`, `organization_unit_references`.
- Schema is managed by EF Core migrations.

## Configuration

| Section | Key | Default | Description |
|---------|-----|---------|-------------|
| `ConnectionStrings` | `KnowledgeDb` | `Host=localhost;Port=5432;Database=communityos_knowledge;Username=communityos;Password=communityos` | PostgreSQL connection string |
| `Jwt` | `Issuer` / `Audience` / `MetadataAddress` | *(local)* | Token validation for the API |
| `RabbitMq` | `Host` / `Port` / `Username` / `Password` | `localhost` / `5672` / `guest` / `guest` | Message bus for integration events |
| `AuthorizationService` | `BaseUrl` / `AccessToken` / `ClientId` | *(see runbook)* | Authorization check API configuration |

## Testing

- **Unit tests** (`tests/Unit/CommunityOS.Knowledge.Tests`) — domain invariants
  (passage immutability, reference validity, question lifecycle transitions,
  canonicalization rules, AI-suggestion review requirements) and application
  service behaviour through the MediatR pipeline with substitute persistence.
- **Security regression tests** — fail-closed authorization, moderator gating,
  AI-suggestions-never-authoritative enforcement.
- **Integration tests** (`tests/Integration/CommunityOS.Knowledge.IntegrationTests`)
  — EF mapping and migrations against a real PostgreSQL via Testcontainers
  (requires Docker).