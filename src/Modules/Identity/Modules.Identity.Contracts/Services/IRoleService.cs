using FSH.Modules.Identity.Contracts.DTOs;

namespace FSH.Modules.Identity.Contracts.Services;

public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetRolesAsync();
    Task<RoleDto?> GetRoleAsync(string id);
    Task<RoleDto> CreateOrUpdateRoleAsync(string roleId, string name, string description);
    Task DeleteRoleAsync(string id);
    Task<RoleDto> GetWithPermissionsAsync(string id, CancellationToken cancellationToken);

    Task<string> UpdatePermissionsAsync(string roleId, List<string> permissions);
}