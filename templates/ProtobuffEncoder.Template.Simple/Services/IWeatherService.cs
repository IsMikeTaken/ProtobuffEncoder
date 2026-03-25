using ProtobuffEncoder.Attributes;

[ProtoService("WeatherService")]
public interface IWeatherService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherForecast> GetForecast(WeatherRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherForecast> StreamForecasts(WeatherRequest request, CancellationToken ct = default);
}