using FSH.Framework.Shared.Identity;
using FSH.Framework.Shared.Identity.Authorization;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.v1.Users.RegisterUser;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FSH.Framework.Infrastructure.Identity.Users.Endpoints;

public static class SelfRegisterUserEndpoint
{
    internal static RouteHandlerBuilder MapSelfRegisterUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/self-register", (RegisterUserCommand command,
            [FromHeader(Name = MultitenancyConstants.Identifier)] string tenant,
            HttpContext context,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var origin = $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value}";
            command.Origin = origin;
            return mediator.Send(command, cancellationToken);
        })
        .WithName("SelfRegisterUser")
        .WithSummary("Self register user")
        .RequirePermission(IdentityPermissionConstants.Users.Create)
        .WithDescription("Allow a user to self-register.")
        .AllowAnonymous();
    }
}
