using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using FSH.Framework.Core.Domain;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Framework.Persistence.Context;

public class BaseDbContext(IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    DbContextOptions options,
    IOptions<DatabaseOptions> settings,
    IHostEnvironment environment)
    : MultiTenantDbContext(multiTenantContextAccessor, options)
{
    private readonly DatabaseOptions _settings = settings.Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.AppendGlobalQueryFilter<ISoftDeletable>(s => !s.IsDeleted);
        base.OnModelCreating(modelBuilder);
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        if (!string.IsNullOrWhiteSpace(multiTenantContextAccessor?.MultiTenantContext.TenantInfo?.ConnectionString))
        {
            optionsBuilder.ConfigureHeroDatabase(
                _settings.Provider,
                multiTenantContextAccessor.MultiTenantContext.TenantInfo.ConnectionString!,
                _settings.MigrationsAssembly,
                environment.IsDevelopment());
        }
    }
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TenantNotSetMode = TenantNotSetMode.Overwrite;
        int result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
