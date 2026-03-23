using System.Collections.Concurrent;
using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder;

/// <summary>
/// Global registry for auto-discovery and configuration of protobuf types.
/// Use <see cref="Configure"/> to set up options, then <see cref="Register{T}"/>
/// or <see cref="RegisterAssembly"/> to register types for serialization without
/// requiring <c>[ProtoContract]</c> attributes.
/// </summary>
public static class ProtoRegistry
{
    private static readonly ConcurrentDictionary<Type, TypeRegistration> Registrations = new();
    private static volatile ProtoRegistrationOptions _options = new();

    /// <summary>
    /// Configures the global registration options.
    /// </summary>
    public static void Configure(Action<ProtoRegistrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ProtoRegistrationOptions();
        configure(options);
        _options = options;
    }

    /// <summary>
    /// Gets the current registration options.
    /// </summary>
    public static ProtoRegistrationOptions Options => _options;

    /// <summary>
    /// Registers a type for auto-discovery serialization without requiring <c>[ProtoContract]</c>.
    /// Optionally override the field numbering strategy for this type.
    /// </summary>
    public static void Register<T>(FieldNumbering? fieldNumbering = null) where T : class
    {
        Register(typeof(T), fieldNumbering);
    }

    /// <summary>
    /// Registers a type for auto-discovery serialization.
    /// </summary>
    public static void Register(Type type, FieldNumbering? fieldNumbering = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        Registrations[type] = new TypeRegistration(type, fieldNumbering);
    }

    /// <summary>
    /// Scans an assembly and registers all public classes that have public properties
    /// with getters and setters. Types already marked with <c>[ProtoContract]</c> are skipped
    /// (they're already handled by the standard resolver).
    /// </summary>
    public static int RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        int count = 0;
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsInterface)
                continue;

            // Skip types that already have [ProtoContract]
            if (type.GetCustomAttribute<ProtoContractAttribute>() is not null)
                continue;

            // Only register types with at least one public read/write property
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (props.Length == 0 || !Array.Exists(props, p => p.CanRead && p.CanWrite))
                continue;

            Registrations.TryAdd(type, new TypeRegistration(type, null));
            count++;
        }

        return count;
    }

    /// <summary>
    /// Checks whether a type has been explicitly registered.
    /// </summary>
    public static bool IsRegistered(Type type) => Registrations.ContainsKey(type);

    /// <summary>
    /// Checks whether a type is resolvable — either registered, has <c>[ProtoContract]</c>,
    /// or auto-discovery is enabled.
    /// </summary>
    public static bool IsResolvable(Type type)
    {
        if (Registrations.ContainsKey(type))
            return true;

        if (type.GetCustomAttribute<ProtoContractAttribute>() is not null)
            return true;

        return _options.AutoDiscover;
    }

    /// <summary>
    /// Gets the effective field numbering strategy for a type, considering
    /// (in priority order): per-type registration override → <c>[ProtoContract]</c>
    /// attribute → global default.
    /// </summary>
    internal static FieldNumbering GetFieldNumbering(Type type)
    {
        if (Registrations.TryGetValue(type, out var reg) && reg.FieldNumbering.HasValue)
            return reg.FieldNumbering.Value;

        var contract = type.GetCustomAttribute<ProtoContractAttribute>();
        if (contract is { HasFieldNumbering: true })
            return contract.FieldNumbering;

        return _options.DefaultFieldNumbering;
    }

    /// <summary>
    /// Gets all registered types.
    /// </summary>
    public static IReadOnlyCollection<Type> RegisteredTypes =>
        Registrations.Keys.ToArray();

    /// <summary>
    /// Clears all registrations and resets options to defaults.
    /// Primarily for testing.
    /// </summary>
    public static void Reset()
    {
        Registrations.Clear();
        _options = new ProtoRegistrationOptions();
    }

    internal sealed record TypeRegistration(Type Type, FieldNumbering? FieldNumbering);
}
