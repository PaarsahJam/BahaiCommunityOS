# AI Platform Runbook

## Overview

The AI Platform is a stateless service that provides a provider-neutral
abstraction for AI-assisted capabilities. At this implementation gate, the
platform ships with a disabled/no-op provider — no external AI provider is
enabled.

## Service Architecture

- **Type**: ASP.NET Core Web API
- **State**: Stateless (no database, no persistence)
- **Events**: Zero integration events
- **Provider**: Disabled (no-op, fail-visible)

## Configuration

### JWT Configuration

```json
{
  "Jwt": {
    "Authority": "https://identity.example.com",
    "Issuer": "https://identity.example.com",
    "Audience": "ai-platform",
    "RequireHttpsMetadata": true
  }
}
```

### Logging

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" }
    ]
  }
}
```

## Permissions

| Permission | Purpose | Grant |
|---|---|---|
| `ai.assist.invoke` | Invoke AI assistance | GlobalAdministrator, NationalAdministrator, LocalAdministrator, CommitteeMember, Member |
| `ai.platform.manage` | Administer AI platform | GlobalAdministrator, NationalAdministrator |

## Deployment

### Prerequisites

1. JWT authority must be configured and accessible.
2. The service must be deployed behind an HTTPS reverse proxy.
3. No database is required (stateless).

### Health Check

No health endpoint is implemented at this gate. The service is healthy if it
responds to HTTP requests on the configured port.

### Scaling

The service can be horizontally scaled because it is stateless. No session
affinity is required.

## Troubleshooting

### 503 Service Unavailable

**Cause**: AI provider is disabled (expected at this gate).

**Resolution**: This is the expected behavior. The AI Platform ships with a
disabled provider. External provider activation requires:
1. Import of the authoritative data-classification matrix (OQ-3 resolution).
2. Concrete provider selection (OQ-8 resolution).
3. A future governance amendment.

### 401 Unauthorized

**Cause**: Missing or invalid JWT.

**Resolution**: Ensure the JWT is valid, contains a `sub` claim, and is signed
by the configured authority.

### 403 Forbidden

**Cause**: Missing required permission.

**Resolution**: Ensure the user has the required permission (`ai.assist.invoke`
or `ai.platform.manage`).

### 400 Bad Request

**Cause**: Validation error or unsupported capability.

**Resolution**: Check the request body and capability parameter.

## Monitoring

### Operational Metadata (Logged)

- Correlation IDs
- Subject IDs
- Capability names
- Outcome codes
- Provider status
- Timing/diagnostic metadata

### Prohibited from Logging

- Prompts or completions
- Generated content
- User-entered AI input
- Credentials
- Provider secrets
- Sensitive content

## Future Operations

When external providers are activated (future gate):
- Monitor provider API usage and costs.
- Monitor provider health and availability.
- Review rate-limiting configuration.
- Audit data-classification compliance.
