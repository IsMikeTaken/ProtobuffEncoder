using System.Net.WebSockets;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtobuffEncoder.Transport;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Integration tests for the full WebSocket endpoint pipeline using
/// ASP.NET Core TestHost — exercises real middleware, DI, and routing.
/// </summary>
public class EndpointIntegrationTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _httpClient;

    // Track lifecycle events for assertion
    private static readonly List<string> LifecycleEvents = [];
    private static readonly List<Heartbeat> ReceivedMessages = [];
    private static readonly List<(Heartbeat Message, ValidationResult Result)> RejectedMessages = [];

    public async Task InitializeAsync()
    {
        LifecycleEvents.Clear();
        ReceivedMessages.Clear();
        RejectedMessages.Clear();

        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddProtobufWebSocketEndpoint<Heartbeat, Heartbeat>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Echo endpoint — sends back whatever it receives
                        endpoints.MapProtobufWebSocket<Heartbeat, Heartbeat>("/ws/echo", opts =>
                        {
                            opts.OnConnect = conn =>
                            {
                                LifecycleEvents.Add($"connect:{conn.ConnectionId}");
                                return Task.CompletedTask;
                            };
                            opts.OnMessage = async (conn, msg) =>
                            {
                                ReceivedMessages.Add(msg);
                                await conn.SendAsync(new Heartbeat { Timestamp = msg.Timestamp * 2 });
                            };
                            opts.OnDisconnect = conn =>
                            {
                                LifecycleEvents.Add($"disconnect:{conn.ConnectionId}");
                                return Task.CompletedTask;
                            };
                            opts.OnError = (_, ex) =>
                            {
                                LifecycleEvents.Add($"error:{ex.Message}");
                                return Task.CompletedTask;
                            };
                        });

                        // Validated endpoint — rejects negative timestamps
                        endpoints.MapProtobufWebSocket<Heartbeat, Heartbeat>("/ws/validated", opts =>
                        {
                            opts.ConfigureReceiveValidation = pipeline =>
                            {
                                pipeline.Require(m => m.Timestamp > 0, "Timestamp must be positive");
                            };
                            opts.OnInvalidReceive = InvalidMessageBehavior.Skip;
                            opts.OnMessageRejected = (_, msg, result) =>
                            {
                                RejectedMessages.Add((msg, result));
                                return Task.CompletedTask;
                            };
                            opts.OnMessage = async (conn, msg) =>
                            {
                                ReceivedMessages.Add(msg);
                                await conn.SendAsync(msg);
                            };
                        });
                    });
                });
            })
            .Build();

        await _host.StartAsync();
        _httpClient = _host.GetTestServer().CreateClient();
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        if (_host is not null)
            await _host.StopAsync();
        _host?.Dispose();
    }

    private WebSocketClient CreateWsClient()
        => _host!.GetTestServer().CreateWebSocketClient();

    /// <summary>
    /// Safely close a TestHost WebSocket. TestHost may race server-side disposal
    /// against the client close handshake, so we catch expected IOExceptions.
    /// </summary>
    private static async Task SafeCloseAsync(WebSocket ws)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
        catch (IOException) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
    }

    #region Component-Simulation Pattern — real pipeline round-trip

    [Fact]
    public async Task EchoEndpoint_SendAndReceive_RoundTrip()
    {
        var ws = await CreateWsClient().ConnectAsync(
            new Uri("ws://localhost/ws/echo"), CancellationToken.None);

        await using var duplex = new ProtobufDuplexStream<Heartbeat, Heartbeat>(
            new WebSocketStream(ws), ownsStream: false);

        await duplex.SendAsync(new Heartbeat { Timestamp = 21 });
        var response = await duplex.ReceiveAsync();

        Assert.NotNull(response);
        Assert.Equal(42, response!.Timestamp);

        await SafeCloseAsync(ws);
    }

    #endregion

    #region Process-Sequence Pattern — lifecycle hooks fire in order

    [Fact]
    public async Task EchoEndpoint_LifecycleHooks_FireOnConnectAndDisconnect()
    {
        var ws = await CreateWsClient().ConnectAsync(
            new Uri("ws://localhost/ws/echo"), CancellationToken.None);

        await Task.Delay(50);
        Assert.Contains(LifecycleEvents, e => e.StartsWith("connect:"));

        await SafeCloseAsync(ws);
        await Task.Delay(100);

        Assert.Contains(LifecycleEvents, e => e.StartsWith("disconnect:"));
    }

    #endregion

    #region Process-Sequence Pattern — multiple messages in sequence

    [Fact]
    public async Task EchoEndpoint_MultipleMessages_AllEchoed()
    {
        var ws = await CreateWsClient().ConnectAsync(
            new Uri("ws://localhost/ws/echo"), CancellationToken.None);

        await using var duplex = new ProtobufDuplexStream<Heartbeat, Heartbeat>(
            new WebSocketStream(ws), ownsStream: false);

        for (int i = 1; i <= 5; i++)
        {
            await duplex.SendAsync(new Heartbeat { Timestamp = i });
            var response = await duplex.ReceiveAsync();

            Assert.NotNull(response);
            Assert.Equal(i * 2, response!.Timestamp);
        }

        await SafeCloseAsync(ws);
    }

    #endregion

    #region Process-Rule Pattern — validation rejects bad messages

    [Fact]
    public async Task ValidatedEndpoint_BadMessage_IsSkipped()
    {
        var ws = await CreateWsClient().ConnectAsync(
            new Uri("ws://localhost/ws/validated"), CancellationToken.None);

        await using var duplex = new ProtobufDuplexStream<Heartbeat, Heartbeat>(
            new WebSocketStream(ws), ownsStream: false);

        // Send invalid (timestamp = -1 fails "must be positive")
        // Note: using -1 instead of 0 because 0 encodes to an empty protobuf
        // payload whose trailing empty WebSocket frame confuses stream framing.
        await duplex.SendAsync(new Heartbeat { Timestamp = -1 });
        await Task.Delay(50);

        // Send valid
        await duplex.SendAsync(new Heartbeat { Timestamp = 42 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await duplex.ReceiveAsync(cts.Token);

        Assert.NotNull(response);
        Assert.Equal(42, response!.Timestamp);

        await Task.Delay(50);
        Assert.Single(ReceivedMessages, m => m.Timestamp == 42);
        Assert.Contains(RejectedMessages, r => r.Message.Timestamp == -1);

        await SafeCloseAsync(ws);
    }

    #endregion

    #region Rollback Pattern — connection cleanup after abrupt disconnect

    [Fact]
    public async Task EchoEndpoint_AbruptDisconnect_ServerCleansUp()
    {
        var ws = await CreateWsClient().ConnectAsync(
            new Uri("ws://localhost/ws/echo"), CancellationToken.None);

        await Task.Delay(50);
        Assert.Contains(LifecycleEvents, e => e.StartsWith("connect:"));

        ws.Abort();
        await Task.Delay(200);

        Assert.Contains(LifecycleEvents, e => e.StartsWith("disconnect:"));
    }

    #endregion

    #region Loading-Test Pattern — concurrent clients

    [Fact]
    public async Task EchoEndpoint_MultipleConcurrentClients_AllServed()
    {
        const int clientCount = 10;

        var tasks = Enumerable.Range(1, clientCount).Select(async i =>
        {
            var timestamp = (long)i * 100;
            var ws = await CreateWsClient().ConnectAsync(
                new Uri("ws://localhost/ws/echo"), CancellationToken.None);

            await using var duplex = new ProtobufDuplexStream<Heartbeat, Heartbeat>(
                new WebSocketStream(ws), ownsStream: false);

            await duplex.SendAsync(new Heartbeat { Timestamp = timestamp });
            var response = await duplex.ReceiveAsync();

            await SafeCloseAsync(ws);

            return response?.Timestamp ?? -1;
        }).ToList();

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < clientCount; i++)
            Assert.Equal((i + 1) * 200, results[i]);
    }

    #endregion

    #region Service-Simulation Pattern — non-websocket request gets 400

    [Fact]
    public async Task EchoEndpoint_NonWebSocketRequest_Returns400()
    {
        var response = await _httpClient!.GetAsync("/ws/echo");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Resource-Stress-Test Pattern — rapid connect/disconnect

    [Fact]
    public async Task EchoEndpoint_RapidConnectDisconnect_NoLeaks()
    {
        for (int i = 0; i < 20; i++)
        {
            var ws = await CreateWsClient().ConnectAsync(
                new Uri("ws://localhost/ws/echo"), CancellationToken.None);
            await SafeCloseAsync(ws);
        }

        await Task.Delay(200);

        var manager = _host!.Services.GetRequiredService<WebSocketConnectionManager<Heartbeat, Heartbeat>>();
        Assert.Equal(0, manager.Count);
    }

    #endregion
}
