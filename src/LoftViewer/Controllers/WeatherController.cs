using LoftViewer.Models;
using LoftViewer.Services.Weather;
using Microsoft.AspNetCore.Mvc;

namespace LoftViewer.Controllers;

[ApiController]
[Route("api/weather")]
[Produces("application/json")]
public sealed class WeatherController(IWeatherClient weather, WeatherCache cache) : ControllerBase
{
    /// <summary>Live conditions for any city. Counts against the daily OpenWeatherMap budget.</summary>
    [HttpGet]
    [ProducesResponseType<WeatherReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetForCity([FromQuery] string? city, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return Problem("City is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!weather.IsConfigured)
        {
            return Problem("Weather is not configured on this server.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var report = await weather.GetCurrentAsync(city.Trim(), cancellationToken);
        return report is null
            ? Problem($"Weather data for {city} was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(report);
    }

    /// <summary>The most recent background refresh of the default city.</summary>
    [HttpGet("latest")]
    [ProducesResponseType<WeatherReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetLatest() =>
        cache.Latest is { } report
            ? Ok(report)
            : Problem("No weather data available yet.", statusCode: StatusCodes.Status404NotFound);
}
