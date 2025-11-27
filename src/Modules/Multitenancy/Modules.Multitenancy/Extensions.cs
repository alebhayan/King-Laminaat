using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Multitenancy.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Multitenancy;

public static class Extensions
{
    private static IEnumerable<AppTenantInfo> TenantStoreSetup(IApplicationBuilder app)
    {
        var scope = app.ApplicationServices.CreateScope();
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>()
                             .CreateLogger("MultitenancySetup");

        // tenant master schema migration
        var tenantDbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        if (tenantDbContext.Database.GetPendingMigrations().Any())
        {
            tenantDbContext.Database.Migrate();
            logger.LogInformation("applied database migrations for tenant module");
        }

        // default tenant seeding
        if (tenantDbContext.TenantInfo.Find(MultitenancyConstants.Root.Id) is null)
        {
            var rootTenant = new AppTenantInfo(
                MultitenancyConstants.Root.Id,
                MultitenancyConstants.Root.Name,
                string.Empty,
                MultitenancyConstants.Root.EmailAddress,
                issuer: MultitenancyConstants.Root.Issuer);

            rootTenant.SetValidity(DateTime.UtcNow.AddYears(1));
            tenantDbContext.TenantInfo.Add(rootTenant);
            tenantDbContext.SaveChanges();
            logger.LogInformation("configured default tenant data");
        }

        // get all tenants from store
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenants = tenantStore.GetAllAsync().Result;

        //dispose scope
        scope.Dispose();

        return tenants;
    }

    public static WebApplication UseHeroMultiTenantDatabases(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMultiTenant();

        // set up tenant store
        var tenants = TenantStoreSetup(app);

        // set up tenant databases only when explicitly enabled
        var options = app.Services.GetService<IOptions<MultitenancyOptions>>();
        app.SetupTenantDatabases(tenants, options?.Value.RunTenantMigrationsOnStartup);

        return app;
    }
    private static IApplicationBuilder SetupTenantDatabases(this IApplicationBuilder app, IEnumerable<AppTenantInfo> tenants, bool? runMigrations)
    {
        foreach (var tenant in tenants)
        {
            // create a scope for tenant
            using var tenantScope = app.ApplicationServices.CreateScope();

            //set current tenant so that the right connection string is used
            tenantScope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
                .MultiTenantContext = new MultiTenantContext<AppTenantInfo>()
                {
                    TenantInfo = tenant
                };

            // set up tenant databases only when explicitly enabled
            if (runMigrations.HasValue && runMigrations == false)
            {
                continue;
            }

            // using the scope, perform migrations / seeding
            var initializers = tenantScope.ServiceProvider.GetServices<IDbInitializer>();
            foreach (var initializer in initializers)
            {
                initializer.MigrateAsync(CancellationToken.None).Wait();
                initializer.SeedAsync(CancellationToken.None).Wait();
            }
        }
        return app;
    }
}
