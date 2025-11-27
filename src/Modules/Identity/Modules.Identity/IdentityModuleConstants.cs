using FSH.Framework.Web.Modules;

namespace FSH.Modules.Identity;

public sealed class IdentityModuleConstants : IModuleConstants
{
    public string ModuleId => throw new NotImplementedException();

    public string ModuleName => throw new NotImplementedException();

    public string ApiPrefix => throw new NotImplementedException();
    public const string SchemaName = "identity";
    public const int PasswordLength = 10;
}
