// ProtobuffEncoder — Normal Template
//
// Builds on the Simple template with collections, maps, nullable fields,
// OneOf groups, custom text encoding, ProtoValue for bare values, and
// ProtoMessage for dynamic schema-less messages. Also defines a chat
// service interface with multiple method types.
//
// Run with: dotnet run

using ProtobuffEncoder;

Console.WriteLine("ProtobuffEncoder — Normal Template\n");

// Collections and maps are first-class. Team contains a List<string> for
// members and a Dictionary<string,int> for scores. Mark dictionaries with
// [ProtoMap] alongside a [ProtoField] number.

var team = new Team
{
    Name = "Platform",
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

// Nullable fields are omitted from the wire when null, saving space. The
// IsRequired flag on SensorId causes the encoder to always include it,
// even when the value is the type's default.

Console.WriteLine("\nNullable and required fields...");

var reading = new SensorReading
{
    SensorId = "temp-01",
    Value = 22.5,
    Timestamp = DateTime.UtcNow,
    ErrorMargin = null
};

var readingBytes = ProtobufEncoder.Encode(reading);
var decodedReading = ProtobufEncoder.Decode<SensorReading>(readingBytes);
Console.WriteLine($"  Sensor {decodedReading.SensorId}: {decodedReading.Value}, margin={decodedReading.ErrorMargin?.ToString() ?? "N/A"}");

// OneOf groups model mutually exclusive fields. Only one member of the
// "channel" group is serialized per message. Set Email and leave the rest
// null to see how it round-trips.

Console.WriteLine("\nOneOf groups...");

var alert = new Alert
{
    Id = 1,
    Text = "Server CPU above 90%",
    Email = "ops@example.com"
};

var alertBytes = ProtobufEncoder.Encode(alert);
var decodedAlert = ProtobufEncoder.Decode<Alert>(alertBytes);
Console.WriteLine($"  Alert #{decodedAlert.Id}: via email={decodedAlert.Email}, sms={decodedAlert.Sms ?? "(none)"}");

// Custom text encoding on a contract enables full emoji support. Set
// DefaultEncoding to "utf-8" on the contract and emoji round-trips cleanly.

Console.WriteLine("\nCustom encoding with emoji...");

var chat = new ChatMessage
{
    Author = "Alice",
    Text = "Deploying now \U0001F680 wish me luck \U0001F340"
};

var chatBytes = ProtobufEncoder.Encode(chat);
var decodedChat = ProtobufEncoder.Decode<ChatMessage>(chatBytes);
Console.WriteLine($"  {decodedChat.Author}: {decodedChat.Text}");

// ProtoValue encodes and decodes standalone values without a contract.
// Useful for configuration flags, counters, or single-field payloads.

Console.WriteLine("\nProtoValue (bare values)...");

var encStr = ProtoValue.Encode("Hello from ProtoValue");
var encInt = ProtoValue.Encode(42);
var encGuid = ProtoValue.Encode(Guid.NewGuid());
var encDate = ProtoValue.Encode(DateTime.UtcNow);

Console.WriteLine($"  string: {ProtoValue.DecodeString(encStr)} ({encStr.Length} bytes)");
Console.WriteLine($"  int:    {ProtoValue.DecodeInt32(encInt)}");
Console.WriteLine($"  guid:   {ProtoValue.DecodeGuid(encGuid)}");
Console.WriteLine($"  date:   {ProtoValue.DecodeDateTime(encDate):yyyy-MM-dd HH:mm:ss}");

// ProtoMessage builds messages dynamically without any contract class.
// Fields are set by number and retrieved by number. Nested ProtoMessages
// are supported too.

Console.WriteLine("\nProtoMessage (dynamic, no contract needed)...");

var msg = new ProtoMessage()
    .Set(1, "ProtobuffEncoder")
    .Set(2, 42)
    .Set(3, true)
    .Set(4, DateTime.UtcNow)
    .Set(5, new ProtoMessage()
        .Set(1, "nested-child")
        .Set(2, 100));

var msgBytes = msg.ToBytes();
var decodedMsg = ProtoMessage.FromBytes(msgBytes);
Console.WriteLine($"  {decodedMsg.FieldCount} fields, {msgBytes.Length} bytes");
Console.WriteLine($"  Field 1: {decodedMsg.GetString(1)}");
Console.WriteLine($"  Field 2: {decodedMsg.Get<int>(2)}");
Console.WriteLine($"  Field 5 (nested): {decodedMsg.GetMessage(5)?.GetString(1)}");

// The service interface below defines a chat service with Unary and
// DuplexStreaming methods. You would implement and host this through the
// gRPC integration package.

Console.WriteLine("\nService interface declared: IChatService");
Console.WriteLine("  Send(ChatMessage)    -> ChatReply        [Unary]");
Console.WriteLine("  LiveChat(stream)     -> stream           [DuplexStreaming]");

Console.WriteLine("\nDone.");
