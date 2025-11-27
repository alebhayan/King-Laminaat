using FSH.Framework.Shared.Identity;
using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Identity.Contracts.v1.Roles.GetRole;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Identity.Features.v1.Roles.GetRoleById;

public static class GetRoleByIdEndpoint
{
    public static RouteHandlerBuilder MapGetRoleByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/roles/{id:guid}", (string id, IMediator mediator, CancellationToken cancellationToken) =>
            mediator.Send(new GetRoleQuery(id), cancellationToken))
        .WithName("GetRole")
        .WithSummary("Get role by ID")
        .RequirePermission(IdentityPermissionConstants.Roles.View)
        .WithDescription("Retrieve details of a specific role by its unique identifier.");
    }
}
