using FSH.Framework.Shared.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSH.Framework.Persistence;

internal sealed class DatabaseOptionsStartupLogger : IHostedService
{
    private readonly ILogger<DatabaseOptionsStartupLogger> _logger;
    private readonly IOptions<DatabaseOptions> _options;

    public DatabaseOptionsStartupLogger(
        ILogger<DatabaseOptionsStartupLogger> logger,
        IOptions<DatabaseOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        _logger.LogInformation("current db provider: {Provider}", options.Provider);
        _logger.LogInformation("for docs: https://www.fullstackhero.net");
        _logger.LogInformation("sponsor: https://opencollective.com/fullstackhero");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

