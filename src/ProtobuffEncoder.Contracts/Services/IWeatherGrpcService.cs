using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts.Services;

/// <summary>
/// gRPC service contract for weather operations.
/// Demonstrates Unary and ServerStreaming method patterns.
/// </summary>
[ProtoService("Weather")]
public interface IWeatherGrpcService
{
    /// <summary>
    /// Gets a weather forecast for a single city (unary request-response).
    /// </summary>
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherResponse> GetForecast(WeatherRequest request);

    /// <summary>
    /// Streams forecasts for multiple cities over time (server streaming).
    /// Send one request, receive a stream of responses.
    /// </summary>
    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherResponse> StreamForecasts(WeatherRequest request, CancellationToken cancellationToken = default);
}
