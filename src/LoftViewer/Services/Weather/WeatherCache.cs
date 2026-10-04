using LoftViewer.Models;

namespace LoftViewer.Services.Weather;

/// <summary>Holds the most recent report produced by <see cref="WeatherRefreshService"/>.</summary>
public sealed class WeatherCache
{
    private WeatherReport? _latest;

    public WeatherReport? Latest
    {
        get => Volatile.Read(ref _latest);
        set => Volatile.Write(ref _latest, value);
    }
}
