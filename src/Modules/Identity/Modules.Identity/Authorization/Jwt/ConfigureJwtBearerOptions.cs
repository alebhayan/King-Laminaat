using FSH.Framework.Core.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace FSH.Modules.Identity.Authorization.Jwt;

public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _options;
    private readonly string _hangfireRoute;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> options, IConfiguration configuration)
    {
        _options = options.Value;

        // Read Hangfire dashboard route from configuration (HangfireOptions:Route).
        // Fallback to "/jobs" if not configured.
        _hangfireRoute = configuration.GetSection("HangfireOptions").GetValue<string>("Route") ?? "/jobs";
    }

    public void Configure(JwtBearerOptions options)
    {
        Configure(string.Empty, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.ASCII.GetBytes(_options.SigningKey);

        options.RequireHttpsMetadata = true;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidIssuer = _options.Issuer,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidAudience = _options.Audience,
            ValidateAudience = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();

                var path = context.HttpContext.Request.Path;

                if (!context.Response.HasStarted)
                {
                    var method = context.HttpContext.Request.Method;

                    // You can include more details if needed like headers, etc.
                    throw new UnauthorizedException($"Unauthorized access to {method} {path}");
                }

                return Task.CompletedTask;
            },
            OnForbidden = _ => throw new ForbiddenException(),
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/notifications", StringComparison.OrdinalIgnoreCase))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    }
}
