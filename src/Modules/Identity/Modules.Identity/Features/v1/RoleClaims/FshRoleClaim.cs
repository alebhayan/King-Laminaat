using Microsoft.AspNetCore.Identity;

namespace FSH.Modules.Identity.Features.v1.RoleClaims;

public class FshRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
}