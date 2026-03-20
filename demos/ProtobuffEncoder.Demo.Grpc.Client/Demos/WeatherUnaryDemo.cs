using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Client.Demos;

public class WeatherUnaryDemo(IWeatherGrpcService client) : IDemoStrategy
{
    public string DisplayName => "Weather — Unary GetForecast";

    public async Task ExecuteAsync()
    {
        Console.WriteLine("\n  Calling Weather/GetForecast (Unary)...");

        var response = await client.GetForecast(new WeatherRequest
        {
            City = "Amsterdam",
            Days = 5,
            IncludeHourly = false
        });

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  {response.City} — {response.Forecasts.Count} day forecast:");
        Console.ResetColor();

        foreach (var day in response.Forecasts)
        {
            Console.WriteLine($"    {day.Date}: {day.TemperatureMin:F1}–{day.TemperatureMax:F1}°C | {day.Condition} | Humidity: {day.HumidityPercent}%");
        }

        Console.WriteLine();
    }
}
