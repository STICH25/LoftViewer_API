namespace LoftViewer.Utilities;

public static class WindDirection
{
    private static readonly string[] Points =
        ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];

    /// <summary>Converts a meteorological bearing in degrees to a 16-point compass direction.</summary>
    public static string FromDegrees(double degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        var index = (int)Math.Round(normalized / 22.5, MidpointRounding.AwayFromZero) % Points.Length;
        return Points[index];
    }
}
