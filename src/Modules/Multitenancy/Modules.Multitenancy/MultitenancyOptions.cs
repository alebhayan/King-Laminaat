namespace FSH.Modules.Multitenancy;

/// <summary>
/// Options controlling multitenancy behavior at startup.
/// </summary>
public sealed class MultitenancyOptions
{
    /// <summary>
    /// When true, runs per-tenant migrations and seeding for all registered <c>IDbInitializer</c>
    /// implementations during <c>UseHeroMultiTenantDatabases</c>. Recommended for development and demos.
    /// In production, prefer running migrations explicitly and leaving this disabled for faster startup.
    /// </summary>
    public bool RunTenantMigrationsOnStartup { get; set; }
}

