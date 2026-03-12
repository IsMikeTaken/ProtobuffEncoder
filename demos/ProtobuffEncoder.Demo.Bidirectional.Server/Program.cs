using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Transport;
using ProtobuffEncoder.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Register connection managers for each protobuf WebSocket endpoint
builder.Services.AddProtobufWebSocketEndpoint<NotificationMessage, NotificationMessage>();
builder.Services.AddProtobufWebSocketEndpoint<WeatherResponse, WeatherRequest>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromMinutes(2) });
app.UseDefaultFiles();
app.UseStaticFiles();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
string[] conditions = ["Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy", "Windy"];

// =============================================================================
// Protobuf WebSocket endpoints — using the framework
// =============================================================================

var chatManager = app.Services.GetRequiredService<WebSocketConnectionManager<NotificationMessage, NotificationMessage>>();

app.MapProtobufWebSocket<NotificationMessage, NotificationMessage>("/ws/chat", options =>
{
    options.ConfigureReceiveValidation = v =>
        v.Require(m => !string.IsNullOrEmpty(m.Text), "Message text cannot be empty");

    options.OnInvalidReceive = InvalidMessageBehavior.Skip;

    options.OnMessageRejected = async (conn, msg, result) =>
    {
        await conn.SendAsync(new NotificationMessage
        {
            Source = "Server",
            Text = $"Rejected: {result.ErrorMessage}",
            Level = NotificationLevel.Error,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    };

    // Background push task per connection
    options.OnConnect = async conn =>
    {
        app.Logger.LogInformation("[Chat] Client {Id} connected ({Count} total)",
            conn.ConnectionId, chatManager.Count);

        // Fire a background task that pushes system messages on a timer
        _ = Task.Run(async () =>
        {
            string[] tips =
            [
                "Tip: You can type /time to get the server's UTC time.",
                "Tip: Type /quote for a random programming quote.",
                "System: Memory usage is stable at 45MB.",
                "System: Simulated backup completed successfully."
            ];

            try
            {
                while (conn.IsConnected)
                {
                    await Task.Delay(Random.Shared.Next(8000, 15000));
                    if (!conn.IsConnected) break;

                    await conn.SendAsync(new NotificationMessage
                    {
                        Source = "SystemBot",
                        Text = tips[Random.Shared.Next(tips.Length)],
                        Level = NotificationLevel.Info,
                        TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Tags = ["system", "broadcast"]
                    });
                }
            }
            catch { /* connection closed */ }
        });
    };

    int messageCount = 0;

    options.OnMessage = async (conn, incoming) =>
    {
        var count = Interlocked.Increment(ref messageCount);

        app.Logger.LogInformation("[Chat] [{Level}] from {Source}: {Text}",
            incoming.Level, incoming.Source, incoming.Text);

        // Smart command routing
        string responseText;
        var text = incoming.Text.Trim().ToLower();

        if (text == "/ping") responseText = "Pong!";
        else if (text == "/time") responseText = $"Server Time: {DateTime.UtcNow:O}";
        else if (text == "/quote") responseText = "\"There are only two hard things in Computer Science: cache invalidation and naming things.\"";
        else if (text == "/broadcast")
        {
            // Demonstrate broadcast: send to ALL connected chat clients
            await chatManager.BroadcastAsync(new NotificationMessage
            {
                Source = "Broadcast",
                Text = $"[{conn.ConnectionId}] says: {incoming.Text}",
                Level = NotificationLevel.Warning,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["broadcast"]
            });
            return;
        }
        else responseText = $"Ack #{count}: received \"{incoming.Text}\"";

        await conn.SendAsync(new NotificationMessage
        {
            Source = "EchoBot",
            Text = responseText,
            Level = NotificationLevel.Info,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Tags = ["echo", $"msg-{count}"]
        });
    };

    options.OnDisconnect = conn =>
    {
        app.Logger.LogInformation("[Chat] Client {Id} disconnected", conn.ConnectionId);
        return Task.CompletedTask;
    };
});

app.MapProtobufWebSocket<WeatherResponse, WeatherRequest>("/ws/weather-stream", options =>
{
    options.OnConnect = conn =>
    {
        app.Logger.LogInformation("[Weather] Client {Id} connected", conn.ConnectionId);
        return Task.CompletedTask;
    };

    options.OnMessage = async (conn, request) =>
    {
        app.Logger.LogInformation("[Weather] Request: {City} for {Days} days", request.City, request.Days);

        // Progressive streaming: immediate ack, then full response
        await conn.SendAsync(new WeatherResponse
        {
            City = $"[Calculating data for {request.City}...]",
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Forecasts = []
        });

        await Task.Delay(Random.Shared.Next(800, 1500));

        await conn.SendAsync(BuildWeatherResponse(request.City, request.Days, request.IncludeHourly));
    };

    options.OnDisconnect = conn =>
    {
        app.Logger.LogInformation("[Weather] Client {Id} disconnected", conn.ConnectionId);
        return Task.CompletedTask;
    };
});

// =============================================================================
// JSON WebSocket endpoints (browser dashboard bridge)
// =============================================================================

app.MapGet("/ws/chat/json", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Dashboard] Chat client connected");

    var validator = new ValidationPipeline<NotificationMessage>();
    validator.Require(m => !string.IsNullOrEmpty(m.Text), "Message text cannot be empty");

    int messageCount = 0;

    try
    {
        await foreach (var incoming in ReadJsonStreamAsync<ChatJsonMessage>(ws, jsonOpts, ctx.RequestAborted))
        {
            var msg = new NotificationMessage
            {
                Source = incoming.Source ?? "Browser",
                Text = incoming.Text ?? "",
                Level = (NotificationLevel)incoming.Level,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = incoming.Tags ?? []
            };

            var sw = Stopwatch.StartNew();
            byte[] encoded = ProtobuffEncoder.ProtobufEncoder.Encode(msg);
            var decoded = ProtobuffEncoder.ProtobufEncoder.Decode<NotificationMessage>(encoded);
            sw.Stop();

            var validation = validator.Validate(decoded);
            if (!validation.IsValid)
            {
                await SendJsonAsync(ws, new ChatJsonMessage
                {
                    Source = "Server",
                    Text = $"Rejected: {validation.ErrorMessage}",
                    Level = 2, Tags = [],
                    ByteSize = encoded.Length,
                    ProcessingTimeMs = sw.Elapsed.TotalMilliseconds
                }, ctx.RequestAborted);
                continue;
            }

            messageCount++;
            app.Logger.LogInformation("[Dashboard] [{Level}] from {Source}: {Text} ({Bytes}b in {Ms}ms)",
                decoded.Level, decoded.Source, decoded.Text, encoded.Length, sw.Elapsed.TotalMilliseconds.ToString("0.000"));

            await SendJsonAsync(ws, new ChatJsonMessage
            {
                Source = "Server",
                Text = $"Ack #{messageCount}: received \"{decoded.Text}\"",
                Level = 0,
                Tags = ["echo", $"msg-{messageCount}"],
                ByteSize = encoded.Length,
                ProcessingTimeMs = sw.Elapsed.TotalMilliseconds
            }, ctx.RequestAborted);
        }
    }
    catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException) { }

    app.Logger.LogInformation("[Dashboard] Chat client disconnected. Total: {Count}", messageCount);
});

app.MapGet("/ws/weather-stream/json", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Dashboard] Weather client connected");

    try
    {
        await foreach (var req in ReadJsonStreamAsync<WeatherJsonRequest>(ws, jsonOpts, ctx.RequestAborted))
        {
            var sw = Stopwatch.StartNew();

            var request = new WeatherRequest { City = req.City ?? "Unknown", Days = req.Days, IncludeHourly = req.IncludeHourly };
            byte[] reqBytes = ProtobuffEncoder.ProtobufEncoder.Encode(request);
            var decodedReq = ProtobuffEncoder.ProtobufEncoder.Decode<WeatherRequest>(reqBytes);

            var response = BuildWeatherResponse(decodedReq.City, decodedReq.Days, decodedReq.IncludeHourly);
            byte[] resBytes = ProtobuffEncoder.ProtobufEncoder.Encode(response);
            var decodedRes = ProtobuffEncoder.ProtobufEncoder.Decode<WeatherResponse>(resBytes);

            sw.Stop();

            app.Logger.LogInformation("[Dashboard] Weather: {City} (Req: {ReqBytes}b, Res: {ResBytes}b, in {Ms}ms)",
                decodedReq.City, reqBytes.Length, resBytes.Length, sw.Elapsed.TotalMilliseconds.ToString("0.000"));

            var responsePayload = new
            {
                decodedRes.City,
                decodedRes.GeneratedAtUtc,
                decodedRes.Forecasts,
                ProtobufRequestBytes = reqBytes.Length,
                ProtobufResponseBytes = resBytes.Length,
                EncodingTimeMs = sw.Elapsed.TotalMilliseconds
            };

            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responsePayload, jsonOpts)),
                WebSocketMessageType.Text, true, ctx.RequestAborted);
        }
    }
    catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException) { }
});

app.MapGet("/health", () => Results.Ok("Bidirectional server is running"));

app.Run();

// =============================================================================
// Helpers
// =============================================================================

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

async Task SendJsonAsync<T>(WebSocket ws, T msg, CancellationToken ct)
{
    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg, jsonOpts));
    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
}

async IAsyncEnumerable<T> ReadJsonStreamAsync<T>(WebSocket ws, JsonSerializerOptions opts, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
{
    var buffer = ArrayPool<byte>.Shared.Rent(4096);
    using var ms = new MemoryStream();

    try
    {
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) yield break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            ms.Position = 0;

            T? deserializedObj = default;
            try { deserializedObj = await JsonSerializer.DeserializeAsync<T>(ms, opts, ct); }
            catch (JsonException) { continue; }

            if (deserializedObj is not null)
                yield return deserializedObj;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
