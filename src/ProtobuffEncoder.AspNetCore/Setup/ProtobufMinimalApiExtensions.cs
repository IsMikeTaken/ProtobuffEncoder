using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ProtobuffEncoder.Transport;
using System.Threading.Tasks;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Extension methods for quick Minimal API endpoints handling Protocol Buffer streaming.
/// </summary>
public static class ProtobufMinimalApiExtensions
{
    /// <summary>
    /// Maps a Minimal API endpoint that streams incoming protobuf messages from the client.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapProtobufReceiver&lt;MyMessage&gt;("/upload", async receiver => {
    ///     await foreach (var msg in receiver.ReceiveAllAsync()) { ... }
    /// });
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapProtobufReceiver<TReceive>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<ValidatedProtobufReceiver<TReceive>, HttpContext, Task> handler)
        where TReceive : class, new()
    {
        return endpoints.MapPost(pattern, async (HttpContext ctx) =>
        {
            if (!ctx.Request.ContentType?.Contains(ProtobufMediaType.Protobuf) ?? true)
            {
                ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            await using var receiver = new ValidatedProtobufReceiver<TReceive>(ctx.Request.Body, ownsStream: false);
            await handler(receiver, ctx);
        });
    }

    /// <summary>
    /// Maps a Minimal API endpoint that streams outgoing protobuf messages to the client.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapProtobufSender&lt;MyMessage&gt;("/download", async sender => {
    ///     await sender.SendAsync(new MyMessage());
    /// });
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapProtobufSender<TSend>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<ValidatedProtobufSender<TSend>, HttpContext, Task> handler)
        where TSend : class, new()
    {
        return endpoints.MapGet(pattern, async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = ProtobufMediaType.Protobuf;

            await using var sender = new ValidatedProtobufSender<TSend>(ctx.Response.Body, ownsStream: false);
            await handler(sender, ctx);
        });
    }

    /// <summary>
    /// Maps a Minimal API endpoint that supports full-duplex protobuf streaming.
    /// Note: Full duplex streaming requires HTTP/2 or WebSockets depending on the client.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapProtobufDuplex&lt;MyReq, MyRes&gt;("/stream", async duplex => {
    ///     await duplex.ListenAsync(async req => await duplex.SendAsync(new MyRes()));
    /// });
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapProtobufDuplex<TSend, TReceive>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<ValidatedDuplexStream<TSend, TReceive>, HttpContext, Task> handler)
        where TSend : class, new()
        where TReceive : class, new()
    {
        return endpoints.MapPost(pattern, async (HttpContext ctx) =>
        {
            if (!ctx.Request.ContentType?.Contains(ProtobufMediaType.Protobuf) ?? true)
            {
                ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            ctx.Response.ContentType = ProtobufMediaType.Protobuf;

            await using var duplex = new ValidatedDuplexStream<TSend, TReceive>(
                sendStream: ctx.Response.Body,
                receiveStream: ctx.Request.Body,
                ownsStreams: false);

            await handler(duplex, ctx);
        });
    }
}
