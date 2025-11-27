# Auditing Module

The Auditing module centralizes the capture and querying of security events, exceptions, and general audit activity across the platform.

Namespace root: `FSH.Modules.Auditing`  
Implementation: `src/Modules/Auditing/Modules.Auditing`  
Contracts: `src/Modules/Auditing/Modules.Auditing.Contracts`

---

## Responsibilities

- Provide a structured audit pipeline with:
  - `IAuditClient` for writing events.
  - `IAuditSink` implementations for persistence and other outputs.
  - `IAuditScope` for contextual audit information.
- Persist audit records via `AuditDbContext`.
- Expose HTTP endpoints for querying audits.
- Provide `ISecurityAudit` for security-focused events (login, tokens).
- Integrate with:
  - Global exception handling.
  - Identity module (security events).
  - Observability (traces and correlation IDs).

---

## Architecture

### AuditingModule

File: `src/Modules/Auditing/Modules.Auditing/AuditingModule.cs`

Implements `IModule`:

- **ConfigureServices**:
  - Registers:
    - `AuditDbContext` with EF Core via building-block persistence.
    - `IAuditClient` and supporting services:
      - `IAuditSerializer`
      - `IAuditSink` implementations.
      - `IAuditMaskingService`, `IAuditEnricher`, `IAuditMutatingEnricher`.
    - `ISecurityAudit` implemented by `SecurityAudit` (writes security events via `IAuditClient`).
    - Health checks for `AuditDbContext` (if configured).
  - Enables cross-cutting integration:
    - HTTP pipeline can inject audit scopes.
    - Exception handling middleware can log exceptions via `IAuditClient`.

- **MapEndpoints**:
  - Defines `api/v1/auditing` route group.
  - Maps Minimal API endpoints for:
    - `GetAudits`
    - `GetAuditById`
    - `GetSecurityAudits`
    - `GetExceptionAudits`
    - `GetAuditsByCorrelation`
    - `GetAuditsByTrace`
    - `GetAuditSummary`
  - Endpoints rely on Mediator query handlers in `Features/v1`.

---

## Contracts & DTOs

Path: `src/Modules/Auditing/Modules.Auditing.Contracts`

Key abstractions:

- `IAuditClient` – main entry point for writing audits.
- `IAuditSink` – output target (DB, external system).
- `IAuditSerializer` – convert events and metadata to JSON or other formats.
- `IAuditScope` – ambient context (user, tenant, correlation, trace).
- `IAuditEnricher` / `IAuditMutatingEnricher` – enrich audit events with additional metadata.
- `IAuditMaskingService` – mask sensitive data.
- `ISecurityAudit` – high-level API for login/token-related events.
- `AuditEnvelope` – canonical representation of an audit event (action, subject, tenant, correlation ID, payload).
- `SecurityEventPayload`, `ExceptionEventPayload`, `ActivityEventPayload`, `EntityChangeEventPayload`.
- `AuditEnums`:
  - `SecurityAction` (e.g., `LoginSucceeded`, `LoginFailed`, `TokenIssued`, `TokenRevoked`).
  - `AuditSeverity` (Information, Warning, Error, Critical, etc).

Query contracts (v1):

Located under `src/Modules/Auditing/Modules.Auditing.Contracts/v1`:

- `GetAuditsQuery`
- `GetAuditByIdQuery`
- `GetSecurityAuditsQuery`
- `GetExceptionAuditsQuery`
- `GetAuditsByTraceQuery`
- `GetAuditsByCorrelationQuery`
- `GetAuditSummaryQuery`

Each is a Mediator query with a corresponding handler in the module implementation.

---

## Persistence

Path: `src/Modules/Auditing/Modules.Auditing/Persistence`

### AuditDbContext

- EF Core DbContext to store audit records.
- Entity types mirror `AuditEnvelope` structure with per-event payload fields.
- Multi-tenancy support:
  - Typically, a tenant field is stored to separate audit data per tenant.

Db initialization:

- `IDbInitializer` implementation seeds necessary structures (if any).
- Multitenancy integration ensures audits are stored under the current tenant context.

---

## Endpoints

Endpoints live under `src/Modules/Auditing/Modules.Auditing/Features/v1` and follow the pattern:

- Contracts (queries).
- Handlers (Mediator).
- Minimal API mapping.

Example patterns:

- `GET /api/v1/auditing/security` – query security audits (`GetSecurityAuditsQuery`).
- `GET /api/v1/auditing/exceptions` – query exception audits.
- `GET /api/v1/auditing/{id}` – fetch specific audit record.
- `GET /api/v1/auditing/by-trace/{traceId}` – correlate by trace/activity IDs.

All endpoints:

- Use `IMediator` to execute queries.
- Are protected via permissions or roles as required (typically reserved for admins/ops).

---

## Security Audit Integration

Implementation: `src/Modules/Auditing/Modules.Auditing/Core/SecurityAudit.cs`

`SecurityAudit` implements `ISecurityAudit`:

- Methods:

  ```csharp
  ValueTask LoginSucceededAsync(...);
  ValueTask LoginFailedAsync(...);
  ValueTask TokenIssuedAsync(...);
  ValueTask TokenRevokedAsync(...);
  ```

- Implementation routes events to `IAuditClient.WriteSecurityAsync` with:
  - `SecurityAction` enum.
  - Subject ID (user id or email).
  - Client ID.
  - Auth method (e.g., `"Password"`).
  - Reason codes.
  - Claims / extra dictionary payload (e.g., IP, UserAgent, token fingerprint, expiry).

Identity module:

- Uses `ISecurityAudit` in:
  - `GenerateTokenCommandHandler`:
    - `LoginSucceededAsync` / `LoginFailedAsync`.
    - `TokenIssuedAsync` with short SHA-256 fingerprint of access token.
  - `RefreshTokenCommandHandler`:
    - `TokenRevokedAsync` for invalid tokens, subject mismatch, and rotation.
    - `TokenIssuedAsync` for newly issued access tokens.

This provides a **full token lifecycle** audit trail.

---

## Exception & HTTP Audits

Global web exception middleware (from BuildingBlocks.Web) can:

- Capture unhandled exceptions.
- Classify severity using `ExceptionSeverityClassifier`.
- Write event via `IAuditClient` using `ExceptionEventPayload`.

HTTP pipeline integration:

- Selected requests can be wrapped in an `IAuditScope` capturing:
  - User.
  - Tenant.
  - Correlation ID.
  - Trace ID.
- Activity events (`ActivityEventPayload`) can capture high-level operations for observability.

This makes the Auditing module the **canonical source** for operational and security events.

---

## Usage from Other Modules

Typical integration pattern:

1. Inject `ISecurityAudit` or `IAuditClient`.
2. Call the appropriate method in relevant flows.

Example (security audit from Identity):

```csharp
await _securityAudit.LoginFailedAsync(
    subjectIdOrName: request.Email,
    clientId: clientId!,
    reason: "InvalidCredentials",
    ip: ip,
    ct: cancellationToken);
```

Example (token issued):

```csharp
var fingerprint = Sha256Short(token.AccessToken);
await _securityAudit.TokenIssuedAsync(
    userId: subject,
    userName: userName,
    clientId: clientId!,
    tokenFingerprint: fingerprint,
    expiresUtc: token.AccessTokenExpiresAt,
    ct: cancellationToken);
```

---

## Gaps & Potential Improvements

Potential enhancements for the Auditing module:

- **Queryable schemas**:
  - Provide more advanced filtering options (e.g., free-text search over payloads, index tuning).
- **Redaction strategies**:
  - Expand `IAuditMaskingService` with configurable strategies (per field, per module).
- **Retention policies**:
  - Automatically purge or archive audit records older than configured thresholds.
- **Outbox/inbox patterns**:
  - Optionally persist audits to an outbox for guaranteed delivery to external systems.
- **OpenTelemetry integration**:
  - Enrich audit events with OTel trace/span IDs systematically (some correlation already exists but can be deepened).

Even as-is, the Auditing module gives a robust base for compliance and security observability in multi-tenant apps.

