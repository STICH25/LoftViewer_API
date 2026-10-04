namespace LoftViewer.Models;

public sealed record WeatherReport(
    string City,
    string Temperature,
    string? Description,
    string WindDirection,
    string WindSpeed,
    int Humidity,
    string IconUrl,
    DateTimeOffset Timestamp);
