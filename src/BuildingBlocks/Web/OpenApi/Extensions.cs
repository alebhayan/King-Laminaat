using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace FSH.Framework.Web.OpenApi;

public static class Extensions
{
    public static IServiceCollection AddHeroOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OpenApiOptions>()
            .Bind(configuration.GetSection(nameof(OpenApiOptions)))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Title), "OpenApi:Title is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Description), "OpenApi:Description is required.")
            .ValidateOnStart();

        // Minimal OpenAPI generator (ASP.NET Core 8)
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddDocumentTransformer(async (document, context, ct) =>
            {
                var provider = context.ApplicationServices;
                var openApi = provider.GetRequiredService<IOptions<OpenApiOptions>>().Value;

                // Title/metadata
                document.Info = new OpenApiInfo
                {
                    Title = openApi.Title,
                    Version = openApi.Version,
                    Description = openApi.Description,
                    Contact = openApi.Contact is null ? null : new OpenApiContact
                    {
                        Name = openApi.Contact.Name,
                        Url = openApi.Contact.Url,
                        Email = openApi.Contact.Email
                    },
                    License = openApi.License is null ? null : new OpenApiLicense
                    {
                        Name = openApi.License.Name,
                        Url = openApi.License.Url
                    }
                };
                await Task.CompletedTask;
            });
        });

        return services;
    }

    public static void UseHeroOpenApi(
        this WebApplication app,
        string openApiPath = "/openapi/{documentName}.json")
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi(openApiPath);

        app.MapScalarApiReference(options =>
        {
            var configuration = app.Configuration;
            options
                .WithTitle(configuration["OpenApi:Title"] ?? "FSH API")
                .WithTheme(Scalar.AspNetCore.ScalarTheme.Alternate)
                .EnableDarkMode()
                .HideModels()
                .WithOpenApiRoutePattern(openApiPath)
                .AddPreferredSecuritySchemes("Bearer");
        });
    }
}
