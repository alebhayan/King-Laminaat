using FSH.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Framework.Persistence;

public static class Extensions
{
    public static IServiceCollection AddHeroDatabaseOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(nameof(DatabaseOptions)))
            .ValidateDataAnnotations()
            .Validate(o => !string.IsNullOrWhiteSpace(o.Provider), "DatabaseOptions.Provider is required.")
            .ValidateOnStart();
        services.AddHostedService<DatabaseOptionsStartupLogger>();
        return services;
    }

    public static IServiceCollection AddHeroDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<TContext>((sp, options) =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var dbConfig = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.ConfigureHeroDatabase(dbConfig.Provider, dbConfig.ConnectionString, dbConfig.MigrationsAssembly, env.IsDevelopment());
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        });
        return services;
    }
}
