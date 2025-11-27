using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Web.Origin;
using FSH.Modules.Identity.Features.v1.RoleClaims;
using FSH.Modules.Identity.Features.v1.Roles;
using FSH.Modules.Identity.Features.v1.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Identity.Data;

internal sealed class IdentityDbInitializer(
    ILogger<IdentityDbInitializer> logger,
    IdentityDbContext context,
    RoleManager<FshRole> roleManager,
    UserManager<FshUser> userManager,
    TimeProvider timeProvider,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    IOptions<OriginOptions> originSettings) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[{Tenant}] applied database migrations for identity module", context.TenantInfo?.Identifier);
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRolesAsync();
        await SeedAdminUserAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (string roleName in RoleConstants.DefaultRoles)
        {
            if (await roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName)
                is not FshRole role)
            {
                // create role
                role = new FshRole(roleName, $"{roleName} Role for {multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id} Tenant");
                await roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == RoleConstants.Basic)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Basic, role);
            }
            else if (roleName == RoleConstants.Admin)
            {
                await AssignPermissionsToRoleAsync(context, PermissionConstants.Admin, role);

                if (multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id == MultitenancyConstants.Root.Id)
                {
                    await AssignPermissionsToRoleAsync(context, PermissionConstants.Root, role);
                }
            }
        }
    }

    private async Task AssignPermissionsToRoleAsync(IdentityDbContext dbContext, IReadOnlyList<FshPermission> permissions, FshRole role)
    {
        var currentClaims = await roleManager.GetClaimsAsync(role);
        var newClaims = permissions
            .Where(permission => !currentClaims.Any(c => c.Type == ClaimConstants.Permission && c.Value == permission.Name))
            .Select(permission => new FshRoleClaim
            {
                RoleId = role.Id,
                ClaimType = ClaimConstants.Permission,
                ClaimValue = permission.Name,
                CreatedBy = "application",
                CreatedOn = timeProvider.GetUtcNow()
            })
            .ToList();

        foreach (var claim in newClaims)
        {
            logger.LogInformation("Seeding {Role} Permission '{Permission}' for '{TenantId}' Tenant.", role.Name, claim.ClaimValue, multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            await dbContext.RoleClaims.AddAsync(claim);
        }

        // Save changes to the database context
        if (newClaims.Count != 0)
        {
            await dbContext.SaveChangesAsync();
        }

    }

    private async Task SeedAdminUserAsync()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id) || string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail))
        {
            return;
        }

        if (await userManager.Users.FirstOrDefaultAsync(u => u.Email == multiTenantContextAccessor.MultiTenantContext.TenantInfo!.AdminEmail)
            is not FshUser adminUser)
        {
            string adminUserName = $"{multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim()}.{RoleConstants.Admin}".ToUpperInvariant();
            adminUser = new FshUser
            {
                FirstName = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id.Trim().ToUpperInvariant(),
                LastName = RoleConstants.Admin,
                Email = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail,
                UserName = adminUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.AdminEmail!.ToUpperInvariant(),
                NormalizedUserName = adminUserName.ToUpperInvariant(),
                ImageUrl = new Uri(originSettings.Value.OriginUrl! + MultitenancyConstants.Root.DefaultProfilePicture),
                IsActive = true
            };

            logger.LogInformation("Seeding Default Admin User for '{TenantId}' Tenant.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            var password = new PasswordHasher<FshUser>();
            adminUser.PasswordHash = password.HashPassword(adminUser, MultitenancyConstants.DefaultPassword);
            await userManager.CreateAsync(adminUser);
        }

        // Assign role to user
        if (!await userManager.IsInRoleAsync(adminUser, RoleConstants.Admin))
        {
            logger.LogInformation("Assigning Admin Role to Admin User for '{TenantId}' Tenant.", multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Id);
            await userManager.AddToRoleAsync(adminUser, RoleConstants.Admin);
        }
    }
}