using FSH.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace FSH.Modules.Identity.Contracts.v1.Roles.GetRoles;

public sealed record GetRolesQuery : IQuery<IEnumerable<RoleDto>>;

