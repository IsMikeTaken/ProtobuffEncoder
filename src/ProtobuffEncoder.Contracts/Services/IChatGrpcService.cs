using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts.Services;

/// <summary>
/// gRPC service contract for real-time chat.
/// Demonstrates DuplexStreaming and Unary patterns.
/// </summary>
[ProtoService("Chat")]
public interface IChatGrpcService
{
    /// <summary>
    /// Bidirectional chat stream. Both client and server can send
    /// <see cref="NotificationMessage"/> concurrently.
    /// </summary>
    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<NotificationMessage> Chat(
        IAsyncEnumerable<NotificationMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a single notification and receives an acknowledgement (unary).
    /// </summary>
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<AckResponse> SendNotification(NotificationMessage message);
}
