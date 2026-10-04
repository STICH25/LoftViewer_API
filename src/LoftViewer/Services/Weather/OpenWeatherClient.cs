using System.Globalization;
using System.Text.Json;
using LoftViewer.Configuration;
using LoftViewer.Models;
using LoftViewer.Utilities;
using Microsoft.Extensions.Options;

namespace LoftViewer.Services.Weather;

public interface IWeatherClient
{
    bool IsConfigured { get; }

    /// <summary>Current conditions for <paramref name="city"/>, or null when the city is unknown or the call fails.</summary>
    Task<WeatherReport?> GetCurrentAsync(string city, CancellationToken cancellationToken);
}

/// <summary>Typed client for OpenWeatherMap's geocoding and One Call 3.0 APIs.</summary>
public sealed partial class OpenWeatherClient(
    HttpClient http,
    IOptions<WeatherOptions> options,
    WeatherCallBudget budget,
    TimeProvider timeProvider,
    ILogger<OpenWeatherClient> logger) : IWeatherClient
{
    // A lookup is two calls: geocode the city, then fetch conditions for its coordinates.
    private const int CallsPerLookup = 2;

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<WeatherReport?> GetCurrentAsync(string city, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        if (!budget.TryConsume(CallsPerLookup))
        {
            LogBudgetExhausted(logger);
            return null;
        }

        try
        {
            var coordinates = await GeocodeAsync(city, cancellationToken);
            return coordinates is { } c ? await GetConditionsAsync(city, c.Latitude, c.Longitude, cancellationToken) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            LogLookupFailed(logger, ex, city);
            return null;
        }
    }

    private async Task<(string Latitude, string Longitude)?> GeocodeAsync(string city, CancellationToken cancellationToken)
    {
        var url = $"geo/1.0/direct?q={Uri.EscapeDataString(city)}&limit=1&appid={options.Value.ApiKey}";
        using var document = await GetJsonAsync(url, cancellationToken);

        var results = document.RootElement;
        if (results.GetArrayLength() == 0)
        {
            LogCityNotFound(logger, city);
            return null;
        }

        return (
            results[0].GetProperty("lat").GetDecimal().ToString(CultureInfo.InvariantCulture),
            results[0].GetProperty("lon").GetDecimal().ToString(CultureInfo.InvariantCulture));
    }

    private async Task<WeatherReport> GetConditionsAsync(string city, string latitude, string longitude, CancellationToken cancellationToken)
    {
        var url = $"data/3.0/onecall?lat={latitude}&lon={longitude}&appid={options.Value.ApiKey}&units=imperial&exclude=minutely,hourly,daily,alerts";
        using var document = await GetJsonAsync(url, cancellationToken);

        var current = document.RootElement.GetProperty("current");
        var weather = current.GetProperty("weather")[0];

        return new WeatherReport(
            City: city,
            Temperature: Math.Round(current.GetProperty("temp").GetDouble()).ToString(CultureInfo.InvariantCulture),
            Description: weather.GetProperty("description").GetString(),
            WindDirection: WindDirection.FromDegrees(current.GetProperty("wind_deg").GetDouble()),
            WindSpeed: Math.Round(current.GetProperty("wind_speed").GetDouble()).ToString(CultureInfo.InvariantCulture),
            Humidity: current.GetProperty("humidity").GetInt32(),
            IconUrl: IconFor(weather.GetProperty("main").GetString()),
            Timestamp: timeProvider.GetUtcNow());
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    }

    internal static string IconFor(string? mainCondition) => mainCondition switch
    {
        "Clear" => "/images/weathericons/sun.gif",
        "Rain" => "/images/weathericons/rain.gif",
        "Clouds" => "/images/weathericons/cloudy.gif",
        "Thunderstorm" => "/images/weathericons/storm.gif",
        "Drizzle" => "/images/weathericons/drizzle.gif",
        "Mist" or "Fog" or "Haze" => "/images/weathericons/foggy.gif",
        _ => "/images/weathericons/default.gif",
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily OpenWeatherMap call budget reached; skipping lookup")]
    private static partial void LogBudgetExhausted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No coordinates found for city {City}")]
    private static partial void LogCityNotFound(ILogger logger, string city);

    [LoggerMessage(Level = LogLevel.Error, Message = "Weather lookup failed for {City}")]
    private static partial void LogLookupFailed(ILogger logger, Exception exception, string city);
}
