namespace ProtobuffEncoder.Transport;

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