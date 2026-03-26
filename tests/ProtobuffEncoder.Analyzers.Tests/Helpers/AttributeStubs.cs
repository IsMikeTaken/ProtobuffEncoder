namespace ProtobuffEncoder.Analyzers.Tests.Helpers;

/// <summary>
/// Provides minimal attribute stub source code that the Roslyn analyzer verifier
/// compiles alongside the test source, so analyzers can resolve the fully-qualified
/// attribute names they look for.
/// </summary>
internal static class AttributeStubs
{
    public const string Source = """
        namespace ProtobuffEncoder.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum)]
            public sealed class ProtoContractAttribute : System.Attribute
            {
                public ProtoContractAttribute() { }
                public ProtoContractAttribute(string name) { }
                public bool ExplicitFields { get; set; }
                public bool IncludeBaseFields { get; set; }
                public bool ImplicitFields { get; set; }
                public bool SkipDefaults { get; set; } = true;
                public string DefaultEncoding { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ProtoFieldAttribute : System.Attribute
            {
                public ProtoFieldAttribute() { }
                public ProtoFieldAttribute(int fieldNumber) { FieldNumber = fieldNumber; }
                public int FieldNumber { get; set; }
                public string Encoding { get; set; }
                public bool IsRequired { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ProtoIgnoreAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ProtoOneOfAttribute : System.Attribute
            {
                public string GroupName { get; }
                public ProtoOneOfAttribute(string groupName) { GroupName = groupName; }
            }

            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ProtoMapAttribute : System.Attribute
            {
                public string KeyType { get; set; }
                public string ValueType { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            public sealed class ProtoIncludeAttribute : System.Attribute
            {
                public int FieldNumber { get; }
                public System.Type DerivedType { get; }
                public ProtoIncludeAttribute(int fieldNumber, System.Type derivedType)
                {
                    FieldNumber = fieldNumber;
                    DerivedType = derivedType;
                }
            }

            [System.AttributeUsage(System.AttributeTargets.Interface | System.AttributeTargets.Class)]
            public sealed class ProtoServiceAttribute : System.Attribute
            {
                public string ServiceName { get; }
                public ProtoServiceAttribute(string serviceName) { ServiceName = serviceName; }
            }

            public enum ProtoMethodType { Unary = 0, ServerStreaming = 1, ClientStreaming = 2, DuplexStreaming = 3 }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class ProtoMethodAttribute : System.Attribute
            {
                public ProtoMethodType MethodType { get; }
                public ProtoMethodAttribute(ProtoMethodType methodType) { MethodType = methodType; }
            }
        }
        """;
}
