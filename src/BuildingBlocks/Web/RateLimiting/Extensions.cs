using FSH.Framework.Shared.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FSH.Framework.Web.RateLimiting;

public static class Extensions
{
    public static IServiceCollection AddHeroRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(nameof(RateLimitingOptions)).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(nameof(RateLimitingOptions));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            if (!settings.Enabled)
            {
                return;
            }

            string GetPartitionKey(HttpContext context)
            {
                var tenant = context.User?.FindFirst(ClaimConstants.Tenant)?.Value;
                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(tenant)) return $"tenant:{tenant}";
                if (!string.IsNullOrWhiteSpace(userId)) return $"user:{userId}";
                var ip = context.Connection.RemoteIpAddress?.ToString();
                return string.IsNullOrWhiteSpace(ip) ? "ip:unknown" : $"ip:{ip}";
            }

            bool IsHealthPath(PathString path) =>
                path.StartsWithSegments("/health") || path.StartsWithSegments("/healthz") || path.StartsWithSegments("/ready") || path.StartsWithSegments("/live");

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsHealthPath(context.Request.Path))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                var key = GetPartitionKey(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Global.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Global.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = settings.Global.QueueLimit
                    });
            });

            options.AddPolicy<string>("global", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Global.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Global.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = settings.Global.QueueLimit
                    }));

            options.AddPolicy<string>("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.Auth.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.Auth.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = settings.Auth.QueueLimit
                    }));
        });

        return services;
    }

    public static IApplicationBuilder UseHeroRateLimiting(this IApplicationBuilder app)
    {
        var opts = app.ApplicationServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        if (opts.Enabled)
        {
            app.UseRateLimiter();
        }
        return app;
    }
}
