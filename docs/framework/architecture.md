# FSH Framework Architecture

This document explains the architecture of the `fullstackhero` .NET 10 starter kit, focusing on the **framework** layer (BuildingBlocks) and the **modular application** layer (Modules). It also highlights how these pieces are composed in `FSH.Playground.Api`.

> The goal is to treat this repo as a reusable platform you can drop into any .NET 10 Web API and then light up identity, multitenancy, auditing, caching, jobs, storage, and observability with minimal wiring.

---

## High-Level Structure

Solution root: `src/`

- `BuildingBlocks/`
  - `Core/` – Domain + core abstractions (DDD primitives, exceptions, context)
  - `Persistence/` – EF Core integration, pagination, specifications, DB initializers, interceptors
  - `Caching/` – ICacheService abstraction with distributed cache implementation (Redis-ready)
  - `Mailing/` – Mail options, DTOs, and `IMailService`-based infrastructure
  - `Jobs/` – Background jobs via Hangfire (jobs, filters, host integration)
  - `Storage/` – File storage abstractions and local storage implementation
  - `Web/` – Web host wiring: auth, CORS, versioning, rate limiting, OpenAPI, Mediator, modules, observability, health, exception handling
- `Modules/`
  - `Auditing/` – Cross-cutting audit infrastructure and HTTP/exception/security auditing APIs
  - `Identity/` – ASP.NET Identity, JWT auth, user + role management, tokens, permissions
  - `Multitenancy/` – Tenant management, tenant-aware EF Core, migrations, and health checks
- `Playground/`
  - `Playground.Api/` – Example API host that composes the platform
  - `FSH.Playground.AppHost/` – Aspire-based distributed app host for Postgres, Redis, and the API
  - `Migrations.PostgreSQL/` – EF Core migrations for Postgres

The framework assumes:

- **Vertical modules** (Identity, Auditing, Multitenancy) that are self-contained.
- **Building blocks** that provide cross-cutting capabilities (persistence, caching, jobs, mailing, storage, web host primitives).
- A **host app** that glues everything together with a few extension methods.

---

## Modular Architecture

### IModule & Module Loading

Key types:

- `FSH.Framework.Web.Modules.IModule`  
  (`src/BuildingBlocks/Web/Modules/IModule.cs`)
- `FSH.Framework.Web.Modules.ModuleLoader`  
  (`src/BuildingBlocks/Web/Modules/ModuleLoader.cs`)

Each module implements:

```csharp
public interface IModule
{
    void ConfigureServices(IHostApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

Modules are discovered and loaded by the host:

- You pass module assemblies to `builder.AddModules(...)`.
- `ModuleLoader` finds all types implementing `IModule` and:
  - Calls `ConfigureServices` during app startup.
  - Calls `MapEndpoints` on a grouped route prefix (`api/v{version:apiVersion}/{module}`) when mapping endpoints.

Example modules:

- `IdentityModule` – `src/Modules/Identity/Modules.Identity/IdentityModule.cs`
- `MultitenancyModule` – `src/Modules/Multitenancy/Modules.Multitenancy/MultitenancyModule.cs`
- `AuditingModule` – `src/Modules/Auditing/Modules.Auditing/AuditingModule.cs`

This allows the host to remain thin while modules configure their own:

- DI registrations
- EF Core DbContexts
- Health checks
- Endpoints
- Metrics

---

## BuildingBlocks Overview

### Core

Path: `src/BuildingBlocks/Core`

Responsibilities:

- Shared **domain primitives** and **DDD-friendly abstractions**.
- Cross-cutting **exceptions** for consistent error handling.
- **Context** and identity abstractions (e.g., current user).

Highlights:

- `Exceptions/` – custom exception types used across modules.
- `Domain/` – domain events and base entity types (used with EF interceptors).
- `Context/` – abstractions for current tenant/user and correlation.

The core building block is intentionally thin but provides the foundational language for domain logic and error handling.

### Persistence

Path: `src/BuildingBlocks/Persistence`

Responsibilities:

- EF Core integration helpers.
- Multi-tenant DbContext setup.
- Pagination and specification pattern support.
- DB initializers and connection string validation.
- Domain event dispatch via interceptors.

Key files:

- `Extensions.cs` – wire-up extension methods such as `AddHeroDbContext<TContext>()`.
- `IConnectionStringValidator` & `ConnectionStringValidator` – validate DB connection settings early at startup.
- `IDbInitializer` – contract for seeding databases.
- `ModelBuilderExtensions` – apply conventions (e.g., multi-tenancy, auditable entities).
- `Inteceptors/DomainEventsInterceptor.cs` – intercepts EF changes and dispatches domain events via Mediator.
- `Pagination/` – request/response models and helpers for paged queries.
- `Specifications/` – base specification types that encapsulate query predicates, includes, sorting, and paging.

This block formalizes **DDD** in persistence:

- Entities raise domain events.
- Interceptors publish those events after EF `SaveChanges`.
- Query-side logic uses specifications instead of ad-hoc LINQ in handlers.

### Caching

Path: `src/BuildingBlocks/Caching`

Responsibilities:

- Abstract cache operations via `ICacheService`.
- Provide a distributed cache implementation (backed by Redis in Playground/AppHost).

Key types:

- `ICacheService` – get/set/remove over typed values with sliding/absolute expirations.
- `DistributedCacheService` – implementation wrapping `IDistributedCache`.
- `CachingOptions` – binds to configuration (`CachingOptions__Redis`, etc).
- `Extensions.cs` – `AddCaching()` wiring (e.g., `services.AddStackExchangeRedisCache`).

Used by:

- Identity module (e.g., user-related caching).
- Jobs and other modules for cross-request cache.

### Mailing

Path: `src/BuildingBlocks/Mailing`

Responsibilities:

- Abstract outbound email sending with environment-specific implementations.

Key types:

- `MailOptions` – SMTP/SendGrid/etc config.
- `MailRequest` – DTO describing `To`, `Subject`, `Body`, attachments.
- `Services/` – `IMailService` and default implementation wiring.
- `Extensions.cs` – `AddMailing()`, binding `MailOptions` and registering `IMailService`.

Used by:

- Identity (`UserService`) for e-mail confirmation and password reset.

### Jobs

Path: `src/BuildingBlocks/Jobs`

Responsibilities:

- Host-neutral background jobs with Hangfire.

Key types:

- `Extensions.cs` – `AddJobs()` sets up Hangfire server/dashboard based on `HangfireOptions`.
- `FshJobActivator` – integrates DI with Hangfire jobs.
- `FshJobFilter` & `LogJobFilter` – cross-cutting job filters for logging and error handling.
- `HangfireCustomBasicAuthenticationFilter` – protects Hangfire dashboard with basic auth.

Jobs are suitable for:

- Email sending.
- Data migration.
- Long-running maintenance tasks.

### Storage

Path: `src/BuildingBlocks/Storage`

Responsibilities:

- Abstract file storage (local and cloud-friendly).

Key types:

- `FileType` – classifies files (Images, Documents, etc).
- `DTOs/` – descriptors for stored files.
- `Services/IStorageService` – basic CRUD around binary objects.
- `Local/LocalStorageService` – default storage rooted in disk.
- `Extensions.cs` – `AddStorage()` for registering `IStorageService`.

Used by:

- Identity module for storing user profile images.

### Web

Path: `src/BuildingBlocks/Web`

Responsibilities:

- Opinionated configuration for:
  - Authentication and authorization.
  - CORS policy.
  - Exception handling and problem details.
  - Health checks.
  - Mediator pipeline.
  - Minimal API module discovery.
  - OpenAPI (Swashbuckle/NSwag).
  - Rate limiting (ASP.NET built-in).
  - Versioning (Asp.Versioning).
  - Security headers.
  - Observability (OpenTelemetry).

Key files:

- `Extensions.cs` – root `AddHeroPlatform` and `UseHeroPlatform` logic.
- `Auth/` – JWT auth setup, current user, and policy-based authorization.
- `Cors/` – CORS configuration from configuration.
- `Exceptions/` – global exception handling middleware and mapping to problem details.
- `Health/` – health checks endpoints and UI configuration.
- `Mediator/Extensions.cs` – `EnableMediator(...)` with `ValidationBehavior`.
- `Modules/` – `IModule`, `ModuleLoader`.
- `Observability/` – OpenTelemetry tracing/metrics/logging wiring.
- `OpenApi/` – swagger / OpenAPI docs.
- `Origin/` – origin/host based filtering.
- `RateLimiting/` – named policies like `"auth"`, `"default"`.
- `Security/` – cross-cutting security configuration.
- `Versioning/` – API versioning setup and grouping.

This is the **primary integration surface** between the host API and the building blocks.

---

## Modules Overview

Each module lives under `src/Modules/<Name>/Modules.<Name>`.

### Identity Module

Path: `src/Modules/Identity/Modules.Identity`

Responsibilities:

- ASP.NET Identity + EF Core user/role store.
- JWT authentication & token generation/refresh.
- User management (CRUD, roles, status).
- Permissions and role claims.
- Security audits for login/token events.

Key pieces:

- `IdentityModule.cs` – implements `IModule`:
  - Registers:
    - `ICurrentUser` + `ICurrentUserInitializer`.
    - `ITokenService` (JWT implementation).
    - `IIdentityService` (credentials + refresh-token validation).
    - `IUserService`, `IRoleService`.
    - `IdentityDbContext` via `AddHeroDbContext<IdentityDbContext>()`.
    - `IDbInitializer` for seeding default users/roles.
    - ASP.NET Identity with `FshUser` and `FshRole`.
    - Health checks for the Identity DB.
    - `IdentityMetrics` and `ConfigureJwtAuth()`.
  - Maps endpoints under `api/v1/identity`:
    - Tokens:
      - `POST /token` – `GenerateTokenEndpoint` (access + refresh, rate-limited `"auth"`).
      - `POST /token/refresh` – `RefreshTokenEndpoint` (refresh flow).
    - Roles: CRUD and permissions endpoints.
    - Users: registration, profile, password, status, role assignment, image upload.
- `Authorization/` – permission constants, JWT options, authorization handlers.
- `Data/IdentityDbContext.cs` – multi-tenant Identity DbContext.
- `Features/v1/Users` – endpoints and handlers for all user-related operations.
- `Features/v1/Tokens` – token issuance and refresh handlers.
- `Services/IdentityService.cs` – credential and refresh-token validation, user lookup.
- `Services/TokenService.cs` – JWT creation with configurable lifetimes and signing key.

Persistence:

- Uses EF Core with multi-tenancy (`MultiTenantIdentityDbContext<FshUser, FshRole, ...>`).
- `FshUser` includes `TenantId`, `IsActive`, `RefreshToken` (hashed), `RefreshTokenExpiryTime`.
- Data seeding via `IdentityDbInitializer`.

Security:

- Password policies defined in `IdentityModule.ConfigureServices`.
- Email confirmation required.
- Tenant validity (`ValidUpto`) checked on login/refresh.
- Audits login success/failure and token issuance/revocation.

### Auditing Module

Path: `src/Modules/Auditing/Modules.Auditing`

Responsibilities:

- Centralized audit pipeline for:
  - Security events (login, token issuance, revocation).
  - Exceptions.
  - Entity change events.
  - Activity/trace-level events.

Key pieces:

- `AuditingModule.cs` – implements `IModule`:
  - Registers:
    - `IAuditClient` and supporting infrastructure.
    - `ISecurityAudit` implementation.
    - `AuditDbContext` with migrations.
    - HTTP and exception audit sinks.
  - Maps endpoints (under `api/v1/auditing`) to query audits:
    - `GetAudits`, `GetAuditById`, `GetSecurityAudits`, `GetExceptionAudits`, etc.
- `Contracts` (`Modules.Auditing.Contracts`) – defines:
  - DTOs for audits.
  - `ISecurityAudit` (used by Identity).
  - `IAuditClient`, `IAuditSink`, `IAuditScope`, etc.
  - Query contracts using Mediator.
- `Persistence` – `AuditDbContext` and EF models.
- `Core` – audit composition, masking, enrichers.

Cross-cutting:

- Exception middleware in Web building block writes exceptions to this module via `IAuditClient`.
- Identity module writes security audits via `ISecurityAudit`.
- Activity IDs / traces can be correlated across services.

### Multitenancy Module

Path: `src/Modules/Multitenancy/Modules.Multitenancy`

Responsibilities:

- Tenant management and provisioning.
- Tenant-specific connection strings / DB providers.
- Tenant migrations orchestration.
- Health checks for tenant databases.

Key pieces:

- `MultitenancyModule.cs` – implements `IModule`:
  - Registers:
    - Tenant management services.
    - Finbuckle.MultiTenant integration.
    - Tenant-specific DbContexts.
    - `TenantMigrationsHealthCheck`.
  - Maps endpoints under `api/v1/multitenancy`:
    - `GetTenants`, `CreateTenant`, `ChangeTenantActivation`, `GetTenantStatus`, `UpgradeTenant`, `GetTenantMigrations`, etc.
- `Extensions.cs` – helpers to register multi-tenant DBs and services.
- `Services` – `ITenantService` and implementations.
- `Data` – tenant DbContext and EF configuration.
- `Web` (Modules.Multitenancy.Web) – UI/SPA-friendly endpoints where needed.

Persistence & Health:

- Uses EF Core with Finbuckle multi-tenancy per tenant DB.
- `TenantMigrationsHealthCheck` verifies migration status and DB connectivity.

---

## Cross-Cutting Concerns

### Mediator

Mediator library: `Mediator.Abstractions` (dotnet independent mediator).

- Configured in host via:

  ```csharp
  builder.Services.AddMediator(o =>
  {
      o.ServiceLifetime = ServiceLifetime.Scoped;
      o.Assemblies = [
          typeof(GenerateTokenCommand),
          typeof(GenerateTokenCommandHandler),
          typeof(GetTenantStatusQuery),
          typeof(GetTenantStatusQueryHandler),
          typeof(FSH.Modules.Auditing.Contracts.AuditEnvelope),
          typeof(FSH.Modules.Auditing.Persistence.AuditDbContext)];
  });
  ```

- Pipeline behavior:
  - `ValidationBehavior` in `src/BuildingBlocks/Web/Mediator/Behaviors/ValidationBehavior.cs`:
    - Runs FluentValidation validators before handlers.
    - Returns errors or lets handler execute.

Usage pattern:

- Commands/queries live in `Modules.*.Contracts` as `record`s.
- Handlers live in module implementation assembly.
- Minimal API endpoints call `IMediator` to send commands/queries.

### Fluent Validation

- Validators per command/query live alongside features:
  - Example: `TokenGenerationCommandValidator` in `src/Modules/Identity/Modules.Identity/Features/v1/Tokens/TokenGeneration/GenerateTokenCommandValidator.cs`.
  - Example: `RefreshTokenCommandValidator` in `src/Modules/Identity/Modules.Identity/Features/v1/Tokens/RefreshToken/RefreshTokenCommandValidator.cs`.
- Automatically picked up by Mediator validation behavior.

### Exception Handling

- Global exception handler in `src/BuildingBlocks/Web/Exceptions/`:
  - Catches custom exceptions from Core (e.g., `UnauthorizedException`, `NotFoundException`).
  - Maps them to `ProblemDetails` with appropriate HTTP codes.
  - Logs and emits audits via `IAuditClient`.

Key points:

- Domain-level exceptions bubble up from modules to central handler.
- HTTP responses are standardized across modules.

### DDD & Interceptors

DDD style:

- Entities in Core Domain raise domain events.
- EF Core `DomainEventsInterceptor` (`src/BuildingBlocks/Persistence/Inteceptors/DomainEventsInterceptor.cs`) listens to changes:
  - After `SaveChanges`, collects domain events.
  - Dispatches them via Mediator to relevant handlers.

Benefits:

- Side effects (e.g., notifications, further commands) are decoupled from core logic.
- Modules can subscribe to events without tight coupling.

### Paging & Specifications

Paging:

- Pagination classes in `src/BuildingBlocks/Persistence/Pagination/`.
- Common pattern:
  - Query DTO defines `PageNumber`, `PageSize`, filters.
  - Handler converts to specification and calls repository/DbContext with `ApplyPagination`.

Specifications:

- Base specification abstractions in `src/BuildingBlocks/Persistence/Specifications/`.
- Handlers:
  - Build specifications describing query conditions.
  - Reuse them across endpoints and services.

### Security, CORS, Rate Limiting, Versioning

Configured in `src/BuildingBlocks/Web`:

- **Security**:
  - JWT authentication (configured once, used by `Identity`).
  - Policy-based authorization with permission constants (from Identity).
  - Path-aware authorization handler for special cases (e.g., public endpoints).
- **CORS**:
  - Enabled via `builder.AddHeroPlatform(o => o.EnableCors = true);`.
  - Policies drawn from configuration (`CorsOptions`).
- **Rate Limiting**:
  - Named policies in `RateLimiting/`:
    - `"auth"` used to protect token endpoints from brute force.
    - `"default"` for general usage.
- **Versioning**:
  - API versioning via `Asp.Versioning`.
  - Modules define `api/v1/...` routes with `ApiVersionSet`.

### Observability (OpenTelemetry)

Path: `src/BuildingBlocks/Web/Observability`

Responsibilities:

- Configure OpenTelemetry:
  - Traces
  - Metrics
  - Logs
- Exporter configuration via `OpenTelemetryOptions` (OTLP exporter, etc).

Playground host:

- `FSH.Playground.AppHost/AppHost.cs` sets environment variables for the API project:

  - `OpenTelemetryOptions__Exporter__Otlp__Endpoint`
  - `OpenTelemetryOptions__Exporter__Otlp__Protocol`
  - `OpenTelemetryOptions__Exporter__Otlp__Enabled`

This enables first-class distributed tracing when running under Aspire.

### Health Checks

- Modules register DB health checks:
  - Example: Identity module:
    - `builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>(name: "db:identity", failureStatus: HealthStatus.Unhealthy);`
  - Multitenancy module:
    - `TenantMigrationsHealthCheck` monitors tenant DB migration status.
- Web building block maps health endpoints (`/health`, `/health/ready`, etc.) with appropriate tags.

### Logging

- Uses structured logging via `ILogger<T>`.
- Security and exception events are also pushed into Auditing module.
- OpenTelemetry logs integration is enabled (when configured) to correlate logs with traces.

---

## Endpoints Style & Adding New Endpoints

Endpoints are implemented with **Minimal APIs** using extension methods on `IEndpointRouteBuilder`.

Pattern:

1. A static class per endpoint group, e.g.:

   - `GenerateTokenEndpoint` in `src/Modules/Identity/Modules.Identity/Features/v1/Tokens/TokenGeneration/GenerateTokenEndpoint.cs`.
2. A `MapXyzEndpoint` method that returns `RouteHandlerBuilder`:

   ```csharp
   public static RouteHandlerBuilder MapGenerateTokenEndpoint(this IEndpointRouteBuilder endpoint)
   {
       return endpoint.MapPost("/token",
           [AllowAnonymous] async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
           ([FromBody] GenerateTokenCommand command,
            [FromHeader(Name = "tenant")] string tenant,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
           {
               var token = await mediator.Send(command, ct);
               return token is null ? TypedResults.Unauthorized() : TypedResults.Ok(token);
           })
           .WithName("IssueJwtTokens")
           .WithSummary("Issue JWT access and refresh tokens")
           .WithDescription("...")
           .Produces<TokenResponse>(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status401Unauthorized)
           .Produces(StatusCodes.Status400BadRequest)
           .Produces(StatusCodes.Status500InternalServerError);
   }
   ```

3. The module’s `MapEndpoints` calls this method on the grouped route:

   ```csharp
   group.MapGenerateTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
   ```

To add a new endpoint to a module:

1. Define a command/query in the `Modules.<Name>.Contracts` project.
2. Implement a handler in `Modules.<Name>` using Mediator.
3. Add a FluentValidator (if needed).
4. Create a Minimal API mapping extension under `Features/v1/...`.
5. Register the mapping in the module’s `MapEndpoints`.

---

## Adding a New Module

Steps:

1. Create two projects:
   - `Modules.MyModule/Modules.MyModule.csproj`
   - `Modules.MyModule/Modules.MyModule.Contracts.csproj`
2. In `Modules.MyModule`:
   - Implement an `IModule`:

     ```csharp
     public class MyModule : IModule
     {
         public void ConfigureServices(IHostApplicationBuilder builder)
         {
             var services = builder.Services;
             // register DbContexts, services, health checks, etc.
         }

         public void MapEndpoints(IEndpointRouteBuilder endpoints)
         {
             var apiVersionSet = endpoints.NewApiVersionSet()
                 .HasApiVersion(new ApiVersion(1))
                 .ReportApiVersions()
                 .Build();

             var group = endpoints
                 .MapGroup("api/v{version:apiVersion}/my-module")
                 .WithTags("MyModule")
                 .WithApiVersionSet(apiVersionSet);

             group.MapMyFeatureEndpoint();
         }
     }
     ```

3. Add commands/queries/DTOs to `Modules.MyModule.Contracts`.
4. Add EF DbContext (if needed) and register with `AddHeroDbContext<TContext>()`.
5. In the host (`Playground.Api` or your own), add `typeof(MyModule).Assembly` to `moduleAssemblies`.

---

## Using the Framework in Any .NET 10 Web API

You can adopt this framework in a new Web API project by:

1. **Reference building blocks and modules**:
   - Add project references to:
     - `BuildingBlocks/Core`
     - `BuildingBlocks/Web`
     - `BuildingBlocks/Persistence`
     - `BuildingBlocks/Caching`
     - `BuildingBlocks/Mailing`
     - `BuildingBlocks/Jobs`
     - `BuildingBlocks/Storage`
     - `Modules/Auditing/Modules.Auditing`
     - `Modules/Identity/Modules.Identity`
     - `Modules/Multitenancy/Modules.Multitenancy`
2. **Configure services** in `Program.cs` similar to `Playground.Api`:

   ```csharp
   var builder = WebApplication.CreateBuilder(args);

   // Mediator: register assemblies that contain handlers and contracts
   builder.Services.AddMediator(o =>
   {
       o.ServiceLifetime = ServiceLifetime.Scoped;
       o.Assemblies = [
           typeof(GenerateTokenCommand),
           typeof(GenerateTokenCommandHandler),
           typeof(GetTenantStatusQuery),
           typeof(GetTenantStatusQueryHandler),
           typeof(FSH.Modules.Auditing.Contracts.AuditEnvelope),
           typeof(FSH.Modules.Auditing.Persistence.AuditDbContext)
       ];
   });

   var moduleAssemblies = new[]
   {
       typeof(IdentityModule).Assembly,
       typeof(MultitenancyModule).Assembly,
       typeof(AuditingModule).Assembly
   };

   builder.AddHeroPlatform(o =>
   {
       o.EnableCors = true;
       o.EnableOpenApi = true;
       o.EnableCaching = true;
       o.EnableMailing = true;
       o.EnableJobs = true;
   });

   builder.AddModules(moduleAssemblies);
   var app = builder.Build();

   app.UseHeroMultiTenantDatabases();
   app.UseHeroPlatform(p => { p.MapModules = true; });

   app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
      .WithTags("Root")
      .AllowAnonymous();

   await app.RunAsync();
   ```

3. **Configure environment**:
   - Database provider and connection string:
     - `DatabaseOptions__Provider=POSTGRESQL`
     - `DatabaseOptions__ConnectionString=<your-conn>`
     - `DatabaseOptions__MigrationsAssembly=Your.Migrations.Assembly`
   - Caching:
     - `CachingOptions__Redis=<redis-connection>`
   - JWT:
     - `JwtOptions:Issuer`, `Audience`, `SigningKey`.
   - OpenTelemetry:
     - `OpenTelemetryOptions__Exporter__Otlp__Endpoint`, etc.

4. **Run migrations and DB initializers**:
   - Each module exposes `IDbInitializer` implementations.
   - Web host helper `UseHeroMultiTenantDatabases()` applies migrations & seeding.

From there, your app inherits:

- Identity endpoints for login/refresh/users/roles.
- Multitenancy endpoints and tenant-aware DBs.
- Auditing APIs and cross-cutting logging.
- Health, OpenAPI, rate limiting, CORS, etc.

---

## Aspire Integration

`FSH.Playground.AppHost` is an **Aspire** distributed application host:

- Configures:
  - `postgres` container with data volume.
  - `redis` container with data volume.
  - `playground-api` project with:
    - References to Postgres and Redis.
    - Environment variables for:
      - DB provider, connection string, migrations assembly.
      - OTLP endpoint/protocol for OpenTelemetry.
      - Redis connection string for caching.
  - `playground-blazor` project.

Usage:

- Run the AppHost (`dotnet run` in `src/Playground/FSH.Playground.AppHost`):
  - Aspire spins up infrastructure.
  - The API and Blazor projects run against those resources.
  - Traces and metrics flow to the configured OTel collector.

This demonstrates how the framework fits into a **cloud-native** environment with infra described in code.

---

## Gaps & Potential Improvements

Some areas where the framework could be strengthened:

- **Documentation & discoverability**:
  - Add XML comments on more public APIs in BuildingBlocks.
  - Provide ready-made `.http` examples per module for quick testing (some exist, but not exhaustive).
- **Specification implementation**:
  - Ensure all read-heavy endpoints consistently use the specification pattern to avoid ad-hoc query logic.
  - Add more base specs for common filters (e.g., `PagedByCreatedDate`, `ActiveOnly`).
- **Validation coverage**:
  - A few commands/queries may not yet have FluentValidation validators; adding them would tighten safety.
- **Security hardening**:
  - Consider optional refresh token **family IDs** for more advanced rotation semantics.
  - Add configurable **lockout** thresholds on token refresh failures.
- **Observability defaults**:
  - Provide opinionated OTel configuration profiles (e.g., `Development`, `Production`) with sensible sampling and exporters.
- **Extensibility hooks**:
  - Expose more pipeline hooks for modules to plug into web-level behaviors (e.g., additional middleware registrations).

Despite these, the current architecture is already strong:

- Clear separation between BuildingBlocks and Modules.
- Consistent Minimal API + Mediator pattern across modules.
- Solid DDD/persistence foundations and multi-tenancy integration.

This document should be read together with module-specific docs in this folder to get a complete picture of each capability.

