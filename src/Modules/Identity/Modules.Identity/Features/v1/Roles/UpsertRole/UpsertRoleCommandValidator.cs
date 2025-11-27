using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.Roles.UpsertRole;

namespace FSH.Modules.Identity.Features.v1.Roles.UpsertRole;

public class UpsertRoleCommandValidator : AbstractValidator<UpsertRoleCommand>
{
    public UpsertRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Role name is required.");
    }
}