using System.Collections.Concurrent;
using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder;

/// <summary>
/// Resolves and caches field descriptors for proto-contract types using reflection and attributes.
/// Supports inheritance (IncludeBaseFields), implicit nested types, dictionaries (maps),
/// oneof groups, and packed-encoding overrides.
/// </summary>
internal static class ContractResolver
{
    private static readonly ConcurrentDictionary<Type, FieldDescriptor[]> Cache = new();

    // Track types currently being resolved to detect and handle implicit nesting safely
    private static readonly ConcurrentDictionary<Type, bool> ImplicitlyRegistered = new();

    public static FieldDescriptor[] Resolve(Type type)
    {
        return Cache.GetOrAdd(type, static t => ResolveCore(t, implicitMode: false));
    }

    /// <summary>
    /// Resolves a type that may not have [ProtoContract] when in implicit mode.
    /// </summary>
    internal static FieldDescriptor[] ResolveImplicit(Type type)
    {
        return Cache.GetOrAdd(type, static t => ResolveCore(t, implicitMode: true));
    }

    /// <summary>
    /// Returns the [ProtoInclude] derived-type mappings declared on a base type.
    /// </summary>
    internal static ProtoIncludeAttribute[] GetIncludes(Type type)
    {
        return type.GetCustomAttributes<ProtoIncludeAttribute>().ToArray();
    }

    /// <summary>
    /// Checks whether a type is resolvable as a proto contract (explicit or implicit).
    /// </summary>
    internal static bool IsContractType(Type type)
    {
        return type.GetCustomAttribute<ProtoContractAttribute>() is not null;
    }

    private static FieldDescriptor[] ResolveCore(Type type, bool implicitMode)
    {
        var contract = type.GetCustomAttribute<ProtoContractAttribute>();

        if (contract is null && !implicitMode)
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is not marked with [ProtoContract].");

        // When implicit mode and no contract, treat as if [ProtoContract(ImplicitFields = true)]
        bool explicitFields = contract?.ExplicitFields ?? false;
        bool includeBase = contract?.IncludeBaseFields ?? false;
        bool implicitFields = contract?.ImplicitFields ?? implicitMode;

        // Collect properties: optionally walk base classes
        var properties = GetProperties(type, includeBase);

        // Also collect [ProtoInclude] subtype field numbers as reserved
        var includes = type.GetCustomAttributes<ProtoIncludeAttribute>().ToArray();

        // Pass 1: collect all explicitly assigned field numbers so auto-assignment can avoid them
        var reservedNumbers = new HashSet<int>();
        foreach (var inc in includes)
            reservedNumbers.Add(inc.FieldNumber);

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<ProtoIgnoreAttribute>() is not null)
                continue;
            var attr = prop.GetCustomAttribute<ProtoFieldAttribute>();
            if (attr?.FieldNumber is > 0)
                reservedNumbers.Add(attr.FieldNumber);
        }

        // Pass 2: build descriptors, auto-assigning numbers that skip reserved ones
        var descriptors = new List<FieldDescriptor>();
        int autoFieldNumber = 0;

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<ProtoIgnoreAttribute>() is not null)
                continue;

            var fieldAttr = prop.GetCustomAttribute<ProtoFieldAttribute>();
            var mapAttr = prop.GetCustomAttribute<ProtoMapAttribute>();
            var oneOfAttr = prop.GetCustomAttribute<ProtoOneOfAttribute>();

            if (explicitFields && fieldAttr is null && mapAttr is null)
                continue;

            int fieldNumber;
            if (fieldAttr?.FieldNumber is > 0)
            {
                fieldNumber = fieldAttr.FieldNumber;
            }
            else
            {
                do { autoFieldNumber++; }
                while (reservedNumbers.Contains(autoFieldNumber));
                fieldNumber = autoFieldNumber;
            }

            string name = fieldAttr?.Name ?? prop.Name;
            bool writeDefault = fieldAttr?.WriteDefault ?? false;
            bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) is not null;

            // Resolve text encoding: field-level overrides contract-level, both fall back to null (UTF-8)
            ProtoEncoding? encoding = null;
            var encodingName = fieldAttr?.Encoding ?? contract?.DefaultEncoding;
            if (encodingName is not null)
                encoding = ProtoEncoding.FromName(encodingName);

            // Check for dictionary / map type
            bool isMap = mapAttr is not null || IsDictionaryType(prop.PropertyType, out _, out _);
            if (isMap && IsDictionaryType(prop.PropertyType, out var keyType, out var valueType))
            {
                var keyWire = InferWireType(keyType!);
                var valueWire = InferWireType(valueType!);

                // If value type is a nested class without [ProtoContract] and implicit mode is on,
                // register it implicitly
                if (implicitFields && valueType is not null && !IsScalarType(valueType) && !IsContractType(valueType))
                    ImplicitlyRegistered.TryAdd(valueType, true);

                descriptors.Add(new FieldDescriptor
                {
                    FieldNumber = fieldNumber,
                    Name = name,
                    WireType = WireType.LengthDelimited,
                    Property = prop,
                    WriteDefault = writeDefault,
                    IsCollection = false,
                    IsNullable = isNullable,
                    IsMap = true,
                    MapKeyType = keyType,
                    MapValueType = valueType,
                    MapKeyWireType = keyWire,
                    MapValueWireType = valueWire,
                    OneOfGroup = oneOfAttr?.GroupName,
                    IsPacked = fieldAttr?.IsPacked,
                    IsDeprecated = fieldAttr?.IsDeprecated ?? false,
                    IsRequired = fieldAttr?.IsRequired ?? false,
                    Encoding = encoding,
                });
                continue;
            }

            bool isCollection = IsCollectionType(prop.PropertyType, out var elementType);

            WireType wireType;
            WireType elementWireType = default;
            bool isImplicit = false;

            if (isCollection && elementType is not null)
            {
                // If element is a nested class without [ProtoContract] and implicit mode is on
                if (implicitFields && !IsScalarType(elementType) && !IsContractType(elementType))
                {
                    ImplicitlyRegistered.TryAdd(elementType, true);
                    isImplicit = true;
                }

                elementWireType = InferWireType(elementType);
                wireType = fieldAttr?.WireType ?? WireType.LengthDelimited;
            }
            else
            {
                var propType = prop.PropertyType;
                var underlying = Nullable.GetUnderlyingType(propType) ?? propType;

                // Deep nesting: if the type is a class (not scalar) and implicit mode is on,
                // resolve it implicitly so it can be serialized even without [ProtoContract]
                if (implicitFields && !IsScalarType(underlying) && !IsContractType(underlying)
                    && underlying.IsClass && underlying != typeof(string) && underlying != typeof(byte[]))
                {
                    ImplicitlyRegistered.TryAdd(underlying, true);
                    isImplicit = true;
                }

                wireType = fieldAttr?.WireType ?? InferWireType(propType);
            }

            descriptors.Add(new FieldDescriptor
            {
                FieldNumber = fieldNumber,
                Name = name,
                WireType = wireType,
                Property = prop,
                WriteDefault = writeDefault,
                IsCollection = isCollection,
                ElementType = elementType,
                ElementWireType = elementWireType,
                IsNullable = isNullable,
                OneOfGroup = oneOfAttr?.GroupName,
                IsPacked = fieldAttr?.IsPacked,
                IsDeprecated = fieldAttr?.IsDeprecated ?? false,
                IsRequired = fieldAttr?.IsRequired ?? false,
                IsImplicit = isImplicit,
                Encoding = encoding
            });
        }

        return descriptors.OrderBy(d => d.FieldNumber).ToArray();
    }

    /// <summary>
    /// Gets properties for a type, optionally including base class properties.
    /// </summary>
    private static PropertyInfo[] GetProperties(Type type, bool includeBase)
    {
        if (!includeBase)
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Walk the inheritance chain, collecting properties from base → derived
        // so that base class fields get lower auto-assigned numbers.
        var chain = new List<Type>();
        var current = type;
        while (current is not null && current != typeof(object))
        {
            chain.Add(current);
            current = current.BaseType;
        }
        chain.Reverse(); // base first

        var seen = new HashSet<string>();
        var result = new List<PropertyInfo>();
        foreach (var t in chain)
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var p in props)
            {
                if (seen.Add(p.Name))
                    result.Add(p);
            }
        }

        return result.ToArray();
    }

    #region Type detection helpers

    internal static bool IsCollectionType(Type type, out Type? elementType)
    {
        elementType = null;

        // Exclude string, byte[], and dictionaries
        if (type == typeof(string) || type == typeof(byte[]))
            return false;

        if (IsDictionaryType(type, out _, out _))
            return false;

        // T[]
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        // IEnumerable<T>, List<T>, IList<T>, ICollection<T>, etc.
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>)
                || genDef == typeof(ICollection<>) || genDef == typeof(IEnumerable<>)
                || genDef == typeof(IReadOnlyList<>) || genDef == typeof(IReadOnlyCollection<>)
                || genDef == typeof(HashSet<>) || genDef == typeof(ISet<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        // Check interfaces for types that implement IEnumerable<T> but aren't generic themselves
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = iface.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    internal static bool IsDictionaryType(Type type, out Type? keyType, out Type? valueType)
    {
        keyType = null;
        valueType = null;

        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(Dictionary<,>) || genDef == typeof(IDictionary<,>)
                || genDef == typeof(IReadOnlyDictionary<,>))
            {
                var args = type.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }
        }

        // Check interfaces
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = iface.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true for primitive/scalar types that don't need nested-message encoding.
    /// </summary>
    internal static bool IsScalarType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum
            || underlying == typeof(string) || underlying == typeof(byte[])
            || underlying == typeof(decimal) || underlying == typeof(Guid)
            || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan) || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly)
            || underlying == typeof(Int128) || underlying == typeof(UInt128)
            || underlying == typeof(nint) || underlying == typeof(nuint)
            || underlying == typeof(Half) || underlying == typeof(System.Numerics.BigInteger)
            || underlying == typeof(System.Numerics.Complex) || underlying == typeof(Version)
            || underlying == typeof(Uri);
    }

    internal static WireType InferWireType(Type clrType)
    {
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlying == typeof(double) || underlying == typeof(long) || underlying == typeof(ulong)
            || underlying == typeof(DateTime) || underlying == typeof(TimeSpan))
            return WireType.Fixed64;

        if (underlying == typeof(float))
            return WireType.Fixed32;

        if (underlying == typeof(int) || underlying == typeof(uint)
            || underlying == typeof(short) || underlying == typeof(ushort)
            || underlying == typeof(byte) || underlying == typeof(sbyte)
            || underlying == typeof(bool) || underlying.IsEnum
            || underlying == typeof(nint) || underlying == typeof(nuint))
            return WireType.Varint;

        // strings, byte[], Guid, decimal, Int128, BigInteger, etc. -> length-delimited
        return WireType.LengthDelimited;
    }

    /// <summary>
    /// Returns true when a wire type supports packed encoding inside a repeated field.
    /// </summary>
    internal static bool IsPackable(WireType wireType)
    {
        return wireType is WireType.Varint or WireType.Fixed32 or WireType.Fixed64;
    }

    /// <summary>
    /// Determines whether a repeated field should use packed encoding, considering
    /// the explicit override and the element wire type.
    /// </summary>
    internal static bool ShouldPack(FieldDescriptor field)
    {
        if (field.IsPacked.HasValue)
            return field.IsPacked.Value && IsPackable(field.ElementWireType);

        return IsPackable(field.ElementWireType);
    }

    #endregion
}
