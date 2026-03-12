using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Transport;
using System.Net.WebSockets;

Console.WriteLine("=== Protobuf Bidirectional Streaming Client ===\n");

var serverUrl = args.Length > 0 ? args[0] : "ws://localhost:5300";
bool isVerbose = false;

while (true)
{
    Console.WriteLine("\n===========================================");
    Console.WriteLine(" Select a Demo to Run:");
    Console.WriteLine(" 1. Run Bidirectional Chat Demo");
    Console.WriteLine(" 2. Run Weather Request/Response Demo");
    Console.WriteLine($" 3. Toggle Verbose Logging (Currently: {(isVerbose ? "ON" : "OFF")})");
    Console.WriteLine(" 4. Quit");
    Console.WriteLine("===========================================");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            await RunChatDemo(serverUrl, isVerbose);
            break;
        case "2":
            await RunWeatherDemo(serverUrl, isVerbose);
            break;
        case "3":
            isVerbose = !isVerbose;
            Console.WriteLine($"\n[System] Verbose logging is now {(isVerbose ? "ENABLED" : "DISABLED")}.");
            break;
        case "4":
        case "quit":
        case "exit":
            Console.WriteLine("\nExiting. Goodbye!");
            return;
        default:
            Console.WriteLine("\n[Error] Invalid choice. Please select 1-4.");
            break;
    }
}

// ====================================================================
// DEMO 1: Chat Stream
// ====================================================================
static async Task RunChatDemo(string serverUrl, bool verbose)
{
    Console.WriteLine("\n--- Starting Bidirectional Chat Demo ---");

    // ==========================================
    // CONFIGURATION PHASE
    // ==========================================
    Console.WriteLine("Configure your test run (Press Enter to use defaults):");

    Console.Write("  Number of messages to send [Default: 5]: ");
    if (!int.TryParse(Console.ReadLine(), out int msgCount) || msgCount < 0) msgCount = 5;

    Console.Write("  Delay between messages in ms [Default: 300]: ");
    if (!int.TryParse(Console.ReadLine(), out int delayMs) || delayMs < 0) delayMs = 300;

    Console.Write("  Send an intentionally invalid message at the end? (y/n) [Default: y]: ");
    bool sendInvalid = Console.ReadLine()?.Trim().ToLower() != "n";

    Console.Write("  Custom message text [Default: 'Hello from client']: ");
    string customText = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(customText)) customText = "Hello from client";

    Console.WriteLine($"\n[Config] Sending {msgCount} messages ('{customText}'), {delayMs}ms apart. Invalid msg: {sendInvalid}\n");

    // ==========================================
    // EXECUTION PHASE
    // ==========================================
    LogVerbose(verbose, "Initializing ClientWebSocket...");

    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri($"{serverUrl}/ws/chat"), CancellationToken.None);
    Console.WriteLine("  Connected to chat endpoint.");

    LogVerbose(verbose, "Wrapping WebSocket in ProtobufDuplexStream<NotificationMessage, NotificationMessage>...");
    await using var duplex = new ProtobufDuplexStream<NotificationMessage, NotificationMessage>(
        new ClientWebSocketStream(ws), ownsStream: true);

    LogVerbose(verbose, "Starting concurrent Send and Receive tasks...");

    // --- Send Task ---
    var sendTask = Task.Run(async () =>
    {
        for (int i = 1; i <= msgCount; i++)
        {
            string text = $"{customText} #{i}";
            var msg = new NotificationMessage
            {
                Source = "Client",
                Text = text,
                Level = NotificationLevel.Info,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["demo", "auto-generated"]
            };

            LogVerbose(verbose, $"[SendTask] Serializing and pushing message {i}/{msgCount} to stream...");
            await duplex.SendAsync(msg);
            Console.WriteLine($"  [Sent] {text}");

            if (i < msgCount || sendInvalid) // Don't delay after the last message unless sending the invalid one
            {
                LogVerbose(verbose, $"[SendTask] Pacing next message (delay {delayMs}ms)...");
                await Task.Delay(delayMs);
            }
        }

        // Trigger Validation Rejection conditionally
        if (sendInvalid)
        {
            LogVerbose(verbose, "[SendTask] Firing intentionally invalid (empty) message to test server validation...");
            await duplex.SendAsync(new NotificationMessage
            {
                Source = "Client",
                Text = "",
                Level = NotificationLevel.Warning,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            Console.WriteLine("  [Sent] (empty — should be rejected)");
            await Task.Delay(500); // Give server a moment to reply with the error
        }

        LogVerbose(verbose, "[SendTask] Initiating graceful closure...");
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        LogVerbose(verbose, "[SendTask] Closure frame sent. Send task complete.");
    });

    // --- Receive Task ---
    var receiveTask = Task.Run(async () =>
    {
        LogVerbose(verbose, "[ReceiveTask] Awaiting incoming Protobuf frames via ReceiveAllAsync()...");

        await foreach (var reply in duplex.ReceiveAllAsync())
        {
            LogVerbose(verbose, $"[ReceiveTask] Deserialized incoming frame from {reply.Source}.");

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
        LogVerbose(verbose, "[ReceiveTask] IAsyncEnumerable completed. Connection closed by server.");
    });

    await Task.WhenAll(sendTask, receiveTask);
    Console.WriteLine("--- Chat demo complete ---\n");
}

// ====================================================================
// DEMO 2: Weather Stream
// ====================================================================
static async Task RunWeatherDemo(string serverUrl, bool verbose)
{
    Console.WriteLine("\n--- Starting Weather Request/Response Demo ---");

    // ==========================================
    // CONFIGURATION PHASE
    // ==========================================
    Console.WriteLine("Configure your test run (Press Enter to use defaults):");

    Console.Write("  Enter cities separated by commas [Default: Amsterdam, London, Tokyo]: ");
    string citiesInput = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(citiesInput)) citiesInput = "Amsterdam, London, Tokyo";

    // Clean up the input string into a neat array
    string[] cities = citiesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.Write("  Number of forecast days [Default: 3]: ");
    if (!int.TryParse(Console.ReadLine(), out int days) || days <= 0) days = 3;

    Console.Write("  Include detailed/hourly data (e.g., wind speed)? (y/n) [Default: y]: ");
    bool includeHourly = Console.ReadLine()?.Trim().ToLower() != "n";

    Console.WriteLine($"\n[Config] Requesting {days}-day forecast for {cities.Length} cities. Hourly data: {includeHourly}\n");

    // ==========================================
    // EXECUTION PHASE
    // ==========================================
    LogVerbose(verbose, "Initializing ClientWebSocket...");

    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri($"{serverUrl}/ws/weather-stream"), CancellationToken.None);
    Console.WriteLine("  Connected to weather stream endpoint.");

    LogVerbose(verbose, "Wrapping WebSocket in ProtobufDuplexStream<WeatherRequest, WeatherResponse>...");
    await using var duplex = new ProtobufDuplexStream<WeatherRequest, WeatherResponse>(new ClientWebSocketStream(ws), ownsStream: true);

    foreach (var city in cities)
    {
        var request = new WeatherRequest
        {
            City = city,
            Days = days,
            IncludeHourly = includeHourly
        };

        LogVerbose(verbose, $"\n[Weather] Encoding WeatherRequest for '{city}'...");
        await duplex.SendAsync(request);
        Console.WriteLine($"  [Sent] Weather request for {city}");

        LogVerbose(verbose, "[Weather] Yielding thread, awaiting WeatherResponse from stream...");
        var response = await duplex.ReceiveAsync();

        if (response is null)
        {
            LogVerbose(verbose, "[Weather] Stream returned null (EOF).");
            break;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [Recv] {response.City} — {response.Forecasts.Count} day forecast:");
        Console.ResetColor();

        foreach (var day in response.Forecasts)
        {
            var wind = day.WindSpeed.HasValue ? $" | Wind: {day.WindSpeed:F1} km/h" : "";
            Console.WriteLine($"         {day.Date}: {day.TemperatureMin:F1}°C – {day.TemperatureMax:F1}°C | {day.Condition} | Humidity: {day.HumidityPercent}%{wind}");
        }
        LogVerbose(verbose, "[Weather] Response fully processed.");
    }

    LogVerbose(verbose, "Closing WebSocket gracefully...");
    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    Console.WriteLine("--- Weather demo complete ---\n");
}

// ====================================================================
// UTILITIES
// ====================================================================
static void LogVerbose(bool isVerbose, string message)
{
    if (!isVerbose) return;

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"    [DEBUG] {message}");
    Console.ResetColor();
}