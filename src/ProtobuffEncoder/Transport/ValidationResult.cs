namespace ProtobuffEncoder.Transport;

/// <summary>
/// The result of a message validation check.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static readonly ValidationResult Success = new(true, null);
    public static ValidationResult Fail(string error) => new(false, error);
}