using System.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// Extension methods for HttpClient to send and receive protobuf messages.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Sends a POST request with a protobuf-encoded body and deserializes the protobuf response.
    /// </summary>
    public static async Task<TResponse> PostProtobufAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TResponse : new()
    {
        using var content = new ProtobufHttpContent(request!);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ProtobufMediaType.Protobuf));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return ProtobufEncoder.Decode<TResponse>(bytes);
    }

    /// <summary>
    /// Sends a POST request with a protobuf-encoded body (fire-and-forget, no deserialized response).
    /// </summary>
    public static async Task<HttpResponseMessage> PostProtobufAsync<TRequest>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var content = new ProtobufHttpContent(request!);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ProtobufMediaType.Protobuf));

        var response = await client.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    /// <summary>
    /// Sends a GET request and deserializes the protobuf response.
    /// </summary>
    public static async Task<T> GetProtobufAsync<T>(
        this HttpClient client,
        string requestUri,
        CancellationToken cancellationToken = default)
        where T : new()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ProtobufMediaType.Protobuf));

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return ProtobufEncoder.Decode<T>(bytes);
    }
}
