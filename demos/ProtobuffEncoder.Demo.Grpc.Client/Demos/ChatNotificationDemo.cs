using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Client.Demos;

public class ChatNotificationDemo(IChatGrpcService client) : IDemoStrategy
{
    public string DisplayName => "Chat    — Send Notification (Unary)";

    public async Task ExecuteAsync()
    {
        Console.WriteLine("  Calling Chat/SendNotification (Unary)...");

        var ack = await client.SendNotification(new NotificationMessage
        {
            Source = "GrpcClient",
            Text = "Hello from gRPC Framework Demo!",
            Level = NotificationLevel.Info,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Tags = ["grpc", "demo"]
        });

        Console.ForegroundColor = ack.Accepted ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"\n  Ack: Accepted={ack.Accepted}, MessageId={ack.MessageId}\n");
        Console.ResetColor();
    }
}
