namespace ProtobuffEncoder;

/// <summary>
/// Thrown when a protobuf payload cannot be decoded — either because it is malformed,
/// truncated, contains an unsupported wire type, or exceeds the maximum nesting depth.
/// </summary>
/// <remarks>
/// This exception is the single, typed failure surface for all decode errors.
/// Callers that want non-throwing decode should use
/// <see cref="ProtobufEncoder.TryDecode{T}(System.ReadOnlySpan{byte},out T,out string?)"/>
/// instead.
/// </remarks>
public sealed class ProtobufDecodeException : Exception
{
    /// <summary>The byte offset in the payload at which the error was detected.</summary>
    public int Offset { get; }

    /// <summary>The target CLR type being decoded when the error occurred.</summary>
    public Type? TargetType { get; }

    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="offset">Byte offset in the payload where decoding failed.</param>
    /// <param name="targetType">The CLR type being decoded, or <see langword="null"/> if unknown.</param>
    /// <param name="inner">Optional inner exception from a lower-level operation.</param>
    public ProtobufDecodeException(string message, int offset = -1, Type? targetType = null, Exception? inner = null)
        : base(message, inner)
    {
        Offset = offset;
        TargetType = targetType;
    }
}
