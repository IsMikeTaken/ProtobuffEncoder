using System.Text.RegularExpressions;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Parses .proto files into <see cref="ProtoFile"/> models.
/// Supports proto3 syntax with messages, enums, repeated, optional, map, oneof, and deprecated fields.
/// </summary>
public static partial class ProtoSchemaParser
{
    public static ProtoFile Parse(string protoContent)
    {
        var file = new ProtoFile();
        var lines = protoContent.Split('\n').Select(l => l.Trim()).ToList();

        int i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];

            if (line.StartsWith("syntax"))
            {
                var match = SyntaxRegex().Match(line);
                if (match.Success)
                    file.Syntax = match.Groups[1].Value;
            }
            else if (line.StartsWith("package"))
            {
                var match = PackageRegex().Match(line);
                if (match.Success)
                    file.Package = match.Groups[1].Value;
            }
            else if (line.StartsWith("message"))
            {
                var msg = ParseMessage(lines, ref i);
                file.Messages.Add(msg);
                continue; // ParseMessage advances i past the closing brace
            }
            else if (line.StartsWith("enum"))
            {
                var enumDef = ParseEnum(lines, ref i);
                file.Enums.Add(enumDef);
                continue;
            }

            i++;
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

    private static ProtoMessageDef ParseMessage(List<string> lines, ref int i)
    {
        var match = MessageRegex().Match(lines[i]);
        var msg = new ProtoMessageDef { Name = match.Groups[1].Value };
        i++; // skip "message Name {"

        while (i < lines.Count)
        {
            var line = lines[i];

            if (line == "}" || line.StartsWith('}'))
            {
                i++;
                return msg;
            }

            if (line.StartsWith("message"))
            {
                msg.NestedMessages.Add(ParseMessage(lines, ref i));
                continue;
            }

            if (line.StartsWith("enum"))
            {
                msg.NestedEnums.Add(ParseEnum(lines, ref i));
                continue;
            }

            // oneof block
            if (line.StartsWith("oneof"))
            {
                var oneOf = ParseOneOf(lines, ref i);
                msg.OneOfs.Add(oneOf);
                continue;
            }

            // map<K, V> field
            var mapMatch = MapFieldRegex().Match(line);
            if (mapMatch.Success)
            {
                bool isDeprecated = line.Contains("[deprecated = true]");
                msg.Fields.Add(new ProtoFieldDef
                {
                    IsMap = true,
                    MapKeyType = mapMatch.Groups[1].Value,
                    MapValueType = mapMatch.Groups[2].Value,
                    Name = mapMatch.Groups[3].Value,
                    FieldNumber = int.Parse(mapMatch.Groups[4].Value),
                    IsDeprecated = isDeprecated
                });
                i++;
                continue;
            }

            // Regular field
            var fieldMatch = FieldRegex().Match(line);
            if (fieldMatch.Success)
            {
                bool isRepeated = fieldMatch.Groups[1].Value == "repeated";
                bool isOptional = fieldMatch.Groups[1].Value == "optional";
                bool isDeprecated = line.Contains("[deprecated = true]");
                msg.Fields.Add(new ProtoFieldDef
                {
                    IsRepeated = isRepeated,
                    IsOptional = isOptional,
                    TypeName = fieldMatch.Groups[2].Value,
                    Name = fieldMatch.Groups[3].Value,
                    FieldNumber = int.Parse(fieldMatch.Groups[4].Value),
                    IsDeprecated = isDeprecated
                });
            }

            i++;
        }

        return msg;
    }

    private static ProtoOneOfDef ParseOneOf(List<string> lines, ref int i)
    {
        var match = OneOfRegex().Match(lines[i]);
        var oneOf = new ProtoOneOfDef { Name = match.Groups[1].Value };
        i++; // skip "oneof name {"

        while (i < lines.Count)
        {
            var line = lines[i];

            if (line == "}" || line.StartsWith('}'))
            {
                i++;
                return oneOf;
            }

            var fieldMatch = FieldRegex().Match(line);
            if (fieldMatch.Success)
            {
                bool isDeprecated = line.Contains("[deprecated = true]");
                oneOf.Fields.Add(new ProtoFieldDef
                {
                    TypeName = fieldMatch.Groups[2].Value,
                    Name = fieldMatch.Groups[3].Value,
                    FieldNumber = int.Parse(fieldMatch.Groups[4].Value),
                    OneOfGroup = oneOf.Name,
                    IsDeprecated = isDeprecated
                });
            }

            i++;
        }

        return oneOf;
    }

    private static ProtoEnumDef ParseEnum(List<string> lines, ref int i)
    {
        var match = EnumRegex().Match(lines[i]);
        var enumDef = new ProtoEnumDef { Name = match.Groups[1].Value };
        i++; // skip "enum Name {"

        while (i < lines.Count)
        {
            var line = lines[i];

            if (line == "}" || line.StartsWith('}'))
            {
                i++;
                return enumDef;
            }

            var valueMatch = EnumValueRegex().Match(line);
            if (valueMatch.Success)
            {
                enumDef.Values.Add(new ProtoEnumValue
                {
                    Name = valueMatch.Groups[1].Value,
                    Number = int.Parse(valueMatch.Groups[2].Value)
                });
            }

            i++;
        }

        return enumDef;
    }

    [GeneratedRegex(@"syntax\s*=\s*""(\w+)""\s*;")]
    private static partial Regex SyntaxRegex();

    [GeneratedRegex(@"package\s+([\w.]+)\s*;")]
    private static partial Regex PackageRegex();

    [GeneratedRegex(@"message\s+(\w+)\s*\{")]
    private static partial Regex MessageRegex();

    [GeneratedRegex(@"enum\s+(\w+)\s*\{")]
    private static partial Regex EnumRegex();

    [GeneratedRegex(@"oneof\s+(\w+)\s*\{")]
    private static partial Regex OneOfRegex();

    [GeneratedRegex(@"map<\s*(\w+)\s*,\s*(\w+)\s*>\s+(\w+)\s*=\s*(\d+)\s*")]
    private static partial Regex MapFieldRegex();

    [GeneratedRegex(@"(repeated\s+|optional\s+)?(\w+)\s+(\w+)\s*=\s*(\d+)\s*")]
    private static partial Regex FieldRegex();

    [GeneratedRegex(@"(\w+)\s*=\s*(-?\d+)\s*;")]
    private static partial Regex EnumValueRegex();
}
