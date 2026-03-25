using ProtobuffEncoder.Attributes;

[ProtoService("ChatService")]
public interface IChatService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<ChatReply> Send(ChatMessage message);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<ChatReply> LiveChat(
        IAsyncEnumerable<ChatMessage> messages,
        CancellationToken ct = default);
}