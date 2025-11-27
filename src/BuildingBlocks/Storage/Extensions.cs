using FSH.Framework.Storage.Local;
using FSH.Framework.Storage.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.Framework.Storage;

public static class Extensions
{
    public static IServiceCollection AddHeroLocalFileStorage(this IServiceCollection services)
    {
        services.AddScoped<IStorageService, LocalStorageService>();
        return services;
    }
}
