using System.Globalization;
using System.Reflection.Metadata;
using System.Text.Json;
using LoftViewer.interfaces;
using LoftViewer.Models;
using LoftViewer.Utilities;

namespace LoftViewer.Services;

public class WeatherService : IWeather
{
    private readonly ConvertWinDirection _convertWinDirection;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _filePath = Path.Combine(Directory.GetCurrentDirectory(), "weather_data.json");
    private readonly string _apiCallLogPath = Path.Combine(Directory.GetCurrentDirectory(), "api_call_log.json");
    private readonly ILogger<WeatherService> _logger;
    private int _apiCallCount;
    private const int MaxApiCallsPerDay = 999;
    private Timer? _timer;

    public WeatherService(ILogger<WeatherService> logger)
    {
        _httpClient = new HttpClient();
        _convertWinDirection = new ConvertWinDirection();
        _apiKey = LoadApiKeyFromFile();
        _logger = logger;

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new Exception("API Key for OpenWeatherMap is missing in weathersetting.json");
        }

        _apiCallCount = LoadApiCallCount();
        StartWeatherUpdateTask();
    }

    private void StartWeatherUpdateTask()
    {
        _timer = new Timer(async _ => await UpdateWeatherAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(2));
    }

    private async Task UpdateWeatherAsync()
    {
        string cityName = "Tampa";
        if (_apiCallCount >= MaxApiCallsPerDay)
        {
            _logger.LogWarning("API call limit reached. Skipping weather update.");
            return;
        }
        
        if (File.Exists(_filePath))
        {
            string jsonData = await File.ReadAllTextAsync(_filePath);
            var oldData =  JsonSerializer.Deserialize<WeatherModel>(jsonData);
            cityName = oldData.City;
        }
        
        var weatherData = await GetWeatherAsync(cityName);
        if (weatherData != null)
        {
            await SaveWeatherDataAsync(weatherData);
            _apiCallCount++;
            SaveApiCallCount();
            _logger.LogInformation($"Weather data updated at {DateTime.UtcNow}. API calls today: {_apiCallCount}");
        }
    }

    public async Task<WeatherModel> GetWeatherAsync(string city)
    {
        try
        {
            var (latitude, longitude) = await GetCoordinatesAsync(city);
            if (latitude == null || longitude == null)
                return null;

            string apiUrl = $"https://api.openweathermap.org/data/3.0/onecall?lat={latitude}&lon={longitude}&appid={_apiKey}&units=imperial";
            string response = await _httpClient.GetStringAsync(apiUrl);

            using var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;
            
            // rounded values
            //temperature
            var _temp = root.GetProperty("current").GetProperty("temp").GetDouble();
            var roundedTemp = Math.Round(_temp);
            // wind speed
            var _Speed = root.GetProperty("current").GetProperty("wind_speed").GetDouble();
            var roundedSpeed = Math.Round(_Speed);
            
            
            // icons
            var mainWeather = root.GetProperty("current").GetProperty("weather")[0].GetProperty("main").GetString();
            
            var iconUrl = mainWeather switch
            {
                "Clear" => "/images/weathericons/sun.gif",
                "Rain" => "/images/weathericons/rain.gif",
                "Clouds" => "/images/weathericons/cloudy.gif",
                "Thunderstorm" => "/images/weathericons/storm.gif",
                "Drizzle" => "/images/weathericons/drizzle.gif",
                "Mist" or "Fog" or "Haze" => "/images/weathericons/foggy.gif",
                _ => "/images/weathericons/default.gif"
            };

            var weatherData = new WeatherModel()
            {
                
                
                City = city,
                Temperature = roundedTemp.ToString(),
                Description = root.GetProperty("current").GetProperty("weather")[0].GetProperty("description").GetString(),
                WindDirection = _convertWinDirection.ConvertWindDirection(root.GetProperty("current").GetProperty("wind_deg").GetInt32()),
                WindSpeed = roundedSpeed.ToString(),
                Humidity = root.GetProperty("current").GetProperty("humidity").GetInt32(),
                IconUrl = iconUrl,
                Timestamp = DateTime.UtcNow
            };

            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching weather data: {ex.Message}");
            return null;
        }
    }

    private async Task<(string? Latitude, string? Longitude)> GetCoordinatesAsync(string city)
    {
        try
        {
            string geoApiUrl = $"http://api.openweathermap.org/geo/1.0/direct?q={city}&limit=1&appid={_apiKey}";
            string response = await _httpClient.GetStringAsync(geoApiUrl);

            using var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            if (root.GetArrayLength() == 0)
            {
                _logger.LogWarning($"No coordinates found for city: {city}");
                return (null, null);
            }

            var lat = root[0].GetProperty("lat").GetDecimal().ToString(CultureInfo.InvariantCulture);
            var lon = root[0].GetProperty("lon").GetDecimal().ToString(CultureInfo.InvariantCulture);

            return (lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching coordinates: {ex.Message}");
            return (null, null);
        }
    }

    public async Task SaveWeatherDataAsync(WeatherModel weatherModel)
    {
        try
        {
            string jsonData = JsonSerializer.Serialize(weatherModel, new JsonSerializerOptions { WriteIndented = true });

            string? directoryPath = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(directoryPath) && directoryPath != null)
            {
                Directory.CreateDirectory(directoryPath);
            }

            await File.WriteAllTextAsync(_filePath, jsonData);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving weather data: {ex.Message}");
        }
    }

    public async Task<WeatherModel?> LoadWeatherDataAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var jsonData = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<WeatherModel>(jsonData);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading weather data: {ex.Message}");
            return null;
        }
    }

    private int LoadApiCallCount()
    {
        try
        {
            if (File.Exists(_apiCallLogPath))
            {
                var jsonData = File.ReadAllText(_apiCallLogPath);
                var log = JsonSerializer.Deserialize<WaetherApiCallLogModel>(jsonData);
                if (log?.ApiCallDate.Date == DateTime.UtcNow.Date)
                    return log.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading API call count: {ex.Message}");
        }

        return 0;
    }

    private void SaveApiCallCount()
    {
        try
        {
            var log = new WaetherApiCallLogModel
            {
                ApiCallDate = DateTime.UtcNow,
                Count = _apiCallCount,
            };
            var jsonData = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_apiCallLogPath, jsonData);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving API call count: {ex.Message}");
        }
    }

    private string LoadApiKeyFromFile()
    {
        try
        {
            var json = File.ReadAllText("weathersettings.json");
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            return jsonData?["WeatherSettings"]["ApiKey"]?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading API Key: {ex.Message}");
            return string.Empty;
        }
    }
}