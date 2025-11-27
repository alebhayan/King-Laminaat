using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FSH.Framework.Caching;

public static class Extensions
{
    public static IServiceCollection AddHeroCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<ICacheService, DistributedCacheService>();
        ArgumentNullException.ThrowIfNull(configuration);

        var cacheOptions = configuration.GetSection(nameof(CachingOptions)).Get<CachingOptions>();
        if (cacheOptions == null || string.IsNullOrEmpty(cacheOptions.Redis))
        {
            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            var config = ConfigurationOptions.Parse(cacheOptions.Redis);
            config.AbortOnConnectFail = true;

            options.ConfigurationOptions = config;
        });

        return services;
    }
}