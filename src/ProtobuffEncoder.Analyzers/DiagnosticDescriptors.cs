using Microsoft.CodeAnalysis;

namespace ProtobuffEncoder.Analyzers;

/// <summary>
/// All diagnostic descriptors for the ProtobuffEncoder analyser suite.
/// Each diagnostic helps developers catch common serialisation mistakes at compile time.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "ProtobuffEncoder";

    /// <summary>
    /// PROTO001: A class has [ProtoContract] but no serializable properties.
    /// </summary>
    public static readonly DiagnosticDescriptor ProtoContractWithoutFields = new(
        id: "PROTO001",
        title: "ProtoContract has no serializable fields",
        messageFormat: "Type '{0}' is marked with [ProtoContract] but has no public properties with getters and setters — nothing will be serialised",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A class marked with [ProtoContract] should have at least one public read/write property to be useful for serialisation.");

    /// <summary>
    /// PROTO002: Two or more [ProtoField] attributes share the same field number.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateFieldNumber = new(
        id: "PROTO002",
        title: "Duplicate protobuf field number",
        messageFormat: "Field number {0} is used by both '{1}' and '{2}' on type '{3}' — each field must have a unique number",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Protobuf field numbers must be unique within a message. Duplicate numbers cause data corruption during serialisation.");

    /// <summary>
    /// PROTO003: A [ProtoContract] type lacks a parameterless constructor.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingParameterlessConstructor = new(
        id: "PROTO003",
        title: "ProtoContract type has no parameterless constructor",
        messageFormat: "Type '{0}' is marked with [ProtoContract] but has no accessible parameterless constructor — deserialisation will fail at runtime",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The protobuf decoder creates instances via Activator.CreateInstance, which requires a parameterless constructor.");

    /// <summary>
    /// PROTO004: A property on a [ProtoContract] lacks a setter.
    /// </summary>
    public static readonly DiagnosticDescriptor PropertyWithoutSetter = new(
        id: "PROTO004",
        title: "Serialised property has no setter",
        messageFormat: "Property '{0}' on type '{1}' has no setter — it will be encoded but cannot be decoded. Add a setter or mark it with [ProtoIgnore].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Properties without setters can be encoded but will be ignored during decoding, which usually indicates a mistake.");

    /// <summary>
    /// PROTO005: [ProtoField] is used on a class that lacks [ProtoContract].
    /// </summary>
    public static readonly DiagnosticDescriptor FieldWithoutContract = new(
        id: "PROTO005",
        title: "[ProtoField] used without [ProtoContract]",
        messageFormat: "Property '{0}' on type '{1}' has [ProtoField] but the type is not marked with [ProtoContract]. This is only valid if the type is registered via ProtoRegistry or used as an implicit nested type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [ProtoField] attribute only takes effect on types that are either marked with [ProtoContract], registered via ProtoRegistry, or used as an implicit nested type in a parent contract.");

    /// <summary>
    /// PROTO006: A [ProtoField] has a field number less than 1.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidFieldNumber = new(
        id: "PROTO006",
        title: "Invalid protobuf field number",
        messageFormat: "Field number {0} on property '{1}' is invalid — protobuf field numbers must be between 1 and 536,870,911 (2^29 - 1)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Protobuf field numbers must be positive integers between 1 and 536,870,911. Numbers 19,000–19,999 are reserved by the protobuf specification.");

    /// <summary>
    /// PROTO007: A [ProtoField] uses a reserved field number range (19,000–19,999).
    /// </summary>
    public static readonly DiagnosticDescriptor ReservedFieldNumber = new(
        id: "PROTO007",
        title: "Reserved protobuf field number",
        messageFormat: "Field number {0} on property '{1}' falls in the reserved range 19,000–19,999 — these are reserved by the protobuf specification",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field numbers 19,000 through 19,999 are reserved by the protobuf wire format for internal use.");

    /// <summary>
    /// PROTO008: A [ProtoContract] type is a struct but has mutable fields.
    /// </summary>
    public static readonly DiagnosticDescriptor MutableStructContract = new(
        id: "PROTO008",
        title: "Mutable struct as ProtoContract",
        messageFormat: "Struct '{0}' is marked with [ProtoContract] — consider using a class instead, as struct serialisation copies may cause unexpected behaviour",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Protobuf serialisation works best with reference types. Struct types are boxed during encoding/decoding, which may cause copies and unexpected mutation behaviour.");

    /// <summary>
    /// PROTO009: A [ProtoOneOf] group has only one member.
    /// </summary>
    public static readonly DiagnosticDescriptor SingleMemberOneOf = new(
        id: "PROTO009",
        title: "OneOf group has only one member",
        messageFormat: "OneOf group '{0}' on type '{1}' has only one member — a OneOf group should have at least two alternatives",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A OneOf group with a single member provides no semantic benefit. Either add more alternatives or remove the [ProtoOneOf] attribute.");

    /// <summary>
    /// PROTO010: An encoding name on [ProtoField] or [ProtoContract] is not recognized.
    /// </summary>
    public static readonly DiagnosticDescriptor UnrecognisedEncoding = new(
        id: "PROTO010",
        title: "Unrecognised encoding name",
        messageFormat: "Encoding '{0}' on '{1}' is not a recognised encoding name — use 'utf-8', 'utf-16', 'utf-32', 'ascii', or 'latin-1'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The encoding name should be one recognised by System.Text.Encoding.GetEncoding(). Common values: utf-8, utf-16, utf-32, ascii, latin-1.");
}
