using MultiDashboard.Models;

namespace MultiDashboard.Services;

public class WeatherService
{
    private readonly Random _random = new();
    private double _currentTemp = 72.0;
    private readonly string[] _conditions = { "Sunny", "Cloudy", "Rainy", "Stormy", "Clear", "Partly Cloudy" };
    private int _conditionIndex = 0;

    public async Task<WeatherData> GetWeatherAsync()
    {
        // Simulate API delay
        await Task.Delay(_random.Next(100, 500));

        // Random walk for temperature
        _currentTemp += _random.NextDouble() * 2 - 1;
        _currentTemp = Math.Clamp(_currentTemp, 32, 95);

        _conditionIndex = (_conditionIndex + 1) % _conditions.Length;

        return new WeatherData
        {
            Temp = Math.Round(_currentTemp, 1),
            Condition = _conditions[_conditionIndex],
            Humidity = _random.Next(30, 80),
            Forecast = GenerateForecast()
        };
    }

    private List<DayForecast> GenerateForecast()
    {
        var forecast = new List<DayForecast>();
        for (int i = 0; i < 5; i++)
        {
            forecast.Add(new DayForecast
            {
                Day = DateTime.Now.AddDays(i + 1).ToString("ddd"),
                High = _random.Next(70, 90),
                Low = _random.Next(50, 70),
                Condition = _conditions[_random.Next(_conditions.Length)]
            });
        }
        return forecast;
    }
}
