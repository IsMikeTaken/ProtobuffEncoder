namespace ProtobuffEncoder;

/// <summary>
/// Configuration options for the <see cref="ProtoRegistry"/>. Controls auto-discovery
/// behavior and default field numbering strategy for types resolved without explicit attributes.
/// </summary>
public sealed class ProtoRegistrationOptions
{
    /// <summary>
    /// When true, types without <c>[ProtoContract]</c> can be resolved automatically
    /// by the encoder when encountered as nested types, method parameters, or when
    /// explicitly registered via <see cref="ProtoRegistry"/>.
    /// Default is <c>false</c>.
    /// </summary>
    public bool AutoDiscover { get; set; }

    /// <summary>
    /// The default field numbering strategy for auto-discovered types and contracts
    /// that don't specify their own <c>FieldNumbering</c>.
    /// Default is <see cref="FieldNumbering.DeclarationOrder"/>.
    /// </summary>
    public FieldNumbering DefaultFieldNumbering { get; set; } = FieldNumbering.DeclarationOrder;

    /// <summary>
    /// The default text encoding name for auto-discovered types.
    /// Default is <c>null</c> (UTF-8).
    /// </summary>
    public string? DefaultEncoding { get; set; }
}
