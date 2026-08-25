# AI Platform API

## Base URL

```
/api/v1/ai
```

## Authentication

All endpoints require JWT Bearer authentication (RS256). The JWT must contain
a `sub` claim identifying the user.

## Endpoints

### POST /api/v1/ai/assist/{capability}

Invokes AI assistance for a specific capability.

**Permission**: `ai.assist.invoke`

**Request Body**:
```json
{
  "input": "string (required, max 100,000 characters)"
}
```

**Path Parameters**:
- `capability` (string, required): The AI capability to invoke.

**Responses**:

| Status | Description |
|---|---|
| 200 | Success (at this gate, never returned — provider disabled) |
| 400 | Validation error or unsupported capability |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.assist.invoke` permission) |
| 503 | AI provider is disabled |

**Example 503 Response**:
```json
{
  "outcome": "provider_disabled",
  "error": "AI provider is disabled. No external AI provider is configured."
}
```

### GET /api/v1/ai/capabilities

Lists available AI capabilities.

**Permission**: `ai.assist.invoke`

**Responses**:

| Status | Description |
|---|---|
| 200 | Empty list (no capabilities available) |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.assist.invoke` permission) |

**Example 200 Response**:
```json
{
  "capabilities": []
}
```

### GET /api/v1/ai/admin/providers

Lists configured AI providers.

**Permission**: `ai.platform.manage`

**Responses**:

| Status | Description |
|---|---|
| 200 | List of providers (at this gate, only "disabled") |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.platform.manage` permission) |

**Example 200 Response**:
```json
[
  {
    "name": "disabled",
    "status": "disabled"
  }
]
```

### GET /api/v1/ai/admin/models

Lists available AI models.

**Permission**: `ai.platform.manage`

**Responses**:

| Status | Description |
|---|---|
| 200 | Empty list (no models configured) |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.platform.manage` permission) |

**Example 200 Response**:
```json
[]
```

### GET /api/v1/ai/admin/templates

Lists prompt templates.

**Permission**: `ai.platform.manage`

**Responses**:

| Status | Description |
|---|---|
| 200 | Empty list (no templates configured) |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.platform.manage` permission) |

**Example 200 Response**:
```json
[]
```

### GET /api/v1/ai/admin/usage

Returns usage summaries.

**Permission**: `ai.platform.manage`

**Responses**:

| Status | Description |
|---|---|
| 200 | Zeroed usage summary (no persistence at this gate) |
| 401 | Authentication required |
| 403 | Forbidden (missing `ai.platform.manage` permission) |

**Example 200 Response**:
```json
{
  "totalRequests": 0,
  "totalTokens": 0
}
```

## Error Responses

All error responses follow the Problem Details format:

```json
{
  "title": "string",
  "status": 400
}
```

Validation errors include an `errors` array:

```json
{
  "title": "Validation failed.",
  "status": 400,
  "errors": [
    {
      "propertyName": "Input",
      "errorMessage": "Input is required."
    }
  ]
}
```

## Rate Limiting

No rate limiting is implemented at this gate. Future provider activation may
introduce per-provider rate limiting.

## Versioning

API versioning follows the established CommunityOS convention:
- Default version: 1.0
- Version reported in response headers
- Version substituted in URL path
