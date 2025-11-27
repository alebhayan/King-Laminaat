using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.UpgradeTenant;
using Mediator;

namespace FSH.Modules.Multitenancy.Features.v1.UpgradeTenant;

public sealed class UpgradeTenantCommandHandler(ITenantService service)
    : ICommandHandler<UpgradeTenantCommand, UpgradeTenantCommandResponse>
{
    public async ValueTask<UpgradeTenantCommandResponse> Handle(UpgradeTenantCommand command, CancellationToken cancellationToken)
    {
        var validUpto = await service.UpgradeSubscription(command.Tenant, command.ExtendedExpiryDate);
        return new UpgradeTenantCommandResponse(validUpto, command.Tenant);
    }
}