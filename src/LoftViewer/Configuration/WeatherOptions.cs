using System.ComponentModel.DataAnnotations;

namespace LoftViewer.Configuration;

public sealed class WeatherOptions
{
    public const string SectionName = "WeatherSettings";

    /// <summary>OpenWeatherMap key. Leave empty to disable the weather feature.</summary>
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string DefaultCity { get; set; } = "Tampa";

    /// <summary>How often the background refresh fetches <see cref="DefaultCity"/>.</summary>
    [Range(1, 24 * 60)]
    public int RefreshMinutes { get; set; } = 10;

    /// <summary>Outbound call budget per UTC day. The One Call 3.0 free tier allows 1,000.</summary>
    [Range(1, 100_000)]
    public int MaxCallsPerDay { get; set; } = 900;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
