namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a parsed .proto file schema.
/// </summary>
public sealed class ProtoFile
{
    public string Syntax { get; set; } = "proto3";
    public string Package { get; set; } = "";
    public List<ProtoMessageDef> Messages { get; set; } = [];
    public List<ProtoEnumDef> Enums { get; set; } = [];
}

/// <summary>
/// Represents a message definition in a .proto file.
/// </summary>
public sealed class ProtoMessageDef
{
    public string Name { get; set; } = "";
    public List<ProtoFieldDef> Fields { get; set; } = [];
    public List<ProtoMessageDef> NestedMessages { get; set; } = [];
    public List<ProtoEnumDef> NestedEnums { get; set; } = [];
    public List<ProtoOneOfDef> OneOfs { get; set; } = [];
}

/// <summary>
/// Represents a field definition in a proto message.
/// </summary>
public sealed class ProtoFieldDef
{
    public string Name { get; set; } = "";
    public int FieldNumber { get; set; }
    public string TypeName { get; set; } = "";
    public bool IsRepeated { get; set; }
    public bool IsOptional { get; set; }
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// True when this field is a map field (map&lt;K, V&gt;).
    /// </summary>
    public bool IsMap { get; set; }

    /// <summary>
    /// The map key proto type (e.g. "string", "int32"). Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public string MapKeyType { get; set; } = "";

    /// <summary>
    /// The map value proto type. Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public string MapValueType { get; set; } = "";

    /// <summary>
    /// The oneof group this field belongs to, or null.
    /// </summary>
    public string? OneOfGroup { get; set; }
}

/// <summary>
/// Represents a oneof definition in a proto message.
/// </summary>
public sealed class ProtoOneOfDef
{
    public string Name { get; set; } = "";
    public List<ProtoFieldDef> Fields { get; set; } = [];
}

/// <summary>
/// Represents an enum definition in a .proto file.
/// </summary>
public sealed class ProtoEnumDef
{
    public string Name { get; set; } = "";
    public List<ProtoEnumValue> Values { get; set; } = [];
}

/// <summary>
/// A single value inside a proto enum.
/// </summary>
public sealed class ProtoEnumValue
{
    public string Name { get; set; } = "";
    public int Number { get; set; }
}
