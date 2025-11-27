using FSH.Framework.Shared.Identity;
using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.v1.Users.GetUserRoles;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Users.GetUserRoles;

public static class GetUserRolesEndpoint
{
    internal static RouteHandlerBuilder MapGetUserRolesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/users/{id:guid}/roles", (string id, IMediator mediator, CancellationToken cancellationToken) =>
            mediator.Send(new GetUserRolesQuery(id), cancellationToken))
        .WithName("GetUserRoles")
        .WithSummary("Get user roles")
        .RequirePermission(IdentityPermissionConstants.Users.View)
        .WithDescription("Retrieve the roles assigned to a specific user.");
    }
}
