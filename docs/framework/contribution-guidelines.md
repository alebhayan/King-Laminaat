# Contribution & Coding Guidelines

This document is for contributors and AI agents working in this repo. It defines how to structure code, where to put things, and which patterns to follow.

Use this together with:

- `architecture.md`
- `building-blocks.md`
- `developer-cookbook.md`

The guiding principle: **respect the existing patterns and keep modules cohesive and vertical.**

---

## 1. Folder & Project Layout

- **BuildingBlocks** (`src/BuildingBlocks`)
  - Cross-cutting infrastructure (Core, Persistence, Web, Caching, Mailing, Jobs, Storage, etc.).
  - Do **not** put domain-specific logic here.
- **Modules** (`src/Modules`)
  - Each business capability is a module (Identity, Auditing, Multitenancy, …).
  - Each module has:
    - Implementation project: `Modules.<Name>`
    - Contracts project: `Modules.<Name>.Contracts`
    - Optional `.Web` project for UI-specific endpoints.
- **Playground** (`src/Playground`)
  - Example applications (API, Blazor, AppHost, Migrations).
  - Treat them as consumers of the framework, not as a dumping ground for shared logic.

When adding new functionality:

- Put shared infra in **BuildingBlocks** only if truly cross-cutting and generic.
- Put domain-specific code in a **Module**.

---

## 2. When to Add a Module vs. a Feature

Add a **new module** when:

- You introduce a distinct bounded context or capability:
  - E.g., Catalog, Billing, Notifications.
- It has its own:
  - DbContext or persistence concerns.
  - Public API surface.
  - Internal services.

Add a **new feature** inside an existing module when:

- It belongs to an existing bounded context:
  - E.g., new user operation → Identity.
  - New audit query → Auditing.
  - New tenant admin action → Multitenancy.

Do **not** mix unrelated domain concerns into existing modules just because they’re convenient.

---

## 3. Patterns to Follow (Strongly Recommended)

### Minimal APIs + Mediator

- Endpoints should be Minimal APIs (no MVC controllers).
- Each endpoint:
  - Lives in a static class under `Features/v1/<Area>/<FeatureName>`.
  - Has a `MapXyzEndpoint(this IEndpointRouteBuilder)` extension.
  - Uses `IMediator` to dispatch command/query.
- Never put business logic in the endpoint delegate; it should just translate HTTP → Mediator.

### Commands, Queries, and DTOs in Contracts

- Place public-facing contracts in `Modules.<Name>.Contracts`:
  - `v1/<Area>/<Feature>/<Command|Query>.cs`
  - DTOs (`*Request`, `*Response`, `*Dto`) that cross module boundaries.
- Use Mediator abstractions:
  - `ICommand<TResponse>`
  - `IQuery<TResponse>`

### Handlers in Implementation Projects

- Handlers for commands/queries live in `Modules.<Name>/Features/v1/...`.
- Naming:
  - `SomethingCommandHandler`
  - `SomethingQueryHandler`
- Handlers should:
  - Use domain/persistence services.
  - Be small and focused.
  - Rely on specs and repositories/DbContexts instead of inline complex LINQ.

### FluentValidation for Input

- Each command/query that accepts external input should have a corresponding validator.
- Validators live next to handlers in feature folders:
  - `SomethingCommandValidator`.
- Do not put validation logic inside handlers – keep it in validators so `ValidationBehavior` can enforce it.

### DDD + Specifications

- Use DDD concepts from Core:
  - Entities and aggregates raise domain events for side effects.
  - Domain events are dispatched via Persistence interceptors and Mediator.
- For read models and queries:
  - Use specifications where queries are complex or reused.
  - Prefer `Spec` classes over scattering LINQ everywhere.

---

## 4. Coding Style & Naming

General rules:

- Use **PascalCase** for types and methods, **camelCase** for locals and parameters.
- Prefer descriptive names over abbreviations.
- Avoid one-letter variable names (except simple loops).
- Keep handlers and endpoints concise; extract complex logic to services.

Specific conventions:

- Modules:
  - Project names: `Modules.<Name>`, `Modules.<Name>.Contracts`.
  - Root namespaces match project names.
- Features:
  - Folder: `Features/v1/<Area>/<FeatureName>`.
  - File names: `<FeatureName>Endpoint.cs`, `<FeatureName>CommandHandler.cs`, `<FeatureName>CommandValidator.cs`.
- DTOs:
  - Suffix with `Dto`, `Request`, or `Response` as appropriate.

Comments:

- Use XML comments for public contracts and options where helpful.
- Avoid inline comments in implementation code unless necessary for clarity.

---

## 5. Exceptions & Error Handling

- Throw domain/application exceptions from Core:
  - `NotFoundException` when a resource doesn’t exist.
  - `UnauthorizedException` for authentication/authorization issues.
  - `CustomException` for business rule violations.
- Do **not** return ad-hoc error objects from handlers.
- Rely on global exception middleware (BuildingBlocks.Web) to:
  - Map exceptions to HTTP status codes.
  - Emit `ProblemDetails`.
  - Log and audit exceptions when needed.

When in doubt:

- Look at existing handlers in Identity and Multitenancy for how they handle errors.

---

## 6. Persistence & DbContexts

- Use `AddHeroDbContext<TContext>()` to register DbContexts; do not call `AddDbContext` directly unless there is a specific reason.
- Apply multi-tenancy via Finbuckle when appropriate:
  - Mark entities with `.IsMultiTenant()` in EF configurations.
  - Include `TenantId` where needed.
- Use `IDbInitializer` implementations for seeding (per module).

Never:

- Hardcode connection strings.
- Bypass DbContexts to talk directly to the database (no raw ADO unless absolutely required and well-justified).

---

## 7. Security & Auth

- Use JWT auth wired through BuildingBlocks.Web.
- In modules:
  - Use `RequirePermission(...)` for endpoints needing specific rights (see Identity permission constants).
  - Use `ICurrentUser` for accessing current user identity, not `HttpContext.User` directly unless required.
- For tokens:
  - Use `ITokenService` and `IIdentityService` flows.
  - Do not generate tokens manually in handlers.

When adding security-related features:

- Integrate with `ISecurityAudit` to record important events (logins, token changes, data access if sensitive).

---

## 8. Multitenancy

- When working in tenant-aware modules:
  - Use `IMultiTenantContextAccessor<AppTenantInfo>` to ensure tenant context is present and valid.
  - Enforce tenant validity (`IsActive`, `ValidUpto`) where appropriate.
- For new entities:
  - Include `TenantId` if they are tenant-bound.
  - Mark EF configurations with `.IsMultiTenant()`.

Do not:

- Skip tenant checks in handlers that operate on tenant-specific data.

---

## 9. Caching, Mailing, Storage, Jobs

Always use abstractions:

- Caching:
  - `ICacheService` from BuildingBlocks.Caching.
- Mailing:
  - `IMailService` from BuildingBlocks.Mailing.
- Storage:
  - `IStorageService` from BuildingBlocks.Storage.
- Jobs:
  - Hangfire integration via BuildingBlocks.Jobs (e.g., `RecurringJob.*`) and DI-backed jobs.

Do not:

- Bypass these abstractions with direct Redis calls, raw SMTP clients, or filesystem operations in modules.

---

## 10. Logging, Auditing & Observability

- Use `ILogger<T>` for logging in services/handlers.
- Use `ISecurityAudit` / `IAuditClient` for:
  - Security-sensitive events (Logins, Tokens, Admin operations).
  - Exceptions (through global exception pipeline).
- For traces:
  - Use `ActivitySource` when adding custom spans.

Avoid:

- Writing logs that contain secrets or raw tokens.
- Logging entire request bodies with sensitive data.

---

## 11. Testing & Validation of Changes

When adding new features:

- Add or update tests in `src/Tests` (follow existing test patterns):
  - Unit tests for handlers/services.
  - Integration tests for important flows where feasible.
- Run:
  - `dotnet build src/FSH.Framework.slnx`
  - And relevant test projects.

For AI agents:

- Prefer to at least compile the solution after non-trivial changes.
- When unable to run tests, reason carefully about the correctness and point out any assumptions.

---

## 12. AI-Agent Specific Notes

For Codex or other AI agents:

- Before editing, **scan relevant docs** under `docs/framework` and the nearby code:
  - Architecture, module docs, developer-cookbook, and this file.
- Use **existing features as templates**:
  - Identity token endpoints.
  - Auditing queries.
  - Multitenancy endpoints.
- Avoid:
  - Introducing new competing patterns (e.g., controllers instead of Minimal APIs).
  - Reorganizing folders or renaming existing types unless explicitly requested.
- Keep diffs focused:
  - Only modify files necessary for the requested change.
  - Don’t refactor unrelated code opportunistically.

Following these guidelines will keep the codebase coherent and predictable, and make it easier for both humans and agents to collaborate effectively.

