using Grpc.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtobuffEncoder.Grpc.Server;

/// <summary>
/// A robust base variant for a gRPC service that handles both bidirectional
/// streaming (consumer/sender) and unidirectional streaming.
/// </summary>
public abstract class ProtobufGrpcStreamService<TRequest, TResponse>
    where TRequest : class, new()
    where TResponse : class, new()
{
    /// <summary>
    /// Handles a bidirectional stream connecting a consumer and sender both ways.
    /// Override this method to implement your duplex streaming logic.
    /// </summary>
    public virtual async Task DuplexStreamingAsync(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context)
    {
        await foreach (var request in ReadAllAsync(requestStream, context.CancellationToken))
        {
            var response = await ProcessRequestAsync(request, context);
            if (response is not null)
            {
                await responseStream.WriteAsync(response);
            }
        }
    }

    /// <summary>
    /// Processes a single request in the duplex stream to optionally yield a response.
    /// Used by the default <see cref="DuplexStreamingAsync"/> implementation.
    /// </summary>
    protected virtual Task<TResponse?> ProcessRequestAsync(TRequest request, ServerCallContext context)
    {
        return Task.FromResult<TResponse?>(null);
    }

    /// <summary>
    /// Handles a client-to-server stream (consumer).
    /// </summary>
    public virtual async Task<TResponse> ClientStreamingAsync(
        IAsyncStreamReader<TRequest> requestStream, 
        ServerCallContext context)
    {
        await foreach (var request in ReadAllAsync(requestStream, context.CancellationToken))
        {
            await ProcessClientStreamRequestAsync(request, context);
        }

        return await FinalizeClientStreamAsync(context);
    }

    protected virtual Task ProcessClientStreamRequestAsync(TRequest request, ServerCallContext context) 
        => Task.CompletedTask;

    protected virtual Task<TResponse> FinalizeClientStreamAsync(ServerCallContext context) 
        => Task.FromResult(new TResponse());

    /// <summary>
    /// Handles a server-to-client stream (sender).
    /// </summary>
    public virtual async Task ServerStreamingAsync(
        TRequest request, 
        IServerStreamWriter<TResponse> responseStream, 
        ServerCallContext context)
    {
        await foreach (var response in GenerateServerStreamAsync(request, context))
        {
            await responseStream.WriteAsync(response);
        }
    }

    protected virtual IAsyncEnumerable<TResponse> GenerateServerStreamAsync(TRequest request, ServerCallContext context)
    {
        return EmptyAsyncEnumerable();
    }

    // Helper to read Grpc stream
    protected static async IAsyncEnumerable<TRequest> ReadAllAsync(
        IAsyncStreamReader<TRequest> stream, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await stream.MoveNext(cancellationToken))
        {
            yield return stream.Current;
        }
    }

    private static async IAsyncEnumerable<TResponse> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
