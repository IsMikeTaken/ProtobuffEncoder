using Microsoft.Extensions.DependencyInjection;
using ProtobuffEncoder.Contracts.Services;
using ProtobuffEncoder.Demo.Grpc.Client;
using ProtobuffEncoder.Demo.Grpc.Client.Demos;
using ProtobuffEncoder.Grpc.Client;

Console.WriteLine("=== ProtobuffEncoder gRPC Client (Strategy Edition) ===\n");

var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5401";
Console.WriteLine($"Target Server: {serverUrl}\n");

// Setup Dependency Injection completely decoupling Demo Logic
var services = new ServiceCollection();

// Standard Easy gRPC Registration from Framework Base
services.AddProtobufGrpcClient<IWeatherGrpcService>(serverUrl);
services.AddProtobufGrpcClient<IChatGrpcService>(serverUrl);
services.AddProtobufGrpcClient<IOrderProcessingService>(serverUrl);

// Register Demo Strategies
services.AddTransient<IDemoStrategy, WeatherUnaryDemo>();
services.AddTransient<IDemoStrategy, WeatherStreamingDemo>();
services.AddTransient<IDemoStrategy, ChatNotificationDemo>();
services.AddTransient<IDemoStrategy, ChatDuplexDemo>();
services.AddTransient<IDemoStrategy, OrderProcessingDemo>();

await using var provider = services.BuildServiceProvider();

// Discover all demos automatically
var demos = provider.GetServices<IDemoStrategy>().ToList();

while (true)
{
    Console.WriteLine("===========================================");
    Console.WriteLine(" Select a Demo to Run:");
    for (int i = 0; i < demos.Count; i++)
    {
        Console.WriteLine($" {i + 1}. {demos[i].DisplayName}");
    }
    Console.WriteLine($" {demos.Count + 1}. Quit");
    Console.WriteLine("===========================================");
    Console.Write("> ");

    var choiceStr = Console.ReadLine()?.Trim();
    if (int.TryParse(choiceStr, out int choice) && choice > 0 && choice <= demos.Count + 1)
    {
        if (choice == demos.Count + 1)
        {
            Console.WriteLine("\nGoodbye!");
            break;
        }

        try
        {
            var demo = demos[choice - 1];
            await demo.ExecuteAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Error] {ex.GetType().Name}: {ex.Message}\n");
            Console.ResetColor();
        }
    }
    else
    {
        Console.WriteLine($"\n[Error] Invalid choice. Select 1-{demos.Count + 1}.\n");
    }
}
