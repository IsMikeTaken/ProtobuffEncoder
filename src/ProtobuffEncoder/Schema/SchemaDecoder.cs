using System.Text;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Decodes protobuf binary messages using only .proto schema definitions — no C# types required.
/// Returns <see cref="DecodedMessage"/> instances with fields accessible by name.
/// Supports map fields, oneof groups, nested messages, enums, and packed repeated fields.
/// </summary>
public sealed class SchemaDecoder
{
    private readonly Dictionary<string, ProtoMessageDef> _messages = new();
    private readonly Dictionary<string, ProtoEnumDef> _enums = new();

    /// <summary>
    /// Creates a decoder from one or more parsed .proto files.
    /// </summary>
    public SchemaDecoder(params ProtoFile[] protoFiles)
    {
        foreach (var file in protoFiles)
            Register(file);
    }

    /// <summary>
    /// Creates a decoder by loading all .proto files from a directory.
    /// </summary>
    public static SchemaDecoder FromDirectory(string directory)
    {
        var files = ProtoSchemaParser.ParseDirectory(directory);
        return new SchemaDecoder(files.ToArray());
    }

    /// <summary>
    /// Creates a decoder from a single .proto file path.
    /// </summary>
    public static SchemaDecoder FromFile(string filePath)
    {
        var file = ProtoSchemaParser.ParseFile(filePath);
        return new SchemaDecoder(file);
    }

    /// <summary>
    /// Creates a decoder from raw .proto content.
    /// </summary>
    public static SchemaDecoder FromProtoContent(string protoContent)
    {
        var file = ProtoSchemaParser.Parse(protoContent);
        return new SchemaDecoder(file);
    }

    /// <summary>
    /// Registers additional .proto definitions.
    /// </summary>
    public void Register(ProtoFile file)
    {
        RegisterMessages(file.Messages);
        RegisterEnums(file.Enums);
    }

    /// <summary>
    /// Decodes a protobuf binary payload as the specified message type.
    /// The messageName must match a message definition in the loaded schemas.
    /// </summary>
    public DecodedMessage Decode(string messageName, ReadOnlySpan<byte> data)
    {
        if (!_messages.TryGetValue(messageName, out var msgDef))
            throw new InvalidOperationException($"Unknown message type '{messageName}'. Load the .proto schema first.");

        return DecodeMessage(msgDef, data);
    }

    /// <summary>
    /// Lists all registered message names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredMessages => _messages.Keys;

    /// <summary>
    /// Lists all registered enum names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredEnums => _enums.Keys;

    #region Registration

    private void RegisterMessages(List<ProtoMessageDef> messages)
    {
        foreach (var msg in messages)
        {
            _messages[msg.Name] = msg;
            RegisterMessages(msg.NestedMessages);
            RegisterEnums(msg.NestedEnums);

            // Register oneof fields into the main field list so they are found by field number
            foreach (var oneOf in msg.OneOfs)
            {
                foreach (var field in oneOf.Fields)
                {
                    if (!msg.Fields.Any(f => f.FieldNumber == field.FieldNumber))
                        msg.Fields.Add(field);
                }
            }
        }
    }

    private void RegisterEnums(List<ProtoEnumDef> enums)
    {
        foreach (var e in enums)
            _enums[e.Name] = e;
    }

    #endregion

    #region Decode core

    private DecodedMessage DecodeMessage(ProtoMessageDef msgDef, ReadOnlySpan<byte> data)
    {
        var result = new DecodedMessage(msgDef.Name);
        var fieldsByNumber = msgDef.Fields.ToDictionary(f => f.FieldNumber);

        // Pre-create lists for repeated fields
        var repeatedLists = new Dictionary<int, List<object?>>();
        foreach (var f in msgDef.Fields)
        {
            if (f.IsRepeated)
                repeatedLists[f.FieldNumber] = [];
        }

        // Pre-create dictionaries for map fields
        var mapDicts = new Dictionary<int, Dictionary<object, object?>>();
        foreach (var f in msgDef.Fields)
        {
            if (f.IsMap)
                mapDicts[f.FieldNumber] = [];
        }

        int offset = 0;
        while (offset < data.Length)
        {
            int tag = (int)ReadVarint(data, ref offset);
            int fieldNumber = tag >> 3;
            var wireType = (WireType)(tag & 0x07);

            if (!fieldsByNumber.TryGetValue(fieldNumber, out var fieldDef))
            {
                SkipField(data, wireType, ref offset);
                continue;
            }

            if (fieldDef.IsMap)
            {
                DecodeMapEntry(data, fieldDef, ref offset, mapDicts[fieldNumber]);
            }
            else if (fieldDef.IsRepeated)
            {
                DecodeRepeatedField(data, fieldDef, wireType, ref offset, repeatedLists[fieldNumber]);
            }
            else
            {
                var value = DecodeFieldValue(data, fieldDef, wireType, ref offset);
                result.Fields[fieldDef.Name] = value;
            }
        }

        // Assign repeated fields
        foreach (var (fieldNum, list) in repeatedLists)
        {
            var fieldDef = fieldsByNumber[fieldNum];
            result.Fields[fieldDef.Name] = list;
        }

        // Assign map fields
        foreach (var (fieldNum, dict) in mapDicts)
        {
            var fieldDef = fieldsByNumber[fieldNum];
            result.Fields[fieldDef.Name] = dict;
        }

        return result;
    }

    private void DecodeMapEntry(ReadOnlySpan<byte> data, ProtoFieldDef fieldDef, ref int offset, Dictionary<object, object?> target)
    {
        // Map entry is a length-delimited message with key=1, value=2
        int length = (int)ReadVarint(data, ref offset);
        int end = offset + length;

        object? key = null;
        object? value = null;

        var keyWire = GetWireTypeForProtoType(fieldDef.MapKeyType);
        var valueWire = GetWireTypeForProtoType(fieldDef.MapValueType);

        while (offset < end)
        {
            int entryTag = (int)ReadVarint(data, ref offset);
            int entryField = entryTag >> 3;
            var entryWire = (WireType)(entryTag & 0x07);

            if (entryField == 1)
            {
                key = ReadScalarForProtoType(data, fieldDef.MapKeyType, entryWire, ref offset);
            }
            else if (entryField == 2)
            {
                // Check if value type is a known message
                if (_messages.ContainsKey(fieldDef.MapValueType) && entryWire == WireType.LengthDelimited)
                {
                    int valLength = (int)ReadVarint(data, ref offset);
                    var payload = data.Slice(offset, valLength);
                    offset += valLength;
                    value = DecodeMessage(_messages[fieldDef.MapValueType], payload);
                }
                else if (_enums.TryGetValue(fieldDef.MapValueType, out var enumDef) && entryWire == WireType.Varint)
                {
                    var raw = (int)(long)ReadVarint(data, ref offset);
                    var enumVal = enumDef.Values.Find(v => v.Number == raw);
                    value = enumVal?.Name ?? raw.ToString();
                }
                else
                {
                    value = ReadScalarForProtoType(data, fieldDef.MapValueType, entryWire, ref offset);
                }
            }
            else
            {
                SkipField(data, entryWire, ref offset);
            }
        }

        if (key is not null)
            target[key] = value;
    }

    private void DecodeRepeatedField(ReadOnlySpan<byte> data, ProtoFieldDef fieldDef, WireType wireType, ref int offset, List<object?> target)
    {
        var expectedWireType = GetWireTypeForProtoType(fieldDef.TypeName);

        // Packed encoding: length-delimited blob of packed scalars
        if (wireType == WireType.LengthDelimited && IsPackableProtoType(fieldDef.TypeName))
        {
            int length = (int)ReadVarint(data, ref offset);
            int end = offset + length;
            while (offset < end)
            {
                target.Add(ReadScalarForProtoType(data, fieldDef.TypeName, expectedWireType, ref offset));
            }
        }
        else
        {
            // Non-packed: single element
            target.Add(DecodeFieldValue(data, fieldDef, wireType, ref offset));
        }
    }

    private object? DecodeFieldValue(ReadOnlySpan<byte> data, ProtoFieldDef fieldDef, WireType wireType, ref int offset)
    {
        // Check if it's a known message type
        if (_messages.ContainsKey(fieldDef.TypeName) && wireType == WireType.LengthDelimited)
        {
            int length = (int)ReadVarint(data, ref offset);
            var payload = data.Slice(offset, length);
            offset += length;
            return DecodeMessage(_messages[fieldDef.TypeName], payload);
        }

        // Enum — decode as varint, resolve name
        if (_enums.TryGetValue(fieldDef.TypeName, out var enumDef) && wireType == WireType.Varint)
        {
            var raw = (int)(long)ReadVarint(data, ref offset);
            var enumVal = enumDef.Values.Find(v => v.Number == raw);
            return enumVal?.Name ?? raw.ToString();
        }

        return ReadScalarForProtoType(data, fieldDef.TypeName, wireType, ref offset);
    }

    private static object? ReadScalarForProtoType(ReadOnlySpan<byte> data, string protoType, WireType wireType, ref int offset)
    {
        return wireType switch
        {
            WireType.Varint => ReadVarintAsType(data, protoType, ref offset),
            WireType.Fixed32 => ReadFixed32AsType(data, protoType, ref offset),
            WireType.Fixed64 => ReadFixed64AsType(data, protoType, ref offset),
            WireType.LengthDelimited => ReadLengthDelimitedAsType(data, protoType, ref offset),
            _ => throw new NotSupportedException($"Wire type {wireType} not supported for schema decode.")
        };
    }

    private static object ReadVarintAsType(ReadOnlySpan<byte> data, string protoType, ref int offset)
    {
        ulong raw = ReadVarint(data, ref offset);
        return protoType switch
        {
            "bool" => raw != 0,
            "int32" => (long)(int)(long)raw,
            "uint32" => (long)(uint)raw,
            "int64" => (long)raw,
            "uint64" => (long)raw,
            _ => (long)raw
        };
    }

    private static object ReadFixed32AsType(ReadOnlySpan<byte> data, string protoType, ref int offset)
    {
        var bytes = data.Slice(offset, 4);
        offset += 4;
        return protoType switch
        {
            "float" => (double)BitConverter.ToSingle(bytes),
            "fixed32" => (long)BitConverter.ToUInt32(bytes),
            "sfixed32" => (long)BitConverter.ToInt32(bytes),
            _ => (double)BitConverter.ToSingle(bytes)
        };
    }

    private static object ReadFixed64AsType(ReadOnlySpan<byte> data, string protoType, ref int offset)
    {
        var bytes = data.Slice(offset, 8);
        offset += 8;
        return protoType switch
        {
            "double" => BitConverter.ToDouble(bytes),
            "fixed64" => (long)BitConverter.ToInt64(bytes),
            "sfixed64" => (long)BitConverter.ToInt64(bytes),
            "int64" => BitConverter.ToInt64(bytes),
            "uint64" => (long)BitConverter.ToUInt64(bytes),
            _ => BitConverter.ToDouble(bytes)
        };
    }

    private static object? ReadLengthDelimitedAsType(ReadOnlySpan<byte> data, string protoType, ref int offset)
    {
        int length = (int)ReadVarint(data, ref offset);
        var payload = data.Slice(offset, length);
        offset += length;

        return protoType switch
        {
            "string" => Encoding.UTF8.GetString(payload),
            "bytes" => payload.ToArray(),
            _ => Encoding.UTF8.GetString(payload) // best-effort fallback
        };
    }

    #endregion

    #region Wire format helpers

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        throw new InvalidOperationException("Unexpected end of data reading varint.");
    }

    private static void SkipField(ReadOnlySpan<byte> data, WireType wireType, ref int offset)
    {
        switch (wireType)
        {
            case WireType.Varint:
                ReadVarint(data, ref offset);
                break;
            case WireType.Fixed64:
                offset += 8;
                break;
            case WireType.Fixed32:
                offset += 4;
                break;
            case WireType.LengthDelimited:
                int length = (int)ReadVarint(data, ref offset);
                offset += length;
                break;
            default:
                throw new NotSupportedException($"Cannot skip wire type {wireType}.");
        }
    }

    private static WireType GetWireTypeForProtoType(string protoType) => protoType switch
    {
        "bool" or "int32" or "uint32" or "int64" or "uint64" or "sint32" or "sint64" => WireType.Varint,
        "float" or "fixed32" or "sfixed32" => WireType.Fixed32,
        "double" or "fixed64" or "sfixed64" => WireType.Fixed64,
        _ => WireType.LengthDelimited
    };

    private static bool IsPackableProtoType(string protoType) => protoType switch
    {
        "bool" or "int32" or "uint32" or "int64" or "uint64" or "sint32" or "sint64"
            or "float" or "fixed32" or "sfixed32"
            or "double" or "fixed64" or "sfixed64" => true,
        _ => false
    };

    #endregion
}
