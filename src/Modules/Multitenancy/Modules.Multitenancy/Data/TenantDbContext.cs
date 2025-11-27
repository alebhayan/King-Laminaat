using Finbuckle.MultiTenant.EntityFrameworkCore.Stores.EFCoreStore;
using FSH.Framework.Shared.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Multitenancy.Data;

public class TenantDbContext : EFCoreStoreDbContext<AppTenantInfo>
{
    public const string Schema = "tenant";
    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppTenantInfo>().ToTable("Tenants", Schema);
    }
}