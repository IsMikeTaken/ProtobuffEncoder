namespace ProtobuffEncoder.Transport;

/// <summary>
/// A pipeline of validators that can be applied to incoming or outgoing messages.
/// </summary>
public sealed class ValidationPipeline<T>
{
    private readonly List<IMessageValidator<T>> _validators = [];

    /// <summary>
    /// Adds a validator to the pipeline.
    /// </summary>
    public ValidationPipeline<T> Add(IMessageValidator<T> validator)
    {
        _validators.Add(validator);
        return this;
    }

    /// <summary>
    /// Adds a delegate-based validation rule.
    /// </summary>
    public ValidationPipeline<T> Add(Func<T, ValidationResult> rule)
    {
        _validators.Add(new DelegateValidator<T>(rule));
        return this;
    }

    /// <summary>
    /// Adds a simple predicate rule that fails with the given message when false.
    /// </summary>
    public ValidationPipeline<T> Require(Func<T, bool> predicate, string errorMessage)
    {
        _validators.Add(new DelegateValidator<T>(msg =>
                                                     predicate(msg) ? ValidationResult.Success : ValidationResult.Fail(errorMessage)));
        return this;
    }

    /// <summary>
    /// Runs all validators. Returns the first failure, or success if all pass.
    /// </summary>
    public ValidationResult Validate(T message)
    {
        foreach (var validator in _validators)
        {
            var result = validator.Validate(message);
            if (!result.IsValid)
                return result;
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Runs all validators. Throws <see cref="MessageValidationException"/> on failure.
    /// </summary>
    public void ValidateOrThrow(T message)
    {
        var result = Validate(message);
        if (!result.IsValid)
            throw new MessageValidationException(result.ErrorMessage!, message);
    }

    public bool HasValidators => _validators.Count > 0;
}