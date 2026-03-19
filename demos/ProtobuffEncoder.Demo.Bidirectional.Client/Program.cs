using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.WebSockets;

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

    // Configuration
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

    // Use the framework client with retry
    await using var client = new ProtobufWebSocketClient<NotificationMessage, NotificationMessage>(new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri($"{serverUrl}/ws/chat"),
            RetryPolicy = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromSeconds(1) },
            OnConnect = () =>
            {
                Console.WriteLine("  Connected to chat endpoint.");
                return Task.CompletedTask;
            },
            OnRetry = (attempt, delay) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [Retry] Attempt #{attempt}, waiting {delay.TotalSeconds:F1}s...");
                Console.ResetColor();
                return Task.CompletedTask;
            },
            OnError = ex =>
            {
                LogVerbose(verbose, $"[Error] {ex.Message}");
                return Task.CompletedTask;
            }
        });

    LogVerbose(verbose, "Connecting with automatic retry...");
    await client.ConnectAsync();

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

            LogVerbose(verbose, $"[SendTask] Pushing message {i}/{msgCount}...");
            await client.SendAsync(msg);
            Console.WriteLine($"  [Sent] {text}");

            if (i < msgCount || sendInvalid)
            {
                LogVerbose(verbose, $"[SendTask] Pacing ({delayMs}ms)...");
                await Task.Delay(delayMs);
            }
        }

        if (sendInvalid)
        {
            LogVerbose(verbose, "[SendTask] Firing intentionally invalid (empty) message...");
            await client.SendAsync(new NotificationMessage
            {
                Source = "Client",
                Text = "",
                Level = NotificationLevel.Warning,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            Console.WriteLine("  [Sent] (empty — should be rejected)");
            await Task.Delay(500);
        }

        LogVerbose(verbose, "[SendTask] Initiating graceful closure...");
        await client.CloseAsync();
        LogVerbose(verbose, "[SendTask] Send task complete.");
    });

    // --- Receive Task ---
    var receiveTask = Task.Run(async () =>
    {
        LogVerbose(verbose, "[ReceiveTask] Listening via ReceiveAllAsync()...");

        await foreach (var reply in client.ReceiveAllAsync())
        {
            LogVerbose(verbose, $"[ReceiveTask] Received from {reply.Source}.");

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
        LogVerbose(verbose, "[ReceiveTask] Stream ended.");
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

    // Configuration
    Console.WriteLine("Configure your test run (Press Enter to use defaults):");

    Console.Write("  Enter cities separated by commas [Default: Amsterdam, London, Tokyo]: ");
    string citiesInput = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(citiesInput)) citiesInput = "Amsterdam, London, Tokyo";

    string[] cities = citiesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.Write("  Number of forecast days [Default: 3]: ");
    if (!int.TryParse(Console.ReadLine(), out int days) || days <= 0) days = 3;

    Console.Write("  Include detailed/hourly data (e.g., wind speed)? (y/n) [Default: y]: ");
    bool includeHourly = Console.ReadLine()?.Trim().ToLower() != "n";

    Console.WriteLine($"\n[Config] Requesting {days}-day forecast for {cities.Length} cities. Hourly data: {includeHourly}\n");

    // Use the framework client with retry
    await using var client = new ProtobufWebSocketClient<WeatherRequest, WeatherResponse>(new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri($"{serverUrl}/ws/weather-stream"),
            RetryPolicy = new RetryPolicy { MaxRetries = 3 },
            OnConnect = () =>
            {
                Console.WriteLine("  Connected to weather stream endpoint.");
                return Task.CompletedTask;
            },
            OnRetry = (attempt, delay) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [Retry] Attempt #{attempt}, waiting {delay.TotalSeconds:F1}s...");
                Console.ResetColor();
                return Task.CompletedTask;
            }
        });

    LogVerbose(verbose, "Connecting...");
    await client.ConnectAsync();

    foreach (var city in cities)
    {
        var request = new WeatherRequest
        {
            City = city,
            Days = days,
            IncludeHourly = includeHourly
        };

        LogVerbose(verbose, $"\n[Weather] Sending request for '{city}'...");
        await client.SendAsync(request);
        Console.WriteLine($"  [Sent] Weather request for {city}");

        LogVerbose(verbose, "[Weather] Awaiting response...");
        var response = await client.ReceiveAsync();

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
            Console.WriteLine($"         {day.Date}: {day.TemperatureMin:F1} - {day.TemperatureMax:F1} C | {day.Condition} | Humidity: {day.HumidityPercent}%{wind}");
        }
        LogVerbose(verbose, "[Weather] Response processed.");
    }

    LogVerbose(verbose, "Closing connection...");
    await client.CloseAsync();
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
