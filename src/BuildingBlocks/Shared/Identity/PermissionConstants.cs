namespace FSH.Framework.Shared.Constants;
public static class PermissionConstants
{
    private static readonly List<FshPermission> _all = new()
    {
        // Built-in permissions

        // Tenants
        new("View Tenants", ActionConstants.View, ResourceConstants.Tenants, IsRoot: true),
        new("Create Tenants", ActionConstants.Create, ResourceConstants.Tenants, IsRoot: true),
        new("Update Tenants", ActionConstants.Update, ResourceConstants.Tenants, IsRoot: true),
        new("Upgrade Tenant Subscription", ActionConstants.UpgradeSubscription, ResourceConstants.Tenants, IsRoot: true),

        // Identity
        new("View Users", ActionConstants.View, ResourceConstants.Users, IsBasic: true),
        new("Search Users", ActionConstants.Search, ResourceConstants.Users),
        new("Create Users", ActionConstants.Create, ResourceConstants.Users),
        new("Update Users", ActionConstants.Update, ResourceConstants.Users),
        new("Delete Users", ActionConstants.Delete, ResourceConstants.Users),
        new("Export Users", ActionConstants.Export, ResourceConstants.Users),
        new("View UserRoles", ActionConstants.View, ResourceConstants.UserRoles, IsBasic: true),
        new("Update UserRoles", ActionConstants.Update, ResourceConstants.UserRoles),
        new("View Roles", ActionConstants.View, ResourceConstants.Roles, IsBasic: true),
        new("Create Roles", ActionConstants.Create, ResourceConstants.Roles),
        new("Update Roles", ActionConstants.Update, ResourceConstants.Roles),
        new("Delete Roles", ActionConstants.Delete, ResourceConstants.Roles),
        new("View RoleClaims", ActionConstants.View, ResourceConstants.RoleClaims, IsBasic: true),
        new("Update RoleClaims", ActionConstants.Update, ResourceConstants.RoleClaims),

        // Audit
        new("View Audit Trails", ActionConstants.View, ResourceConstants.AuditTrails, IsBasic: true),

        // Hangfire / Dashboard
        new("View Hangfire", ActionConstants.View, ResourceConstants.Hangfire, IsBasic: true),
        new("View Dashboard", ActionConstants.View, ResourceConstants.Dashboard, IsBasic: true),
    };

    /// <summary>
    /// Register additional permissions from external projects/modules.
    /// </summary>
    public static void Register(IEnumerable<FshPermission> additionalPermissions)
    {
        _all.AddRange(from permission in additionalPermissions
                      where !_all.Any(p => p.Name == permission.Name)
                      select permission);
    }
    public const string RequiredPermissionPolicyName = "RequiredPermission";
    public static IReadOnlyList<FshPermission> All => _all.AsReadOnly();
    public static IReadOnlyList<FshPermission> Root => [.. _all.Where(p => p.IsRoot)];
    public static IReadOnlyList<FshPermission> Admin => [.. _all.Where(p => !p.IsRoot)];
    public static IReadOnlyList<FshPermission> Basic => [.. _all.Where(p => p.IsBasic)];
}

public record FshPermission(string Description, string Action, string Resource, bool IsBasic = false, bool IsRoot = false)
{
    public string Name => NameFor(Action, Resource);
    public static string NameFor(string action, string resource)
    {
        return $"Permissions.{resource}.{action}";
    }
}
