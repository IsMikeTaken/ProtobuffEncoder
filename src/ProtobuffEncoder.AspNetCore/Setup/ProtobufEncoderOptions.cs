using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Central configuration for the ProtobuffEncoder framework, compatible with
/// <c>IOptions&lt;ProtobufEncoderOptions&gt;</c>.
/// <para>
/// Configured via <see cref="ProtobufEncoderBuilder"/> returned by
/// <c>builder.Services.AddProtobuffEncoder()</c>.
/// </para>
/// </summary>
public sealed class ProtobufEncoderOptions
{
    /// <summary>
    /// Default behavior when an incoming message fails validation across all transports.
    /// Individual endpoints can override this. Default: <see cref="InvalidMessageBehavior.Skip"/>.
    /// </summary>
    public InvalidMessageBehavior DefaultInvalidMessageBehavior { get; set; } = InvalidMessageBehavior.Skip;

    /// <summary>
    /// When true, enables the <c>application/x-protobuf</c> MVC input/output formatters
    /// so controllers and minimal APIs can accept and return protobuf bodies.
    /// Default: false (opt-in to avoid interfering with JSON-only APIs).
    /// </summary>
    public bool EnableMvcFormatters { get; set; }

    /// <summary>
    /// Called whenever any transport-level validation rejects a message.
    /// Useful for centralized logging/telemetry. Optional.
    /// </summary>
    public Action<object, ValidationResult>? OnGlobalValidationFailure { get; set; }
}
