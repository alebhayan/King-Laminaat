using FSH.Modules.Multitenancy.Contracts;
using FSH.Modules.Multitenancy.Contracts.v1.CreateTenant;
using Mediator;

namespace FSH.Modules.Multitenancy.Features.v1.CreateTenant;

public class CreateTenantCommandHandler(ITenantService service)
    : ICommandHandler<CreateTenantCommand, CreateTenantCommandResponse>
{
    public async ValueTask<CreateTenantCommandResponse> Handle(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var tenantId = await service.CreateAsync(command.Id,
           command.Name,
           command.ConnectionString,
           command.AdminEmail,
           command.Issuer,
           cancellationToken);
        return new CreateTenantCommandResponse(tenantId);
    }
}