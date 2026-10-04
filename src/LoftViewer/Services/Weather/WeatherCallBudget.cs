using LoftViewer.Configuration;
using Microsoft.Extensions.Options;

namespace LoftViewer.Services.Weather;

/// <summary>Caps outbound OpenWeatherMap calls per UTC day so the free tier is never exceeded.</summary>
public sealed class WeatherCallBudget(IOptions<WeatherOptions> options, TimeProvider timeProvider)
{
    private readonly Lock _gate = new();
    private DateOnly _day;
    private int _used;

    public bool TryConsume(int calls)
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            if (today != _day)
            {
                _day = today;
                _used = 0;
            }

            if (_used + calls > options.Value.MaxCallsPerDay)
            {
                return false;
            }

            _used += calls;
            return true;
        }
    }
}
