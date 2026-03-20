# Transport

The transport layer provides typed, stream-based communication primitives built on top of length-delimited protobuf framing.

## ProtobufSender<>

Sends protobuf-encoded messages over a stream with automatic length-delimited framing.

```C#
using ProtobuffEncoder.Transport;

await using var sender = new ProtobufSender<Person>(networkStream);

// Send one
await sender.SendAsync(person);
sender.Send(person); // synchronous

// Send many
await sender.SendManyAsync(people);
await sender.SendManyAsync(asyncStream); // IAsyncEnumerable<T>
```

## ProtobufReceiver<>

Receives protobuf-encoded messages from a stream.

```C#
await using var receiver = new ProtobufReceiver<Person>(networkStream);

// Read one (returns null at end of stream)
var msg = receiver.Receive();

// Async stream
await foreach (var person in receiver.ReceiveAllAsync(cancellationToken))
{
    Console.WriteLine(person.Name);
}

// Callback listener
await receiver.ListenAsync(async person =>
{
    Console.WriteLine($"Received: {person.Name}");
}, cancellationToken);
```

## ProtobufDuplexStream\<TSend, TReceive\>

Full bi-directional streaming over a single stream (or a pair of streams). Supports sending and receiving concurrently with thread-safe internal locking.

```C#
// Same type both directions
await using var duplex = new ProtobufDuplexStream<Message>(networkStream);

// Different types each direction
await using var duplex = new ProtobufDuplexStream<Request, Response>(
    sendStream, receiveStream);
```

### Send & Receive

```C#
await duplex.SendAsync(message);
duplex.Send(message); // synchronous

var response = await duplex.ReceiveAsync();
var response = duplex.Receive(); // synchronous

await foreach (var msg in duplex.ReceiveAllAsync(cancellationToken))
{
    // process messages as they arrive
}
```

### Request-Response

```C#
// Send a request and wait for a single response
var response = await duplex.SendAndReceiveAsync(request);
```

### Concurrent Duplex

```C#
// Send and receive concurrently
await duplex.RunDuplexAsync(
    outgoing: GenerateMessages(),
    onReceived: async msg => Console.WriteLine(msg.Text),
    cancellationToken
);
```

### Server-Side Processing

```C#
// Process incoming messages and send responses
await duplex.ProcessAsync(async request =>
{
    // Transform each request into a response
    return new Response { Result = Process(request) };
}, cancellationToken);
```

## Validation

The validation layer adds message validation to any transport primitive.

### ValidationPipeline<>

Build validation rules using predicates, delegates, or custom `IMessageValidator<T>` implementations.

```C#
var pipeline = new ValidationPipeline<Person>();

// Simple predicate rules
pipeline.Require(p => !string.IsNullOrEmpty(p.Name), "Name is required");
pipeline.Require(p => p.Age >= 0, "Age must be non-negative");

// Delegate rule
pipeline.Add(p => p.Age > 200
    ? ValidationResult.Fail("Unrealistic age")
    : ValidationResult.Success);

// Custom validator
pipeline.Add(new CustomPersonValidator());

// Validate
var result = pipeline.Validate(person); // returns ValidationResult
pipeline.ValidateOrThrow(person);       // throws MessageValidationException
```

### ValidatedProtobufSender<>

Validates messages before sending. Invalid messages throw `MessageValidationException` and are never written to the stream.

```C#
await using var sender = new ValidatedProtobufSender<Person>(stream);

sender.Validation
    .Require(p => !string.IsNullOrEmpty(p.Name), "Name is required")
    .Require(p => p.Age >= 0, "Age must be non-negative");

await sender.SendAsync(validPerson);    // OK
await sender.SendAsync(invalidPerson);  // throws MessageValidationException
```

### ValidatedProtobufReceiver<>

Validates messages after deserialization with configurable behavior for invalid messages.

```C#
await using var receiver = new ValidatedProtobufReceiver<Person>(stream);

receiver.Validation.Require(p => p.Age > 0, "Age required");

// Configure behavior for invalid messages (default: Throw)
receiver.OnInvalid = InvalidMessageBehavior.Skip;  // or Throw, ReturnNull

// Optional: get notified when messages are rejected
receiver.MessageRejected += (msg, result) =>
{
    Console.WriteLine($"Rejected: {result.ErrorMessage}");
};

await foreach (var person in receiver.ReceiveAllAsync())
{
    // Only valid messages arrive here
}
```

**InvalidMessageBehavior options:**

| Behavior | Effect |
|----------|--------|
| `Throw` | Throws `MessageValidationException` (default) |
| `Skip` | Skips the invalid message and continues |
| `ReturnNull` | Stops the stream |

### ValidatedDuplexStream\<TSend, TReceive\>

Combines bi-directional streaming with validation on both send and receive sides.

```C#
await using var duplex = new ValidatedDuplexStream<Request, Response>(
    sendStream, receiveStream);

// Validate outgoing
duplex.SendValidation.Require(r => !string.IsNullOrEmpty(r.Id), "Id required");

// Validate incoming
duplex.ReceiveValidation.Require(r => r.Status >= 0, "Invalid status");
duplex.OnInvalidReceive = InvalidMessageBehavior.Skip;

// Validated request-response
var response = await duplex.SendAndReceiveAsync(request);

// Validated bidirectional
await duplex.RunDuplexAsync(outgoing, onReceived, cancellationToken);
```

## Stream Ownership

All transport types accept an `ownsStream` parameter (default: `true`). When `true`, disposing the transport also disposes the underlying stream.

```C#
// Transport owns the stream — disposes it on cleanup
await using var sender = new ProtobufSender<T>(stream, ownsStream: true);

// Caller manages stream lifetime
await using var sender = new ProtobufSender<T>(stream, ownsStream: false);
```
