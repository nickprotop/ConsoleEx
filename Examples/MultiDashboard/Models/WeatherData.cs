namespace MultiDashboard.Models;

public class WeatherData
{
    public double Temp { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Humidity { get; set; }
    public List<DayForecast> Forecast { get; set; } = new();
}

public class DayForecast
{
    public string Day { get; set; } = string.Empty;
    public int High { get; set; }
    public int Low { get; set; }
    public string Condition { get; set; } = string.Empty;
}
