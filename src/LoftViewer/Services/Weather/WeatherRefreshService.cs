using LoftViewer.Configuration;
using Microsoft.Extensions.Options;

namespace LoftViewer.Services.Weather;

/// <summary>Periodically fetches the default city's weather into <see cref="WeatherCache"/>.</summary>
public sealed partial class WeatherRefreshService(
    IServiceScopeFactory scopeFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    ILogger<WeatherRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            LogDisabled(logger);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.RefreshMinutes));
        do
        {
            await RefreshAsync(settings.DefaultCity, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(string city, CancellationToken cancellationToken)
    {
        // The typed HttpClient is transient; resolve it per tick so its handler can be recycled.
        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IWeatherClient>();

        if (await client.GetCurrentAsync(city, cancellationToken) is { } report)
        {
            cache.Latest = report;
            LogRefreshed(logger, city);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "WeatherSettings:ApiKey is not set; weather refresh is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Weather refreshed for {City}")]
    private static partial void LogRefreshed(ILogger logger, string city);
}
