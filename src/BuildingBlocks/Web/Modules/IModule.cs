using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace FSH.Framework.Web.Modules;

public interface IModule
{
    // DI/Options/Health/etc. — don’t depend on ASP.NET types here
    void ConfigureServices(IHostApplicationBuilder builder);

    // HTTP wiring — Minimal APIs only
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}