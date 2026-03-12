namespace ProtobuffEncoder.Transport;

/// <summary>
/// Validates messages using a delegate function.
/// </summary>
public sealed class DelegateValidator<T> : IMessageValidator<T>
{
    private readonly Func<T, ValidationResult> _validate;

    public DelegateValidator(Func<T, ValidationResult> validate)
    {
        _validate = validate;
    }

    public ValidationResult Validate(T message) => _validate(message);
}