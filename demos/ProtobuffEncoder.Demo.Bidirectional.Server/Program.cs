using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Transport;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
string[] conditions = ["Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy", "Windy"];

// =============================================================================
// Protobuf WebSocket endpoints (for native clients using ProtobufDuplexStream)
// =============================================================================

app.MapGet("/ws/chat", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Protobuf] Chat client connected");

    await using var duplex = new ProtobufDuplexStream<NotificationMessage, NotificationMessage>(
        new WebSocketStream(ws), ownsStream: true);

    var validator = new ValidationPipeline<NotificationMessage>();
    validator.Require(m => !string.IsNullOrEmpty(m.Text), "Message text cannot be empty");

    int messageCount = 0;
    await foreach (var incoming in duplex.ReceiveAllAsync())
    {
        var validation = validator.Validate(incoming);
        if (!validation.IsValid)
        {
            await duplex.SendAsync(new NotificationMessage
            {
                Source = "Server", Text = $"Rejected: {validation.ErrorMessage}",
                Level = NotificationLevel.Error,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            continue;
        }

        messageCount++;
        app.Logger.LogInformation("[Protobuf] [{Level}] from {Source}: {Text}", incoming.Level, incoming.Source, incoming.Text);

        await duplex.SendAsync(new NotificationMessage
        {
            Source = "Server", Text = $"Ack #{messageCount}: received \"{incoming.Text}\"",
            Level = NotificationLevel.Info,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Tags = ["echo", $"msg-{messageCount}"]
        });

        if (messageCount % 3 == 0)
        {
            await duplex.SendAsync(new NotificationMessage
            {
                Source = "Server",
                Text = $"Milestone! {messageCount} messages processed in this session.",
                Level = NotificationLevel.Warning,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["milestone"]
            });
        }
    }

    app.Logger.LogInformation("[Protobuf] Chat client disconnected. Total: {Count}", messageCount);
});

app.MapGet("/ws/weather-stream", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

    await using var duplex = new ProtobufDuplexStream<WeatherResponse, WeatherRequest>(
        new WebSocketStream(ws), ownsStream: true);

    await foreach (var request in duplex.ReceiveAllAsync())
    {
        app.Logger.LogInformation("[Protobuf] Weather: {City} for {Days} days", request.City, request.Days);
        await duplex.SendAsync(BuildWeatherResponse(request.City, request.Days, request.IncludeHourly));
    }
});

// =============================================================================
// JSON WebSocket endpoints (for the browser dashboard)
// =============================================================================

app.MapGet("/ws/chat/json", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Dashboard] Chat client connected");

    var validator = new ValidationPipeline<NotificationMessage>();
    validator.Require(m => !string.IsNullOrEmpty(m.Text), "Message text cannot be empty");

    int messageCount = 0;
    var buf = new byte[4096];

    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) break;

        var json = Encoding.UTF8.GetString(buf, 0, result.Count);
        var incoming = JsonSerializer.Deserialize<ChatJsonMessage>(json, jsonOpts);
        if (incoming is null) continue;

        var msg = new NotificationMessage
        {
            Source = incoming.Source ?? "Browser",
            Text = incoming.Text ?? "",
            Level = (NotificationLevel)(incoming.Level),
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Tags = incoming.Tags ?? []
        };

        // Encode to protobuf and back to prove the round-trip
        byte[] encoded = ProtobuffEncoder.ProtobufEncoder.Encode(msg);
        var decoded = ProtobuffEncoder.ProtobufEncoder.Decode<NotificationMessage>(encoded);

        var validation = validator.Validate(decoded);
        if (!validation.IsValid)
        {
            await SendJson(ws, new ChatJsonMessage
            {
                Source = "Server", Text = $"Rejected: {validation.ErrorMessage}",
                Level = 2, Tags = [], ByteSize = encoded.Length
            });
            continue;
        }

        messageCount++;
        app.Logger.LogInformation("[Dashboard] [{Level}] from {Source}: {Text} ({Bytes}b protobuf)",
            decoded.Level, decoded.Source, decoded.Text, encoded.Length);

        await SendJson(ws, new ChatJsonMessage
        {
            Source = "Server",
            Text = $"Ack #{messageCount}: received \"{decoded.Text}\"",
            Level = 0,
            Tags = ["echo", $"msg-{messageCount}"],
            ByteSize = encoded.Length
        });

        if (messageCount % 3 == 0)
        {
            var milestone = new NotificationMessage
            {
                Source = "Server",
                Text = $"Milestone! {messageCount} messages processed.",
                Level = NotificationLevel.Warning,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["milestone"]
            };
            byte[] milestoneBytes = ProtobuffEncoder.ProtobufEncoder.Encode(milestone);

            await SendJson(ws, new ChatJsonMessage
            {
                Source = "Server",
                Text = milestone.Text,
                Level = 1,
                Tags = ["milestone"],
                ByteSize = milestoneBytes.Length
            });
        }
    }

    app.Logger.LogInformation("[Dashboard] Chat client disconnected. Total: {Count}", messageCount);
});

app.MapGet("/ws/weather-stream/json", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Dashboard] Weather client connected");

    var buf = new byte[4096];

    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) break;

        var json = Encoding.UTF8.GetString(buf, 0, result.Count);
        var req = JsonSerializer.Deserialize<WeatherJsonRequest>(json, jsonOpts);
        if (req is null) continue;

        // Encode request to protobuf
        var request = new WeatherRequest { City = req.City ?? "Unknown", Days = req.Days, IncludeHourly = req.IncludeHourly };
        byte[] reqBytes = ProtobuffEncoder.ProtobufEncoder.Encode(request);
        var decodedReq = ProtobuffEncoder.ProtobufEncoder.Decode<WeatherRequest>(reqBytes);

        app.Logger.LogInformation("[Dashboard] Weather: {City} ({Bytes}b protobuf)", decodedReq.City, reqBytes.Length);

        // Build response, encode to protobuf, decode back
        var response = BuildWeatherResponse(decodedReq.City, decodedReq.Days, decodedReq.IncludeHourly);
        byte[] resBytes = ProtobuffEncoder.ProtobufEncoder.Encode(response);
        var decodedRes = ProtobuffEncoder.ProtobufEncoder.Decode<WeatherResponse>(resBytes);

        // Send as JSON with byte size metadata
        var responseJson = JsonSerializer.Serialize(new
        {
            decodedRes.City,
            decodedRes.GeneratedAtUtc,
            decodedRes.Forecasts,
            ProtobufRequestBytes = reqBytes.Length,
            ProtobufResponseBytes = resBytes.Length
        }, jsonOpts);

        await ws.SendAsync(Encoding.UTF8.GetBytes(responseJson),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }
});

app.MapGet("/health", () => Results.Ok("Bidirectional server is running"));

app.Run();

// --- Helpers ---

WeatherResponse BuildWeatherResponse(string city, int days, bool includeWind) => new()
{
    City = city,
    GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    Forecasts = Enumerable.Range(0, Math.Clamp(days, 1, 14)).Select(i => new DayForecast
    {
        Date = DateTime.UtcNow.AddDays(i).ToString("yyyy-MM-dd"),
        TemperatureMin = Math.Round(Random.Shared.NextDouble() * 15 - 5, 1),
        TemperatureMax = Math.Round(Random.Shared.NextDouble() * 20 + 10, 1),
        Condition = conditions[Random.Shared.Next(conditions.Length)],
        HumidityPercent = Random.Shared.Next(30, 95),
        WindSpeed = includeWind ? Math.Round(Random.Shared.NextDouble() * 50, 1) : null
    }).ToList()
};

async Task SendJson(WebSocket ws, ChatJsonMessage msg)
{
    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, jsonOpts));
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}

// --- JSON DTOs for browser communication ---

record ChatJsonMessage
{
    public string? Source { get; init; }
    public string? Text { get; init; }
    public int Level { get; init; }
    public List<string>? Tags { get; init; }
    public int ByteSize { get; init; }
}

record WeatherJsonRequest
{
    public string? City { get; init; }
    public int Days { get; init; }
    public bool IncludeHourly { get; init; }
}

// --- WebSocket → Stream adapter ---

sealed class WebSocketStream : Stream
{
    private readonly WebSocket _ws;
    private readonly MemoryStream _receiveBuffer = new();
    private bool _receiveComplete;

    public WebSocketStream(WebSocket ws) => _ws = ws;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer, offset, count);
        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;
        var segment = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(segment), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) { _receiveComplete = true; return 0; }
            _receiveBuffer.Write(segment, 0, result.Count);
        } while (!result.EndOfMessage);

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer, offset, count);
    }

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
            if (result.MessageType == WebSocketMessageType.Close) { _receiveComplete = true; return 0; }
            _receiveBuffer.Write(segment, 0, result.Count);
        } while (!result.EndOfMessage);

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer.Span);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _ws.SendAsync(new ArraySegment<byte>(buffer, offset, count),
            WebSocketMessageType.Binary, true, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use WriteAsync");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _receiveBuffer.Dispose();
            if (_ws.State == WebSocketState.Open)
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None)
                    .GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
