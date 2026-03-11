using System.Runtime.CompilerServices;

namespace ProtobuffEncoder.Transport;

/// <summary>
/// Bi-directional streaming with validation on both send and receive sides.
/// Combines <see cref="ProtobufDuplexStream{TSend, TReceive}"/> with
/// <see cref="ValidationPipeline{T}"/> for both directions.
/// </summary>
public sealed class ValidatedDuplexStream<TSend, TReceive> : IAsyncDisposable, IDisposable
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly ProtobufDuplexStream<TSend, TReceive> _inner;

    public ValidatedDuplexStream(Stream duplexStream, bool ownsStream = true)
    {
        _inner = new ProtobufDuplexStream<TSend, TReceive>(duplexStream, ownsStream);
    }

    public ValidatedDuplexStream(Stream sendStream, Stream receiveStream, bool ownsStreams = true)
    {
        _inner = new ProtobufDuplexStream<TSend, TReceive>(sendStream, receiveStream, ownsStreams);
    }

    /// <summary>
    /// Validation pipeline for outgoing messages. Add rules to validate before sending.
    /// </summary>
    public ValidationPipeline<TSend> SendValidation { get; } = new();

    /// <summary>
    /// Validation pipeline for incoming messages. Add rules to validate after receiving.
    /// </summary>
    public ValidationPipeline<TReceive> ReceiveValidation { get; } = new();

    /// <summary>
    /// Determines behavior when a received message fails validation.
    /// </summary>
    public InvalidMessageBehavior OnInvalidReceive { get; set; } = InvalidMessageBehavior.Throw;

    /// <summary>
    /// Fired when a received message fails validation (when not throwing).
    /// </summary>
    public event Action<TReceive, ValidationResult>? MessageRejected;

    public async Task SendAsync(TSend message, CancellationToken cancellationToken = default)
    {
        SendValidation.ValidateOrThrow(message);
        await _inner.SendAsync(message, cancellationToken);
    }

    public void Send(TSend message)
    {
        SendValidation.ValidateOrThrow(message);
        _inner.Send(message);
    }

    public async Task<TReceive?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var message = await _inner.ReceiveAsync(cancellationToken);
            if (message is null)
                return null;

            if (!ReceiveValidation.HasValidators)
                return message;

            var result = ReceiveValidation.Validate(message);
            if (result.IsValid)
                return message;

            switch (OnInvalidReceive)
            {
                case InvalidMessageBehavior.Throw:
                    throw new MessageValidationException(result.ErrorMessage!, message);
                case InvalidMessageBehavior.Skip:
                    MessageRejected?.Invoke(message, result);
                    continue;
                case InvalidMessageBehavior.ReturnNull:
                    MessageRejected?.Invoke(message, result);
                    return null;
            }
        }
    }

    public async IAsyncEnumerable<TReceive> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveAsync(cancellationToken);
            if (message is null)
                yield break;
            yield return message;
        }
    }

    /// <summary>
    /// Validated request-response: validates the request before sending,
    /// validates the response after receiving.
    /// </summary>
    public async Task<TReceive?> SendAndReceiveAsync(TSend request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, cancellationToken);
        return await ReceiveAsync(cancellationToken);
    }

    /// <summary>
    /// Runs validated bi-directional streaming.
    /// </summary>
    public async Task RunDuplexAsync(
        IAsyncEnumerable<TSend> outgoing,
        Func<TReceive, Task> onReceived,
        CancellationToken cancellationToken = default)
    {
        async IAsyncEnumerable<TSend> ValidatedOutgoing(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var msg in outgoing.WithCancellation(ct))
            {
                SendValidation.ValidateOrThrow(msg);
                yield return msg;
            }
        }

        var sendTask = _inner.SendManyAsync(ValidatedOutgoing(cancellationToken), cancellationToken);
        var receiveTask = ListenAsync(onReceived, cancellationToken);
        await Task.WhenAll(sendTask, receiveTask);
    }

    public async Task ListenAsync(Func<TReceive, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllAsync(cancellationToken))
        {
            await handler(message);
        }
    }

    /// <summary>
    /// Validated process: validates incoming requests and outgoing responses.
    /// </summary>
    public async Task ProcessAsync(
        Func<TReceive, Task<TSend>> processor,
        CancellationToken cancellationToken = default)
    {
        await foreach (var request in ReceiveAllAsync(cancellationToken))
        {
            var response = await processor(request);
            await SendAsync(response, cancellationToken);
        }
    }

    public void Dispose() => _inner.Dispose();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
