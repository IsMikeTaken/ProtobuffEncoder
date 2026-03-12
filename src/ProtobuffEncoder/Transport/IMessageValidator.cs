namespace ProtobuffEncoder.Transport;

/// <summary>
/// A rule that validates a message. Implement this to add custom validation logic
/// to the transport pipeline.
/// </summary>
public interface IMessageValidator<in T>
{
    ValidationResult Validate(T message);
}