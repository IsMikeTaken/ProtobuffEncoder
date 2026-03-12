using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Transport;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Configure WebSocket options (e.g., keep-alive intervals to prevent proxy timeouts)
var webSocketOptions = new WebSocketOptions { KeepAliveInterval = TimeSpan.FromMinutes(2) };
app.UseWebSockets(webSocketOptions);

app.UseDefaultFiles();
app.UseStaticFiles();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
string[] conditions = ["Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy", "Windy"];

// =============================================================================
// Protobuf WebSocket endpoints
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

    // Create a linked token so we can cancel the background task when the socket closes
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);

    // FEATURE 1: Unsolicited Server Push (Background Thread)
    // This demonstrates true full-duplex capabilities: writing to the stream 
    // on a timer while the main thread is blocked waiting to read.
    var serverPushTask = Task.Run(async () =>
    {
        string[] tips = [
            "Tip: You can type /time to get the server's UTC time.",
            "Tip: Type /quote for a random programming quote.",
            "System: Memory usage is stable at 45MB.",
            "System: Simulated backup completed successfully."
        ];

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(Random.Shared.Next(8000, 15000), cts.Token);

                await duplex.SendAsync(new NotificationMessage
                {
                    Source = "SystemBot",
                    Text = tips[Random.Shared.Next(tips.Length)],
                    Level = NotificationLevel.Info,
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Tags = ["system", "broadcast"]
                });
            }
        }
        catch (OperationCanceledException) { /* Expected on disconnect */ }
    });

    try
    {
        // FEATURE 2: Smart Command Routing
        await foreach (var incoming in duplex.ReceiveAllAsync().WithCancellation(cts.Token))
        {
            var validation = validator.Validate(incoming);
            if (!validation.IsValid)
            {
                await duplex.SendAsync(new NotificationMessage { Source = "Server", Text = $"Rejected: {validation.ErrorMessage}", Level = NotificationLevel.Error, TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                continue;
            }

            messageCount++;
            app.Logger.LogInformation("[Protobuf] [{Level}] from {Source}: {Text}", incoming.Level, incoming.Source, incoming.Text);

            // Handle Bot Commands
            string responseText;
            var text = incoming.Text.Trim().ToLower();

            if (text == "/ping") responseText = "Pong! 🏓";
            else if (text == "/time") responseText = $"Server Time: {DateTime.UtcNow:O}";
            else if (text == "/quote") responseText = "“There are only two hard things in Computer Science: cache invalidation and naming things.”";
            else responseText = $"Ack #{messageCount}: received \"{incoming.Text}\"";

            await duplex.SendAsync(new NotificationMessage
            {
                Source = "EchoBot",
                Text = responseText,
                Level = NotificationLevel.Info,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["echo", $"msg-{messageCount}"]
            });
        }
    }
    catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException) { }
    finally
    {
        cts.Cancel(); // Ensure the background push task stops when the user disconnects
    }

    app.Logger.LogInformation("[Protobuf] Chat client disconnected. Total: {Count}", messageCount);
});

app.MapGet("/ws/weather-stream", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    app.Logger.LogInformation("[Protobuf] Weather client connected");

    await using var duplex = new ProtobufDuplexStream<WeatherResponse, WeatherRequest>(
        new WebSocketStream(ws), ownsStream: true);

    try
    {
        await foreach (var request in duplex.ReceiveAllAsync().WithCancellation(ctx.RequestAborted))
        {
            app.Logger.LogInformation("[Protobuf] Weather request: {City} for {Days} days", request.City, request.Days);

            // FEATURE 3: Progressive Streaming
            // 1. Send an immediate "Ack/Processing" frame so the UI feels incredibly responsive
            await duplex.SendAsync(new WeatherResponse
            {
                City = $"[Calculating data for {request.City}...]",
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Forecasts = [] // Empty array to denote pending state
            });

            // 2. Simulate heavy lifting (e.g., reaching out to a 3rd party Weather API)
            await Task.Delay(Random.Shared.Next(800, 1500), ctx.RequestAborted);

            // 3. Stream the final, calculated payload
            await duplex.SendAsync(BuildWeatherResponse(request.City, request.Days, request.IncludeHourly));
        }
    }
    catch (Exception ex) when (ex is WebSocketException || ex is OperationCanceledException) { }

    app.Logger.LogInformation("[Protobuf] Weather client disconnected");
});

// =============================================================================
// JSON WebSocket endpoints
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
        // Using the abstracted helper to clean up the loop
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
                    Level = 2,
                    Tags = [],
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

// -----------------------------------------------------------------------------
// CORE ENHANCEMENT: Reusable, safe, non-allocating JSON WebSocket Reader
// -----------------------------------------------------------------------------
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
            try
            {
                deserializedObj = await JsonSerializer.DeserializeAsync<T>(ms, opts, ct);
            }
            catch (JsonException)
            {
                // If the browser sends malformed JSON, ignore it rather than crashing the loop
                continue;
            }

            if (deserializedObj is not null)
            {
                yield return deserializedObj;
            }
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}