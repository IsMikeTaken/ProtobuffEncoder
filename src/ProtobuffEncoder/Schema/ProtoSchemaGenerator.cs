using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Generates .proto schema files from C# types marked with <see cref="ProtoContractAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Follows the official proto3 best-practice rules from
/// <see href="https://protobuf.dev/best-practices/dos-donts/">protobuf.dev/best-practices</see>:
/// <list type="bullet">
///   <item>Enum values use <c>SCREAMING_SNAKE_CASE</c> with a <c>TYPE_NAME_</c> prefix.</item>
///   <item>Every enum has an <c>UNSPECIFIED = 0</c> sentinel unless suppressed.</item>
///   <item>Removed field numbers and names are emitted as <c>reserved</c> statements.</item>
///   <item>Services use PascalCase RPC names; each RPC gets its own <c>FooRequest</c> /
///         <c>FooResponse</c> message wrapper.</item>
///   <item>Well-known types (<c>google.protobuf.Timestamp</c>, <c>Duration</c>,
///         <c>Empty</c>) replace custom encodings where the CLR type matches.</item>
///   <item>Field names are rendered in <c>snake_case</c>.</item>
/// </list>
/// </para>
/// </remarks>
public static class ProtoSchemaGenerator
{
    // ── Well-known type map ───────────────────────────────────────────────────

    /// <summary>
    /// Maps CLR types to their google.protobuf well-known type names and the import path
    /// required to use them.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, (string ProtoType, string Import)> WellKnownTypes
        = new Dictionary<Type, (string, string)>
        {
            [typeof(DateTime)] = ("google.protobuf.Timestamp", "google/protobuf/timestamp.proto"),
            [typeof(DateTimeOffset)] = ("google.protobuf.Timestamp", "google/protobuf/timestamp.proto"),
            [typeof(TimeSpan)] = ("google.protobuf.Duration", "google/protobuf/duration.proto"),
            [typeof(object)] = ("google.protobuf.Any", "google/protobuf/any.proto"),
        };

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Generates a <c>.proto</c> file for <paramref name="type"/> and all its dependencies.
    /// </summary>
    public static string Generate(Type type)
    {
        var file = BuildProtoFile(type);
        return Render(file);
    }

    /// <summary>
    /// Generates <c>.proto</c> files for all <see cref="ProtoContractAttribute"/>-decorated
    /// types in <paramref name="assembly"/>.
    /// Returns a dictionary of <c>filename → proto content</c>.
    /// </summary>
    public static Dictionary<string, string> GenerateAll(Assembly assembly)
    {
        var contractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() is not null ||
                        t.GetCustomAttribute<ProtoServiceAttribute>() is not null)
            .ToList();

        // Phase 1: build type → file-key registry.
        var typeToFileKey = new Dictionary<Type, string>();
        foreach (var type in contractTypes)
            typeToFileKey[type] = ResolveFileKey(type);

        // Discover [ProtoService] interfaces referenced by implementation types.
        var serviceInterfaces = new List<Type>();
        foreach (var type in contractTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.GetCustomAttribute<ProtoServiceAttribute>() is not null
                    && !typeToFileKey.ContainsKey(iface))
                {
                    typeToFileKey[iface] = ResolveServiceFileKey(iface);
                    serviceInterfaces.Add(iface);
                }
            }
        }

        // Group by file key.
        var grouped = contractTypes.Concat(serviceInterfaces).GroupBy(t => typeToFileKey[t]);

        // Phase 2: build ProtoFile models.
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
            var wellKnownImports = new HashSet<string>();

            foreach (var type in group)
                CollectType(type, file, visited, typeToFileKey, group.Key, wellKnownImports);

            foreach (var type in group)
                CollectServices(type, file, visited, typeToFileKey, group.Key, wellKnownImports);

            file.WellKnownImports = wellKnownImports;
            files[group.Key] = file;
        }

        // Phase 3: cross-file imports.
        ResolveImports(files, typeToFileKey);

        // Phase 4: render.
        return files.ToDictionary(kv => kv.Key, kv => Render(kv.Value));
    }

    /// <summary>
    /// Generates all <c>.proto</c> files from <paramref name="assembly"/> and writes them to
    /// <paramref name="outputDirectory"/>. Returns the list of generated file paths.
    /// </summary>
    public static List<string> GenerateToDirectory(Assembly assembly, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var files = GenerateAll(assembly);
        var paths = new List<string>(files.Count);

        foreach (var (filename, content) in files)
        {
            var path = Path.Combine(outputDirectory, filename);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content);
            paths.Add(path);
        }

        return paths;
    }

    // ── File-key resolution ───────────────────────────────────────────────────

    /// <summary>
    /// Determines the output <c>.proto</c> file key for a type.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    ///   <item><see cref="ProtoServiceAttribute"/>-only interfaces → keyed by service name.</item>
    ///   <item>Explicit <see cref="ProtoContractAttribute.Name"/> override.</item>
    ///   <item>Versioned contracts: <c>v{N}/namespace_or_typename.proto</c>.</item>
    ///   <item>Types with a namespace → <c>namespace_dotted.proto</c>.</item>
    ///   <item>Global-namespace types → <c>typename.proto</c>.</item>
    /// </list>
    /// </remarks>
    internal static string ResolveFileKey(Type type)
    {
        var contractAttr = type.GetCustomAttribute<ProtoContractAttribute>();
        var serviceAttr = type.GetCustomAttribute<ProtoServiceAttribute>();

        if (serviceAttr is not null && contractAttr is null)
            return ResolveServiceFileKey(type);

        if (contractAttr is not null)
        {
            if (!string.IsNullOrEmpty(contractAttr.Name))
            {
                string dir = contractAttr.Version > 0 ? $"v{contractAttr.Version}/" : "";
                return $"{dir}{contractAttr.Name}.proto";
            }

            if (contractAttr.Version > 0)
            {
                string baseName = string.IsNullOrEmpty(type.Namespace)
                    ? type.Name.ToLowerInvariant()
                    : type.Namespace.Replace('.', '_').ToLowerInvariant();
                return $"v{contractAttr.Version}/{baseName}.proto";
            }
        }

        if (!string.IsNullOrEmpty(type.Namespace))
            return type.Namespace.Replace('.', '_').ToLowerInvariant() + ".proto";

        return type.Name.ToLowerInvariant() + ".proto";
    }

    private static string ResolveServiceFileKey(Type serviceType)
    {
        var serviceAttr = serviceType.GetCustomAttribute<ProtoServiceAttribute>();
        if (serviceAttr is null)
        {
            return string.IsNullOrEmpty(serviceType.Namespace)
                ? serviceType.Name.ToLowerInvariant() + ".proto"
                : serviceType.Namespace.Replace('.', '_').ToLowerInvariant() + ".proto";
        }

        string dir = serviceAttr.Version > 0 ? $"v{serviceAttr.Version}/" : "";
        return $"{dir}{serviceAttr.ServiceName}.proto";
    }

    // ── Import resolution ─────────────────────────────────────────────────────

    private static void ResolveImports(Dictionary<string, ProtoFile> files, Dictionary<Type, string> typeToFileKey)
    {
        var declaredTypes = BuildDeclaredTypeLookup(files);

        foreach (var (fileKey, file) in files)
        {
            var imports = file.WellKnownImports is { Count: > 0 }
                ? new HashSet<string>(file.WellKnownImports, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            foreach (var msg in file.Messages)
                CollectImportsFromMessage(msg, fileKey, typeToFileKey, declaredTypes, imports);

            foreach (var svc in file.Services)
            {
                foreach (var rpc in svc.Methods)
                {
                    TryAddImportForTypeName(rpc.RequestTypeName, fileKey, declaredTypes, imports);
                    TryAddImportForTypeName(rpc.ResponseTypeName, fileKey, declaredTypes, imports);
                }
            }

            file.Imports = [.. imports];
            file.Imports.Sort(StringComparer.Ordinal);
        }
    }

    private static Dictionary<string, string> BuildDeclaredTypeLookup(Dictionary<string, ProtoFile> files)
    {
        var declaredTypes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (fileKey, file) in files)
        {
            foreach (var message in file.Messages)
            {
                declaredTypes.TryAdd(message.Name, fileKey);
            }

            foreach (var enumDef in file.Enums)
            {
                declaredTypes.TryAdd(enumDef.Name, fileKey);
            }
        }

        return declaredTypes;
    }

    private static void CollectImportsFromMessage(ProtoMessageDef msg, string currentFileKey,
        Dictionary<Type, string> typeToFileKey, IReadOnlyDictionary<string, string> declaredTypes, HashSet<string> imports)
    {
        foreach (var field in msg.Fields)
        {
            TryAddImportForTypeName(field.TypeName, currentFileKey, declaredTypes, imports);
            if (field.IsMap)
            {
                TryAddImportForTypeName(field.MapKeyType, currentFileKey, declaredTypes, imports);
                TryAddImportForTypeName(field.MapValueType, currentFileKey, declaredTypes, imports);
            }
        }

        foreach (var oneOf in msg.OneOfs)
        {
            foreach (var field in oneOf.Fields)
            {
                TryAddImportForTypeName(field.TypeName, currentFileKey, declaredTypes, imports);
            }
        }

        foreach (var nested in msg.NestedMessages)
        {
            CollectImportsFromMessage(nested, currentFileKey, typeToFileKey, declaredTypes, imports);
        }

        if (msg.SourceType is not null
            && typeToFileKey.TryGetValue(msg.SourceType, out var sourceFileKey)
            && sourceFileKey != currentFileKey)
        {
            imports.Add(sourceFileKey);
        }
    }

    private static void TryAddImportForTypeName(string? typeName, string currentFileKey,
        IReadOnlyDictionary<string, string> declaredTypes, HashSet<string> imports)
    {
        if (string.IsNullOrEmpty(typeName) || IsScalarProtoType(typeName))
            return;

        if (typeName.StartsWith("google.protobuf.", StringComparison.Ordinal))
            return;

        if (declaredTypes.TryGetValue(typeName, out var fileKey) && fileKey != currentFileKey)
        {
            imports.Add(fileKey);
        }
    }

    private static bool IsScalarProtoType(string typeName) => typeName switch
    {
        "bool" or "int32" or "uint32" or "int64" or "uint64" or "float" or "double"
            or "string" or "bytes" or "sint32" or "sint64" or "fixed32" or "fixed64"
            or "sfixed32" or "sfixed64" => true,
        _ => false
    };

    // ── Model builders ────────────────────────────────────────────────────────

    private static ProtoFile BuildProtoFile(Type type)
    {
        var file = new ProtoFile { Syntax = "proto3", Package = type.Namespace ?? "" };
        var visited = new HashSet<Type>();
        var wellKnownImports = new HashSet<string>();
        CollectType(type, file, visited, wellKnownImports: wellKnownImports);
        file.WellKnownImports = wellKnownImports;
        return file;
    }

    private static void CollectType(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
    {
        if (!visited.Add(type)) return;

        if (typeToFileKey is not null && currentFileKey is not null)
        {
            if (typeToFileKey.TryGetValue(type, out var ownerKey) && ownerKey != currentFileKey)
            {
                visited.Remove(type);
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

        var msgDef = BuildMessage(type, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
        file.Messages.Add(msgDef);

        foreach (var inc in type.GetCustomAttributes<ProtoIncludeAttribute>())
            CollectType(inc.DerivedType, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
    }

    private static void CollectServices(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
    {
        var serviceAttr = type.GetCustomAttribute<ProtoServiceAttribute>();
        if (serviceAttr is not null)
        {
            var svcDef = BuildService(type, serviceAttr, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
            if (svcDef is not null) file.Services.Add(svcDef);
        }

        foreach (var iface in type.GetInterfaces())
        {
            var ifaceAttr = iface.GetCustomAttribute<ProtoServiceAttribute>();
            if (ifaceAttr is not null && !file.Services.Any(s => s.SourceType == iface))
            {
                var svcDef = BuildService(iface, ifaceAttr, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
                if (svcDef is not null) file.Services.Add(svcDef);
            }
        }
    }

    // ── Enum builder ──────────────────────────────────────────────────────────

    private static ProtoEnumDef BuildEnum(Type enumType)
    {
        var enumAttr = enumType.GetCustomAttribute<ProtoEnumAttribute>();
        string prefix = enumAttr?.Prefix ?? ToScreamingSnakeCase(enumType.Name);

        var def = new ProtoEnumDef
        {
            Name = enumType.Name,
            AllowAlias = enumAttr?.AllowAlias ?? false,
            Comment = enumAttr?.Comment,
        };

        var members = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

        // Determine if an explicit zero-value entry exists.
        bool hasExplicitZero = members.Any(m =>
        {
            int clrValue = (int)m.GetRawConstantValue()!;
            var valAttr = m.GetCustomAttribute<ProtoEnumValueAttribute>();
            int protoNum = valAttr?.Number ?? clrValue;
            return protoNum == 0;
        });

        // Proto3 best practice: every enum needs a zero-value UNSPECIFIED sentinel.
        bool suppressUnspecified = enumAttr?.SuppressUnspecified ?? false;
        if (!hasExplicitZero && !suppressUnspecified)
        {
            def.Values.Add(new ProtoEnumValue
            {
                Name = $"{prefix}_UNSPECIFIED",
                Number = 0,
            });
        }

        // Build value list — stable sort by number ascending (proto3 style).
        var valueList = new List<ProtoEnumValue>();
        foreach (var member in members)
        {
            int clrValue = (int)member.GetRawConstantValue()!;
            var valAttr = member.GetCustomAttribute<ProtoEnumValueAttribute>();
            var fieldAttr = member.GetCustomAttribute<ProtoFieldAttribute>();

            int protoNum = valAttr?.Number ?? clrValue;
            string protoName = valAttr?.Name ?? $"{prefix}_{ToScreamingSnakeCase(member.Name)}";
            bool isDeprecated = fieldAttr?.IsDeprecated ?? false;

            valueList.Add(new ProtoEnumValue
            {
                Name = protoName,
                Number = protoNum,
                IsDeprecated = isDeprecated,
            });
        }

        // Sort by number; within same number keep declaration order (aliases).
        valueList.Sort(static (a, b) => a.Number != b.Number
            ? a.Number.CompareTo(b.Number)
            : 0);

        def.Values.AddRange(valueList);
        return def;
    }

    // ── Message builder ───────────────────────────────────────────────────────

    private static ProtoMessageDef BuildMessage(Type type, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
    {
        var descriptors = ContractResolver.Resolve(type);
        var contract = type.GetCustomAttribute<ProtoContractAttribute>();

        var msg = new ProtoMessageDef
        {
            Name = !string.IsNullOrEmpty(contract?.Name) ? contract.Name : type.Name,
            SourceType = type,
            Metadata = contract?.Metadata,
        };

        var oneOfGroups = new Dictionary<string, ProtoOneOfDef>();

        foreach (var field in descriptors)
        {
            var fieldDef = BuildFieldDef(field, file, visited, typeToFileKey, currentFileKey, wellKnownImports);

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

        foreach (var inc in type.GetCustomAttributes<ProtoIncludeAttribute>())
        {
            CollectType(inc.DerivedType, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
            msg.Fields.Add(new ProtoFieldDef
            {
                Name = ToSnakeCase(inc.DerivedType.Name),
                FieldNumber = inc.FieldNumber,
                TypeName = inc.DerivedType.Name,
                IsOptional = true,
            });
        }

        return msg;
    }

    private static ProtoFieldDef BuildFieldDef(FieldDescriptor field, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
    {
        if (field.IsMap && field.MapKeyType is not null && field.MapValueType is not null)
        {
            return new ProtoFieldDef
            {
                Name = field.Name,
                FieldNumber = field.FieldNumber,
                IsMap = true,
                MapKeyType = MapToProtoType(field.MapKeyType, file, visited, typeToFileKey, currentFileKey, wellKnownImports),
                MapValueType = MapToProtoType(field.MapValueType, file, visited, typeToFileKey, currentFileKey, wellKnownImports),
                IsDeprecated = field.IsDeprecated,
            };
        }

        var propType = field.Property.PropertyType;
        var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
        bool isOptional = field.IsNullable;
        bool isRepeated = field.IsCollection;

        string typeName = isRepeated && field.ElementType is not null
            ? MapToProtoType(field.ElementType, file, visited, typeToFileKey, currentFileKey, wellKnownImports)
            : MapToProtoType(underlying, file, visited, typeToFileKey, currentFileKey, wellKnownImports);

        return new ProtoFieldDef
        {
            Name = field.Name,
            FieldNumber = field.FieldNumber,
            TypeName = typeName,
            IsRepeated = isRepeated,
            IsOptional = isOptional,
            IsDeprecated = field.IsDeprecated,
        };
    }

    // ── Service builder ───────────────────────────────────────────────────────

    private static ProtoServiceDef? BuildService(Type serviceType, ProtoServiceAttribute attr, ProtoFile file,
        HashSet<Type> visited, Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
    {
        var svc = new ProtoServiceDef
        {
            Name = attr.ServiceName,
            SourceType = serviceType,
            Metadata = attr.Metadata ?? serviceType.GetCustomAttribute<ProtoContractAttribute>()?.Metadata,
        };

        var allMethods = serviceType.GetMethods();
        bool anyExplicit = allMethods.Any(m => m.GetCustomAttribute<ProtoMethodAttribute>() is not null);

        // Track generated wrapper names to avoid duplicates when multiple RPCs share types.
        var usedWrapperNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in allMethods)
        {
            var methodAttr = method.GetCustomAttribute<ProtoMethodAttribute>();
            if (!anyExplicit)
                methodAttr = InferProtoMethodAttribute(method);
            if (methodAttr is null) continue;

            var (reqType, resType) = ExtractRpcTypes(method, methodAttr.MethodType);
            string rpcName = methodAttr.Name ?? method.Name;

            string baseReqName = MapToProtoType(reqType, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
            string baseResName = resType == typeof(void)
                ? "google.protobuf.Empty"
                : MapToProtoType(resType, file, visited, typeToFileKey, currentFileKey, wellKnownImports);

            // Ensure google.protobuf.Empty import is tracked.
            if (baseResName == "google.protobuf.Empty")
                wellKnownImports?.Add("google/protobuf/empty.proto");

            // Proto3 best practice: every RPC gets its own FooRequest / FooResponse message.
            string reqWrapperName = EnsureWrapper(rpcName + "Request", baseReqName, "request", file, usedWrapperNames);
            string resWrapperName = EnsureWrapper(rpcName + "Response", baseResName, "response", file, usedWrapperNames);

            svc.Methods.Add(new ProtoRpcDef
            {
                Name = rpcName,
                MethodType = methodAttr.MethodType,
                RequestTypeName = reqWrapperName,
                ResponseTypeName = resWrapperName,
                IsDeprecated = method.GetCustomAttribute<ObsoleteAttribute>() is not null,
                Comment = methodAttr.Name is not null ? null : $"Auto-inferred from {method.Name} signature",
            });
        }

        return svc.Methods.Count > 0 ? svc : null;
    }

    /// <summary>
    /// Ensures a dedicated <c>FooRequest</c>/<c>FooResponse</c> wrapper message exists
    /// in <paramref name="file"/> when the underlying type name doesn't already end with
    /// the expected suffix.  Returns the final message name to use in the RPC declaration.
    /// </summary>
    private static string EnsureWrapper(string wrapperName, string innerTypeName,
        string fieldLabel, ProtoFile file, HashSet<string> usedWrapperNames)
    {
        // If the inner type is already correctly named, use it directly.
        if (innerTypeName.Equals(wrapperName, StringComparison.OrdinalIgnoreCase))
            return innerTypeName;

        // If we've already emitted this wrapper in this service, reuse it.
        if (usedWrapperNames.Contains(wrapperName))
            return wrapperName;

        // If a message with this name already exists (from a previous service), reuse it.
        if (file.Messages.Any(m => m.Name == wrapperName))
        {
            usedWrapperNames.Add(wrapperName);
            return wrapperName;
        }

        var wrapper = new ProtoMessageDef
        {
            Name = wrapperName,
            Metadata = $"Auto-generated RPC {fieldLabel} wrapper",
        };

        bool isEmpty = innerTypeName is "google.protobuf.Empty" or "empty";
        if (!isEmpty)
        {
            wrapper.Fields.Add(new ProtoFieldDef
            {
                Name = "data",
                FieldNumber = 1,
                TypeName = innerTypeName,
            });
        }

        file.Messages.Add(wrapper);
        usedWrapperNames.Add(wrapperName);
        return wrapperName;
    }

    // ── RPC type extraction ───────────────────────────────────────────────────

    private static ProtoMethodAttribute InferProtoMethodAttribute(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(static p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        bool streamingIn = parameters.Length > 0
            && parameters[0].ParameterType.IsGenericType
            && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);

        Type returnType = method.ReturnType;
        bool streamingOut = returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);

        ProtoMethodType methodType = (streamingIn, streamingOut) switch
        {
            (true, true) => ProtoMethodType.DuplexStreaming,
            (true, false) => ProtoMethodType.ClientStreaming,
            (false, true) => ProtoMethodType.ServerStreaming,
            _ => ProtoMethodType.Unary,
        };

        return new ProtoMethodAttribute(methodType) { Name = method.Name };
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
                resType = resType.GetGenericArguments()[0];
        }

        return (reqType, resType);
    }

    // ── CLR → proto type mapping ──────────────────────────────────────────────

    private static string MapToProtoType(Type clrType, ProtoFile file, HashSet<Type> visited,
        Dictionary<Type, string>? typeToFileKey = null, string? currentFileKey = null,
        HashSet<string>? wellKnownImports = null)
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

        // Well-known types.
        if (WellKnownTypes.TryGetValue(underlying, out var wkt))
        {
            wellKnownImports?.Add(wkt.Import);
            return wkt.ProtoType;
        }

        // Enum.
        if (underlying.IsEnum)
        {
            CollectType(underlying, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
            return underlying.Name;
        }

        // Explicit [ProtoContract] message.
        if (underlying.GetCustomAttribute<ProtoContractAttribute>() is { } contract)
        {
            CollectType(underlying, file, visited, typeToFileKey, currentFileKey, wellKnownImports);
            return !string.IsNullOrEmpty(contract.Name) ? contract.Name : underlying.Name;
        }

        // Implicit nested type (class used in an ImplicitFields context).
        if (underlying.IsClass && underlying != typeof(string) && underlying != typeof(object))
        {
            if (visited.Add(underlying))
            {
                var implicitDescriptors = ContractResolver.ResolveImplicit(underlying);
                var implicitMsg = new ProtoMessageDef { Name = underlying.Name };
                foreach (var f in implicitDescriptors)
                    implicitMsg.Fields.Add(BuildFieldDef(f, file, visited, typeToFileKey, currentFileKey, wellKnownImports));
                file.Messages.Add(implicitMsg);
            }
            return underlying.Name;
        }

        return "bytes"; // fallback
    }

    // ── Renderer ──────────────────────────────────────────────────────────────

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

        var allImports = file.WellKnownImports is { Count: > 0 }
            ? new List<string>(file.WellKnownImports.Count + file.Imports.Count)
            : new List<string>(file.Imports.Count);
        var seenImports = new HashSet<string>(StringComparer.Ordinal);

        if (file.WellKnownImports is not null)
        {
            foreach (var import in file.WellKnownImports)
            {
                if (seenImports.Add(import))
                {
                    allImports.Add(import);
                }
            }
        }

        foreach (var import in file.Imports)
        {
            if (seenImports.Add(import))
            {
                allImports.Add(import);
            }
        }

        allImports.Sort(StringComparer.Ordinal);

        if (allImports.Count > 0)
        {
            foreach (var import in allImports)
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

    private static void RenderEnum(StringBuilder sb, ProtoEnumDef enumDef, int indent)
    {
        var pad = Pad(indent);

        if (!string.IsNullOrWhiteSpace(enumDef.Comment))
            sb.AppendLine($"{pad}// {enumDef.Comment}");

        sb.AppendLine($"{pad}enum {enumDef.Name} {{");

        if (enumDef.AllowAlias)
            sb.AppendLine($"{pad}  option allow_alias = true;");

        // reserved numbers.
        if (enumDef.ReservedNumbers.Count > 0)
            sb.AppendLine($"{pad}  reserved {string.Join(", ", enumDef.ReservedNumbers.OrderBy(n => n))};");

        // reserved names.
        if (enumDef.ReservedNames.Count > 0)
            sb.AppendLine($"{pad}  reserved {string.Join(", ", enumDef.ReservedNames.OrderBy(n => n).Select(n => $"\"{n}\""))};");

        foreach (var val in enumDef.Values)
        {
            var deprecated = val.IsDeprecated ? " [deprecated = true]" : "";
            sb.AppendLine($"{pad}  {val.Name} = {val.Number}{deprecated};");
        }

        sb.AppendLine($"{pad}}}");
    }

    private static void RenderMessage(StringBuilder sb, ProtoMessageDef msg, int indent)
    {
        var pad = Pad(indent);

        if (msg.SourceType is not null)
            sb.AppendLine($"{pad}// source: {msg.SourceType.FullName}");

        if (!string.IsNullOrWhiteSpace(msg.Metadata))
            sb.AppendLine($"{pad}// {msg.Metadata}");

        sb.AppendLine($"{pad}message {msg.Name} {{");

        // reserved numbers block.
        if (msg.ReservedNumbers.Count > 0)
            sb.AppendLine($"{pad}  reserved {string.Join(", ", msg.ReservedNumbers.OrderBy(n => n))};");

        // reserved names block.
        if (msg.ReservedNames.Count > 0)
            sb.AppendLine($"{pad}  reserved {string.Join(", ", msg.ReservedNames.OrderBy(n => n).Select(n => $"\"{n}\""))};");

        foreach (var nestedEnum in msg.NestedEnums)
            RenderEnum(sb, nestedEnum, indent + 1);

        foreach (var nested in msg.NestedMessages)
            RenderMessage(sb, nested, indent + 1);

        foreach (var field in msg.Fields)
            RenderField(sb, field, pad + "  ");

        foreach (var oneOf in msg.OneOfs)
        {
            sb.AppendLine($"{pad}  oneof {oneOf.Name} {{");
            foreach (var field in oneOf.Fields)
                RenderField(sb, field, pad + "    ");
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

    private static void RenderService(StringBuilder sb, ProtoServiceDef svc, int indent)
    {
        var pad = Pad(indent);

        if (svc.SourceType is not null)
            sb.AppendLine($"{pad}// source: {svc.SourceType.FullName}");

        if (!string.IsNullOrWhiteSpace(svc.Metadata))
            sb.AppendLine($"{pad}// {svc.Metadata}");

        sb.AppendLine($"{pad}service {svc.Name} {{");

        if (svc.IsDeprecated)
            sb.AppendLine($"{pad}  option deprecated = true;");

        foreach (var rpc in svc.Methods)
        {
            if (!string.IsNullOrEmpty(rpc.Comment))
                sb.AppendLine($"{pad}  // {rpc.Comment}");

            string reqStream = rpc.MethodType is ProtoMethodType.ClientStreaming or ProtoMethodType.DuplexStreaming ? "stream " : "";
            string resStream = rpc.MethodType is ProtoMethodType.ServerStreaming or ProtoMethodType.DuplexStreaming ? "stream " : "";

            if (rpc.IsDeprecated)
            {
                sb.AppendLine($"{pad}  rpc {rpc.Name} ({reqStream}{rpc.RequestTypeName}) returns ({resStream}{rpc.ResponseTypeName}) {{");
                sb.AppendLine($"{pad}    option deprecated = true;");
                sb.AppendLine($"{pad}  }}");
            }
            else
            {
                sb.AppendLine($"{pad}  rpc {rpc.Name} ({reqStream}{rpc.RequestTypeName}) returns ({resStream}{rpc.ResponseTypeName});");
            }
        }

        sb.AppendLine($"{pad}}}");
    }

    // ── Naming helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a PascalCase or camelCase name to <c>snake_case</c> for proto field names.
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        return ConvertCase(name, uppercase: false);
    }

    /// <summary>
    /// Converts a PascalCase or camelCase name to <c>SCREAMING_SNAKE_CASE</c> for enum values.
    /// </summary>
    private static string ToScreamingSnakeCase(string name)
    {
        return ConvertCase(name, uppercase: true);
    }

    private static string ConvertCase(string name, bool uppercase)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 4);
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (index > 0 && char.IsUpper(current) && char.IsLetterOrDigit(name[index - 1]) && !char.IsUpper(name[index - 1]))
            {
                builder.Append('_');
            }

            builder.Append(uppercase ? char.ToUpperInvariant(current) : char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static string Pad(int indent) => new(' ', indent * 2);
}

