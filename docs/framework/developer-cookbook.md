# Developer Cookbook

This cookbook gives **concrete recipes** for working in this framework – aimed at both human developers and AI agents (like Codex) who will help build features.

Use this in combination with:

- `docs/framework/architecture.md`
- `docs/framework/building-blocks.md`
- `docs/framework/module-identity.md`
- `docs/framework/module-auditing.md`
- `docs/framework/module-multitenancy.md`
- `docs/framework/using-framework-in-your-api.md`

---

## 1. Conventions at a Glance

- **Modules** live under `src/Modules/<Name>/Modules.<Name>` and implement `IModule`.
- **Contracts** for a module live in `src/Modules/<Name>/Modules.<Name>.Contracts`.
- **Features** are organized by version and verb:
  - `Features/v1/<Area>/<FeatureName>`
  - Each feature has:
    - Command/query record (in Contracts).
    - Handler (Mediator).
    - Minimal API endpoint mapping.
    - Optional FluentValidation validator.
- **DbContexts** live inside modules or BuildingBlocks.Persistence and are registered via `AddHeroDbContext<TContext>()`.
- **Cross-cutting** (auth, exceptions, logging, health, OTel, rate limiting, versioning) comes from `BuildingBlocks/Web`.

When in doubt, copy an existing pattern from Identity, Auditing, or Multitenancy.

---

## 2. Recipe: Add a New Endpoint in an Existing Module

Goal: Add a new feature (e.g., `ChangeEmail`) to the Identity module.

**Step 1 – Define contracts**

1. In `Modules.Identity.Contracts`:
   - Create a folder like `v1/Users/ChangeEmail`.
   - Add:

   ```csharp
   public sealed record ChangeEmailCommand(string UserId, string NewEmail)
       : ICommand<ChangeEmailResponse>;

   public sealed record ChangeEmailResponse(string UserId, string Email);
   ```

   - Use `Mediator.ICommand<TResponse>` or `IQuery<TResponse>` as appropriate.

**Step 2 – Implement handler**

1. In `Modules.Identity` under `Features/v1/Users/ChangeEmail`:
   - Add `ChangeEmailCommandHandler`:

   ```csharp
   public sealed class ChangeEmailCommandHandler
       : ICommandHandler<ChangeEmailCommand, ChangeEmailResponse>
   {
       private readonly UserManager<FshUser> _userManager;

       public ChangeEmailCommandHandler(UserManager<FshUser> userManager)
           => _userManager = userManager;

       public async ValueTask<ChangeEmailResponse> Handle(ChangeEmailCommand request, CancellationToken ct)
       {
           var user = await _userManager.FindByIdAsync(request.UserId)
               ?? throw new NotFoundException("user not found");

           user.Email = request.NewEmail;
           user.UserName = request.NewEmail;

           var result = await _userManager.UpdateAsync(user);
           if (!result.Succeeded)
               throw new CustomException("could not change email");

           return new ChangeEmailResponse(user.Id, user.Email!);
       }
   }
   ```

   - Follow exception types from Core (`NotFoundException`, `CustomException`).

**Step 3 – Add validator**

1. Add `ChangeEmailCommandValidator` in the same feature folder:

   ```csharp
   public sealed class ChangeEmailCommandValidator : AbstractValidator<ChangeEmailCommand>
   {
       public ChangeEmailCommandValidator()
       {
           RuleFor(x => x.UserId)
               .NotEmpty();

           RuleFor(x => x.NewEmail)
               .NotEmpty()
               .EmailAddress();
       }
   }
   ```

   - FluentValidation will be invoked by `ValidationBehavior`.

**Step 4 – Map Minimal API endpoint**

1. Create `ChangeEmailEndpoint`:

   ```csharp
   public static class ChangeEmailEndpoint
   {
       internal static RouteHandlerBuilder MapChangeEmailEndpoint(this IEndpointRouteBuilder endpoints)
       {
           return endpoints.MapPost("/users/{id:guid}/change-email",
               async Task<Ok<ChangeEmailResponse>> (
                   string id,
                   [FromBody] ChangeEmailRequest request,
                   IMediator mediator,
                   CancellationToken ct) =>
               {
                   var command = new ChangeEmailCommand(id, request.NewEmail);
                   var result = await mediator.Send(command, ct);
                   return TypedResults.Ok(result);
               })
               .RequirePermission(IdentityPermissionConstants.Users.Update)
               .WithName("ChangeUserEmail")
               .WithSummary("Change user email address");
       }
   }
   ```

   - Optionally define a separate request DTO if needed.

**Step 5 – Register endpoint in module**

1. In `IdentityModule.MapEndpoints`, add:

   ```csharp
   group.MapChangeEmailEndpoint();
   ```

**Step 6 – Build & test**

- Run `dotnet build src/FSH.Framework.slnx`.
- Optionally call the new endpoint via `.http` file or Postman.

> **For AI agents**: When adding endpoints, always follow this pattern and reuse existing Identity/Auditing endpoints as templates. Do not create controllers; stay with Minimal APIs and Mediator.

---

## 3. Recipe: Add a New Module

Goal: Create a `Catalog` module for products.

**Step 1 – Create projects**

1. Create `src/Modules/Catalog/Modules.Catalog.csproj`.
2. Create `src/Modules/Catalog/Modules.Catalog.Contracts.csproj`.
3. Reference BuildingBlocks projects and other needed Modules from `Modules.Catalog`.

**Step 2 – Implement IModule**

In `Modules.Catalog`:

```csharp
public sealed class CatalogModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        // DbContext
        services.AddHeroDbContext<CatalogDbContext>();

        // Services
        services.AddScoped<IProductService, ProductService>();

        // Health checks
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<CatalogDbContext>(
                name: "db:catalog",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var apiVersionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/catalog")
            .WithTags("Catalog")
            .WithApiVersionSet(apiVersionSet);

        group.MapGetProductsEndpoint();
        group.MapCreateProductEndpoint();
    }
}
```

**Step 3 – Define DbContext and entities**

1. Add `CatalogDbContext` under `Modules.Catalog/Data`:

   ```csharp
   public sealed class CatalogDbContext : DbContext
   {
       public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
           : base(options) { }

       public DbSet<Product> Products => Set<Product>();

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           base.OnModelCreating(modelBuilder);
           modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
       }
   }
   ```

2. Add `Product` entity and configuration applying DDD & multi-tenancy patterns if required.

**Step 4 – Add features**

- Follow the endpoint recipe for each feature (e.g., product list, details, create, update).

**Step 5 – Wire module in host**

1. In your API `Program.cs`, add module assembly:

   ```csharp
   var moduleAssemblies = new[]
   {
       typeof(IdentityModule).Assembly,
       typeof(MultitenancyModule).Assembly,
       typeof(AuditingModule).Assembly,
       typeof(CatalogModule).Assembly
   };

   builder.AddModules(moduleAssemblies);
   ```

2. Ensure Mediator is configured for Catalog commands/queries.

---

## 4. Recipe: Add a Background Job

Goal: Add a job to send weekly reports.

**Step 1 – Ensure Jobs are enabled**

- In host `Program.cs`:

  ```csharp
  builder.AddHeroPlatform(o =>
  {
      o.EnableJobs = true;
      // other options...
  });
  ```

**Step 2 – Create job class**

In an appropriate module (e.g., `Modules.Auditing` or a new module):

```csharp
public sealed class WeeklyReportJob
{
    private readonly IReportService _reportService;

    public WeeklyReportJob(IReportService reportService) => _reportService = reportService;

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _reportService.GenerateAndSendWeeklyReportsAsync(ct);
    }
}
```

**Step 3 – Schedule job**

Somewhere in startup (module or host), for example in `ConfigureServices` of a module:

```csharp
public void ConfigureServices(IHostApplicationBuilder builder)
{
    // ...
    builder.Services.AddScoped<WeeklyReportJob>();

    // Use Hangfire recurring job after Hangfire is configured
    builder.Services.AddHangfireServer(); // via Jobs.Extensions, usually already done by AddHeroPlatform
}
```

Then, in a startup hook or `IDbInitializer`, schedule:

```csharp
RecurringJob.AddOrUpdate<WeeklyReportJob>(
    "weekly-report",
    job => job.RunAsync(CancellationToken.None),
    Cron.Weekly);
```

> For AI agents: use Hangfire’s DI integration (`FshJobActivator`) – do not instantiate jobs manually or spawn background threads yourself.

---

## 5. Recipe: Add a New DbContext & Use Specifications

Goal: Add a `ReportingDbContext` and query it using specifications.

**Step 1 – Create DbContext**

```csharp
public sealed class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : base(options) { }

    public DbSet<Report> Reports => Set<Report>();
}
```

**Step 2 – Register DbContext**

In appropriate module `ConfigureServices`:

```csharp
services.AddHeroDbContext<ReportingDbContext>();
```

This ensures:

- Common EF configuration.
- Interceptors (domain events).
- Connection string/provider from configuration.

**Step 3 – Define a specification**

In `Reporting` module:

```csharp
public sealed class ReportsByStatusSpec : Specification<Report>
{
    public ReportsByStatusSpec(string status)
    {
        Query.Where(r => r.Status == status);
        Query.OrderByDescending(r => r.CreatedOn);
    }
}
```

**Step 4 – Use specification in handler**

```csharp
public sealed class GetReportsQueryHandler
    : IQueryHandler<GetReportsQuery, PagedResponse<ReportDto>>
{
    private readonly ReportingDbContext _db;

    public GetReportsQueryHandler(ReportingDbContext db) => _db = db;

    public async ValueTask<PagedResponse<ReportDto>> Handle(GetReportsQuery request, CancellationToken ct)
    {
        var spec = new ReportsByStatusSpec(request.Status);

        // Example pattern: apply specification and pagination
        var query = spec.Apply(_db.Reports.AsQueryable());
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ReportDto(/* ... */))
            .ToListAsync(ct);

        return new PagedResponse<ReportDto>(items, total, request.PageNumber, request.PageSize);
    }
}
```

> For AI agents: prefer specifications over sprinkling raw LINQ across handlers; look at existing specs under `src/BuildingBlocks/Persistence/Specifications` for patterns.

---

## 6. Recipe: Add Integration with External Service (e.g., Mail)

Goal: Use `IMailService` to send emails from a feature.

**Step 1 – Ensure Mailing is enabled**

In host `Program.cs`:

```csharp
builder.AddHeroPlatform(o =>
{
    o.EnableMailing = true;
});
```

Configure `MailOptions` in appsettings (SMTP or other provider).

**Step 2 – Inject and use IMailService**

In a handler (e.g., user self-registration):

```csharp
public sealed class SendWelcomeEmailHandler
    : ICommandHandler<SendWelcomeEmailCommand, Unit>
{
    private readonly IMailService _mailService;

    public SendWelcomeEmailHandler(IMailService mailService)
        => _mailService = mailService;

    public async ValueTask<Unit> Handle(SendWelcomeEmailCommand request, CancellationToken ct)
    {
        var mail = new MailRequest
        {
            To = request.Email,
            Subject = "Welcome!",
            Body = "Thanks for registering..."
        };

        await _mailService.SendAsync(mail, ct);
        return Unit.Value;
    }
}
```

> For AI agents: never talk directly to `SmtpClient` from modules – always use `IMailService` so the implementation can be swapped.

---

## 7. Recipe: Add Observability (Custom Spans/Attributes)

Goal: Enrich OpenTelemetry traces in a handler.

**Step 1 – Ensure Observability is enabled**

- Host config (via `AddHeroPlatform`) and `OpenTelemetryOptions` environment variables or appsettings.

**Step 2 – Use ActivitySource**

In a service/handler:

```csharp
private static readonly ActivitySource ActivitySource = new("FSH.Catalog.Products");

public async Task<ProductDto> GetProductAsync(string id, CancellationToken ct)
{
    using var activity = ActivitySource.StartActivity("GetProduct");
    activity?.SetTag("product.id", id);

    // business logic...
}
```

> For AI agents: check existing usage of OpenTelemetry in `BuildingBlocks/Web/Observability` to align naming conventions.

---

## 8. Guidance for AI Agents

When making changes in this repo:

- **Follow existing patterns**:
  - Prefer **Minimal APIs + Mediator** over controllers.
  - Place new features in `Features/v1/<Area>/<Feature>` folders.
  - Put command/query records in the corresponding `Modules.<Name>.Contracts` project.
  - Always add FluentValidation validators for new commands/queries.
- **Respect DDD & specifications**:
  - Use domain events and EF interceptors where appropriate.
  - Use specifications for queries instead of ad-hoc LINQ.
- **Leverage building blocks**:
  - Use `ICacheService` for caching.
  - Use `IMailService` for emails.
  - Use `IStorageService` for files.
  - Use `ISecurityAudit` and `IAuditClient` for security and operational events.
- **Be multi-tenant aware**:
  - When accessing tenant-specific data, use `IMultiTenantContextAccessor<AppTenantInfo>` to validate tenant context.
  - Include `TenantId` in new entities and mark them as multi-tenant when needed.
- **Avoid anti-patterns**:
  - Do not bypass central exception handling – throw domain/application exceptions instead of writing raw responses.
  - Do not introduce new DI containers or background thread “managers” – use Jobs/Hangfire.
  - Do not hardcode secrets; rely on options/configuration.

If you’re unsure, search for an existing example (e.g., Identity user endpoints, Auditing queries, Multitenancy endpoints) and copy the pattern.

