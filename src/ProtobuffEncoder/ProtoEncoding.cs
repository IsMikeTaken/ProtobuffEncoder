using System.ComponentModel;
using System.Text;

namespace ProtobuffEncoder;

/// <summary>
/// Wraps <see cref="System.Text.Encoding"/> with protobuf-aware defaults and named presets.
/// All Unicode-capable encodings (UTF-8, UTF-16, UTF-32) fully support emoji and
/// supplementary Unicode planes. UTF-8 is the default and the protobuf wire-format standard.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ProtoEncoding
{
    /// <summary>
    /// UTF-8 encoding (protobuf default). Supports full Unicode including emoji.
    /// </summary>
    public static ProtoEncoding UTF8 { get; } = new("utf-8", Encoding.UTF8, supportsEmoji: true);

    /// <summary>
    /// UTF-16 Little Endian. Supports full Unicode including emoji.
    /// </summary>
    public static ProtoEncoding UTF16 { get; } = new("utf-16", Encoding.Unicode, supportsEmoji: true);

    /// <summary>
    /// UTF-16 Big Endian. Supports full Unicode including emoji.
    /// </summary>
    public static ProtoEncoding UTF16BE { get; } = new("utf-16be", Encoding.BigEndianUnicode, supportsEmoji: true);

    /// <summary>
    /// UTF-32 Little Endian. Supports full Unicode including emoji.
    /// </summary>
    public static ProtoEncoding UTF32 { get; } = new("utf-32", Encoding.UTF32, supportsEmoji: true);

    /// <summary>
    /// ASCII encoding. Does not support emoji or non-ASCII characters.
    /// Characters outside the ASCII range are replaced with '?'.
    /// </summary>
    public static ProtoEncoding ASCII { get; } = new("ascii", Encoding.ASCII, supportsEmoji: false);

    /// <summary>
    /// Latin-1 (ISO 8859-1). Supports Western European characters but not emoji.
    /// </summary>
    public static ProtoEncoding Latin1 { get; } = new("latin-1", Encoding.Latin1, supportsEmoji: false);

    private ProtoEncoding(string name, Encoding encoding, bool supportsEmoji)
    {
        Name = name;
        Encoding = encoding;
        SupportsEmoji = supportsEmoji;
    }

    /// <summary>
    /// Creates a ProtoEncoding from any <see cref="System.Text.Encoding"/>.
    /// </summary>
    public static ProtoEncoding FromEncoding(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);

        bool emoji = encoding.CodePage is 65001  // UTF-8
                                       or 1200   // UTF-16 LE
                                       or 1201   // UTF-16 BE
                                       or 12000  // UTF-32 LE
                                       or 12001; // UTF-32 BE

        return new ProtoEncoding(encoding.WebName, encoding, emoji);
    }

    /// <summary>
    /// Resolves a ProtoEncoding by name. Supports "utf-8", "utf-16", "utf-32", "ascii", "latin-1",
    /// and any encoding name recognized by <see cref="Encoding.GetEncoding(string)"/>.
    /// </summary>
    public static ProtoEncoding FromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => UTF8,
            "utf-16" or "utf16" or "unicode" => UTF16,
            "utf-16be" or "utf16be" => UTF16BE,
            "utf-32" or "utf32" => UTF32,
            "ascii" or "us-ascii" => ASCII,
            "latin-1" or "latin1" or "iso-8859-1" => Latin1,
            _ => FromEncoding(Encoding.GetEncoding(name))
        };
    }

    /// <summary>
    /// The short name of this encoding (e.g. "utf-8", "utf-16", "ascii").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The underlying <see cref="System.Text.Encoding"/> instance.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// True when this encoding supports the full Unicode range including emoji
    /// and supplementary planes (astral codepoints U+10000 and above).
    /// </summary>
    public bool SupportsEmoji { get; }

    /// <summary>
    /// Encodes a string to bytes using this encoding.
    /// </summary>
    public byte[] GetBytes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.GetBytes(value);
    }

    /// <summary>
    /// Decodes bytes to a string using this encoding.
    /// </summary>
    public string GetString(ReadOnlySpan<byte> data) => Encoding.GetString(data);

    /// <summary>
    /// Decodes a byte array to a string using this encoding.
    /// </summary>
    public string GetString(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Encoding.GetString(data);
    }

    public override string ToString() => $"ProtoEncoding({Name}, emoji={SupportsEmoji})";
}
