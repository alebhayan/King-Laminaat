using FluentValidation;
using FSH.Framework.Persistence;
using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.CreateTenant;

namespace FSH.Modules.Multitenancy.Features.v1.CreateTenant;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator(ITenantService tenantService, IConnectionStringValidator connectionStringValidator)
    {
        RuleFor(t => t.Id).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (id, _) => !await tenantService.ExistsWithIdAsync(id).ConfigureAwait(false))
            .WithMessage((_, id) => $"Tenant {id} already exists.");

        RuleFor(t => t.Name).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (name, _) => !await tenantService.ExistsWithNameAsync(name!).ConfigureAwait(false))
            .WithMessage((_, name) => $"Tenant {name} already exists.");

        RuleFor(t => t.ConnectionString).Cascade(CascadeMode.Stop)
            .Must((_, cs) => string.IsNullOrWhiteSpace(cs) || connectionStringValidator.TryValidate(cs))
            .WithMessage("Connection string invalid.");

        RuleFor(t => t.AdminEmail).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress();
    }
}