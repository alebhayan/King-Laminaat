using Mediator;

namespace FSH.Modules.Multitenancy.Contracts.v1.CreateTenant;

public sealed record CreateTenantCommand(
    string Id,
    string Name,
    string? ConnectionString,
    string AdminEmail,
    string? Issuer) : ICommand<CreateTenantCommandResponse>;