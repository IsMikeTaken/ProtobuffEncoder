using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Client.Demos;

public class WeatherStreamingDemo(IWeatherGrpcService client) : IDemoStrategy
{
    public string DisplayName => "Weather — Server Streaming";

    public async Task ExecuteAsync()
    {
        Console.WriteLine($"\n  Calling Weather/StreamForecasts (Server Streaming)...");
        Console.WriteLine("  Receiving day-by-day forecasts:\n");

        int count = 0;
        await foreach (var response in client.StreamForecasts(
            new WeatherRequest { City = "Tokyo", Days = 4, IncludeHourly = true }))
        {
            count++;
            var day = response.Forecasts[0];
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  [{count}/4] ");
            Console.ResetColor();
            Console.WriteLine($"{day.Date}: {day.TemperatureMin:F1}–{day.TemperatureMax:F1}°C | {day.Condition}");
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  Stream complete. Received {count} forecasts.\n");
        Console.ResetColor();
    }
}
