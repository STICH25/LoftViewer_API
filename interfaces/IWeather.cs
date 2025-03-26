using LoftViewer.Models;
using System.Threading.Tasks;

namespace LoftViewer.interfaces
{
    public interface IWeather
    {
        Task<WeatherModel> GetWeatherAsync(string city);
        Task SaveWeatherDataAsync(WeatherModel data);
        Task<WeatherModel?> LoadWeatherDataAsync();
    }
}