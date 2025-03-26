using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LoftViewer.interfaces;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly IWeather _weatherService;

    public WeatherController(IWeather weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWeather([FromQuery] string city)
    {
        if (string.IsNullOrEmpty(city))
        {
            return BadRequest("City is required.");
        }

        var weatherData = await _weatherService.GetWeatherAsync(city);
        if (weatherData == null)
        {
            return NotFound($"Weather data for {city} not found.");
        }

        return Ok(weatherData);
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatestWeather()
    {
        var weatherData = await _weatherService.LoadWeatherDataAsync();
        if (weatherData == null)
            return NotFound("No weather data available.");

        return Ok(weatherData);
    }
}