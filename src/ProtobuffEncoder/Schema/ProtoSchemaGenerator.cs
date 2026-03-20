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
        var contractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() is not null ||
                        t.GetCustomAttribute<ProtoServiceAttribute>() is not null)
            .ToList();

        // Phase 1: Build the type → file key registry so we know where every type lives
        var typeToFileKey = new Dictionary<Type, string>();
        foreach (var type in contractTypes)
        {
            var key = ResolveFileKey(type);
            typeToFileKey[type] = key;
        }

        // Also discover [ProtoService] interfaces referenced by implementation types
        var serviceInterfaces = new List<Type>();
        foreach (var type in contractTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.GetCustomAttribute<ProtoServiceAttribute>() is not null && !typeToFileKey.ContainsKey(iface))
                {
                    var key = ResolveServiceFileKey(iface);
                    typeToFileKey[iface] = key;
                    serviceInterfaces.Add(iface);
                }
            }
        }

        // Group types by file key
        var grouped = contractTypes
            .Concat(serviceInterfaces)
            .GroupBy(t => typeToFileKey[t]);

        // Phase 2: Build ProtoFile models with file-boundary-aware collection
        var files = new Dictionary<string, ProtoFile>();

        foreach (var group in grouped)
        {
            var firstType = group.First();
            var file = new ProtoFile
            {
                Syntax = "proto3",
                Package = firstType.Namespace ?? "default",
                FilePath = group.Key
            };

            var visited = new HashSet<Type>();
            foreach (var type in group)
            {
                CollectType(type, file, visited, typeToFileKey, group.Key);
            }

            foreach (var type in group)
            {
                CollectServices(type, file, visited, typeToFileKey, group.Key);
            }

            files[group.Key] = file;
        }

        // Phase 3: Resolve cross-file imports
        ResolveImports(files, typeToFileKey);

        // Phase 4: Render all files
        var results = new Dictionary<string, string>();
        foreach (var (key, file) in files)
        {
            results[key] = Render(file);
        }

        return results;
    }

    /// <summary>
    /// Determines the output .proto file key for a [ProtoContract] type.
    /// </summary>
    internal static string ResolveFileKey(Type type)
    {
        var contractAttr = type.GetCustomAttribute<ProtoContractAttribute>();
        var serviceAttr = type.GetCustomAttribute<ProtoServiceAttribute>();

        // Service interfaces get their own file based on service name + version
        if (serviceAttr is not null && contractAttr is null)
        {
            return ResolveServiceFileKey(type);
        }

        if (contractAttr is not null && (contractAttr.Version > 0 || !string.IsNullOrEmpty(contractAttr.Name)))
        {
            string dir = contractAttr.Version > 0 ? $"v{contractAttr.Version}/" : "";
            string name = !string.IsNullOrEmpty(contractAttr.Name) ? contractAttr.Name : (type.Namespace?.Replace('.', '_').ToLowerInvariant() ?? "default");
            return $"{dir}{name}.proto";
        }

        return (type.Namespace ?? "default").Replace('.', '_').ToLowerInvariant() + ".proto";
    }

    private static string ResolveServiceFileKey(Type serviceType)
    {
        var serviceAttr = serviceType.GetCustomAttribute<ProtoServiceAttribute>();
        if (serviceAttr is null)
            return (serviceType.Namespace ?? "default").Replace('.', '_').ToLowerInvariant() + ".proto";

        string dir = serviceAttr.Version > 0 ? $"v{serviceAttr.Version}/" : "";
        return $"{dir}{serviceAttr.ServiceName}.proto";
    }

    /// <summary>
    /// Walks all files' messages and services to find cross-file type references,
    /// then adds import statements to each file.
    /// </summary>
    private static void ResolveImports(Dictionary<string, ProtoFile> files, Dictionary<Type, string> typeToFileKey)
    {
        foreach (var (fileKey, file) in files)
        {
            var imports = new HashSet<string>();

            // Check message field references
            foreach (var msg in file.Messages)
            {
                CollectImportsFromMessage(msg, fileKey, typeToFileKey, files, imports);
            }

            // Check service RPC type references
            foreach (var svc in file.Services)
            {
                foreach (var rpc in svc.Methods)
                {
                    TryAddImportForTypeName(rpc.RequestTypeName, fileKey, files, imports);
                    TryAddImportForTypeName(rpc.ResponseTypeName, fileKey, files, imports);
                }
            }

            file.Imports = imports.OrderBy(i => i).ToList();
        }
    }

    private static void CollectImportsFromMessage(ProtoMessageDef msg, string currentFileKey,
        Dictionary<Type, string> typeToFileKey, Dictionary<string, ProtoFile> files, HashSet<string> imports)
    {
        // Check if any field references a type in another file
        foreach (var field in msg.Fields)
        {
            TryAddImportForTypeName(field.TypeName, currentFileKey, files, imports);
            if (field.IsMap)
            {
                TryAddImportForTypeName(field.MapKeyType, currentFileKey, files, imports);
                TryAddImportForTypeName(field.MapValueType, currentFileKey, files, imports);
            }
        }

        foreach (var oneOf in msg.OneOfs)
        {
            foreach (var field in oneOf.Fields)
            {
                TryAddImportForTypeName(field.TypeName, currentFileKey, files, imports);
            }
        }

        // Check nested messages recursively
        foreach (var nested in msg.NestedMessages)
        {
            CollectImportsFromMessage(nested, currentFileKey, typeToFileKey, files, imports);
        }

        // Check if the message's SourceType belongs to another file
        if (msg.SourceType is not null && typeToFileKey.TryGetValue(msg.SourceType, out var sourceFileKey)
            && sourceFileKey != currentFileKey)
        {
            imports.Add(sourceFileKey);
        }
    }

    /// <summary>
    /// Checks if a proto type name refers to a message/enum defined in another file and adds the import.
    /// </summary>
    private static void TryAddImportForTypeName(string? typeName, string currentFileKey,
        Dictionary<string, ProtoFile> files, HashSet<string> imports)
    {
        if (string.IsNullOrEmpty(typeName) || IsScalarProtoType(typeName))
            return;

        foreach (var (otherFileKey, otherFile) in files)
        {
            if (otherFileKey == currentFileKey) continue;

            if (otherFile.Messages.Any(m => m.Name == typeName) || otherFile.Enums.Any(e => e.Name == typeName))
            {
                imports.Add(otherFileKey);
                return;
            }
        }
    }

    private static bool IsScalarProtoType(string typeName) => typeName switch
    {
        "bool" or "int32" or "uint32" or "int64" or "uint64" or "float" or "double"
            or "string" or "bytes" or "sint32" or "sint64" or "fixed32" or "fixed64"
            or "sfixed32" or "sfixed64" or "empty" => true,
        _ => false
    };

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
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
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

    private static void CollectType(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
    {
        if (!visited.Add(type))
            return;

        // If file-boundary-aware: skip types that belong to a different file
        if (typeToFileKey is not null && currentFileKey is not null)
        {
            if (typeToFileKey.TryGetValue(type, out var ownerKey) && ownerKey != currentFileKey)
            {
                visited.Remove(type); // allow it to be visited in its own file
                return;
            }
        }

        if (type.IsEnum)
        {
            file.Enums.Add(BuildEnum(type));
            return;
        }

        var contract = type.GetCustomAttribute<ProtoContractAttribute>();
        if (contract is null) return;

        var msgDef = BuildMessage(type, file, visited, typeToFileKey, currentFileKey);
        file.Messages.Add(msgDef);

        // Process [ProtoInclude] — generate messages for derived types too
        var includes = type.GetCustomAttributes<ProtoIncludeAttribute>();
        foreach (var inc in includes)
        {
            CollectType(inc.DerivedType, file, visited, typeToFileKey, currentFileKey);
        }
    }

    private static void CollectServices(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
    {
        // Check if the type itself is a service (e.g. interface with [ProtoService])
        var serviceAttr = type.GetCustomAttribute<ProtoServiceAttribute>();
        if (serviceAttr is not null)
        {
            var svcDef = BuildService(type, serviceAttr, file, visited, typeToFileKey, currentFileKey);
            if (svcDef is not null)
                file.Services.Add(svcDef);
        }

        // Also check interfaces implemented by the type
        foreach (var iface in type.GetInterfaces())
        {
            var ifaceAttr = iface.GetCustomAttribute<ProtoServiceAttribute>();
            if (ifaceAttr is not null && !file.Services.Any(s => s.SourceType == iface))
            {
                var svcDef = BuildService(iface, ifaceAttr, file, visited, typeToFileKey, currentFileKey);
                if (svcDef is not null)
                    file.Services.Add(svcDef);
            }
        }
    }

    private static ProtoServiceDef? BuildService(Type serviceType, ProtoServiceAttribute attr, ProtoFile file,
        HashSet<Type> visited, Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
    {
        var svc = new ProtoServiceDef
        {
            Name = attr.ServiceName,
            SourceType = serviceType,
            Metadata = attr.Metadata ?? serviceType.GetCustomAttribute<ProtoContractAttribute>()?.Metadata
        };

        foreach (var method in serviceType.GetMethods())
        {
            var methodAttr = method.GetCustomAttribute<ProtoMethodAttribute>();
            if (methodAttr is null) continue;

            var (reqType, resType) = ExtractRpcTypes(method, methodAttr.MethodType);
            string rpcName = methodAttr.Name ?? method.Name;

            // Resolve proto type names (which also collects types into the current file if they belong here)
            string baseReqName = MapToProtoType(reqType, file, visited, typeToFileKey, currentFileKey);
            string baseResName = resType == typeof(void) ? "empty" : MapToProtoType(resType, file, visited, typeToFileKey, currentFileKey);

            // Auto-wrap request
            string finalReqName = baseReqName;
            if (!baseReqName.EndsWith("Request", StringComparison.OrdinalIgnoreCase))
            {
                finalReqName = rpcName + "Request";
                var wrapReq = new ProtoMessageDef { Name = finalReqName, SourceType = serviceType, Metadata = "Auto-generated RPC request wrapper" };
                if (baseReqName != "empty")
                    wrapReq.Fields.Add(new ProtoFieldDef { Name = "data", FieldNumber = 1, TypeName = baseReqName });
                file.Messages.Add(wrapReq);
            }

            // Auto-wrap response
            string finalResName = baseResName;
            if (!baseResName.EndsWith("Response", StringComparison.OrdinalIgnoreCase))
            {
                finalResName = rpcName + "Response";
                var wrapRes = new ProtoMessageDef { Name = finalResName, SourceType = serviceType, Metadata = "Auto-generated RPC response wrapper" };
                if (baseResName != "empty")
                    wrapRes.Fields.Add(new ProtoFieldDef { Name = "data", FieldNumber = 1, TypeName = baseResName });
                file.Messages.Add(wrapRes);
            }

            svc.Methods.Add(new ProtoRpcDef
            {
                Name = rpcName,
                MethodType = methodAttr.MethodType,
                RequestTypeName = finalReqName,
                ResponseTypeName = finalResName
            });
        }

        return svc.Methods.Count > 0 ? svc : null;
    }

    private static (Type request, Type response) ExtractRpcTypes(MethodInfo method, ProtoMethodType methodType)
    {
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        Type reqType = parameters.Length > 0 ? parameters[0].ParameterType : typeof(void);
        Type resType = method.ReturnType;

        if (methodType is ProtoMethodType.ClientStreaming or ProtoMethodType.DuplexStreaming)
        {
            if (reqType.IsGenericType && reqType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                reqType = reqType.GetGenericArguments()[0];
        }

        if (resType == typeof(Task) || resType == typeof(ValueTask))
        {
            resType = typeof(void);
        }
        else if (resType.IsGenericType)
        {
            var genericDef = resType.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>) || genericDef == typeof(IAsyncEnumerable<>))
            {
                resType = resType.GetGenericArguments()[0];
            }
        }

        return (reqType, resType);
    }

    private static ProtoMessageDef BuildMessage(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
    {
        var descriptors = ContractResolver.Resolve(type);
        var contract = type.GetCustomAttribute<ProtoContractAttribute>();
        var msg = new ProtoMessageDef
        {
            Name = !string.IsNullOrEmpty(contract?.Name) ? contract.Name : type.Name,
            SourceType = type,
            Metadata = contract?.Metadata
        };

        // Group fields by oneof
        var oneOfGroups = new Dictionary<string, ProtoOneOfDef>();

        foreach (var field in descriptors)
        {
            var fieldDef = BuildFieldDef(field, file, visited, typeToFileKey, currentFileKey);

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
            CollectType(inc.DerivedType, file, visited, typeToFileKey, currentFileKey);
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

    private static ProtoFieldDef BuildFieldDef(FieldDescriptor field, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
    {
        // Map field
        if (field.IsMap && field.MapKeyType is not null && field.MapValueType is not null)
        {
            string keyTypeName = MapToProtoType(field.MapKeyType, file, visited, typeToFileKey, currentFileKey);
            string valueTypeName = MapToProtoType(field.MapValueType, file, visited, typeToFileKey, currentFileKey);

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
            typeName = MapToProtoType(field.ElementType, file, visited, typeToFileKey, currentFileKey);
        }
        else
        {
            typeName = MapToProtoType(underlying, file, visited, typeToFileKey, currentFileKey);
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

    private static string MapToProtoType(Type clrType, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null)
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

        // Enum — collect it if it belongs to this file, reference by name either way
        if (underlying.IsEnum)
        {
            CollectType(underlying, file, visited, typeToFileKey, currentFileKey);
            return underlying.Name;
        }

        // Nested message — works with both explicit [ProtoContract] and implicit types
        if (underlying.GetCustomAttribute<ProtoContractAttribute>() is var contract && contract is not null)
        {
            CollectType(underlying, file, visited, typeToFileKey, currentFileKey);
            return !string.IsNullOrEmpty(contract.Name) ? contract.Name : underlying.Name;
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
                    implicitMsg.Fields.Add(BuildFieldDef(f, file, visited, typeToFileKey, currentFileKey));
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

        if (file.Imports.Count > 0)
        {
            foreach (var import in file.Imports)
            {
                sb.AppendLine($"import \"{import}\";");
            }
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

        foreach (var svc in file.Services)
        {
            RenderService(sb, svc, indent: 0);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void RenderMessage(StringBuilder sb, ProtoMessageDef msg, int indent)
    {
        var pad = new string(' ', indent * 2);

        if (msg.SourceType is not null)
        {
            sb.AppendLine($"{pad}// Imported from C# class: {msg.SourceType.FullName}");
        }
        if (!string.IsNullOrWhiteSpace(msg.Metadata))
        {
            sb.AppendLine($"{pad}// Metadata: {msg.Metadata}");
        }

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

    private static void RenderService(StringBuilder sb, ProtoServiceDef svc, int indent)
    {
        var pad = new string(' ', indent * 2);

        if (svc.SourceType is not null)
            sb.AppendLine($"{pad}// Imported from C# service: {svc.SourceType.FullName}");
        if (!string.IsNullOrWhiteSpace(svc.Metadata))
            sb.AppendLine($"{pad}// Metadata: {svc.Metadata}");

        sb.AppendLine($"{pad}service {svc.Name} {{");

        foreach (var rpc in svc.Methods)
        {
            string reqStream = rpc.MethodType is ProtoMethodType.ClientStreaming or ProtoMethodType.DuplexStreaming ? "stream " : "";
            string resStream = rpc.MethodType is ProtoMethodType.ServerStreaming or ProtoMethodType.DuplexStreaming ? "stream " : "";

            sb.AppendLine($"{pad}  rpc {rpc.Name} ({reqStream}{rpc.RequestTypeName}) returns ({resStream}{rpc.ResponseTypeName});");
        }

        sb.AppendLine($"{pad}}}");
    }

    #endregion
}
