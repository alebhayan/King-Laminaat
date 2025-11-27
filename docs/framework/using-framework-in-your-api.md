# Using the Framework in Your .NET 10 Web API

This guide shows how to use the framework (BuildingBlocks + Modules) in any .NET 10 Web API. It uses `FSH.Playground.Api` as a concrete example.

---

## 1. Project References

In your Web API project, add references to:

- Building blocks:
  - `BuildingBlocks/Core`
  - `BuildingBlocks/Web`
  - `BuildingBlocks/Persistence`
  - `BuildingBlocks/Caching`
  - `BuildingBlocks/Mailing`
  - `BuildingBlocks/Jobs`
  - `BuildingBlocks/Storage`
- Modules:
  - `Modules/Auditing/Modules.Auditing`
  - `Modules/Auditing/Modules.Auditing.Contracts`
  - `Modules/Identity/Modules.Identity`
  - `Modules/Identity/Modules.Identity.Contracts`
  - `Modules/Multitenancy/Modules.Multitenancy`
  - `Modules/Multitenancy/Modules.Multitenancy.Contracts`

You can see how `Playground.Api` references these in `src/Playground/Playground.Api/Playground.Api.csproj`.

---

## 2. Configure Mediator

In `Program.cs`, configure Mediator with the assemblies that contain commands and handlers you want to use:

```csharp
using FSH.Modules.Auditing;
using FSH.Modules.Identity;
using FSH.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using FSH.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using FSH.Modules.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using FSH.Modules.Multitenancy.Features.v1.GetTenantStatus;

var builder = WebApplication.CreateBuilder(args);

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
```

Notes:

- `Assemblies` should include:
  - Command/Query contracts.
  - Their handlers.
  - Any additional types the Mediator library uses for discovery.
- The Web building block also offers `EnableMediator(...)` to encapsulate this wiring.

---

## 3. Register Modules

Identify which modules you want to enable and register them:

```csharp
var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly
};

builder.AddModules(moduleAssemblies);
```

`AddModules` (from BuildingBlocks.Web) uses the module loader to:

- Discover `IModule` implementations in each assembly.
- Call `ConfigureServices` during startup.

---

## 4. Add the Hero Platform

Use the Web building block to wire cross-cutting concerns:

```csharp
builder.AddHeroPlatform(o =>
{
    o.EnableCors = true;
    o.EnableOpenApi = true;
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
});
```

This configures:

- CORS (configurable via appsettings).
- OpenAPI / Swagger.
- Caching and Redis integration.
- Mailing (SMTP/SendGrid style).
- Hangfire-based jobs.
- Authentication & authorization.
- Health checks.
- Observability (OpenTelemetry).
- Rate limiting.

---

## 5. Build and Configure the HTTP Pipeline

After building the app:

```csharp
var app = builder.Build();

app.UseHeroMultiTenantDatabases();
app.UseHeroPlatform(p => { p.MapModules = true; });

app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
   .WithTags("PlayGround")
   .AllowAnonymous();

await app.RunAsync();
```

Key pieces:

- `UseHeroMultiTenantDatabases()`:
  - Ensures tenant-specific and shared databases are migrated and initialized.
  - Calls `IDbInitializer` implementations found in modules (e.g., `IdentityDbInitializer`).
- `UseHeroPlatform`:
  - Adds middleware for:
    - Exception handling, logging, auth, CORS, health, swagger, rate limiting.
  - If `MapModules = true`:
    - Calls `MapEndpoints` on each module to register Minimal API endpoints (Identity, Auditing, Multitenancy, etc.).

At this point:

- Identity endpoints are available under `api/v1/identity`.
- Auditing endpoints under `api/v1/auditing`.
- Multitenancy endpoints under `api/v1/multitenancy`.

---

## 6. Configure Application Settings

The framework is configuration-driven. For a typical setup:

### Database

Set DB provider and connection string (Postgres example):

- `DatabaseOptions__Provider=POSTGRESQL`
- `DatabaseOptions__ConnectionString=Host=...;Database=...;Username=...;Password=...;`
- `DatabaseOptions__MigrationsAssembly=FSH.Playground.Migrations.PostgreSQL` (or your migrations assembly).

### Caching (Redis)

- `CachingOptions__Redis=<redis-connection-string>`

### JWT

Ensure the Identity module has valid JWT settings:

- `JwtOptions:Issuer=your-issuer`
- `JwtOptions:Audience=your-audience`
- `JwtOptions:SigningKey=your-very-long-signing-key-at-least-32-characters`
- `JwtOptions:AccessTokenMinutes=30`
- `JwtOptions:RefreshTokenDays=7`

### OpenTelemetry

If you want observability:

- `OpenTelemetryOptions__Exporter__Otlp__Endpoint=https://localhost:4317`
- `OpenTelemetryOptions__Exporter__Otlp__Protocol=grpc`
- `OpenTelemetryOptions__Exporter__Otlp__Enabled=true`

### Others

Configure:

- CORS options (allowed origins).
- Mailing options (`MailOptions`).
- Jobs (`HangfireOptions`).

The exact structure is defined in the options classes in BuildingBlocks.

---

## 7. Using Aspire (FSH.Playground.AppHost)

`FSH.Playground.AppHost` demonstrates using **Aspire** as a distributed application host:

File: `src/Playground/FSH.Playground.AppHost/AppHost.cs`

Key concepts:

- Uses `DistributedApplication.CreateBuilder(args)` to define:
  - `postgres` resource:
    - `.AddPostgres("postgres").WithDataVolume("fsh-postgres-data").AddDatabase("fsh");`
  - `redis` resource:
    - `.AddRedis("redis").WithDataVolume("fsh-redis-data");`
  - `playground-api` project:
    - `builder.AddProject<Projects.Playground_Api>("playground-api")`
    - `.WithReference(postgres)` and `.WithReference(redis)` to connect API to DB + cache.
    - `.WithEnvironment("DatabaseOptions__ConnectionString", postgres.Resource.ConnectionStringExpression)` etc.
    - `.WithEnvironment("CachingOptions__Redis", redis.Resource.ConnectionStringExpression)`.
  - `playground-blazor` project:
    - `builder.AddProject<Projects.Playground_Blazor>("playground-blazor");`

Running AppHost:

- `dotnet run` in `src/Playground/FSH.Playground.AppHost`:
  - Spins up Postgres and Redis.
  - Starts Playground.Api and Playground.Blazor with correct environment.
  - Enables OTLP exporter for OpenTelemetry by environment variables.

To use Aspire with your own API:

1. Create a new AppHost project.
2. Define resources (DB, Redis, etc.).
3. Add your API project with `.AddProject<Projects.Your_Api>("your-api")`.
4. Wire environment variables for database, cache, and OpenTelemetry.

---

## 8. Adding Your Own Module

To extend the framework with custom domain logic:

1. Create:
   - `src/Modules/YourModule/Modules.YourModule.csproj`
   - `src/Modules/YourModule/Modules.YourModule.Contracts.csproj`
2. Implement `IModule` in `Modules.YourModule`:
   - Configure DbContexts, services, health checks in `ConfigureServices`.
   - Map endpoints in `MapEndpoints` using Minimal APIs + Mediator.
3. Reference both module projects from your Web API.
4. Add `typeof(YourModule).Assembly` to `moduleAssemblies` in `Program.cs`.
5. Optionally, add Web/Blazor front-ends consuming your module endpoints.

This way, your module enjoys the same:

- Multi-tenancy.
- Auditing.
- Observability.
- Security.
- DDD-friendly persistence.

---

## 9. Coding Standards & Best Practices

When building on this framework:

- Use **Mediator** for all business logic:
  - Endpoints should delegate to commands/queries.
- Define **contracts** in `Modules.<Name>.Contracts`:
  - Records for commands, queries, DTOs.
- Place **handlers** and **validators** in module implementation:
  - `Features/v1/<FeatureName>`.
- Use **FluentValidation** for input validation:
  - Automatically enforced by `ValidationBehavior`.
- Use **specifications** for data access instead of ad-hoc LINQ.
- Use **domain events** to model side effects and integrate with other modules.
- Keep **modules independent**:
  - Only depend on BuildingBlocks and cross-module contracts when necessary.

---

## 10. Summary

To adopt the framework in any .NET 10 Web API:

- Reference BuildingBlocks and desired Modules.
- Configure Mediator with your feature assemblies.
- Call `AddHeroPlatform` and `AddModules` in `Program.cs`.
- Configure environment/appsettings (DB, caching, JWT, OTel).
- Use Minimal APIs + Mediator + FluentValidation patterns for all endpoints.
- Optionally, use Aspire to orchestrate infrastructure and app hosting.

Following the `FSH.Playground.Api` example gives you a robust, multi-tenant, observable, and secure API baseline with minimal boilerplate.

