// ProtobuffEncoder — Normal Template
//
// Collections, maps, nullable fields, OneOf groups, custom encoding,
// ProtoValue, ProtoMessage, and a chat service interface.
//
// Contracts live in Contracts/, the service interface in Services/.
//
// Run with: dotnet run

using ProtobuffEncoder;
using ProtobuffEncoder.Template.Normal.Contracts;

Console.WriteLine("ProtobuffEncoder — Normal Template\n");

// Collections and maps.

var team = new Team
{
    Name = "Platform",
    Members = ["Alice", "Bob", "Charlie"],
    Scores = new Dictionary<string, int>
    {
        ["Alice"] = 95, ["Bob"] = 87, ["Charlie"] = 92
    }
};

var teamBytes = ProtobufEncoder.Encode(team);
var decodedTeam = ProtobufEncoder.Decode<Team>(teamBytes);

Console.WriteLine($"Team: {decodedTeam.Name}, {decodedTeam.Members.Count} members");
foreach (var (name, score) in decodedTeam.Scores)
    Console.WriteLine($"  {name}: {score}");

// Nullable and required fields.

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

// OneOf groups — mutually exclusive fields.

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

// Custom encoding with emoji.

Console.WriteLine("\nCustom encoding with emoji...");

var chat = new ChatMessage
{
    Author = "Alice",
    Text = "Deploying now \U0001F680 wish me luck \U0001F340"
};

var chatBytes = ProtobufEncoder.Encode(chat);
var decodedChat = ProtobufEncoder.Decode<ChatMessage>(chatBytes);
Console.WriteLine($"  {decodedChat.Author}: {decodedChat.Text}");

// ProtoValue — bare values without a contract.

Console.WriteLine("\nProtoValue (bare values)...");

var encStr = ProtoValue.Encode("Hello from ProtoValue");
var encInt = ProtoValue.Encode(42);
var encGuid = ProtoValue.Encode(Guid.NewGuid());
var encDate = ProtoValue.Encode(DateTime.UtcNow);

Console.WriteLine($"  string: {ProtoValue.DecodeString(encStr)} ({encStr.Length} bytes)");
Console.WriteLine($"  int:    {ProtoValue.DecodeInt32(encInt)}");
Console.WriteLine($"  guid:   {ProtoValue.DecodeGuid(encGuid)}");
Console.WriteLine($"  date:   {ProtoValue.DecodeDateTime(encDate):yyyy-MM-dd HH:mm:ss}");

// ProtoMessage — dynamic schema-less messages.

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

// The IChatService interface (in Services/) declares Unary and DuplexStreaming.

Console.WriteLine("\nService: IChatService (see Services/IChatService.cs)");
Console.WriteLine("  Send(ChatMessage)    -> ChatReply        [Unary]");
Console.WriteLine("  LiveChat(stream)     -> stream           [DuplexStreaming]");

Console.WriteLine("\nDone.");
