using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtobuffEncoder.AspNetCore.Tests.Fixtures;

namespace ProtobuffEncoder.AspNetCore.Tests;

/// <summary>
/// Component-Simulation pattern: tests HttpClientExtensions with a real ASP.NET Core pipeline.
/// </summary>
public class HttpClientIntegrationTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Echo endpoint — returns same model with modified fields
                        endpoints.MapPost("/api/echo", async context =>
                        {
                            using var ms = new MemoryStream();
                            await context.Request.Body.CopyToAsync(ms);
                            var request = ProtobufEncoder.Decode<TestRequest>(ms.ToArray());

                            var response = new TestResponse
                            {
                                Id = request.Id,
                                Result = $"Echo: {request.Name}",
                                Success = true
                            };

                            var bytes = ProtobufEncoder.Encode(response);
                            context.Response.ContentType = ProtobufMediaType.Protobuf;
                            await context.Response.Body.WriteAsync(bytes);
                        });

                        // Fire-and-forget endpoint
                        endpoints.MapPost("/api/notify", async context =>
                        {
                            using var ms = new MemoryStream();
                            await context.Request.Body.CopyToAsync(ms);
                            context.Response.StatusCode = 200;
                        });

                        // GET endpoint that returns protobuf
                        endpoints.MapGet("/api/status", async context =>
                        {
                            var response = new TestResponse
                            {
                                Id = 1,
                                Result = "running",
                                Success = true
                            };

                            var bytes = ProtobufEncoder.Encode(response);
                            context.Response.ContentType = ProtobufMediaType.Protobuf;
                            await context.Response.Body.WriteAsync(bytes);
                        });
                    });
                });
            })
            .Build();

        await _host.StartAsync();
        _client = _host.GetTestServer().CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    #region Component-Simulation Pattern — POST with response

    [Fact]
    public async Task PostProtobufAsync_WithResponse_RoundTrips()
    {
        var request = new TestRequest { Id = 42, Name = "hello" };
        var response = await _client!.PostProtobufAsync<TestRequest, TestResponse>(
            "/api/echo", request);

        Assert.Equal(42, response.Id);
        Assert.Equal("Echo: hello", response.Result);
        Assert.True(response.Success);
    }

    #endregion

    #region Component-Simulation Pattern — fire-and-forget POST

    [Fact]
    public async Task PostProtobufAsync_FireAndForget_ReturnsSuccessResponse()
    {
        var request = new TestRequest { Id = 1, Name = "fire" };
        using var response = await _client!.PostProtobufAsync<TestRequest>(
            "/api/notify", request);

        Assert.True(response.IsSuccessStatusCode);
    }

    #endregion

    #region Component-Simulation Pattern — GET protobuf

    [Fact]
    public async Task GetProtobufAsync_ReturnsDeserializedResponse()
    {
        var response = await _client!.GetProtobufAsync<TestResponse>("/api/status");

        Assert.Equal(1, response.Id);
        Assert.Equal("running", response.Result);
        Assert.True(response.Success);
    }

    #endregion

    #region Process-Sequence Pattern — multiple sequential requests

    [Fact]
    public async Task MultipleRequests_AllSucceed()
    {
        for (int i = 1; i <= 5; i++)
        {
            var request = new TestRequest { Id = i, Name = $"item-{i}" };
            var response = await _client!.PostProtobufAsync<TestRequest, TestResponse>(
                "/api/echo", request);

            Assert.Equal(i, response.Id);
            Assert.Equal($"Echo: item-{i}", response.Result);
        }
    }

    #endregion

    #region Loading-Test Pattern — concurrent requests

    [Fact]
    public async Task ConcurrentRequests_AllSucceed()
    {
        var tasks = Enumerable.Range(1, 20).Select(async i =>
        {
            var request = new TestRequest { Id = i, Name = $"concurrent-{i}" };
            var response = await _client!.PostProtobufAsync<TestRequest, TestResponse>(
                "/api/echo", request);
            return response;
        }).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(20, results.Length);
    }

    #endregion
}
