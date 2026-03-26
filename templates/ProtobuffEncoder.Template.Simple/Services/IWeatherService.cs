using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Template.Simple.Contracts;

namespace ProtobuffEncoder.Template.Simple.Services;

[ProtoService("WeatherService")]
public interface IWeatherService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherForecast> GetForecast(WeatherRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherForecast> StreamForecasts(WeatherRequest request, CancellationToken ct = default);
}
