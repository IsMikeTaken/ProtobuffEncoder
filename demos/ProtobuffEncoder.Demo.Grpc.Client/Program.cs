using Grpc.Net.Client;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;
using ProtobuffEncoder.Grpc.Client;

Console.WriteLine("=== ProtobuffEncoder gRPC Client ===\n");

var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5401";

using var channel = GrpcChannel.ForAddress(serverUrl);

// Create typed clients from the shared service interfaces — no .proto files, no code gen
var weatherClient = channel.CreateProtobufClient<IWeatherGrpcService>();
var chatClient = channel.CreateProtobufClient<IChatGrpcService>();

Console.WriteLine($"Channel created for {serverUrl}\n");

while (true)
{
    Console.WriteLine("===========================================");
    Console.WriteLine(" Select a Demo to Run:");
    Console.WriteLine(" 1. Weather — Unary GetForecast");
    Console.WriteLine(" 2. Weather — Server Streaming");
    Console.WriteLine(" 3. Chat    — Send Notification (Unary)");
    Console.WriteLine(" 4. Chat    — Duplex Streaming");
    Console.WriteLine(" 5. Quit");
    Console.WriteLine("===========================================");
    Console.Write("> ");

    var choice = Console.ReadLine()?.Trim();

    try
    {
        switch (choice)
        {
            case "1": await RunUnaryWeather(weatherClient); break;
            case "2": await RunStreamingWeather(weatherClient); break;
            case "3": await RunUnaryChat(chatClient); break;
            case "4": await RunDuplexChat(chatClient); break;
            case "5" or "quit" or "exit":
                Console.WriteLine("\nGoodbye!");
                return;
            default:
                Console.WriteLine("\n[Error] Invalid choice. Select 1-5.\n");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[Error] {ex.GetType().Name}: {ex.Message}\n");
        Console.ResetColor();
    }
}

// ====================================================================
// DEMO 1: Unary Weather
// ====================================================================
static async Task RunUnaryWeather(IWeatherGrpcService client)
{
    Console.Write("\n  City [Amsterdam]: ");
    var city = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(city)) city = "Amsterdam";

    Console.Write("  Days [5]: ");
    if (!int.TryParse(Console.ReadLine(), out int days) || days <= 0) days = 5;

    Console.Write("  Include wind? (y/n) [y]: ");
    bool wind = Console.ReadLine()?.Trim().ToLower() != "n";

    Console.WriteLine($"\n  Calling Weather/GetForecast (Unary)...");

    var response = await client.GetForecast(new WeatherRequest
    {
        City = city,
        Days = days,
        IncludeHourly = wind
    });

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n  {response.City} — {response.Forecasts.Count} day forecast:");
    Console.ResetColor();

    foreach (var day in response.Forecasts)
    {
        var windInfo = day.WindSpeed.HasValue ? $" | Wind: {day.WindSpeed:F1} km/h" : "";
        Console.WriteLine($"    {day.Date}: {day.TemperatureMin:F1}–{day.TemperatureMax:F1}°C | {day.Condition} | Humidity: {day.HumidityPercent}%{windInfo}");
    }

    Console.WriteLine();
}

// ====================================================================
// DEMO 2: Server Streaming Weather
// ====================================================================
static async Task RunStreamingWeather(IWeatherGrpcService client)
{
    Console.Write("\n  City [Tokyo]: ");
    var city = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(city)) city = "Tokyo";

    Console.Write("  Days to stream [7]: ");
    if (!int.TryParse(Console.ReadLine(), out int days) || days <= 0) days = 7;

    Console.WriteLine($"\n  Calling Weather/StreamForecasts (Server Streaming)...");
    Console.WriteLine("  Receiving day-by-day forecasts:\n");

    int count = 0;
    await foreach (var response in client.StreamForecasts(
        new WeatherRequest { City = city, Days = days, IncludeHourly = true }))
    {
        count++;
        var day = response.Forecasts[0];
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  [{count}/{days}] ");
        Console.ResetColor();
        Console.WriteLine($"{day.Date}: {day.TemperatureMin:F1}–{day.TemperatureMax:F1}°C | {day.Condition}");
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n  Stream complete. Received {count} forecasts.\n");
    Console.ResetColor();
}

// ====================================================================
// DEMO 3: Unary Chat Notification
// ====================================================================
static async Task RunUnaryChat(IChatGrpcService client)
{
    Console.Write("\n  Message text [Hello from gRPC!]: ");
    var text = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(text)) text = "Hello from gRPC!";

    Console.WriteLine("  Calling Chat/SendNotification (Unary)...");

    var ack = await client.SendNotification(new NotificationMessage
    {
        Source = "GrpcClient",
        Text = text,
        Level = NotificationLevel.Info,
        TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Tags = ["grpc", "demo"]
    });

    Console.ForegroundColor = ack.Accepted ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"\n  Ack: Accepted={ack.Accepted}, MessageId={ack.MessageId}\n");
    Console.ResetColor();
}

// ====================================================================
// DEMO 4: Duplex Chat Stream
// ====================================================================
static async Task RunDuplexChat(IChatGrpcService client)
{
    Console.Write("\n  Number of messages [5]: ");
    if (!int.TryParse(Console.ReadLine(), out int count) || count <= 0) count = 5;

    Console.Write("  Delay between messages (ms) [500]: ");
    if (!int.TryParse(Console.ReadLine(), out int delay) || delay < 0) delay = 500;

    Console.WriteLine($"\n  Starting Chat/Chat (Duplex Streaming)...");
    Console.WriteLine($"  Sending {count} messages, {delay}ms apart.\n");

    var cts = new CancellationTokenSource();

    // Build the outgoing message stream
    async IAsyncEnumerable<NotificationMessage> GenerateMessages()
    {
        string[] messages = [
            "Hello from gRPC duplex!",
            "/ping",
            "How's the weather?",
            "/time",
            "/stats",
            "Testing the duplex stream",
            "Almost done",
            "Last message!"
        ];

        for (int i = 0; i < count; i++)
        {
            var msg = new NotificationMessage
            {
                Source = "GrpcClient",
                Text = messages[i % messages.Length],
                Level = NotificationLevel.Info,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["grpc", "duplex", $"msg-{i + 1}"]
            };

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [Sent] {msg.Text}");
            Console.ResetColor();

            yield return msg;

            if (i < count - 1)
                await Task.Delay(delay);
        }
    }

    // Run the duplex stream — send messages and receive responses
    await foreach (var reply in client.Chat(GenerateMessages(), cts.Token))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [Recv] [{reply.Level}] {reply.Source}: {reply.Text}");
        if (reply.Tags.Count > 0)
            Console.WriteLine($"         Tags: {string.Join(", ", reply.Tags)}");
        Console.ResetColor();
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n  Duplex stream complete.\n");
    Console.ResetColor();
}
