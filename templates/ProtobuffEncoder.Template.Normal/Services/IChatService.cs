using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Template.Normal.Contracts;

namespace ProtobuffEncoder.Template.Normal.Services;

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
