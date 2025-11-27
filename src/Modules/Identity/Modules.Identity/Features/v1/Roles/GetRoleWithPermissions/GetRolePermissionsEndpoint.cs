using FSH.Framework.Shared.Identity;
using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.v1.Roles.GetRoleWithPermissions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Roles.GetRoleWithPermissions;

public static class GetRolePermissionsEndpoint
{
    public static RouteHandlerBuilder MapGetRolePermissionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/{id:guid}/permissions", (string id, IMediator mediator, CancellationToken cancellationToken) =>
            mediator.Send(new GetRoleWithPermissionsQuery(id), cancellationToken))
        .WithName("GetRolePermissions")
        .WithSummary("Get role permissions")
        .RequirePermission(IdentityPermissionConstants.Roles.View)
        .WithDescription("Retrieve a role along with its assigned permissions.");
    }
}
