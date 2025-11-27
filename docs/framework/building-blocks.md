# Building Blocks

This document describes the **BuildingBlocks** projects that make up the reusable framework foundation: Core, Persistence, Caching, Mailing, Jobs, Storage, Eventing, and Web.

Root: `src/BuildingBlocks`

---

## Core

Path: `src/BuildingBlocks/Core`

Purpose:

- Provide shared abstractions and domain primitives used across modules.
- Centralize exception types and context abstractions.

Key areas:

- `Abstractions/`
  - Common interfaces and base types (e.g., domain entity base, aggregate root, domain events).
- `Domain/`
  - Domain events, event dispatcher contracts.
- `Context/`
  - Abstractions for current user, tenant, correlation ID.
- `Exceptions/`
  - Typed exceptions (e.g., `NotFoundException`, `UnauthorizedException`, `CustomException`).
- `Common/`
  - Helper utilities used across modules.

Design:

- Exceptions from this layer are caught and turned into standardized HTTP responses by Web’s exception middleware.
- Domain events integrate with Persistence interceptors and Mediator.

---

## Persistence

Path: `src/BuildingBlocks/Persistence`

Purpose:

- Encapsulate EF Core setup and conventions.
- Provide pagination and specification patterns.
- Offer services for DB initialization and connection validation.

Key files & folders:

- `Context/`
  - Shared context abstractions and base classes for EF Core DbContexts.
- `Inteceptors/DomainEventsInterceptor.cs`
  - EF Core SaveChanges interceptor that:
    - Collects domain events from entities (Core layer).
    - Dispatches them via Mediator after persistence succeeds.
- `Pagination/`
  - Request/response models and helpers for paging.
  - Standard pattern for returning paged results from queries.
- `Specifications/`
  - Base specification pattern:
    - Encapsulates query predicates, includes, sorting, and paging.
  - Encourages reuse and separation of query definitions from handlers.
- `ConnectionStringValidator.cs`
  - Validates DB connection strings at startup.
- `OptionsBuilderExtensions.cs`, `Extensions.cs`
  - `AddHeroDbContext<TContext>()` to:
    - Register DbContexts with DI.
    - Wire interceptors (domain events).
    - Apply multi-tenancy conventions.
- `IDbInitializer.cs`
  - Contract for modules to implement seeding logic (e.g., `IdentityDbInitializer`).

Design notes:

- Persistence is strongly aligned with DDD and Mediator:
  - Entities raise domain events.
  - Interceptors ensure events are dispatched after successful transactions.
  - Specifications centralize queries for reusability.

---

## Caching

Path: `src/BuildingBlocks/Caching`

Purpose:

- Provide an abstraction over caching (in-memory or distributed) with a default distributed cache implementation.

Key types:

- `ICacheService`
  - Basic operations:
    - Get/Set/Remove.
    - Supports generics and expirations.
- `DistributedCacheService`
  - Implementation on top of `IDistributedCache`.
  - Handles serialization/deserialization.
- `CachingOptions`
  - Configured via `CachingOptions__Redis` and other settings.
- `Extensions.cs`
  - `AddCaching()` to:
    - Bind `CachingOptions`.
    - Configure `IDistributedCache` (e.g., StackExchange Redis).
    - Register `ICacheService`.

Usage:

- Modules like Identity and Multitenancy can use `ICacheService` to:
  - Cache tenant info.
  - Cache permissions.
  - Cache frequently accessed data.

---

## Mailing

Path: `src/BuildingBlocks/Mailing`

Purpose:

- Provide a standard way to send emails, abstracting underlying providers.

Key types:

- `MailOptions`
  - SMTP/SendGrid-like configuration.
- `MailRequest`
  - DTO representing:
    - From, To, CC/BCC.
    - Subject, Body.
    - Attachments.
- `Services/`
  - `IMailService` and implementation(s).
  - Integration with background jobs where appropriate.
- `Extensions.cs`
  - `AddMailing()` wires configuration and `IMailService`.

Usage:

- Identity module uses `IMailService` for:
  - Email confirmation.
  - Password reset.

---

## Jobs

Path: `src/BuildingBlocks/Jobs`

Purpose:

- Provide background processing via Hangfire, integrated with DI and logging.

Key types:

- `Extensions.cs`
  - `AddJobs()`:
    - Configures Hangfire storage (e.g., SQL, Postgres).
    - Registers Hangfire server and dashboard.
    - Applies options from `HangfireOptions`.
- `FshJobActivator`
  - Custom job activator that resolves jobs from DI container.
- `FshJobFilter`, `LogJobFilter`
  - Hangfire filters for logging job states and errors.
- `HangfireCustomBasicAuthenticationFilter`
  - Basic auth for securing Hangfire dashboard.
- `HangfireOptions`
  - Allows configuring dashboard path, credentials, etc.

Usage:

- Modules can schedule jobs for:
  - Email sending.
  - Maintenance tasks.
  - Data processing.

---

## Storage

Path: `src/BuildingBlocks/Storage`

Purpose:

- Abstract file storage operations with a default local implementation.

Key types:

- `FileType`
  - Enum/struct describing file categories (e.g., Image, Document).
- `DTOs/`
  - Request and response types for uploaded files.
- `Services/IStorageService`
  - CRUD operations on file objects:
    - Upload, Download, Delete.
- `Local/LocalStorageService`
  - Stores files on local filesystem under configured root.
- `Extensions.cs`
  - `AddStorage()` to register an `IStorageService` implementation.

Usage:

- Identity module uses `IStorageService` to handle user profile images.
- Other modules can store attachments or binary data without coupling to physical storage details.

---

## Eventing

Path: `src/BuildingBlocks/Eventing`

Purpose:

- Provide a reusable abstraction for integration events and event-driven communication between modules/services.

Key components:

- Abstractions (`Abstractions/`)
  - `IIntegrationEvent`
    - Base contract for all integration events.
    - Includes `Id`, `OccurredOnUtc`, `TenantId`, `CorrelationId`, `Source`.
  - `IEventBus`
    - Interface for publishing integration events.
    - Initial implementation: in-memory bus for single-process apps.
  - `IIntegrationEventHandler<TEvent>`
    - Interface for handlers of integration events.
  - `IEventSerializer`
    - Abstraction for serializing events to and from JSON.

- Outbox (`Outbox/`)
  - `OutboxMessage`
    - EF entity representing a persisted integration event.
    - Tracks type, payload, tenant, correlation, retries, and dead-letter status.
  - `IOutboxStore`
    - Abstraction for adding and reading outbox messages.
  - `EfCoreOutboxStore<TDbContext>`
    - Generic EF Core implementation of `IOutboxStore`.
  - `OutboxDispatcher`
    - Service that reads pending outbox messages, deserializes them, publishes via `IEventBus`, and marks them processed or dead.
    - Intended to be run by a scheduler (e.g., Hangfire recurring job).

- Inbox (`Inbox/`)
  - `InboxMessage`
    - EF entity to track processed events per handler for idempotent consumers.
  - `IInboxStore`
    - Abstraction to check/mark events as processed.
  - `EfCoreInboxStore<TDbContext>`
    - Generic EF Core implementation of `IInboxStore`.

- In-memory bus (`InMemory/`)
  - `InMemoryEventBus`
    - Implementation of `IEventBus` for single-process deployments.
    - Resolves `IIntegrationEventHandler<T>` from DI and optionally uses `IInboxStore` for idempotency.

- Serialization (`Serialization/`)
  - `JsonEventSerializer`
    - Implementation of `IEventSerializer` using `System.Text.Json`.

- Configuration (`EventingOptions`, `ServiceCollectionExtensions`)
  - `EventingOptions`
    - `Provider` (currently `"InMemory"`).
    - `OutboxBatchSize`, `OutboxMaxRetries`, `EnableInbox`.
  - `AddEventingCore(IConfiguration)`
    - Registers eventing options, serializer, and `IEventBus`.
  - `AddEventingForDbContext<TDbContext>()`
    - Registers EF-based outbox and inbox stores and `OutboxDispatcher` for a DbContext.
  - `AddIntegrationEventHandlers(Assembly[])`
    - Scans assemblies for `IIntegrationEventHandler<TEvent>` and registers them.

Usage:

- Modules define integration events in their Contracts projects by implementing `IIntegrationEvent`.
- Modules that want to publish events:
  - Inject `IOutboxStore` and call `AddAsync` inside the same transaction as domain changes.
  - A scheduler (e.g., Hangfire) invokes `OutboxDispatcher.DispatchAsync()` to deliver events.
- Modules that want to react to events:
  - Implement `IIntegrationEventHandler<TEvent>` in their implementation project.
  - Register handlers via `AddIntegrationEventHandlers`.
  - Receive events via the in-memory bus in single-process deployments, or via external bus integrations in the future.

---

## Web

Path: `src/BuildingBlocks/Web`

Purpose:

- Provide opinionated web host configuration, wiring together all cross-cutting concerns:
  - Modules.
  - Auth & security.
  - CORS.
  - Exception handling.
  - Health checks.
  - Mediator.
  - Observability (OpenTelemetry).
  - OpenAPI.
  - Rate limiting.
  - Versioning.

Key entrypoints:

- `Extensions.cs`
  - `AddHeroPlatform(Action<HeroPlatformOptions> configure)`
    - Enables/disables features:
      - CORS.
      - OpenAPI.
      - Caching.
      - Mailing.
      - Jobs.
    - Registers necessary services in DI:
      - Auth.
      - Health checks.
      - Observability.
      - Rate limiting.
  - `UseHeroPlatform(Action<HeroPlatformPipelineOptions> configure)`
    - Adds middleware:
      - Exception handling.
      - Authentication/Authorization.
      - CORS.
      - Health check endpoints.
      - Swagger/OpenAPI.
      - Rate limiting.
      - Request logging.
    - Optionally maps module endpoints (`MapModules = true`).
  - `AddModules(Assembly[] moduleAssemblies)`
    - Delegates to the module loader.

### Modules Infrastructure

Path: `src/BuildingBlocks/Web/Modules`

- `IModule` – contract implemented by all modules.
- `IModuleConstants` – module-specific constants (schema names, route names).
- `ModuleLoader` – scans assemblies for `IModule` implementations:
  - Calls `ConfigureServices` during startup.
  - Later calls `MapEndpoints` during `UseHeroPlatform`.

### Mediator Integration

Path: `src/BuildingBlocks/Web/Mediator`

- `Extensions.cs`:
  - `EnableMediator(IServiceCollection services, params Assembly[] featureAssemblies)`
  - Adds Mediator and registers pipeline behaviors (e.g., `ValidationBehavior`).
- `Mediator/Behaviors/ValidationBehavior.cs`:
  - Runs FluentValidation validators before executing handlers.

### Auth & Security

Paths:

- `Auth/`
- `Security/`

Responsibilities:

- Configure JWT bearer authentication:
  - Bind `JwtOptions` from configuration.
  - Add authentication scheme.
- Register authorization policies based on permissions.
- `PathAwareAuthorizationHandler`:
  - Custom `IAuthorizationMiddlewareResultHandler` that can adjust behavior depending on request path (e.g., returning 401 vs 403, redirect logic).

### CORS

Path: `Cors/`

- Configures CORS based on app configuration.
- Typically allows:
  - SPA origins.
  - Preflight requests.

### Exceptions

Path: `Exceptions/`

- Global exception handling middleware:
  - Translates domain and application exceptions into standardized `ProblemDetails`.
  - Integrates with logging and Auditing module (exception events).

### Health

Path: `Health/`

- Configures health checks:
  - `/health` – general status.
  - `/health/ready` – readiness probes.
  - `/health/live` – liveness probes.
- Aggregates module-specific checks (e.g., DB checks, `TenantMigrationsHealthCheck`).

### Observability (OpenTelemetry)

Path: `Observability/`

- Configures OpenTelemetry for:
  - Traces.
  - Metrics.
  - Logs (if enabled).
- Exporter configuration driven by `OpenTelemetryOptions`:
  - OTLP endpoint, protocol, enable/disable flags.

### OpenAPI

Path: `OpenApi/`

- Configures Swagger/NSwag:
  - Generates OpenAPI docs per API version.
  - Adds security definitions (JWT bearer).
  - Tags endpoints by module.

### Rate Limiting

Path: `RateLimiting/`

- Defines ASP.NET rate limiting policies:
  - e.g., `"auth"` for token endpoints.
  - `"default"` for other endpoints.
- Configured via `RateLimitingOptions` in configuration.

### Versioning

Path: `Versioning/`

- Integrates `Asp.Versioning`:
  - Adds API versioning services.
  - Supports `api/v{version:apiVersion}` route patterns.
  - Provides version reporting in responses.

---

## Coding Standards & Patterns

From the building blocks, the following patterns are encouraged:

- **Vertical slices by module**:
  - Features organized as `Features/v1/<FeatureName>` with:
    - Command/Query contracts.
    - Handlers.
    - Validators.
    - Minimal API endpoints.
- **Mediator** for all business operations:
  - Controllers are replaced with minimal endpoints that delegate to Mediator commands/queries.
- **FluentValidation** in front of handlers:
  - All user input should be validated via dedicated validator classes.
- **DDD + Specifications**:
  - Entities raise domain events.
  - Queries use specification objects rather than raw LINQ sprinkled across handlers.
- **Cross-cutting concerns centralized**:
  - Exceptions, logging, auth, health, and rate limiting are configured once in Web and reused by modules.

---

## Gaps & Potential Improvements

Potential enhancements in the BuildingBlocks layer:

- **More opinionated repositories**:
  - Provide generic repository abstractions built directly on specifications and pagination.
- **Extended options models**:
  - For each feature (e.g., CORS, OpenAPI, RateLimiting), provide strongly-typed options with comprehensive XML docs and example appsettings sections.
- **Additional pipeline behaviors**:
  - Add Mediator behaviors for:
    - Caching results.
    - Idempotency.
    - Retry.
- **Stronger validation integration**:
  - Enforce that every command/query has a matching validator in CI.
- **Configuration analyzers**:
  - Provide Roslyn analyzers or startup checks that warn when expected options (e.g., JwtOptions.SigningKey) are missing or insecure.

Despite these opportunities, the building blocks are already cohesive and modular, enabling you to quickly stand up robust, multi-tenant APIs with rich cross-cutting behaviors.
