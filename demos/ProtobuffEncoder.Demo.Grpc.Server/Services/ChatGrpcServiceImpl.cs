using System.Runtime.CompilerServices;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Server.Services;

public class ChatGrpcServiceImpl : IChatGrpcService
{
    private readonly ILogger<ChatGrpcServiceImpl> _logger;

    public ChatGrpcServiceImpl(ILogger<ChatGrpcServiceImpl> logger) => _logger = logger;

    public async IAsyncEnumerable<NotificationMessage> Chat(
        IAsyncEnumerable<NotificationMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int messageCount = 0;

        await foreach (var msg in messages.WithCancellation(cancellationToken))
        {
            messageCount++;
            _logger.LogInformation("[Chat/Duplex] #{Count} [{Level}] {Source}: {Text}",
                messageCount, msg.Level, msg.Source, msg.Text);

            // Smart routing
            string responseText;
            var text = msg.Text.Trim().ToLowerInvariant();

            if (text == "/ping") responseText = "Pong!";
            else if (text == "/time") responseText = $"Server UTC: {DateTime.UtcNow:O}";
            else if (text == "/stats") responseText = $"Messages processed this session: {messageCount}";
            else responseText = $"Ack #{messageCount}: received \"{msg.Text}\"";

            yield return new NotificationMessage
            {
                Source = "GrpcServer",
                Text = responseText,
                Level = NotificationLevel.Info,
                TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Tags = ["grpc", "echo", $"msg-{messageCount}"]
            };
        }

        _logger.LogInformation("[Chat/Duplex] Stream ended. Total messages: {Count}", messageCount);
    }

    public Task<AckResponse> SendNotification(NotificationMessage message)
    {
        _logger.LogInformation("[Chat/Unary] [{Level}] {Source}: {Text}",
            message.Level, message.Source, message.Text);

        return Task.FromResult(new AckResponse
        {
            Accepted = !string.IsNullOrWhiteSpace(message.Text),
            MessageId = Guid.NewGuid().ToString("N")[..12]
        });
    }
}
