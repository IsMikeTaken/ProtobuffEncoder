using System.Runtime.CompilerServices;

namespace ProtobuffEncoder.Transport;

/// <summary>
/// Wraps a <see cref="ProtobufReceiver{T}"/> with an incoming validation pipeline.
/// Messages are validated after deserialization — invalid messages can be rejected,
/// skipped, or cause an exception depending on the configured <see cref="InvalidMessageBehavior"/>.
/// </summary>
public sealed class ValidatedProtobufReceiver<T> : IAsyncDisposable, IDisposable
    where T : class, new()
{
    private readonly ProtobufReceiver<T> _inner;
    private readonly ValidationPipeline<T> _pipeline;

    public ValidatedProtobufReceiver(Stream stream, bool ownsStream = true)
        : this(new ProtobufReceiver<T>(stream, ownsStream))
    {
    }

    public ValidatedProtobufReceiver(ProtobufReceiver<T> receiver)
    {
        _inner = receiver;
        _pipeline = new ValidationPipeline<T>();
    }

    /// <summary>
    /// The validation pipeline for incoming messages.
    /// Add rules to validate messages after they are deserialized.
    /// </summary>
    public ValidationPipeline<T> Validation => _pipeline;

    /// <summary>
    /// Determines what happens when a received message fails validation.
    /// Default is <see cref="InvalidMessageBehavior.Throw"/>.
    /// </summary>
    public InvalidMessageBehavior OnInvalid { get; set; } = InvalidMessageBehavior.Throw;

    /// <summary>
    /// Fired when a message fails validation and <see cref="OnInvalid"/> is not Throw.
    /// </summary>
    public event Action<T, ValidationResult>? MessageRejected;

    /// <summary>
    /// Receives a single validated message. Returns null at end of stream.
    /// Behavior for invalid messages depends on <see cref="OnInvalid"/>.
    /// </summary>
    public T? Receive()
    {
        while (true)
        {
            var message = _inner.Receive();
            if (message is null)
                return null;

            if (!_pipeline.HasValidators)
                return message;

            var result = _pipeline.Validate(message);
            if (result.IsValid)
                return message;

            switch (OnInvalid)
            {
                case InvalidMessageBehavior.Throw:
                    throw new MessageValidationException(result.ErrorMessage!, message);
                case InvalidMessageBehavior.Skip:
                    MessageRejected?.Invoke(message, result);
                    continue; // try next message
                case InvalidMessageBehavior.ReturnNull:
                    MessageRejected?.Invoke(message, result);
                    return null;
            }
        }
    }

    /// <summary>
    /// Receives all validated messages as an enumerable.
    /// </summary>
    public IEnumerable<T> ReceiveAll()
    {
        while (true)
        {
            var message = Receive();
            if (message is null) yield break;
            yield return message;
        }
    }

    /// <summary>
    /// Receives all validated messages as an async stream.
    /// </summary>
    public async IAsyncEnumerable<T> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var raw in _inner.ReceiveAllAsync(cancellationToken))
        {
            if (!_pipeline.HasValidators)
            {
                yield return raw;
                continue;
            }

            var result = _pipeline.Validate(raw);
            if (result.IsValid)
            {
                yield return raw;
                continue;
            }

            switch (OnInvalid)
            {
                case InvalidMessageBehavior.Throw:
                    throw new MessageValidationException(result.ErrorMessage!, raw);
                case InvalidMessageBehavior.Skip:
                    MessageRejected?.Invoke(raw, result);
                    continue;
                case InvalidMessageBehavior.ReturnNull:
                    MessageRejected?.Invoke(raw, result);
                    yield break;
            }
        }
    }

    /// <summary>
    /// Listens for validated messages, invoking the handler for each valid one.
    /// </summary>
    public async Task ListenAsync(Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllAsync(cancellationToken))
        {
            await handler(message);
        }
    }

    public void Dispose() => _inner.Dispose();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

/// <summary>
/// Determines behavior when a received message fails validation.
/// </summary>
public enum InvalidMessageBehavior
{
    /// <summary>
    /// Throw a <see cref="MessageValidationException"/>. Default.
    /// </summary>
    Throw,

    /// <summary>
    /// Skip the invalid message and continue receiving.
    /// The <see cref="ValidatedProtobufReceiver{T}.MessageRejected"/> event fires.
    /// </summary>
    Skip,

    /// <summary>
    /// Return null / stop the stream.
    /// </summary>
    ReturnNull
}
