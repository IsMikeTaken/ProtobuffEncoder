namespace ProtobuffEncoder.Transport;

/// <summary>
/// Thrown when a message fails validation during receive or send.
/// </summary>
public sealed class MessageValidationException : Exception
{
    /// <summary>
    /// The message object that failed validation.
    /// </summary>
    public object? InvalidMessage { get; }

    public MessageValidationException(string error, object? invalidMessage = null)
        : base(error)
    {
        InvalidMessage = invalidMessage;
    }
}