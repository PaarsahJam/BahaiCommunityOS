# Community Service API

All endpoints are versioned under `/api/v1`, require a valid access token
(`[Authorize]`), and return the entity DTOs. Every guarded operation is
evaluated against the Authorization service (fail-closed). Visibility values are
`public`, `members`, `participants_only` (meetings/events/activities) and
`public`, `members`, `private` (contact visibility).

## Persons — `/persons`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/persons` | `community.person.read` | List persons |
| GET | `/persons/{id}` | `community.person.read` (+ `community.person.contact.read` / `community.person.sensitive.read` for masked fields) | Get a person detail |
| POST | `/persons` | `community.person.create` | Create a person |
| PUT | `/persons/{id}/profile` | `community.person.update` | Update name / date of birth / language |
| PUT | `/persons/{id}/contact-methods` | `community.person.update` | Replace contact methods |
| POST | `/persons/{id}/identity-link` | `community.person.identity.link` | Link a person to an Identity account |
| POST | `/persons/{id}/identity-unlink` | `community.person.identity.link` | Unlink the Identity account |
| POST | `/persons/{id}/deactivate` | `community.person.deactivate` | Deactivate a person |
| POST | `/persons/{id}/reactivate` | `community.person.update` | Reactivate a person |

**Create** body:

```json
{
  "preferredName": "Ruhi Jones",
  "formalName": "Ruhi Jones",
  "preferredLanguage": "en",
  "profileVisibility": "public",
  "contactVisibility": "members",
  "dateOfBirthVisibility": "private"
}
```

**Set contact methods** body:

```json
{
  "contactMethods": [
    {
      "type": "email",
      "value": "ruhi@example.org",
      "isPreferred": true,
      "visibility": "members"
    }
  ]
}
```

Reading a person without `community.person.contact.read` returns the profile
with contact methods empty; without `community.person.sensitive.read` the date
of birth and the identity link are masked (`null` / empty). Privacy preferences
may further restrict exposure.

## My person — `/my-person`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/my-person` | none (self-scoped) | Resolve the authenticated account's own Person |

Returns the `PersonDto` of the Community Person linked to the authenticated
Identity account (JWT `sub`). The endpoint is self-scoped: it accepts no
caller-supplied identifier and can only ever resolve the caller's own Person.
Returns `404` (`application/problem+json`) when the authenticated account has
no linked Person — an unlinked account is a "not found" state, never a server
error.

### Member Portal v1 access

Member Portal v1 lets an authenticated account read their own Community
profile and their own membership through the standard endpoints:

- `GET /my-person` — resolve the caller's own `personId`.
- `GET /persons/{id}` — profile of the caller's own person.
- `GET /memberships/by-person/{personId}` — membership of the caller's own
  person (a missing membership returns `200` with `null`).

The required capabilities are granted as resource-scoped `has_permission`
relations on the member's own Person resource (subject = Identity account;
object = `person`, the member's own `personId`):

| Capability | Scope |
|------------|-------|
| `community.person.read` | `person:{ownPersonId}` |
| `community.person.contact.read` | `person:{ownPersonId}` |
| `community.membership.read` | `person:{ownPersonId}` |

No new endpoint (`/my-membership`), no JWT context claims, and no organization
context are introduced in v1. Authorization is exclusively relationship-based
and evaluated by the Authorization service (fail-closed): the grants only ever
apply to the member's own person resource and grant nothing globally.

Grants are provisioned automatically by the Authorization service: the
Community service publishes `PersonIdentityLinked` / `PersonIdentityUnlinked`
integration events when a Person is linked to or unlinked from an Identity
account. The `PersonIdentityIntegrationEventConsumer` creates or revokes the
member portal relationship tuple for that person. Provisioning is idempotent,
consolidates stale partial tuples, and never removes relationship tuples that
carry other (non-member) permissions.

## Households — `/households`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/households` | `community.household.read` | List households |
| GET | `/households/{id}` | `community.household.read` | Get a household |
| POST | `/households` | `community.household.create` | Create a household |
| PUT | `/households/{id}` | `community.household.update` | Update name and address |
| POST | `/households/{id}/members` | `community.household.update` | Add an effective-dated member |
| DELETE | `/households/{id}/members/{personId}` | `community.household.update` | Remove a member |

**Create** body:

```json
{
  "name": "Nabil Street",
  "line1": "1 Nabil St",
  "city": "Haifa",
  "country": "IL"
}
```

**Add member** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "role": "head",
  "effectiveFrom": "2026-08-11T00:00:00Z"
}
```

Adding an already-present member returns `409 Conflict`.

## Family relationships — `/family-relationships`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/family-relationships?personId={id}` | `community.family.read` | List a person's relationships |
| POST | `/family-relationships` | `community.family.create` | Create a directional relationship |
| POST | `/family-relationships/{id}/end` | `community.family.update` | End a relationship |

**Create** body:

```json
{
  "personIdA": "00000000-0000-0000-0000-000000000001",
  "personIdB": "00000000-0000-0000-0000-000000000002",
  "relationshipType": "parent",
  "effectiveFrom": "2026-08-11T00:00:00Z"
}
```

Types are `spouse`, `parent`, `child`, `sibling`, `grandparent`, `grandchild`,
`guardian`, `dependent`, `other`. Self-relationships return `400`; an active
duplicate of the same type returns `409`.

## Memberships — `/memberships`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/memberships` | `community.membership.read` | List memberships |
| GET | `/memberships/{id}` | `community.membership.read` | Get a membership |
| GET | `/memberships/by-person/{personId}` | `community.membership.read` | Get the membership of a person |
| POST | `/memberships` | `community.membership.create` | Create a membership |
| PATCH | `/memberships/{id}/status` | `community.membership.update` | Change membership status |

**Create** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "status": "active",
  "effectiveFrom": "2026-08-11T00:00:00Z"
}
```

Status values are `pending`, `active`, `suspended`, `lapsed`, `withdrawn`.
Creating a second membership for the same person returns `409`.

## Activities — `/activities`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/activities?from=&to=&organizationUnitId=` | `community.activity.read` | List activities |
| GET | `/activities/{id}` | `community.activity.read` | Get an activity |
| POST | `/activities` | `community.activity.create` | Create an activity |
| PUT | `/activities/{id}` | `community.activity.update` | Update details |
| PATCH | `/activities/{id}/schedule` | `community.activity.update` | Reschedule |
| POST | `/activities/{id}/cancel` | `community.activity.update` | Cancel an activity |

**Create** body:

```json
{
  "title": "Devotional Gathering",
  "description": "Sunday devotional",
  "category": "devotional",
  "organizationUnitId": "00000000-0000-0000-0000-000000000000",
  "location": "Community Hall",
  "isOnline": false,
  "startsAt": "2026-08-18T09:00:00Z",
  "endsAt": "2026-08-18T11:00:00Z",
  "visibility": "public",
  "status": "planned",
  "capacity": 30
}
```

Visibility is `public`, `members`, `participants_only`; status is `planned`,
`active`, `completed`, `cancelled`.

## Community events — `/community-events`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/community-events?from=&to=&organizationUnitId=` | `community.event.read` | List events |
| GET | `/community-events/{id}` | `community.event.read` | Get an event |
| POST | `/community-events` | `community.event.create` | Create an event |
| PUT | `/community-events/{id}` | `community.event.update` | Update details |
| PATCH | `/community-events/{id}/schedule` | `community.event.update` | Reschedule |
| POST | `/community-events/{id}/cancel` | `community.event.update` | Cancel an event |

**Create** body:

```json
{
  "title": "Ridvan Celebration",
  "description": "Feast of Ridvan",
  "startsAt": "2026-08-25T18:00:00Z",
  "endsAt": "2026-08-25T21:00:00Z",
  "timeZone": "Asia/Jerusalem",
  "location": "Garden",
  "isOnline": false,
  "organizationUnitId": "00000000-0000-0000-0000-000000000000",
  "status": "scheduled",
  "visibility": "members",
  "registrationOpen": true,
  "capacity": 100
}
```

Status is `scheduled`, `confirmed`, `cancelled`, `completed`.

## Meetings — `/meetings`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/meetings?from=&to=&organizationUnitId=` | `community.meeting.read` | List meetings |
| GET | `/meetings/{id}` | `community.meeting.read` | Get a meeting |
| POST | `/meetings` | `community.meeting.create` | Create a meeting |
| PUT | `/meetings/{id}` | `community.meeting.update` | Update details |
| PATCH | `/meetings/{id}/schedule` | `community.meeting.update` | Reschedule |
| POST | `/meetings/{id}/participants` | `community.meeting.participant.manage` | Add a participant |
| POST | `/meetings/{id}/attendance` | `community.meeting.participant.manage` | Record attendance |
| POST | `/meetings/{id}/agenda-items` | `community.meeting.update` | Add an agenda item |
| POST | `/meetings/{id}/actions` | `community.meeting.update` | Add an action item |
| POST | `/meetings/{id}/minutes` | `community.meeting.record` | Record minutes |
| POST | `/meetings/{id}/cancel` | `community.meeting.update` | Cancel a meeting |

**Create** body:

```json
{
  "title": "Nineteen Day Feast",
  "description": "Devotional portion",
  "startsAt": "2026-08-14T19:00:00Z",
  "endsAt": "2026-08-14T20:00:00Z",
  "timeZone": "Asia/Jerusalem",
  "location": "Hall",
  "organizationUnitId": "00000000-0000-0000-0000-000000000000",
  "status": "scheduled",
  "visibility": "members"
}
```

**Record attendance** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "attendance": "attended"
}
```

Attendance is `not_marked`, `attended`, `absent`. Recording minutes transitions
the meeting to `completed`. Adding an already-present participant returns
`409`.

## Participations — `/participations`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/participations/{id}` | `community.participation.read` | Get a participation |
| GET | `/participations/by-person/{personId}` | `community.participation.read` | List a person's participation |
| GET | `/participations/by-target?targetType=&targetId=` | `community.participation.read` | List participation by target |
| POST | `/participations` | `community.participation.create` | Record a participation |
| PATCH | `/participations/{id}/status` | `community.participation.update` | Update status |

**Record** body:

```json
{
  "personId": "00000000-0000-0000-0000-000000000000",
  "targetType": "activity",
  "targetId": "00000000-0000-0000-0000-000000000000",
  "role": "coordinator",
  "status": "registered",
  "effectiveFrom": "2026-08-11T00:00:00Z"
}
```

Target types are `activity`, `event`, `meeting`, `volunteer_service`. Status is
`registered`, `attended`, `cancelled`. Duplicate person/target returns `409`.

## Calendar — `/calendar`

| Method | Path | Capability | Description |
|--------|------|------------|-------------|
| GET | `/calendar?from=&to=&organizationUnitId=&types=` | `community.calendar.read` | Combined calendar of activities, events and meetings |

`from` and `to` are required. `types` is a comma-separated subset of
`activity`, `event`, `meeting` (defaults to all).

## Error handling

Errors are returned as JSON with a problem-details body. Common codes:

| HTTP status | Meaning |
|-------------|---------|
| 400 | Validation failure or invalid enum value |
| 401 | Missing / invalid access token |
| 403 | Caller lacks the required capability (fail-closed) |
| 404 | Entity not found |
| 409 | State conflict (duplicate, double end/cancel, invalid transition) |
