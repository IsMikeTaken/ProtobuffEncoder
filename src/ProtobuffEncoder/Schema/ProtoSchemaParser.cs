using System.Globalization;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Parses .proto files into <see cref="ProtoFile"/> models.
/// Supports proto3 syntax with messages, enums, repeated, optional, map, oneof, and deprecated fields.
/// </summary>
public static partial class ProtoSchemaParser
{
    /// <summary>
    /// Parses a .proto document from an in-memory string.
    /// </summary>
    public static ProtoFile Parse(string protoContent)
    {
        ArgumentNullException.ThrowIfNull(protoContent);

        var file = new ProtoFile();
        var lines = new ProtoLineCollection(protoContent);

        int lineIndex = 0;
        while (lineIndex < lines.Count)
        {
            var line = lines[lineIndex];
            if (line.IsEmpty)
            {
                lineIndex++;
                continue;
            }

            if (TryParseSyntax(line, out var syntax))
            {
                file.Syntax = syntax;
            }
            else if (TryParsePackage(line, out var packageName))
            {
                file.Package = packageName;
            }
            else if (TryParseBlockName(line, "message", out _))
            {
                file.Messages.Add(ParseMessage(lines, ref lineIndex));
                continue;
            }
            else if (TryParseBlockName(line, "enum", out _))
            {
                file.Enums.Add(ParseEnum(lines, ref lineIndex));
                continue;
            }

            lineIndex++;
        }

        return file;
    }

    /// <summary>
    /// Parses a .proto file from disk.
    /// </summary>
    public static ProtoFile ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// Parses all .proto files in a directory and returns a combined list of all message and enum definitions.
    /// </summary>
    public static List<ProtoFile> ParseDirectory(string directory)
    {
        var results = new List<ProtoFile>();
        foreach (var file in Directory.GetFiles(directory, "*.proto"))
        {
            results.Add(ParseFile(file));
        }

        return results;
    }

    private static ProtoMessageDef ParseMessage(ProtoLineCollection lines, ref int lineIndex)
    {
        if (!TryParseBlockName(lines[lineIndex], "message", out var messageName))
        {
            throw new FormatException($"Invalid message declaration at line index {lineIndex}.");
        }

        var message = new ProtoMessageDef { Name = messageName };
        lineIndex++;

        while (lineIndex < lines.Count)
        {
            var line = lines[lineIndex];
            if (line.IsEmpty)
            {
                lineIndex++;
                continue;
            }

            if (IsClosingBrace(line))
            {
                lineIndex++;
                return message;
            }

            if (TryParseBlockName(line, "message", out _))
            {
                message.NestedMessages.Add(ParseMessage(lines, ref lineIndex));
                continue;
            }

            if (TryParseBlockName(line, "enum", out _))
            {
                message.NestedEnums.Add(ParseEnum(lines, ref lineIndex));
                continue;
            }

            if (TryParseBlockName(line, "oneof", out _))
            {
                message.OneOfs.Add(ParseOneOf(lines, ref lineIndex));
                continue;
            }

            if (TryParseMapField(line, out var mapField))
            {
                message.Fields.Add(mapField);
                lineIndex++;
                continue;
            }

            if (TryParseField(line, out var field))
            {
                message.Fields.Add(field);
            }

            lineIndex++;
        }

        return message;
    }

    private static ProtoOneOfDef ParseOneOf(ProtoLineCollection lines, ref int lineIndex)
    {
        if (!TryParseBlockName(lines[lineIndex], "oneof", out var oneOfName))
        {
            throw new FormatException($"Invalid oneof declaration at line index {lineIndex}.");
        }

        var oneOf = new ProtoOneOfDef { Name = oneOfName };
        lineIndex++;

        while (lineIndex < lines.Count)
        {
            var line = lines[lineIndex];
            if (line.IsEmpty)
            {
                lineIndex++;
                continue;
            }

            if (IsClosingBrace(line))
            {
                lineIndex++;
                return oneOf;
            }

            if (TryParseField(line, out var field))
            {
                field.OneOfGroup = oneOf.Name;
                oneOf.Fields.Add(field);
            }

            lineIndex++;
        }

        return oneOf;
    }

    private static ProtoEnumDef ParseEnum(ProtoLineCollection lines, ref int lineIndex)
    {
        if (!TryParseBlockName(lines[lineIndex], "enum", out var enumName))
        {
            throw new FormatException($"Invalid enum declaration at line index {lineIndex}.");
        }

        var enumDef = new ProtoEnumDef { Name = enumName };
        lineIndex++;

        while (lineIndex < lines.Count)
        {
            var line = lines[lineIndex];
            if (line.IsEmpty)
            {
                lineIndex++;
                continue;
            }

            if (IsClosingBrace(line))
            {
                lineIndex++;
                return enumDef;
            }

            if (TryParseEnumValue(line, out var value))
            {
                enumDef.Values.Add(value);
            }

            lineIndex++;
        }

        return enumDef;
    }

    private static bool TryParseSyntax(ReadOnlySpan<char> line, out string syntax)
    {
        syntax = string.Empty;
        if (!TryReadAssignmentValue(line, "syntax", allowDots: false, out var value))
        {
            return false;
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            syntax = value[1..^1].ToString();
            return true;
        }

        return false;
    }

    private static bool TryParsePackage(ReadOnlySpan<char> line, out string packageName)
    {
        packageName = string.Empty;
        return TryReadAssignmentValue(line, "package", allowDots: true, out var value)
            && TryReadIdentifier(value, allowDots: true, out packageName);
    }

    private static bool TryParseBlockName(ReadOnlySpan<char> line, string keyword, out string name)
    {
        name = string.Empty;
        if (!TryConsumeKeyword(line, keyword, out var remainder))
        {
            return false;
        }

        if (!TryReadIdentifier(remainder, allowDots: false, out name, out var tail))
        {
            return false;
        }

        tail = TrimWhitespace(tail);
        return tail.Length > 0 && tail[0] == '{';
    }

    private static bool TryParseMapField(ReadOnlySpan<char> line, out ProtoFieldDef field)
    {
        field = null!;
        var cursor = TrimWhitespace(line);
        if (!TryConsumeLiteral(ref cursor, "map<"))
        {
            return false;
        }

        if (!TryReadIdentifier(ref cursor, allowDots: true, out var keyType))
        {
            return false;
        }

        if (!TryConsumeLiteral(ref cursor, ","))
        {
            return false;
        }

        if (!TryReadIdentifier(ref cursor, allowDots: true, out var valueType))
        {
            return false;
        }

        if (!TryConsumeLiteral(ref cursor, ">"))
        {
            return false;
        }

        if (!TryReadIdentifier(ref cursor, allowDots: false, out var name))
        {
            return false;
        }

        if (!TryConsumeLiteral(ref cursor, "="))
        {
            return false;
        }

        if (!TryReadInt32(ref cursor, out var fieldNumber))
        {
            return false;
        }

        field = new ProtoFieldDef
        {
            IsMap = true,
            MapKeyType = keyType,
            MapValueType = valueType,
            Name = name,
            FieldNumber = fieldNumber,
            IsDeprecated = ContainsDeprecatedOption(cursor)
        };

        return true;
    }

    private static bool TryParseField(ReadOnlySpan<char> line, out ProtoFieldDef field)
    {
        field = null!;
        var cursor = TrimWhitespace(line);

        bool isRepeated = TryConsumeKeyword(ref cursor, "repeated");
        bool isOptional = !isRepeated && TryConsumeKeyword(ref cursor, "optional");

        if (!TryReadIdentifier(ref cursor, allowDots: true, out var typeName)
            || !TryReadIdentifier(ref cursor, allowDots: false, out var name)
            || !TryConsumeLiteral(ref cursor, "=")
            || !TryReadInt32(ref cursor, out var fieldNumber))
        {
            return false;
        }

        field = new ProtoFieldDef
        {
            IsRepeated = isRepeated,
            IsOptional = isOptional,
            TypeName = typeName,
            Name = name,
            FieldNumber = fieldNumber,
            IsDeprecated = ContainsDeprecatedOption(cursor)
        };

        return true;
    }

    private static bool TryParseEnumValue(ReadOnlySpan<char> line, out ProtoEnumValue value)
    {
        value = null!;
        var cursor = TrimWhitespace(line);
        if (!TryReadIdentifier(ref cursor, allowDots: false, out var name)
            || !TryConsumeLiteral(ref cursor, "=")
            || !TryReadInt32(ref cursor, out var number))
        {
            return false;
        }

        value = new ProtoEnumValue
        {
            Name = name,
            Number = number
        };

        return true;
    }

    private static bool TryReadAssignmentValue(ReadOnlySpan<char> line, string keyword, bool allowDots, out ReadOnlySpan<char> value)
    {
        value = default;
        if (!TryConsumeKeyword(line, keyword, out var remainder))
        {
            return false;
        }

        remainder = TrimWhitespace(remainder);
        if (remainder.IsEmpty || remainder[0] == '=')
        {
            if (remainder.IsEmpty || remainder[0] != '=')
            {
                return false;
            }

            remainder = TrimWhitespace(remainder[1..]);
        }

        int semicolonIndex = remainder.IndexOf(';');
        if (semicolonIndex < 0)
        {
            return false;
        }

        value = TrimWhitespace(remainder[..semicolonIndex]);
        return !value.IsEmpty && (allowDots || value.IndexOf('.') < 0 || value[0] == '"');
    }

    private static bool TryConsumeKeyword(ReadOnlySpan<char> line, string keyword, out ReadOnlySpan<char> remainder)
    {
        remainder = default;
        line = TrimWhitespace(line);
        if (!line.StartsWith(keyword, StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Length > keyword.Length && !char.IsWhiteSpace(line[keyword.Length]))
        {
            return false;
        }

        remainder = line[keyword.Length..];
        return true;
    }

    private static bool TryConsumeKeyword(ref ReadOnlySpan<char> line, string keyword)
    {
        if (!TryConsumeKeyword(line, keyword, out var remainder))
        {
            return false;
        }

        line = remainder;
        return true;
    }

    private static bool TryConsumeLiteral(ref ReadOnlySpan<char> line, string literal)
    {
        line = TrimWhitespace(line);
        if (!line.StartsWith(literal, StringComparison.Ordinal))
        {
            return false;
        }

        line = line[literal.Length..];
        return true;
    }

    private static bool TryReadIdentifier(ReadOnlySpan<char> line, bool allowDots, out string identifier)
    {
        identifier = string.Empty;
        return TryReadIdentifier(line, allowDots, out identifier, out _);
    }

    private static bool TryReadIdentifier(ReadOnlySpan<char> line, bool allowDots, out string identifier, out ReadOnlySpan<char> remainder)
    {
        identifier = string.Empty;
        remainder = default;
        line = TrimWhitespace(line);
        if (line.IsEmpty)
        {
            return false;
        }

        int length = 0;
        while (length < line.Length && IsIdentifierCharacter(line[length], allowDots))
        {
            length++;
        }

        if (length == 0)
        {
            return false;
        }

        identifier = line[..length].ToString();
        remainder = line[length..];
        return true;
    }

    private static bool TryReadIdentifier(ref ReadOnlySpan<char> line, bool allowDots, out string identifier)
    {
        if (!TryReadIdentifier(line, allowDots, out identifier, out var remainder))
        {
            return false;
        }

        line = remainder;
        return true;
    }

    private static bool TryReadInt32(ref ReadOnlySpan<char> line, out int value)
    {
        line = TrimWhitespace(line);
        int length = 0;
        if (length < line.Length && (line[length] == '-' || line[length] == '+'))
        {
            length++;
        }

        while (length < line.Length && char.IsAsciiDigit(line[length]))
        {
            length++;
        }

        if (length == 0 || (length == 1 && (line[0] == '-' || line[0] == '+')))
        {
            value = default;
            return false;
        }

        if (!int.TryParse(line[..length], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        line = line[length..];
        return true;
    }

    private static bool IsClosingBrace(ReadOnlySpan<char> line)
    {
        line = TrimWhitespace(line);
        return !line.IsEmpty && line[0] == '}';
    }

    private static bool ContainsDeprecatedOption(ReadOnlySpan<char> line)
        => line.IndexOf("deprecated = true", StringComparison.Ordinal) >= 0;

    private static bool IsIdentifierCharacter(char character, bool allowDots)
        => char.IsLetterOrDigit(character)
           || character == '_'
           || (allowDots && character == '.');

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> value)
    {
        int start = 0;
        int end = value.Length - 1;

        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<char>.Empty : value[start..(end + 1)];
    }

    private readonly record struct ProtoLineSegment(int Start, int Length);

    private sealed class ProtoLineCollection
    {
        private readonly string _content;
        private readonly List<ProtoLineSegment> _segments;

        public ProtoLineCollection(string content)
        {
            _content = content;
            _segments = BuildSegments(content);
        }

        public int Count => _segments.Count;

        public ReadOnlySpan<char> this[int index]
        {
            get
            {
                var segment = _segments[index];
                return TrimWhitespace(_content.AsSpan(segment.Start, segment.Length));
            }
        }

        private static List<ProtoLineSegment> BuildSegments(string content)
        {
            var segments = new List<ProtoLineSegment>();
            int lineStart = 0;

            for (int index = 0; index < content.Length; index++)
            {
                if (content[index] != '\n')
                {
                    continue;
                }

                segments.Add(new ProtoLineSegment(lineStart, index - lineStart));
                lineStart = index + 1;
            }

            if (lineStart <= content.Length)
            {
                segments.Add(new ProtoLineSegment(lineStart, content.Length - lineStart));
            }

            return segments;
        }
    }
}
