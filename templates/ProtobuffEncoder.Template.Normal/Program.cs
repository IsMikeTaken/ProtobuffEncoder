// ============================================================================
// ProtobuffEncoder — Normal Template
// ============================================================================
// This template covers intermediate features: collections, maps, nullable
// fields, custom encodings, ProtoValue for single values, ProtoMessage for
// dynamic schema-less messages, and oneOf groups.
//
// Run with:  dotnet run
// ============================================================================

using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

Console.WriteLine("=== ProtobuffEncoder — Normal Template ===\n");

// ── 1. Collections and maps ─────────────────────────────────────────────────

var team = new Team
{
    Name = "Engineering",
    Members = ["Alice", "Bob", "Charlie"],
    Scores = new Dictionary<string, int>
    {
        ["Alice"] = 95,
        ["Bob"] = 87,
        ["Charlie"] = 92
    }
};

var teamBytes = ProtobufEncoder.Encode(team);
var decodedTeam = ProtobufEncoder.Decode<Team>(teamBytes);
Console.WriteLine($"Team: {decodedTeam.Name}, {decodedTeam.Members.Count} members");
foreach (var (name, score) in decodedTeam.Scores)
    Console.WriteLine($"  {name}: {score}");

// ── 2. Nullable and required fields ─────────────────────────────────────────

Console.WriteLine("\n--- Nullable Fields ---");

var sensor = new SensorReading
{
    SensorId = "temp-01",
    Value = 22.5,
    CalibratedAt = DateTime.UtcNow,
    ErrorMargin = null // optional, won't be serialized
};

var sensorBytes = ProtobufEncoder.Encode(sensor);
var decodedSensor = ProtobufEncoder.Decode<SensorReading>(sensorBytes);
Console.WriteLine($"Sensor: {decodedSensor.SensorId}, Value={decodedSensor.Value}, Error={decodedSensor.ErrorMargin?.ToString() ?? "N/A"}");

// ── 3. Custom text encoding (emoji support) ─────────────────────────────────

Console.WriteLine("\n--- Custom Encoding ---");

var chat = new ChatMessage
{
    Author = "Alice",
    Content = "Hello! Greetings from London \U0001F1EC\U0001F1E7\u2615" // GB flag + coffee emoji
};

var chatBytes = ProtobufEncoder.Encode(chat);
var decodedChat = ProtobufEncoder.Decode<ChatMessage>(chatBytes);
Console.WriteLine($"{decodedChat.Author}: {decodedChat.Content}");

// ── 4. ProtoValue — single-value encoding ───────────────────────────────────

Console.WriteLine("\n--- ProtoValue (single values without contracts) ---");

var encodedString = ProtoValue.Encode("Hello, World!");
var encodedBool = ProtoValue.Encode(true);
var encodedInt = ProtoValue.Encode(42);
var encodedDate = ProtoValue.Encode(DateTime.UtcNow);
var encodedGuid = ProtoValue.Encode(Guid.NewGuid());

Console.WriteLine($"String: {ProtoValue.DecodeString(encodedString)} ({encodedString.Length} bytes)");
Console.WriteLine($"Bool:   {ProtoValue.DecodeBool(encodedBool)}");
Console.WriteLine($"Int:    {ProtoValue.DecodeInt32(encodedInt)}");
Console.WriteLine($"Date:   {ProtoValue.DecodeDateTime(encodedDate):O}");
Console.WriteLine($"Guid:   {ProtoValue.DecodeGuid(encodedGuid)}");

// Emoji encoding round-trip
var emoji = "Sending love \u2764\uFE0F and rockets \U0001F680!";
var emojiBytes = ProtoValue.Encode(emoji, ProtoEncoding.UTF8);
var emojiDecoded = ProtoValue.DecodeString(emojiBytes, ProtoEncoding.UTF8);
Console.WriteLine($"Emoji:  {emojiDecoded}");

// ── 5. ProtoMessage — dynamic schema-less messages ──────────────────────────

Console.WriteLine("\n--- ProtoMessage (dynamic, no contract needed) ---");

var msg = new ProtoMessage()
    .Set(1, "ProtobuffEncoder")
    .Set(2, 42)
    .Set(3, true)
    .Set(4, 3.14)
    .Set(5, DateTime.UtcNow)
    .Set(6, "Works with emoji too! \U0001F389");

var msgBytes = msg.ToBytes();
Console.WriteLine($"Dynamic message: {msg.FieldCount} fields, {msgBytes.Length} bytes");

var decoded2 = ProtoMessage.FromBytes(msgBytes);
Console.WriteLine($"  Field 1 (string): {decoded2.GetString(1)}");
Console.WriteLine($"  Field 2 (int):    {decoded2.Get<int>(2)}");
Console.WriteLine($"  Field 3 (bool):   {decoded2.Get<bool>(3)}");
Console.WriteLine($"  Field 6 (emoji):  {decoded2.GetString(6)}");

// Nested dynamic messages
var nested = new ProtoMessage()
    .Set(1, "parent")
    .Set(2, new ProtoMessage()
        .Set(1, "child")
        .Set(2, 100));

var nestedBytes = nested.ToBytes();
var decodedNested = ProtoMessage.FromBytes(nestedBytes);
Console.WriteLine($"\n  Nested parent: {decodedNested.GetString(1)}");

// ── 6. OneOf groups ─────────────────────────────────────────────────────────

Console.WriteLine("\n--- OneOf Groups ---");

var notification = new Notification
{
    Id = 1,
    Email = "user@example.com",
    // Sms and Push are also set but only the first non-default in the group wins
};

var notifBytes = ProtobufEncoder.Encode(notification);
var decodedNotif = ProtobufEncoder.Decode<Notification>(notifBytes);
Console.WriteLine($"Notification #{decodedNotif.Id}: Email={decodedNotif.Email}");

Console.WriteLine("\nDone! This template covers the most common intermediate patterns.");

// ============================================================================
// Models
// ============================================================================

[ProtoContract]
public class Team
{
    [ProtoField(1)]
    public string Name { get; set; } = "";

    [ProtoField(2)]
    public List<string> Members { get; set; } = [];

    [ProtoMap]
    [ProtoField(3)]
    public Dictionary<string, int> Scores { get; set; } = new();
}

[ProtoContract]
public class SensorReading
{
    [ProtoField(1, IsRequired = true)]
    public string SensorId { get; set; } = "";

    [ProtoField(2)]
    public double Value { get; set; }

    [ProtoField(3)]
    public DateTime CalibratedAt { get; set; }

    [ProtoField(4)]
    public double? ErrorMargin { get; set; }
}

[ProtoContract(DefaultEncoding = "utf-8")]
public class ChatMessage
{
    [ProtoField(1)]
    public string Author { get; set; } = "";

    [ProtoField(2)]
    public string Content { get; set; } = "";
}

[ProtoContract]
public class Notification
{
    [ProtoField(1)]
    public int Id { get; set; }

    [ProtoOneOf("delivery")]
    [ProtoField(2)]
    public string? Email { get; set; }

    [ProtoOneOf("delivery")]
    [ProtoField(3)]
    public string? Sms { get; set; }

    [ProtoOneOf("delivery")]
    [ProtoField(4)]
    public string? Push { get; set; }
}
