# Knowledge Service API

All endpoints are versioned under `/api/v1`, require a valid access token
(`[Authorize]`), and return the Knowledge entity DTOs. Every guarded operation
is evaluated against the Authorization service (fail-closed). Library content
is read-only from the community: importing and verifying editions are
moderator operations, and citation text is always resolved from the Library.

## Library — `/library`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/library/works?categoryId=&topicId=` | `knowledge.library.read` | List works |
| GET | `/library/works/{id}` | `knowledge.library.read` | Get a work with editions |
| POST | `/library/works` | `knowledge.library.import` | Create a work |
| PUT | `/library/works/{id}` | `knowledge.library.update` | Update work metadata |
| GET | `/library/editions?workId=&language=&verified=` | `knowledge.library.read` | List editions |
| POST | `/library/editions` | `knowledge.library.import` | Import an edition |
| POST | `/library/editions/{id}/verify` | `knowledge.library.verify` | Mark an edition verified (citable) |
| GET | `/library/passages?editionId=&from=&to=` | `knowledge.library.read` | List passages of an edition |
| GET | `/library/passages/{id}` | `knowledge.library.read` | Get a passage (text + reference path) |
| POST | `/library/passages` | `knowledge.library.import` | Import a passage |

**Create work** body:

```json
{
  "title": "Gleanings from the Writings of Baha'u'llah",
  "workType": "compilation",
  "originalLanguage": "fa",
  "defaultLanguage": "en"
}
```

**Import edition** body:

```json
{
  "workId": "00000000-0000-0000-0000-000000000001",
  "language": "en",
  "translator": "Shoghi Effendi",
  "publisher": "Baha'i Publishing Trust",
  "editionYear": 1954,
  "verified": false
}
```

**Import passage** body:

```json
{
  "editionId": "00000000-0000-0000-0000-000000000002",
  "referencePath": "Gleanings, CXXIII",
  "text": "The best way or method of teaching...",
  "sortOrder": 123
}
```

Only *verified* editions are citable by community content; importing a passage
for an unverified edition is allowed but references to it are rejected until the
edition is verified.

## Questions — `/questions`

Questions follow the lifecycle `Draft → Submitted → Published → Under Review →
Merged | Archived`. All transitions are guarded; canonicalization and archival
require a moderator grant.

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/questions?status=&categoryId=&topicId=&organizationUnitId=&query=` | `knowledge.question.read` | List questions |
| GET | `/questions/{id}` | `knowledge.question.read` | Get a question with answers/discussion |
| POST | `/questions` | `knowledge.question.create` | Create a question (Draft) |
| POST | `/questions/{id}/submit` | `knowledge.question.update` | Submit Draft → Submitted |
| POST | `/questions/{id}/publish` | `knowledge.moderation.review` | Publish Submitted → Published |
| POST | `/questions/{id}/flag` | `knowledge.question.update` | Flag a question for review |
| POST | `/questions/{id}/under-review` | `knowledge.moderation.review` | Move Published → Under Review |
| POST | `/questions/{id}/merge-to/{targetId}` | `knowledge.question.merge` | Merge onto a canonical target (terminal) |
| POST | `/questions/{id}/archive` | `knowledge.moderation.archive` | Archive a question (terminal) |

**Create** body:

```json
{
  "title": "What does the Faith say about the soul?",
  "body": "I would like to understand the Baha'i view of the soul and its journey.",
  "categoryId": "00000000-0000-0000-0000-000000000003",
  "tags": ["soul", "afterlife"],
  "organizationUnitId": "00000000-0000-0000-0000-000000000000"
}
```

Submitting a non-Draft question returns `400`; archiving or merging an already
terminal question returns `409`. A `Merged` question's canonical target cannot
be changed without a new moderation event.

## Answers — `/questions/{id}/answers`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/questions/{id}/answers` | `knowledge.answer.read` | List answers for a question |
| POST | `/questions/{id}/answers` | `knowledge.answer.create` | Add an answer |
| PUT | `/answers/{answerId}` | `knowledge.answer.update` | Update an answer (new revision) |
| POST | `/answers/{answerId}/accept` | `knowledge.moderation.review` | Mark an answer accepted (one per question) |

**Create** body:

```json
{
  "authorId": "00000000-0000-0000-0000-000000000004",
  "body": "The Writings explain that the soul is...",
  "references": [
    {
      "passageId": "00000000-0000-0000-0000-000000000005"
    }
  ]
}
```

References must target verified passages; a reference to an unverified edition
returns `400`. Accepting a second answer returns `409`.

## AI suggestions — `/questions/{id}/ai-suggestions`

AI output is always a clearly-marked, non-authoritative suggestion. It is never
a Library entry, never automatically an accepted answer, and requires a human
moderator grant to be reviewed.

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/questions/{id}/ai-suggestions` | `knowledge.ai.review` | List AI suggestions for a question |
| POST | `/questions/{id}/ai-suggestions/request` | `knowledge.ai.suggest` | Request an AI-generated draft |
| POST | `/ai-suggestions/{id}/accept` | `knowledge.ai.review` | Accept a suggestion as a draft answer (requires human moderator) |
| POST | `/ai-suggestions/{id}/reject` | `knowledge.ai.review` | Reject a suggestion |

**Request** body:

```json
{
  "modelId": "communityos-llm",
  "instruction": "Draft a short answer citing the Academy passage.",
  "passageIds": ["00000000-0000-0000-0000-000000000005"]
}
```

Accepting an AI suggestion creates an `Answer` with `source = "ai"` and the
original model id retained; the resulting answer still follows the normal
question lifecycle and can be further moderated. An AI subject is never a valid
granter of `knowledge.ai.review`.

## Discussions / comments — `/questions/{id}/discussions`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/questions/{id}/discussions` | `knowledge.discussion.read` | List discussion threads |
| POST | `/questions/{id}/discussions` | `knowledge.discussion.create` | Start a discussion thread |
| POST | `/discussions/{id}/comments` | `knowledge.discussion.create` | Add a comment |
| POST | `/discussions/{id}/moderate` | `knowledge.discussion.moderate` | Moderate a thread (delete/hide) |

Comments may carry references to verified passages; they never embed
authoritative text.

## Categories / topics — `/knowledge`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/categories` | `knowledge.library.read` | List categories |
| POST | `/categories` | `knowledge.moderation.manage` | Create a category |
| PUT | `/categories/{id}` | `knowledge.moderation.manage` | Update a category |
| GET | `/topics` | `knowledge.library.read` | List topics |
| POST | `/topics` | `knowledge.moderation.manage` | Create a topic |
| PUT | `/topics/{id}` | `knowledge.moderation.manage` | Update a topic |

## Service-only fact endpoints

Knowledge mirrors the Organization pattern for service-to-service facts:
internal endpoints (e.g. passage citation resolution) are protected by
`X-Client-Id` matching a `Knowledge:InternalClientId` configuration value and
remain behind `[Authorize]`. The header is an identification/routing signal,
not an authorization credential (see `docs/api/organization.md` notes).

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure or invalid enum value / unverified-edition reference |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Entity not found |
| 409 | State conflict (invalid transition, terminal canonicalization, double accept/archive) |