using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Client.Demos;

public class ChatDuplexDemo(IChatGrpcService client) : IDemoStrategy
{
    public string DisplayName => "Chat    — Duplex Streaming";

    public async Task ExecuteAsync()
    {
        Console.WriteLine($"\n  Starting Chat/Chat (Duplex Streaming)...");
        Console.WriteLine($"  Sending 4 messages, 500ms apart.\n");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Build the outgoing message stream
        async IAsyncEnumerable<NotificationMessage> GenerateMessages()
        {
            string[] messages = [
                "Hello from strategy duplex!",
                "Are you there?",
                "/stats",
                "Goodbye!"
            ];

            for (int i = 0; i < messages.Length; i++)
            {
                var msg = new NotificationMessage
                {
                    Source = "StrategyClient",
                    Text = messages[i],
                    Level = NotificationLevel.Info,
                    TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [Sent] {msg.Text}");
                Console.ResetColor();

                yield return msg;
                await Task.Delay(500, cts.Token);
            }
        }

        // Run the duplex stream
        await foreach (var reply in client.Chat(GenerateMessages(), cts.Token))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [Recv] [{reply.Level}] {reply.Source}: {reply.Text}");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  Duplex stream complete.\n");
        Console.ResetColor();
    }
}
