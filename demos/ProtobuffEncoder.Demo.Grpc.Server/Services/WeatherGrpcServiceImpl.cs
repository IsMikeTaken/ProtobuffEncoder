using System.Runtime.CompilerServices;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Server.Services;

public class WeatherGrpcServiceImpl : IWeatherGrpcService
{
    private static readonly string[] Conditions =
        ["Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy", "Windy"];

    private readonly ILogger<WeatherGrpcServiceImpl> _logger;

    public WeatherGrpcServiceImpl(ILogger<WeatherGrpcServiceImpl> logger) => _logger = logger;

    public Task<WeatherResponse> GetForecast(WeatherRequest request)
    {
        _logger.LogInformation("[Weather/Unary] Forecast for {City}, {Days} days", request.City, request.Days);
        return Task.FromResult(BuildResponse(request.City, request.Days, request.IncludeHourly));
    }

    public async IAsyncEnumerable<WeatherResponse> StreamForecasts(
        WeatherRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Weather/Stream] Streaming for {City}, {Days} days", request.City, request.Days);

        // Stream one day at a time with a realistic delay
        for (int i = 0; i < Math.Clamp(request.Days, 1, 14); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(Random.Shared.Next(300, 800), cancellationToken);

            yield return new WeatherResponse
            {
                City = request.City,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Forecasts =
                [
                    BuildDay(i, request.IncludeHourly)
                ]
            };
        }
    }

    private static WeatherResponse BuildResponse(string city, int days, bool includeWind) => new()
    {
        City = city,
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Forecasts = Enumerable.Range(0, Math.Clamp(days, 1, 14))
            .Select(i => BuildDay(i, includeWind))
            .ToList()
    };

    private static DayForecast BuildDay(int dayOffset, bool includeWind) => new()
    {
        Date = DateTime.UtcNow.AddDays(dayOffset).ToString("yyyy-MM-dd"),
        TemperatureMin = Math.Round(Random.Shared.NextDouble() * 15 - 5, 1),
        TemperatureMax = Math.Round(Random.Shared.NextDouble() * 20 + 10, 1),
        Condition = Conditions[Random.Shared.Next(Conditions.Length)],
        HumidityPercent = Random.Shared.Next(30, 95),
        WindSpeed = includeWind ? Math.Round(Random.Shared.NextDouble() * 50, 1) : null
    };
}
