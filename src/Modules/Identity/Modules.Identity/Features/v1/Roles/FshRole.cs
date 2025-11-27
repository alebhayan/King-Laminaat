using Microsoft.AspNetCore.Identity;

namespace FSH.Modules.Identity.Features.v1.Roles;

public class FshRole : IdentityRole
{
    public string? Description { get; set; }

    public FshRole(string name, string? description = null)
        : base(name)
    {
        Description = description;
        NormalizedName = name.ToUpperInvariant();
    }
}