using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Features.v1.RoleClaims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Identity.Features.v1.Roles;

public class RoleService(RoleManager<FshRole> roleManager,
    IdentityDbContext context,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ICurrentUser currentUser) : IRoleService
{
    public async Task<IEnumerable<RoleDto>> GetRolesAsync()
    {
        if (roleManager is null)
            throw new NotFoundException("RoleManager<FshRole> not resolved. Check Identity registration.");

        if (roleManager.Roles is null)
            throw new NotFoundException("Role store not configured. Ensure .AddRoles<FshRole>() and EF stores.");


        var roles = await roleManager.Roles
            .Select(role => new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description })
            .ToListAsync();

        return roles;
    }

    public async Task<RoleDto?> GetRoleAsync(string id)
    {
        FshRole? role = await roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException("role not found");

        return new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description };
    }

    public async Task<RoleDto> CreateOrUpdateRoleAsync(string roleId, string name, string description)
    {
        FshRole? role = await roleManager.FindByIdAsync(roleId);

        if (role != null)
        {
            role.Name = name;
            role.Description = description;
            await roleManager.UpdateAsync(role);
        }
        else
        {
            role = new FshRole(name, description);
            await roleManager.CreateAsync(role);
        }

        return new RoleDto { Id = role.Id, Name = role.Name!, Description = role.Description };
    }

    public async Task DeleteRoleAsync(string id)
    {
        FshRole? role = await roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException("role not found");

        await roleManager.DeleteAsync(role);
    }

    public async Task<RoleDto> GetWithPermissionsAsync(string id, CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(id);
        _ = role ?? throw new NotFoundException("role not found");

        role.Permissions = await context.RoleClaims
            .Where(c => c.RoleId == id && c.ClaimType == ClaimConstants.Permission)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return role;
    }

    public async Task<string> UpdatePermissionsAsync(string roleId, List<string> permissions)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        _ = role ?? throw new NotFoundException("role not found");
        if (role.Name == RoleConstants.Admin)
        {
            throw new CustomException("operation not permitted");
        }

        if (multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id != MultitenancyConstants.Root.Id)
        {
            // Remove Root Permissions if the Role is not created for Root Tenant.
            permissions.RemoveAll(u => u.StartsWith("Permissions.Root.", StringComparison.InvariantCultureIgnoreCase));
        }

        var currentClaims = await roleManager.GetClaimsAsync(role);

        // Remove permissions that were previously selected
        foreach (var claim in currentClaims.Where(c => !permissions.Exists(p => p == c.Value)))
        {
            var result = await roleManager.RemoveClaimAsync(role, claim);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(error => error.Description).ToList();
                throw new CustomException("operation failed", errors);
            }
        }

        // Add all permissions that were not previously selected
        foreach (string permission in permissions.Where(c => !currentClaims.Any(p => p.Value == c)))
        {
            if (!string.IsNullOrEmpty(permission))
            {
                context.RoleClaims.Add(new FshRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = ClaimConstants.Permission,
                    ClaimValue = permission,
                    CreatedBy = currentUser.GetUserId().ToString()
                });
                await context.SaveChangesAsync();
            }
        }

        return "permissions updated";
    }
}