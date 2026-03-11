using System.Reflection;
using System.Text;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Generates .proto schema files from C# types marked with [ProtoContract].
/// Supports map fields, oneof groups, deprecated annotations, inheritance (ProtoInclude),
/// and implicit nested types.
/// </summary>
public static class ProtoSchemaGenerator
{
    /// <summary>
    /// Generates a .proto file content for the given type and all its dependencies.
    /// </summary>
    public static string Generate(Type type)
    {
        var file = BuildProtoFile(type);
        return Render(file);
    }

    /// <summary>
    /// Generates .proto files for all [ProtoContract] types in an assembly.
    /// Returns a dictionary of filename → proto content.
    /// </summary>
    public static Dictionary<string, string> GenerateAll(Assembly assembly)
    {
        var results = new Dictionary<string, string>();
        var contractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() is not null)
            .ToList();

        // Group types by namespace to produce one .proto per namespace
        var grouped = contractTypes.GroupBy(t => t.Namespace ?? "default");

        foreach (var group in grouped)
        {
            var file = new ProtoFile
            {
                Syntax = "proto3",
                Package = group.Key
            };

            var visited = new HashSet<Type>();
            foreach (var type in group)
            {
                CollectType(type, file, visited);
            }

            string filename = group.Key.Replace('.', '_').ToLowerInvariant() + ".proto";
            results[filename] = Render(file);
        }

        return results;
    }

    /// <summary>
    /// Generates all .proto files from an assembly and writes them to the output directory.
    /// Returns the list of generated file paths.
    /// </summary>
    public static List<string> GenerateToDirectory(Assembly assembly, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var files = GenerateAll(assembly);
        var paths = new List<string>();

        foreach (var (filename, content) in files)
        {
            var path = Path.Combine(outputDirectory, filename);
            File.WriteAllText(path, content);
            paths.Add(path);
        }

        return paths;
    }

    #region Build model

    private static ProtoFile BuildProtoFile(Type type)
    {
        var file = new ProtoFile
        {
            Syntax = "proto3",
            Package = type.Namespace ?? ""
        };

        var visited = new HashSet<Type>();
        CollectType(type, file, visited);
        return file;
    }

    private static void CollectType(Type type, ProtoFile file, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;

        if (type.IsEnum)
        {
            file.Enums.Add(BuildEnum(type));
            return;
        }

        var contract = type.GetCustomAttribute<ProtoContractAttribute>();
        if (contract is null) return;

        var msgDef = BuildMessage(type, file, visited);
        file.Messages.Add(msgDef);

        // Process [ProtoInclude] — generate messages for derived types too
        var includes = type.GetCustomAttributes<ProtoIncludeAttribute>();
        foreach (var inc in includes)
        {
            CollectType(inc.DerivedType, file, visited);
        }
    }

    private static ProtoMessageDef BuildMessage(Type type, ProtoFile file, HashSet<Type> visited)
    {
        var descriptors = ContractResolver.Resolve(type);
        var msg = new ProtoMessageDef { Name = type.Name };

        // Group fields by oneof
        var oneOfGroups = new Dictionary<string, ProtoOneOfDef>();

        foreach (var field in descriptors)
        {
            var fieldDef = BuildFieldDef(field, file, visited);

            if (field.OneOfGroup is not null)
            {
                fieldDef.OneOfGroup = field.OneOfGroup;

                if (!oneOfGroups.TryGetValue(field.OneOfGroup, out var oneOf))
                {
                    oneOf = new ProtoOneOfDef { Name = field.OneOfGroup };
                    oneOfGroups[field.OneOfGroup] = oneOf;
                    msg.OneOfs.Add(oneOf);
                }
                oneOf.Fields.Add(fieldDef);
            }
            else
            {
                msg.Fields.Add(fieldDef);
            }
        }

        // Add [ProtoInclude] subtypes as fields on this message
        var includes = type.GetCustomAttributes<ProtoIncludeAttribute>();
        foreach (var inc in includes)
        {
            CollectType(inc.DerivedType, file, visited);
            msg.Fields.Add(new ProtoFieldDef
            {
                Name = inc.DerivedType.Name,
                FieldNumber = inc.FieldNumber,
                TypeName = inc.DerivedType.Name,
                IsOptional = true
            });
        }

        return msg;
    }

    private static ProtoFieldDef BuildFieldDef(FieldDescriptor field, ProtoFile file, HashSet<Type> visited)
    {
        // Map field
        if (field.IsMap && field.MapKeyType is not null && field.MapValueType is not null)
        {
            string keyTypeName = MapToProtoType(field.MapKeyType, file, visited);
            string valueTypeName = MapToProtoType(field.MapValueType, file, visited);

            return new ProtoFieldDef
            {
                Name = field.Name,
                FieldNumber = field.FieldNumber,
                IsMap = true,
                MapKeyType = keyTypeName,
                MapValueType = valueTypeName,
                IsDeprecated = field.IsDeprecated
            };
        }

        var propType = field.Property.PropertyType;
        var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
        bool isOptional = field.IsNullable;
        bool isRepeated = field.IsCollection;

        string typeName;
        if (isRepeated && field.ElementType is not null)
        {
            typeName = MapToProtoType(field.ElementType, file, visited);
        }
        else
        {
            typeName = MapToProtoType(underlying, file, visited);
        }

        return new ProtoFieldDef
        {
            Name = field.Name,
            FieldNumber = field.FieldNumber,
            TypeName = typeName,
            IsRepeated = isRepeated,
            IsOptional = isOptional,
            IsDeprecated = field.IsDeprecated
        };
    }

    private static ProtoEnumDef BuildEnum(Type enumType)
    {
        var def = new ProtoEnumDef { Name = enumType.Name };
        foreach (var name in Enum.GetNames(enumType))
        {
            def.Values.Add(new ProtoEnumValue
            {
                Name = name,
                Number = (int)Enum.Parse(enumType, name)
            });
        }
        return def;
    }

    private static string MapToProtoType(Type clrType, ProtoFile file, HashSet<Type> visited)
    {
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlying == typeof(bool)) return "bool";
        if (underlying == typeof(int) || underlying == typeof(short) || underlying == typeof(sbyte)) return "int32";
        if (underlying == typeof(uint) || underlying == typeof(ushort) || underlying == typeof(byte)) return "uint32";
        if (underlying == typeof(long)) return "int64";
        if (underlying == typeof(ulong)) return "uint64";
        if (underlying == typeof(float)) return "float";
        if (underlying == typeof(double)) return "double";
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(byte[])) return "bytes";

        // Enum — collect it and reference by name
        if (underlying.IsEnum)
        {
            CollectType(underlying, file, visited);
            return underlying.Name;
        }

        // Nested message — works with both explicit [ProtoContract] and implicit types
        if (underlying.GetCustomAttribute<ProtoContractAttribute>() is not null)
        {
            CollectType(underlying, file, visited);
            return underlying.Name;
        }

        // Implicit nested type (class without [ProtoContract] used in an ImplicitFields context)
        if (underlying.IsClass && underlying != typeof(string) && underlying != typeof(object))
        {
            // Generate a message definition for this implicit type
            if (visited.Add(underlying))
            {
                var implicitDescriptors = ContractResolver.ResolveImplicit(underlying);
                var implicitMsg = new ProtoMessageDef { Name = underlying.Name };
                foreach (var f in implicitDescriptors)
                {
                    implicitMsg.Fields.Add(BuildFieldDef(f, file, visited));
                }
                file.Messages.Add(implicitMsg);
            }
            return underlying.Name;
        }

        return "bytes"; // fallback
    }

    #endregion

    #region Render

    private static string Render(ProtoFile file)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"syntax = \"{file.Syntax}\";");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(file.Package))
        {
            sb.AppendLine($"package {file.Package};");
            sb.AppendLine();
        }

        foreach (var enumDef in file.Enums)
        {
            RenderEnum(sb, enumDef, indent: 0);
            sb.AppendLine();
        }

        foreach (var msg in file.Messages)
        {
            RenderMessage(sb, msg, indent: 0);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void RenderMessage(StringBuilder sb, ProtoMessageDef msg, int indent)
    {
        var pad = new string(' ', indent * 2);
        sb.AppendLine($"{pad}message {msg.Name} {{");

        foreach (var nestedEnum in msg.NestedEnums)
            RenderEnum(sb, nestedEnum, indent + 1);

        foreach (var nested in msg.NestedMessages)
            RenderMessage(sb, nested, indent + 1);

        // Regular fields (not in a oneof)
        foreach (var field in msg.Fields)
        {
            RenderField(sb, field, pad + "  ");
        }

        // OneOf groups
        foreach (var oneOf in msg.OneOfs)
        {
            sb.AppendLine($"{pad}  oneof {oneOf.Name} {{");
            foreach (var field in oneOf.Fields)
            {
                RenderField(sb, field, pad + "    ");
            }
            sb.AppendLine($"{pad}  }}");
        }

        sb.AppendLine($"{pad}}}");
    }

    private static void RenderField(StringBuilder sb, ProtoFieldDef field, string pad)
    {
        var deprecated = field.IsDeprecated ? " [deprecated = true]" : "";

        if (field.IsMap)
        {
            sb.AppendLine($"{pad}map<{field.MapKeyType}, {field.MapValueType}> {field.Name} = {field.FieldNumber}{deprecated};");
        }
        else
        {
            var prefix = field.IsRepeated ? "repeated " : field.IsOptional ? "optional " : "";
            sb.AppendLine($"{pad}{prefix}{field.TypeName} {field.Name} = {field.FieldNumber}{deprecated};");
        }
    }

    private static void RenderEnum(StringBuilder sb, ProtoEnumDef enumDef, int indent)
    {
        var pad = new string(' ', indent * 2);
        sb.AppendLine($"{pad}enum {enumDef.Name} {{");
        foreach (var val in enumDef.Values)
        {
            sb.AppendLine($"{pad}  {val.Name} = {val.Number};");
        }
        sb.AppendLine($"{pad}}}");
    }

    #endregion
}
