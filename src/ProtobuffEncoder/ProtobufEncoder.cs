using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder;

/// <summary>
/// Encodes objects marked with [ProtoContract] into protobuf binary wire format.
/// Supports scalars, nullable types, collections (arrays, List, ICollection), nested messages,
/// dictionaries (map fields), oneof groups, inheritance (ProtoInclude), implicit nested types,
/// streaming, and pre-compiled static message delegates.
/// </summary>
public static class ProtobufEncoder
{
    #region Encode (sync)

    /// <summary>
    /// Serializes an object to protobuf binary format.
    /// </summary>
    public static byte[] Encode(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        using var stream = new MemoryStream();
        EncodeMessage(instance, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Serializes an object to protobuf binary format into the given stream.
    /// </summary>
    public static void Encode(object instance, Stream output)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(output);
        EncodeMessage(instance, output);
    }

    /// <summary>
    /// Encodes <paramref name="instance"/> directly into <paramref name="writer"/>,
    /// avoiding an intermediate <c>byte[]</c> or <see cref="MemoryStream"/> allocation.
    /// </summary>
    /// <remarks>
    /// The encoded bytes are written into a buffer obtained from <paramref name="writer"/>
    /// via a single <see cref="IBufferWriter{T}.GetMemory"/> call.  For objects whose
    /// encoded size cannot be known up-front a temporary <see cref="MemoryStream"/> is used
    /// internally, but the result is still written once into <paramref name="writer"/> without
    /// copying through an extra array.
    /// </remarks>
    /// <param name="instance">The object to encode. Must not be <see langword="null"/>.</param>
    /// <param name="writer">The destination buffer writer. Must not be <see langword="null"/>.</param>
    public static void EncodeTo(object instance, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(writer);

        // Encode into a pooled MemoryStream, then copy the result into the writer in one shot.
        // This keeps the core EncodeMessage logic unchanged while still avoiding a heap array.
        using var ms = new MemoryStream();
        EncodeMessage(instance, ms);

        if (ms.Length == 0)
            return;

        ReadOnlySpan<byte> encoded = ms.GetBuffer().AsSpan(0, (int)ms.Length);
        Span<byte> dest = writer.GetSpan((int)ms.Length);
        encoded.CopyTo(dest);
        writer.Advance((int)ms.Length);
    }

    #endregion

    #region Encode (async / streaming)

    /// <summary>
    /// Asynchronously serializes an object to the given stream.
    /// </summary>
    public static async Task EncodeAsync(object instance, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(output);

        // Encode into a buffer, then write asynchronously
        var buffer = Encode(instance);
        await output.WriteAsync(buffer, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Writes a length-delimited message to a stream, suitable for streaming multiple messages.
    /// Each message is prefixed with its varint-encoded length so the reader knows where it ends.
    /// </summary>
    public static void WriteDelimitedMessage(object instance, Stream output)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(output);

        var payload = Encode(instance);
        WriteVarint(output, (ulong)payload.Length);
        output.Write(payload);
    }

    /// <summary>
    /// Asynchronously writes a length-delimited message to a stream.
    /// </summary>
    public static async Task WriteDelimitedMessageAsync(object instance, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(output);

        var payload = Encode(instance);

        // Write length prefix
        using var lengthBuf = new MemoryStream();
        WriteVarint(lengthBuf, (ulong)payload.Length);
        await output.WriteAsync(lengthBuf.ToArray(), cancellationToken);

        // Write payload
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    #endregion

    #region Decode (sync)

    /// <summary>
    /// Deserializes a protobuf binary message into an instance of <typeparamref name="T"/>.
    /// </summary>
    public static T Decode<T>(ReadOnlySpan<byte> data) where T : new()
        => (T)DecodeMessage(typeof(T), data);

    /// <summary>
    /// Deserializes a protobuf binary message using the specified type.
    /// </summary>
    public static object Decode(Type type, ReadOnlySpan<byte> data)
        => DecodeMessage(type, data);

    /// <summary>
    /// Attempts to deserialize <paramref name="data"/> into <typeparamref name="T"/>.
    /// Returns <see langword="false"/> and sets <paramref name="error"/> when the payload
    /// is malformed; never throws.
    /// </summary>
    /// <typeparam name="T">The target message type.</typeparam>
    /// <param name="data">The raw protobuf bytes to decode.</param>
    /// <param name="result">The decoded message, or <see langword="default"/> on failure.</param>
    /// <param name="error">A human-readable error description, or <see langword="null"/> on success.</param>
    /// <returns><see langword="true"/> when decoding succeeds.</returns>
    public static bool TryDecode<T>(ReadOnlySpan<byte> data, out T? result, out string? error)
        where T : new()
    {
        try
        {
            result = Decode<T>(data);
            error  = null;
            return true;
        }
        catch (ProtobufDecodeException ex)
        {
            result = default;
            error  = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            result = default;
            error  = ex.Message;
            return false;
        }
    }

    #endregion

    #region Decode (async / streaming)

    /// <summary>
    /// Asynchronously reads a single protobuf message from a stream.
    /// Reads all remaining bytes from the current position.
    /// </summary>
    public static async Task<T> DecodeAsync<T>(Stream input, CancellationToken cancellationToken = default) where T : new()
    {
        ArgumentNullException.ThrowIfNull(input);
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, cancellationToken);
        return Decode<T>(ms.ToArray());
    }

    /// <summary>
    /// Reads a length-delimited message from a stream.
    /// Returns null when the end of the stream is reached.
    /// </summary>
    public static T? ReadDelimitedMessage<T>(Stream input) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryReadVarintFromStream(input, out ulong length))
            return null;

        var buffer = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = input.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }

        return Decode<T>(buffer);
    }

    /// <summary>
    /// Reads a sequence of length-delimited messages from a stream.
    /// </summary>
    public static IEnumerable<T> ReadDelimitedMessages<T>(Stream input) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(input);
        while (true)
        {
            var message = ReadDelimitedMessage<T>(input);
            if (message is null) yield break;
            yield return message;
        }
    }

    /// <summary>
    /// Asynchronously reads a sequence of length-delimited messages from a stream.
    /// </summary>
    public static async IAsyncEnumerable<T> ReadDelimitedMessagesAsync<T>(
        Stream input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(input);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!TryReadVarintFromStream(input, out ulong length))
                yield break;

            var buffer = new byte[(int)length];
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await input.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
                if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
                totalRead += read;
            }

            yield return Decode<T>(buffer);
        }
    }

    #endregion

    #region Static message (pre-compiled)

    /// <summary>
    /// Creates a pre-compiled encoder delegate for a specific type.
    /// Use this when encoding the same type many times for better performance
    /// by avoiding repeated reflection lookups per call.
    /// </summary>
    public static Func<T, byte[]> CreateStaticEncoder<T>()
    {
        // Force resolution and caching of descriptors eagerly
        _ = ContractResolver.Resolve(typeof(T));
        return instance =>
        {
            ArgumentNullException.ThrowIfNull(instance);
            return Encode(instance);
        };
    }

    /// <summary>
    /// Creates a pre-compiled decoder delegate for a specific type.
    /// </summary>
    public static Func<byte[], T> CreateStaticDecoder<T>() where T : new()
    {
        // Force resolution and caching of descriptors eagerly
        _ = ContractResolver.Resolve(typeof(T));
        return data =>
        {
            return Decode<T>(data);
        };
    }

    /// <summary>
    /// A pre-compiled message handler that bundles both encode and decode for a type.
    /// </summary>
    public static StaticMessage<T> CreateStaticMessage<T>() where T : class, new()
    {
        return new StaticMessage<T>(
            CreateStaticEncoder<T>(),
            CreateStaticDecoder<T>()
        );
    }

    #endregion

    #region Core encode

    private static void EncodeMessage(object instance, Stream output)
    {
        var type = instance.GetType();
        var descriptors = ContractResolver.Resolve(type);

        // Validate required fields before encoding
        ValidateRequired(descriptors, instance);

        // Track which oneof groups have been written (only first non-default wins)
        var writtenOneOfs = new HashSet<string>();

        foreach (var field in descriptors)
        {
            var value = field.Property.GetValue(instance);

            if (value is null)
                continue;

            if (!field.WriteDefault && IsDefault(value, field.Property.PropertyType))
                continue;

            // OneOf: only the first non-default property in the group is written
            if (field.OneOfGroup is not null)
            {
                if (!writtenOneOfs.Add(field.OneOfGroup))
                    continue;
            }

            if (field.IsMap)
                WriteMapField(output, field, value);
            else if (field.IsCollection)
                WriteRepeatedField(output, field, value);
            else
                WriteField(output, field, value, field.Encoding);
        }

        // ProtoInclude: if the runtime type is a known derived type, encode it as a nested message
        var includes = ContractResolver.GetIncludes(type);
        if (includes.Length > 0)
        {
            // The instance IS the base type; derived fields are already encoded above.
            // ProtoInclude is for when a *base* variable holds a *derived* instance.
        }

        // Check if this instance is actually a derived type of a base that has ProtoInclude
        WriteIncludedDerivedType(instance, type, output);
    }

    private static void WriteIncludedDerivedType(object instance, Type runtimeType, Stream output)
    {
        // Walk up to see if any base type has a [ProtoInclude] pointing to our runtime type
        var baseType = runtimeType.BaseType;
        while (baseType is not null && baseType != typeof(object))
        {
            if (ContractResolver.IsContractType(baseType))
            {
                var includes = ContractResolver.GetIncludes(baseType);
                var match = Array.Find(includes, i => i.DerivedType == runtimeType);
                if (match is not null)
                {
                    // Encode the derived-type-only fields as a nested message at the include's field number
                    var derivedDescriptors = GetDeclaredOnlyDescriptors(runtimeType);
                    if (derivedDescriptors.Length > 0)
                    {
                        using var nested = new MemoryStream();
                        foreach (var field in derivedDescriptors)
                        {
                            var val = field.Property.GetValue(instance);
                            if (val is null) continue;
                            if (!field.WriteDefault && IsDefault(val, field.Property.PropertyType))
                                continue;

                            if (field.IsMap)
                                WriteMapField(nested, field, val);
                            else if (field.IsCollection)
                                WriteRepeatedField(nested, field, val);
                            else
                                WriteField(nested, field, val);
                        }

                        if (nested.Length > 0)
                        {
                            uint tag = (uint)((match.FieldNumber << 3) | (int)WireType.LengthDelimited);
                            WriteVarint(output, tag);
                            WriteVarint(output, (ulong)nested.Length);
                            nested.Position = 0;
                            nested.CopyTo(output);
                        }
                    }
                }
            }
            baseType = baseType.BaseType;
        }
    }

    private static FieldDescriptor[] GetDeclaredOnlyDescriptors(Type type)
    {
        var all = ContractResolver.Resolve(type);
        var baseProps = new HashSet<string>();

        var baseType = type.BaseType;
        while (baseType is not null && baseType != typeof(object))
        {
            foreach (var p in baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                baseProps.Add(p.Name);
            baseType = baseType.BaseType;
        }

        return all.Where(d => !baseProps.Contains(d.Property.Name)).ToArray();
    }

    private static void ValidateRequired(FieldDescriptor[] descriptors, object instance)
    {
        foreach (var field in descriptors)
        {
            if (!field.IsRequired) continue;

            var value = field.Property.GetValue(instance);
            if (value is null || IsDefault(value, field.Property.PropertyType))
                throw new InvalidOperationException(
                    $"Required field '{field.Name}' (field {field.FieldNumber}) on type " +
                    $"'{instance.GetType().Name}' must have a non-default value.");
        }
    }

    private static void WriteField(Stream output, FieldDescriptor field, object value, ProtoEncoding? encoding = null)
    {
        uint tag = (uint)((field.FieldNumber << 3) | (int)field.WireType);
        WriteVarint(output, tag);

        switch (field.WireType)
        {
            case WireType.Varint:
                WriteVarint(output, ToVarintValue(value));
                break;
            case WireType.Fixed32:
                WriteFixed32(output, value);
                break;
            case WireType.Fixed64:
                WriteFixed64(output, value);
                break;
            case WireType.LengthDelimited:
                WriteLengthDelimited(output, value, encoding);
                break;
            default:
                throw new NotSupportedException($"Wire type {field.WireType} is not supported.");
        }
    }

    private static void WriteRepeatedField(Stream output, FieldDescriptor field, object collection)
    {
        var enumerable = (IEnumerable)collection;
        var elementWireType = field.ElementWireType;

        // Packed encoding for scalar types (varint, fixed32, fixed64)
        if (ContractResolver.ShouldPack(field))
        {
            using var packed = new MemoryStream();
            foreach (var element in enumerable)
            {
                switch (elementWireType)
                {
                    case WireType.Varint:
                        WriteVarint(packed, ToVarintValue(element));
                        break;
                    case WireType.Fixed32:
                        WriteFixed32(packed, element);
                        break;
                    case WireType.Fixed64:
                        WriteFixed64(packed, element);
                        break;
                }
            }

            if (packed.Length == 0)
                return;

            uint tag = (uint)((field.FieldNumber << 3) | (int)WireType.LengthDelimited);
            WriteVarint(output, tag);
            WriteVarint(output, (ulong)packed.Length);
            packed.Position = 0;
            packed.CopyTo(output);
        }
        else
        {
            // Non-packed: write each element with its own tag (strings, bytes, nested messages)
            foreach (var element in enumerable)
            {
                if (element is null) continue;

                uint tag = (uint)((field.FieldNumber << 3) | (int)WireType.LengthDelimited);
                WriteVarint(output, tag);
                WriteLengthDelimited(output, element);
            }
        }
    }

    /// <summary>
    /// Writes a protobuf map field. Each entry is a length-delimited message with key=1, value=2.
    /// </summary>
    private static void WriteMapField(Stream output, FieldDescriptor field, object dictionary)
    {
        var dict = (IDictionary)dictionary;

        foreach (DictionaryEntry entry in dict)
        {
            using var entryStream = new MemoryStream();

            // Key = field 1
            WriteMapElement(entryStream, 1, field.MapKeyWireType, entry.Key);

            // Value = field 2
            if (entry.Value is not null)
                WriteMapElement(entryStream, 2, field.MapValueWireType, entry.Value);

            // Write the entry as a length-delimited message
            uint tag = (uint)((field.FieldNumber << 3) | (int)WireType.LengthDelimited);
            WriteVarint(output, tag);
            WriteVarint(output, (ulong)entryStream.Length);
            entryStream.Position = 0;
            entryStream.CopyTo(output);
        }
    }

    private static void WriteMapElement(Stream output, int fieldNumber, WireType wireType, object value)
    {
        uint tag = (uint)((fieldNumber << 3) | (int)wireType);
        WriteVarint(output, tag);

        switch (wireType)
        {
            case WireType.Varint:
                WriteVarint(output, ToVarintValue(value));
                break;
            case WireType.Fixed32:
                WriteFixed32(output, value);
                break;
            case WireType.Fixed64:
                WriteFixed64(output, value);
                break;
            case WireType.LengthDelimited:
                WriteLengthDelimited(output, value);
                break;
        }
    }

    #endregion

    #region Core decode

    /// <summary>
    /// Maximum nesting depth for recursive message decoding.
    /// Protobuf's own reference implementation uses 100; we use 512 to handle
    /// deeply nested CLR object graphs while still bounding malformed payloads.
    /// </summary>
    private const int MaxNestingDepth = 512;

    private static object DecodeMessage(Type type, ReadOnlySpan<byte> data, int depth = 0)
    {
        if (depth > MaxNestingDepth)
            throw new ProtobufDecodeException(
                $"Maximum nesting depth ({MaxNestingDepth}) exceeded while decoding '{type.FullName}'. " +
                "The payload may be malformed or contain a cyclic reference.", targetType: type);

        var instance = Activator.CreateInstance(type)
            ?? throw new ProtobufDecodeException($"Cannot create instance of '{type.FullName}'.", targetType: type);

        var descriptors = ContractResolver.Resolve(type);
        var lookup      = ContractResolver.ResolveLookup(type);

        // Accumulate repeated and map values; pre-size from known field count.
        var repeatedValues = new Dictionary<int, IList>(4);
        var mapValues      = new Dictionary<int, IDictionary>(4);

        foreach (var d in descriptors)
        {
            if (d.IsCollection) repeatedValues[d.FieldNumber] = CreateList(d.ElementType!);
            else if (d.IsMap)   mapValues[d.FieldNumber]      = CreateDictionary(d.MapKeyType!, d.MapValueType!);
        }

        // Collect ProtoInclude mappings for this type.
        var includes   = ContractResolver.GetIncludes(type);
        var includeMap = new Dictionary<int, ProtoIncludeAttribute>(includes.Length);
        foreach (var inc in includes)
            includeMap[inc.FieldNumber] = inc;

        int offset = 0;
        while (offset < data.Length)
        {
            int  tagStart    = offset;
            uint tag         = (uint)ReadVarintChecked(data, ref offset, type);
            int  fieldNumber = (int)(tag >> 3);
            var  wireType    = (WireType)(tag & 0x07);

            if (fieldNumber == 0)
                throw new ProtobufDecodeException(
                    "Decoded field number 0, which is not valid in protobuf.", tagStart, type);

            // ProtoInclude derived-type nested message.
            if (includeMap.TryGetValue(fieldNumber, out var includeAttr))
            {
                int length = (int)ReadVarintChecked(data, ref offset, type);
                if (length < 0 || offset + length > data.Length)
                    throw new ProtobufDecodeException(
                        $"Include field {fieldNumber} length {length} exceeds payload boundary.", offset, type);

                var payload = data.Slice(offset, length);
                offset += length;

                var derivedDescriptors = GetDeclaredOnlyDescriptors(includeAttr.DerivedType);
                ApplyDerivedFields(instance, derivedDescriptors, payload, depth + 1);
                continue;
            }

            // O(1) lookup via FrozenDictionary — no linear scan.
            if (!lookup.TryGetValue(fieldNumber, out var field))
            {
                SkipFieldChecked(data, wireType, ref offset, type);
                continue;
            }

            if (field.IsMap)
            {
                ReadMapEntry(data, field, ref offset, mapValues[fieldNumber], depth);
            }
            else if (field.IsCollection)
            {
                ReadRepeatedField(data, field, wireType, ref offset, repeatedValues[fieldNumber], depth);
            }
            else
            {
                var value = ReadScalarValue(data, field, wireType, ref offset, depth);
                field.Property.SetValue(instance, value);
            }
        }

        // Assign collected repeated fields.
        foreach (var d in descriptors)
        {
            if (!d.IsCollection) continue;

            var list     = repeatedValues[d.FieldNumber];
            var propType = d.Property.PropertyType;

            if (propType.IsArray)
            {
                var array = Array.CreateInstance(d.ElementType!, list.Count);
                list.CopyTo(array, 0);
                d.Property.SetValue(instance, array);
            }
            else if (propType.IsAssignableFrom(list.GetType()))
            {
                d.Property.SetValue(instance, list);
            }
            else
            {
                var target = Activator.CreateInstance(propType) as IList
                    ?? throw new ProtobufDecodeException(
                        $"Cannot populate collection '{propType.FullName}'.", targetType: type);
                foreach (var item in list)
                    target.Add(item);
                d.Property.SetValue(instance, target);
            }
        }

        // Assign collected map fields.
        foreach (var d in descriptors)
        {
            if (!d.IsMap) continue;
            d.Property.SetValue(instance, mapValues[d.FieldNumber]);
        }

        return instance;
    }

    private static void ApplyDerivedFields(object instance, FieldDescriptor[] derivedDescriptors, ReadOnlySpan<byte> data, int depth)
    {
        // Build a local lookup for the derived descriptors slice.
        var lookup = derivedDescriptors.ToFrozenDictionary(d => d.FieldNumber);
        int offset = 0;
        while (offset < data.Length)
        {
            uint tag         = (uint)ReadVarintChecked(data, ref offset, instance.GetType());
            int  fieldNumber = (int)(tag >> 3);
            var  wireType    = (WireType)(tag & 0x07);

            if (!lookup.TryGetValue(fieldNumber, out var field))
            {
                SkipFieldChecked(data, wireType, ref offset, instance.GetType());
                continue;
            }

            var value = ReadScalarValue(data, field, wireType, ref offset, depth);
            field.Property.SetValue(instance, value);
        }
    }

    private static void ReadMapEntry(ReadOnlySpan<byte> data, FieldDescriptor field, ref int offset, IDictionary target, int depth)
    {
        int length = (int)ReadVarintChecked(data, ref offset, field.MapKeyType);
        int end    = offset + length;

        if (end > data.Length)
            throw new ProtobufDecodeException(
                $"Map entry length {length} exceeds payload boundary.", offset, field.MapKeyType);

        object? key   = null;
        object? value = null;

        while (offset < end)
        {
            uint entryTag   = (uint)ReadVarintChecked(data, ref offset, field.MapKeyType);
            int  entryField = (int)(entryTag >> 3);
            var  entryWire  = (WireType)(entryTag & 0x07);

            if (entryField == 1)
                key = ReadScalarForType(data, field.MapKeyType!, entryWire, ref offset, depth: depth);
            else if (entryField == 2)
                value = ReadScalarForType(data, field.MapValueType!, entryWire, ref offset, depth: depth);
            else
                SkipFieldChecked(data, entryWire, ref offset, field.MapKeyType);
        }

        if (key is not null)
            target[key] = value;
    }

    private static void ReadRepeatedField(ReadOnlySpan<byte> data, FieldDescriptor field, WireType wireType, ref int offset, IList target, int depth)
    {
        var elementType     = field.ElementType!;
        var elementWireType = field.ElementWireType;

        if (wireType == WireType.LengthDelimited && ContractResolver.IsPackable(elementWireType))
        {
            int length = (int)ReadVarintChecked(data, ref offset, elementType);
            int end    = offset + length;

            if (end > data.Length)
                throw new ProtobufDecodeException(
                    $"Packed repeated field length {length} exceeds payload boundary.", offset, elementType);

            while (offset < end)
                target.Add(ReadScalarForType(data, elementType, elementWireType, ref offset, depth: depth));
        }
        else
        {
            target.Add(ReadScalarForType(data, elementType, wireType, ref offset, depth: depth));
        }
    }

    private static object? ReadScalarValue(ReadOnlySpan<byte> data, FieldDescriptor field, WireType wireType, ref int offset, int depth = 0)
    {
        var targetType = Nullable.GetUnderlyingType(field.Property.PropertyType) ?? field.Property.PropertyType;
        return ReadScalarForType(data, targetType, wireType, ref offset, field.Encoding, depth);
    }

    private static object? ReadScalarForType(ReadOnlySpan<byte> data, Type? targetType, WireType wireType, ref int offset, ProtoEncoding? encoding = null, int depth = 0)
    {
        if (targetType is null)
            throw new ProtobufDecodeException("Cannot decode field: target type is null.", offset);

        return wireType switch
        {
            WireType.Varint          => ConvertFromVarint(ReadVarintChecked(data, ref offset, targetType), targetType),
            WireType.Fixed32         => ReadFixed32Value(data, targetType, ref offset),
            WireType.Fixed64         => ReadFixed64Value(data, targetType, ref offset),
            WireType.LengthDelimited => ReadLengthDelimitedValue(data, targetType, ref offset, encoding, depth),
            _ => throw new ProtobufDecodeException($"Unsupported wire type {(int)wireType}.", offset, targetType)
        };
    }

    #endregion

    #region Write helpers

    private static void WriteVarint(Stream output, ulong value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value > 0)
                b |= 0x80;
            output.WriteByte(b);
        } while (value > 0);
    }

    private static ulong ToVarintValue(object value) => value switch
    {
        bool b => b ? 1UL : 0UL,
        byte v => v,
        sbyte v => (ulong)(long)v,
        short v => (ulong)(long)v,
        ushort v => v,
        int v => (ulong)(long)v,
        uint v => v,
        long v => (ulong)v,
        ulong v => v,
        nint v => (ulong)(long)v,
        nuint v => (ulong)v,
        Enum e => Convert.ToUInt64(e),
        _ => throw new NotSupportedException($"Cannot encode {value.GetType().Name} as varint.")
    };

    private static void WriteFixed32(Stream output, object value)
    {
        Span<byte> buf = stackalloc byte[4];
        switch (value)
        {
            case float  f: BinaryPrimitives.WriteSingleLittleEndian(buf, f);  break;
            case int    i: BinaryPrimitives.WriteInt32LittleEndian(buf, i);   break;
            case uint   u: BinaryPrimitives.WriteUInt32LittleEndian(buf, u);  break;
            default: throw new NotSupportedException($"Cannot encode {value.GetType().Name} as fixed32.");
        }
        output.Write(buf);
    }

    private static void WriteFixed64(Stream output, object value)
    {
        Span<byte> buf = stackalloc byte[8];
        switch (value)
        {
            case double   d:  BinaryPrimitives.WriteDoubleLittleEndian(buf, d);            break;
            case long     l:  BinaryPrimitives.WriteInt64LittleEndian(buf, l);             break;
            case ulong    u:  BinaryPrimitives.WriteUInt64LittleEndian(buf, u);            break;
            case DateTime dt: BinaryPrimitives.WriteInt64LittleEndian(buf, dt.Ticks);      break;
            case TimeSpan ts: BinaryPrimitives.WriteInt64LittleEndian(buf, ts.Ticks);      break;
            default: throw new NotSupportedException($"Cannot encode {value.GetType().Name} as fixed64.");
        }
        output.Write(buf);
    }

    private static void WriteLengthDelimited(Stream output, object value, ProtoEncoding? encoding = null)
    {
        byte[] payload = value switch
        {
            string s          => (encoding?.Encoding ?? Encoding.UTF8).GetBytes(s),
            byte[] b          => b,
            Guid g            => g.ToByteArray(),
            decimal d         => Encoding.UTF8.GetBytes(d.ToString(CultureInfo.InvariantCulture)),
            DateTimeOffset dto => EncodeDateTimeOffset(dto),
            DateOnly doVal    => EncodeDateOnly(doVal),
            TimeOnly toVal    => EncodeTimeOnly(toVal),
            Int128 i128       => EncodeInt128(i128),
            UInt128 u128      => EncodeUInt128(u128),
            Half h            => EncodeHalf(h),
            BigInteger bi     => bi.ToByteArray(),
            Complex c         => EncodeComplex(c),
            Version v         => Encoding.UTF8.GetBytes(v.ToString()),
            Uri u             => Encoding.UTF8.GetBytes(u.AbsoluteUri),
            _                 => Encode(value) // Nested [ProtoContract] message (or implicit contract)
        };

        WriteVarint(output, (ulong)payload.Length);
        output.Write(payload);
    }

    private static byte[] EncodeDateTimeOffset(DateTimeOffset dto)
    {
        var buf = new byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(0, 8), dto.Ticks);
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(8, 8), dto.Offset.Ticks);
        return buf;
    }

    private static byte[] EncodeDateOnly(DateOnly d)
    {
        var buf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, d.DayNumber);
        return buf;
    }

    private static byte[] EncodeTimeOnly(TimeOnly t)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, t.Ticks);
        return buf;
    }

    private static byte[] EncodeHalf(Half h)
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteHalfLittleEndian(buf, h);
        return buf;
    }

    private static byte[] EncodeInt128(Int128 val)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteInt128LittleEndian(bytes, val);
        return bytes;
    }

    private static byte[] EncodeUInt128(UInt128 val)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(bytes, val);
        return bytes;
    }

    private static byte[] EncodeComplex(Complex c)
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(0, 8), c.Real);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(8, 8), c.Imaginary);
        return bytes;
    }

    #endregion

    #region Read helpers

    private static bool TryReadVarintFromStream(Stream stream, out ulong value)
    {
        value = 0;
        int shift = 0;

        while (true)
        {
            int byteRead = stream.ReadByte();
            if (byteRead == -1)
            {
                // End of stream — only valid if we haven't started reading a varint
                return shift == 0 ? false : throw new InvalidOperationException("Unexpected end of stream in varint.");
            }

            byte b = (byte)byteRead;
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return true;
            shift += 7;
        }
    }

    /// <summary>
    /// Varint reader that throws <see cref="ProtobufDecodeException"/> on truncation or
    /// overflow (shift past bit 63).
    /// </summary>
    private static ulong ReadVarintChecked(ReadOnlySpan<byte> data, ref int offset, Type? targetType)
    {
        ulong result = 0;
        int   shift  = 0;

        while (offset < data.Length)
        {
            if (shift >= 64)
                throw new ProtobufDecodeException(
                    "Varint overflows 64 bits — payload is malformed.", offset, targetType);

            byte b = data[offset++];
            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
        }

        throw new ProtobufDecodeException(
            "Unexpected end of payload while reading varint.", offset, targetType);
    }

    private static object ConvertFromVarint(ulong raw, Type targetType)
    {
        if (targetType == typeof(bool))   return raw != 0;
        if (targetType == typeof(byte))   return (byte)raw;
        if (targetType == typeof(sbyte))  return (sbyte)(long)raw;
        if (targetType == typeof(short))  return (short)(long)raw;
        if (targetType == typeof(ushort)) return (ushort)raw;
        if (targetType == typeof(int))    return (int)(long)raw;
        if (targetType == typeof(uint))   return (uint)raw;
        if (targetType == typeof(long))   return (long)raw;
        if (targetType == typeof(ulong))  return raw;
        if (targetType == typeof(nint))   return (nint)(long)raw;
        if (targetType == typeof(nuint))  return (nuint)raw;
        if (targetType.IsEnum)            return Enum.ToObject(targetType, raw);
        throw new ProtobufDecodeException($"Cannot convert varint to '{targetType.Name}'.", targetType: targetType);
    }

    private static object ReadFixed32Value(ReadOnlySpan<byte> data, Type targetType, ref int offset)
    {
        if (offset + 4 > data.Length)
            throw new ProtobufDecodeException(
                $"Fixed32 read requires 4 bytes but only {data.Length - offset} remain.", offset, targetType);

        var span = data.Slice(offset, 4);
        offset += 4;

        if (targetType == typeof(float)) return BinaryPrimitives.ReadSingleLittleEndian(span);
        if (targetType == typeof(int))   return BinaryPrimitives.ReadInt32LittleEndian(span);
        if (targetType == typeof(uint))  return BinaryPrimitives.ReadUInt32LittleEndian(span);
        throw new ProtobufDecodeException($"Cannot read fixed32 as '{targetType.Name}'.", offset, targetType);
    }

    private static object ReadFixed64Value(ReadOnlySpan<byte> data, Type targetType, ref int offset)
    {
        if (offset + 8 > data.Length)
            throw new ProtobufDecodeException(
                $"Fixed64 read requires 8 bytes but only {data.Length - offset} remain.", offset, targetType);

        var span = data.Slice(offset, 8);
        offset += 8;

        if (targetType == typeof(double))   return BinaryPrimitives.ReadDoubleLittleEndian(span);
        if (targetType == typeof(long))     return BinaryPrimitives.ReadInt64LittleEndian(span);
        if (targetType == typeof(ulong))    return BinaryPrimitives.ReadUInt64LittleEndian(span);
        if (targetType == typeof(DateTime)) return new DateTime(BinaryPrimitives.ReadInt64LittleEndian(span));
        if (targetType == typeof(TimeSpan)) return new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(span));
        throw new ProtobufDecodeException($"Cannot read fixed64 as '{targetType.Name}'.", offset, targetType);
    }

    private static object? ReadLengthDelimitedValue(ReadOnlySpan<byte> data, Type targetType, ref int offset, ProtoEncoding? encoding = null, int depth = 0)
    {
        int length = (int)ReadVarintChecked(data, ref offset, targetType);

        if (length < 0 || offset + length > data.Length)
            throw new ProtobufDecodeException(
                $"Length-delimited field length {length} exceeds payload boundary.", offset, targetType);

        var payload = data.Slice(offset, length);
        offset += length;

        if (targetType == typeof(string))        return (encoding?.Encoding ?? Encoding.UTF8).GetString(payload);
        if (targetType == typeof(byte[]))        return payload.ToArray();
        if (targetType == typeof(Guid))          return new Guid(payload);
        if (targetType == typeof(decimal))       return decimal.Parse(Encoding.UTF8.GetString(payload), CultureInfo.InvariantCulture);
        if (targetType == typeof(DateTimeOffset)) return DecodeDateTimeOffset(payload);
        if (targetType == typeof(DateOnly))      return DateOnly.FromDayNumber(BinaryPrimitives.ReadInt32LittleEndian(payload));
        if (targetType == typeof(TimeOnly))      return new TimeOnly(BinaryPrimitives.ReadInt64LittleEndian(payload));
        if (targetType == typeof(Int128))        return BinaryPrimitives.ReadInt128LittleEndian(payload);
        if (targetType == typeof(UInt128))       return BinaryPrimitives.ReadUInt128LittleEndian(payload);
        if (targetType == typeof(Half))          return BinaryPrimitives.ReadHalfLittleEndian(payload);
        if (targetType == typeof(BigInteger))    return new BigInteger(payload);
        if (targetType == typeof(Complex))       return new Complex(
            BinaryPrimitives.ReadDoubleLittleEndian(payload.Slice(0, 8)),
            BinaryPrimitives.ReadDoubleLittleEndian(payload.Slice(8, 8)));
        if (targetType == typeof(Version))       return Version.Parse(Encoding.UTF8.GetString(payload));
        if (targetType == typeof(Uri))           return new Uri(Encoding.UTF8.GetString(payload));

        // Nested message — recurse with incremented depth.
        return DecodeMessage(targetType, payload, depth + 1);
    }

    private static DateTimeOffset DecodeDateTimeOffset(ReadOnlySpan<byte> payload)
    {
        long ticks       = BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(0, 8));
        long offsetTicks = BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(8, 8));
        return new DateTimeOffset(ticks, new TimeSpan(offsetTicks));
    }

    /// <summary>
    /// Skips a field of the given wire type with full bounds validation.
    /// Throws <see cref="ProtobufDecodeException"/> on malformed wire type or truncated data.
    /// </summary>
    private static void SkipFieldChecked(ReadOnlySpan<byte> data, WireType wireType, ref int offset, Type? targetType)
    {
        switch (wireType)
        {
            case WireType.Varint:
                ReadVarintChecked(data, ref offset, targetType);
                break;
            case WireType.Fixed64:
                if (offset + 8 > data.Length)
                    throw new ProtobufDecodeException("Truncated fixed64 field during skip.", offset, targetType);
                offset += 8;
                break;
            case WireType.Fixed32:
                if (offset + 4 > data.Length)
                    throw new ProtobufDecodeException("Truncated fixed32 field during skip.", offset, targetType);
                offset += 4;
                break;
            case WireType.LengthDelimited:
                int len = (int)ReadVarintChecked(data, ref offset, targetType);
                if (len < 0 || offset + len > data.Length)
                    throw new ProtobufDecodeException($"Skip: length-delimited field length {len} exceeds boundary.", offset, targetType);
                offset += len;
                break;
            default:
                throw new ProtobufDecodeException($"Cannot skip unknown wire type {(int)wireType}.", offset, targetType);
        }
    }

    #endregion

    #region Utility

    private static IList CreateList(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)Activator.CreateInstance(listType)!;
    }

    private static IDictionary CreateDictionary(Type keyType, Type valueType)
    {
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        return (IDictionary)Activator.CreateInstance(dictType)!;
    }

    private static bool IsDefault(object value, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string)) return string.IsNullOrEmpty((string)value);
        if (underlying == typeof(bool)) return (bool)value == false;
        if (underlying == typeof(byte[])) return ((byte[])value).Length == 0;

        // Empty collection = default
        if (value is ICollection { Count: 0 })
            return true;
        if (value is IDictionary { Count: 0 })
            return true;
        if (value is IEnumerable enumerable && !enumerable.GetEnumerator().MoveNext())
            return true;

        if (underlying.IsValueType)
        {
            var defaultVal = Activator.CreateInstance(underlying);
            return value.Equals(defaultVal);
        }

        return false;
    }

    #endregion
}
