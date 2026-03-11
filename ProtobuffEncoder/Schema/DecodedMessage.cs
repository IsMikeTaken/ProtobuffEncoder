namespace ProtobuffEncoder.Schema;

/// <summary>
/// The result of decoding a protobuf binary message using only a .proto schema (no C# type).
/// Fields are stored by name in a dictionary. Nested messages are also <see cref="DecodedMessage"/> instances.
/// Repeated fields are <see cref="List{Object}"/>.
/// Map fields are <see cref="Dictionary{Object, Object}"/>.
/// </summary>
public sealed class DecodedMessage
{
    /// <summary>
    /// The proto message type name (e.g. "WeatherRequest").
    /// </summary>
    public string MessageName { get; }

    /// <summary>
    /// Field values keyed by the field name from the .proto schema.
    /// </summary>
    public Dictionary<string, object?> Fields { get; } = new();

    public DecodedMessage(string messageName)
    {
        MessageName = messageName;
    }

    /// <summary>
    /// Gets a field value by name, or null if not present.
    /// </summary>
    public object? this[string fieldName] =>
        Fields.TryGetValue(fieldName, out var value) ? value : null;

    /// <summary>
    /// Gets a field value cast to the specified type.
    /// </summary>
    public T? Get<T>(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Gets a repeated field as a typed list.
    /// </summary>
    public List<T> GetRepeated<T>(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value) && value is List<object?> list)
            return list.Where(x => x is T).Cast<T>().ToList();
        return [];
    }

    /// <summary>
    /// Gets a nested decoded message.
    /// </summary>
    public DecodedMessage? GetMessage(string fieldName)
    {
        return Get<DecodedMessage>(fieldName);
    }

    /// <summary>
    /// Gets a repeated nested message field.
    /// </summary>
    public List<DecodedMessage> GetMessages(string fieldName)
    {
        return GetRepeated<DecodedMessage>(fieldName);
    }

    /// <summary>
    /// Gets a map field as a dictionary with string keys.
    /// </summary>
    public Dictionary<string, T?> GetMap<T>(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value) && value is Dictionary<object, object?> dict)
        {
            var result = new Dictionary<string, T?>();
            foreach (var kv in dict)
            {
                string key = kv.Key.ToString() ?? "";
                result[key] = kv.Value is T typed ? typed : default;
            }
            return result;
        }
        return [];
    }

    /// <summary>
    /// Gets a map field with its raw key/value types.
    /// </summary>
    public Dictionary<object, object?> GetRawMap(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value) && value is Dictionary<object, object?> dict)
            return dict;
        return [];
    }

    /// <summary>
    /// Gets a map field where values are decoded messages.
    /// </summary>
    public Dictionary<string, DecodedMessage> GetMessageMap(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var value) && value is Dictionary<object, object?> dict)
        {
            var result = new Dictionary<string, DecodedMessage>();
            foreach (var kv in dict)
            {
                string key = kv.Key.ToString() ?? "";
                if (kv.Value is DecodedMessage msg)
                    result[key] = msg;
            }
            return result;
        }
        return [];
    }

    public override string ToString()
    {
        var fields = string.Join(", ", Fields.Select(kv =>
        {
            var val = kv.Value switch
            {
                DecodedMessage dm => $"{{{dm}}}",
                List<object?> list => $"[{list.Count} items]",
                Dictionary<object, object?> map => $"{{{map.Count} entries}}",
                _ => kv.Value?.ToString() ?? "null"
            };
            return $"{kv.Key}={val}";
        }));
        return $"{MessageName}{{{fields}}}";
    }
}
