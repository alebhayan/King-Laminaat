using FSH.Framework.Shared.Identity.Claims;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Users.ChangePassword;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace FSH.Modules.Identity.Features.v1.Users.ChangePassword;

public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, string>
{
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChangePasswordCommandHandler(IUserService userService, IHttpContextAccessor httpContextAccessor)
    {
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask<string> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = _httpContextAccessor.HttpContext?.User.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        await _userService.ChangePasswordAsync(command.Password, command.NewPassword, command.ConfirmNewPassword, userId).ConfigureAwait(false);

        return "password reset email sent";
    }
}
