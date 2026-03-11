using ProtobuffEncoder;
using ProtobuffEncoder.Console;
using ProtobuffEncoder.Transport;

// --- Basic encode/decode with complex types ---
var person = new Person
{
    Name = "Alice",
    Email = "alice@example.com",
    Age = 30,
    InternalNotes = "should not appear in output",
    HomeAddress = new Address
    {
        Street = "123 Main St",
        City = "Springfield",
        ZipCode = 62701
    },
    Score = 98.5,
    Type = ContactType.Work,
    Tags = ["developer", "lead"],
    LuckyNumbers = [7, 13, 42],
    PhoneNumbers =
    [
        new PhoneNumber { Number = "+1-555-0100", Type = ContactType.Work },
        new PhoneNumber { Number = "+1-555-0101", Type = ContactType.Personal }
    ]
};

byte[] encoded = ProtobufEncoder.Encode(person);
System.Console.WriteLine($"Encoded {encoded.Length} bytes");

var decoded = ProtobufEncoder.Decode<Person>(encoded);
System.Console.WriteLine($"Name:      {decoded.Name}");
System.Console.WriteLine($"Email:     {decoded.Email}");
System.Console.WriteLine($"Age:       {decoded.Age}");
System.Console.WriteLine($"Score:     {decoded.Score}");
System.Console.WriteLine($"Type:      {decoded.Type}");
System.Console.WriteLine($"Tags:      [{string.Join(", ", decoded.Tags)}]");
System.Console.WriteLine($"Lucky:     [{string.Join(", ", decoded.LuckyNumbers.Select(n => n.ToString()))}]");
System.Console.WriteLine($"Phones:    {decoded.PhoneNumbers.Count}");
foreach (var p in decoded.PhoneNumbers)
    System.Console.WriteLine($"           {p.Number} ({p.Type})");
System.Console.WriteLine($"Address:   {decoded.HomeAddress?.Street}, {decoded.HomeAddress?.City} {decoded.HomeAddress?.ZipCode}");
System.Console.WriteLine($"Notes:     '{decoded.InternalNotes}' (should be empty)");

// --- Static message (pre-compiled) ---
System.Console.WriteLine("\n--- Static Message ---");
var staticMsg = ProtobufEncoder.CreateStaticMessage<Person>();
byte[] fast = staticMsg.Encode(person);
var back = staticMsg.Decode(fast);
System.Console.WriteLine($"Static encode/decode: {back.Name}, tags=[{string.Join(", ", back.Tags)}]");

// --- Streamed delimited messages ---
System.Console.WriteLine("\n--- Streamed Messages ---");
using var stream = new MemoryStream();

ProtobufEncoder.WriteDelimitedMessage(
    new Person { Name = "Bob", Age = 25, Tags = ["admin"] }, stream);
ProtobufEncoder.WriteDelimitedMessage(
    new Person { Name = "Carol", Age = 40, Score = 77.3 }, stream);

stream.Position = 0;
foreach (var msg in ProtobufEncoder.ReadDelimitedMessages<Person>(stream))
{
    System.Console.WriteLine($"  Streamed: {msg.Name}, age={msg.Age}, tags=[{string.Join(", ", msg.Tags)}], score={msg.Score}");
}

// --- Async streamed messages ---
System.Console.WriteLine("\n--- Async Streamed ---");
using var asyncStream = new MemoryStream();

await ProtobufEncoder.WriteDelimitedMessageAsync(
    new Person { Name = "Dave", Age = 35, LuckyNumbers = [1, 2, 3] }, asyncStream);
await ProtobufEncoder.WriteDelimitedMessageAsync(
    new Person { Name = "Eve", Age = 28 }, asyncStream);

asyncStream.Position = 0;
await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<Person>(asyncStream))
{
    System.Console.WriteLine($"  Async: {msg.Name}, age={msg.Age}, lucky=[{string.Join(", ", msg.LuckyNumbers)}]");
}

// --- Bi-directional streaming ---
System.Console.WriteLine("\n--- Bi-Directional Streaming ---");
using var sendPipe = new MemoryStream();
using var recvPipe = new MemoryStream();

// Simulate server: write two responses into recvPipe
ProtobufEncoder.WriteDelimitedMessage(new Person { Name = "Server-Alice", Age = 30 }, recvPipe);
ProtobufEncoder.WriteDelimitedMessage(new Person { Name = "Server-Bob", Age = 25 }, recvPipe);
recvPipe.Position = 0;

await using var duplex = new ProtobufDuplexStream<Person>(sendPipe, recvPipe, ownsStreams: false);

// Send two requests
await duplex.SendAsync(new Person { Name = "Client-Request-1", Age = 1 });
await duplex.SendAsync(new Person { Name = "Client-Request-2", Age = 2 });

// Receive the two server responses
await foreach (var response in duplex.ReceiveAllAsync())
{
    System.Console.WriteLine($"  Received: {response.Name}, age={response.Age}");
}

// Verify what was sent
sendPipe.Position = 0;
foreach (var sent in ProtobufEncoder.ReadDelimitedMessages<Person>(sendPipe))
{
    System.Console.WriteLine($"  Sent:     {sent.Name}, age={sent.Age}");
}

// --- Validated Sender/Receiver ---
System.Console.WriteLine("\n--- Validated Transport ---");
using var validStream = new MemoryStream();

await using var validSender = new ValidatedProtobufSender<Person>(validStream, ownsStream: false);
validSender.Validation
    .Require(p => !string.IsNullOrEmpty(p.Name), "Name is required")
    .Require(p => p.Age >= 0, "Age must be non-negative");

// Valid message — should succeed
await validSender.SendAsync(new Person { Name = "ValidPerson", Age = 25 });
System.Console.WriteLine("  Sent valid message OK");

// Invalid message — should throw
try
{
    await validSender.SendAsync(new Person { Name = "", Age = 25 });
}
catch (MessageValidationException ex)
{
    System.Console.WriteLine("  Rejected send: " + ex.Message);
}

// Read back with validated receiver that skips invalid
validStream.Position = 0;
await using var validReceiver = new ValidatedProtobufReceiver<Person>(validStream, ownsStream: false);
validReceiver.Validation.Require(p => p.Age > 0, "Age must be positive");
validReceiver.OnInvalid = InvalidMessageBehavior.Skip;
validReceiver.MessageRejected += (msg, result) =>
    System.Console.WriteLine("  Receiver skipped: " + msg.Name + " — " + (result.ErrorMessage ?? ""));

await foreach (var msg in validReceiver.ReceiveAllAsync())
{
    System.Console.WriteLine($"  Received valid: {msg.Name}, age={msg.Age}");
}

// --- Duplex with validation ---
System.Console.WriteLine("\n--- Validated Duplex Stream ---");
using var duplexSend = new MemoryStream();
using var duplexRecv = new MemoryStream();

// Pre-fill receive side
ProtobufEncoder.WriteDelimitedMessage(new Person { Name = "Reply", Age = 42 }, duplexRecv);
duplexRecv.Position = 0;

await using var validDuplex = new ValidatedDuplexStream<Person, Person>(duplexSend, duplexRecv, ownsStreams: false);
validDuplex.SendValidation.Require(p => !string.IsNullOrEmpty(p.Name), "Name required on send");
validDuplex.ReceiveValidation.Require(p => p.Age > 0, "Age must be positive on receive");

var reply = await validDuplex.SendAndReceiveAsync(new Person { Name = "Question", Age = 1 });
System.Console.WriteLine($"  Duplex reply: {reply?.Name ?? "null"}, age={reply?.Age.ToString() ?? "null"}");
