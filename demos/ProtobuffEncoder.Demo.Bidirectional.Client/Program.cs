using System.Net.WebSockets;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Transport;

Console.WriteLine("=== Protobuf Bidirectional Streaming Client ===\n");

var serverUrl = args.Length > 0 ? args[0] : "ws://localhost:5300";

// --- Demo 1: Chat (same type both directions) ---
Console.WriteLine("--- Demo 1: Bidirectional Chat ---");
await RunChatDemo(serverUrl);

// --- Demo 2: Weather Stream (different types each direction) ---
Console.WriteLine("\n--- Demo 2: Weather Request/Response Stream ---");
await RunWeatherDemo(serverUrl);

Console.WriteLine("\nAll demos complete.");

static async Task RunChatDemo(string serverUrl)
{
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri($"{serverUrl}/ws/chat"), CancellationToken.None);
    Console.WriteLine("  Connected to chat endpoint");

    await using var duplex = new ProtobufDuplexStream<NotificationMessage, NotificationMessage>(
        new ClientWebSocketStream(ws), ownsStream: true);

    // Send several messages and read responses concurrently
    var sendTask = Task.Run(async () =>
    {
        string[] messages =
        [
            "Hello from the client!",
            "How is the server doing?",
            "This is message three — milestone!",
            "Fourth message here.",
            "Goodbye!"
        ];

        foreach (var text in messages)
        {
            var msg = new NotificationMessage
            {
                Source = "Client",
                Text = text,
                Level = NotificationLevel.Info,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["demo"]
            };
            await duplex.SendAsync(msg);
            Console.WriteLine($"  [Sent] {text}");
            await Task.Delay(300); // simulate pacing
        }

        // Send an empty message to trigger validation rejection
        await duplex.SendAsync(new NotificationMessage
        {
            Source = "Client",
            Text = "",
            Level = NotificationLevel.Warning,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        Console.WriteLine("  [Sent] (empty — should be rejected)");

        await Task.Delay(500);
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    });

    var receiveTask = Task.Run(async () =>
    {
        await foreach (var reply in duplex.ReceiveAllAsync())
        {
            var levelColor = reply.Level switch
            {
                NotificationLevel.Error => ConsoleColor.Red,
                NotificationLevel.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Green
            };
            Console.ForegroundColor = levelColor;
            Console.WriteLine($"  [Recv] [{reply.Level}] {reply.Source}: {reply.Text}");
            if (reply.Tags.Count > 0)
                Console.WriteLine($"         Tags: {string.Join(", ", reply.Tags)}");
            Console.ResetColor();
        }
    });

    await Task.WhenAll(sendTask, receiveTask);
    Console.WriteLine("  Chat demo complete.");
}

static async Task RunWeatherDemo(string serverUrl)
{
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri($"{serverUrl}/ws/weather-stream"), CancellationToken.None);
    Console.WriteLine("  Connected to weather stream endpoint");

    await using var duplex = new ProtobufDuplexStream<WeatherRequest, WeatherResponse>(
        new ClientWebSocketStream(ws), ownsStream: true);

    string[] cities = ["Amsterdam", "London", "Tokyo"];

    foreach (var city in cities)
    {
        var request = new WeatherRequest
        {
            City = city,
            Days = 3,
            IncludeHourly = city == "Tokyo" // only Tokyo gets wind speed
        };

        await duplex.SendAsync(request);
        Console.WriteLine($"  [Sent] Weather request for {city}");

        var response = await duplex.ReceiveAsync();
        if (response is null) break;

        Console.WriteLine($"  [Recv] {response.City} — {response.Forecasts.Count} day forecast:");
        foreach (var day in response.Forecasts)
        {
            var wind = day.WindSpeed.HasValue ? $" | Wind: {day.WindSpeed:F1} km/h" : "";
            Console.WriteLine($"         {day.Date}: {day.TemperatureMin:F1}°C – {day.TemperatureMax:F1}°C | {day.Condition} | Humidity: {day.HumidityPercent}%{wind}");
        }
    }

    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    Console.WriteLine("  Weather demo complete.");
}

/// <summary>
/// Adapts a ClientWebSocket to a Stream for use with ProtobufDuplexStream.
/// </summary>
sealed class ClientWebSocketStream : Stream
{
    private readonly ClientWebSocket _ws;
    private readonly MemoryStream _receiveBuffer = new();
    private bool _receiveComplete;

    public ClientWebSocketStream(ClientWebSocket ws) => _ws = ws;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer.Span);

        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;

        var segment = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(segment), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _receiveComplete = true;
                return 0;
            }
            _receiveBuffer.Write(segment, 0, result.Count);
        } while (!result.EndOfMessage);

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer.Span);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use WriteAsync");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _receiveBuffer.Dispose();
        base.Dispose(disposing);
    }
}
