namespace LoftViewer.Utilities;

public class ConvertWinDirection
{
    public string ConvertWindDirection(int degrees)
    {
        string[] directions = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        int index = (int)Math.Round(degrees / 22.5) % 16;
        return directions[index];
    }
}