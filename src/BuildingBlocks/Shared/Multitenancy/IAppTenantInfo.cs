namespace FSH.Framework.Shared.Multitenancy;

public interface IAppTenantInfo
{
    string? ConnectionString { get; set; }
}