using Microsoft.Extensions.DependencyInjection;
namespace FSH.Framework.Blazor.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHeroUI(this IServiceCollection services)
    {
        services.AddMudServices(options =>
        {
            options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            options.SnackbarConfiguration.ShowCloseIcon = true;
        });

        services.AddMudPopoverService();

        return services;
    }
}