namespace ProtobuffEncoder.Transport;

/// <summary>
/// Wraps a <see cref="ProtobufSender{T}"/> with an outgoing validation pipeline.
/// Messages are validated before being sent — invalid messages throw
/// <see cref="MessageValidationException"/> and are never written to the stream.
/// </summary>
public sealed class ValidatedProtobufSender<T> : IAsyncDisposable, IDisposable
    where T : class, new()
{
    private readonly ProtobufSender<T> _inner;
    private readonly ValidationPipeline<T> _pipeline;

    public ValidatedProtobufSender(Stream stream, bool ownsStream = true)
        : this(new ProtobufSender<T>(stream, ownsStream))
    {
    }

    public ValidatedProtobufSender(ProtobufSender<T> sender)
    {
        _inner = sender;
        _pipeline = new ValidationPipeline<T>();
    }

    /// <summary>
    /// The validation pipeline for outgoing messages.
    /// Add rules to validate messages before they are sent.
    /// </summary>
    public ValidationPipeline<T> Validation => _pipeline;

    public void Send(T instance)
    {
        _pipeline.ValidateOrThrow(instance);
        _inner.Send(instance);
    }

    public async Task SendAsync(T instance, CancellationToken cancellationToken = default)
    {
        _pipeline.ValidateOrThrow(instance);
        await _inner.SendAsync(instance, cancellationToken);
    }

    public async Task SendManyAsync(IEnumerable<T> instances, CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            _pipeline.ValidateOrThrow(instance);
        }
        await _inner.SendManyAsync(instances, cancellationToken);
    }

    public async Task SendManyAsync(IAsyncEnumerable<T> instances, CancellationToken cancellationToken = default)
    {
        await foreach (var instance in instances.WithCancellation(cancellationToken))
        {
            _pipeline.ValidateOrThrow(instance);
            await _inner.SendAsync(instance, cancellationToken);
        }
    }

    public void Dispose() => _inner.Dispose();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
