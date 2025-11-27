using FSH.Framework.Mailing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FSH.Framework.Mailing;

public static class Extensions
{
    public static IServiceCollection AddHeroMailing(this IServiceCollection services)
    {
        services.AddTransient<IMailService, SmtpMailService>();
        services
            .AddOptions<MailOptions>()
            .BindConfiguration(nameof(MailOptions))
            .Validate(o => !string.IsNullOrWhiteSpace(o.From), "MailOptions: From is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "MailOptions: Host is required.")
            .Validate(o => o.Port > 0, "MailOptions: Port must be greater than zero.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName), "MailOptions: UserName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "MailOptions: Password is required.")
            .ValidateOnStart();
        return services;
    }
}