# Transport Layer

The transport layer provides typed, stream-based message passing built on top of length-delimited protobuf encoding. All transport classes are in `ProtobuffEncoder.Transport`.

## ProtobufSender

Sends protobuf-encoded messages over a stream using length-delimited framing.

```C#
public sealed class ProtobufSender<T> : IAsyncDisposable, IDisposable
    where T : class, new()
```

### API

| Method | Description |
|--------|-------------|
| `Send(T instance)` | Synchronous send with flush |
| `SendAsync(T instance, CancellationToken)` | Asynchronous send |
| `SendManyAsync(IEnumerable<T>, CancellationToken)` | Send batch |
| `SendManyAsync(IAsyncEnumerable<T>, CancellationToken)` | Send async stream |

### Example

```C#
await using var sender = new ProtobufSender<OrderMessage>(networkStream);

// Single message
await sender.SendAsync(order);

// Batch
await sender.SendManyAsync(orders);

// Async stream
await sender.SendManyAsync(GenerateOrdersAsync());
```

## ProtobufReceiver

Receives protobuf-encoded messages from a stream using length-delimited framing.

```C#
public sealed class ProtobufReceiver<T> : IAsyncDisposable, IDisposable
    where T : class, new()
```

### API

| Method | Description |
|--------|-------------|
| `Receive()` | Read single message (null at EOF) |
| `ReceiveAll()` | Read all messages as `IEnumerable<T>` |
| `ReceiveAllAsync(CancellationToken)` | Read all as `IAsyncEnumerable<T>` |
| `ListenAsync(Func<T, Task>, CancellationToken)` | Invoke handler per message |
| `ListenAsync(Action<T>, CancellationToken)` | Invoke sync handler per message |

### Example

```C#
await using var receiver = new ProtobufReceiver<OrderMessage>(networkStream);

// Read all
await foreach (var order in receiver.ReceiveAllAsync(ct))
    ProcessOrder(order);

// Or listen with callback
await receiver.ListenAsync(async order =>
{
    await SaveToDatabase(order);
}, ct);
```

## ProtobufDuplexStream

Bi-directional streaming channel. Supports sending and receiving simultaneously with thread-safe internal locks.

```C#
public sealed class ProtobufDuplexStream<TSend, TReceive> : IAsyncDisposable, IDisposable
```

### Constructors

```C#
// Single bi-directional stream (TCP, pipe)
new ProtobufDuplexStream<TSend, TReceive>(duplexStream, ownsStream: true)

// Separate send/receive streams
new ProtobufDuplexStream<TSend, TReceive>(sendStream, receiveStream, ownsStreams: true)
```

### API

| Method | Description |
|--------|-------------|
| `SendAsync(TSend, CancellationToken)` | Thread-safe send |
| `Send(TSend)` | Synchronous thread-safe send |
| `SendManyAsync(IAsyncEnumerable<TSend>, CancellationToken)` | Send stream |
| `ReceiveAsync(CancellationToken)` | Thread-safe receive (null at EOF) |
| `Receive()` | Synchronous receive |
| `ReceiveAllAsync(CancellationToken)` | Async enumerable of incoming messages |
| `ListenAsync(Func<TReceive, Task>, CancellationToken)` | Handler per message |
| `SendAndReceiveAsync(TSend, CancellationToken)` | Request-response pattern |
| `RunDuplexAsync(IAsyncEnumerable<TSend>, Func<TReceive, Task>, CancellationToken)` | Concurrent send + receive |
| `ProcessAsync(Func<TReceive, Task<TSend>>, CancellationToken)` | Transform incoming to outgoing |

### Convenience Alias

For same-type bidirectional streaming:

```C#
// ProtobufDuplexStream<T> wraps ProtobufDuplexStream<T, T>
await using var duplex = new ProtobufDuplexStream<ChatMessage>(tcpStream);
```

### Patterns

#### Request-Response

```C#
var response = await duplex.SendAndReceiveAsync(request);
```

#### Concurrent Bidirectional

```C#
await duplex.RunDuplexAsync(
    outgoing: GenerateRequestsAsync(),
    onReceived: async response =>
    {
        Console.WriteLine($"Got: {response.Data}");
    },
    ct);
```

#### Server-Side Processing

```C#
await duplex.ProcessAsync(async request =>
{
    var result = await ComputeResult(request);
    return new Response { Data = result };
}, ct);
```

## ProtobufValueSender

Sends single values and dynamic messages over a stream without requiring a `[ProtoContract]` class. Supports strings, booleans, integers, floats, dates, GUIDs, byte arrays, and `ProtoMessage` instances.

```C#
public sealed class ProtobufValueSender : IAsyncDisposable, IDisposable
```

### API

| Method | Description |
|--------|-------------|
| `Send(string value)` | Send a string (supports emoji with Unicode encodings) |
| `SendAsync(string, CancellationToken)` | Async string send |
| `Send(bool value)` | Send a boolean |
| `Send(int value)` | Send a 32-bit integer |
| `Send(long value)` | Send a 64-bit integer |
| `Send(double value)` | Send a double |
| `Send(float value)` | Send a float |
| `Send(DateTime value)` | Send a DateTime |
| `Send(Guid value)` | Send a GUID |
| `Send(byte[] value)` | Send raw bytes |
| `Send(ProtoMessage message)` | Send a dynamic message |
| `SendManyAsync(IAsyncEnumerable<string>, CancellationToken)` | Stream strings |
| `SendManyAsync(IAsyncEnumerable<ProtoMessage>, CancellationToken)` | Stream messages |

### Example

```C#
await using var sender = new ProtobufValueSender(networkStream, ProtoEncoding.UTF8);

// Send strings with emoji
await sender.SendAsync("Hello 🌍🎉");
await sender.SendAsync("こんにちは世界");

// Send other types directly
await sender.SendAsync(42);
await sender.SendAsync(true);
await sender.SendAsync(DateTime.UtcNow);
```

## ProtobufValueReceiver

Receives single values and dynamic messages from a stream.

```C#
public sealed class ProtobufValueReceiver : IAsyncDisposable, IDisposable
```

### API

| Method | Description |
|--------|-------------|
| `ReceiveString()` | Read a string (null at EOF) |
| `ReceiveBool()` | Read a boolean (null at EOF) |
| `ReceiveInt32()` | Read an int32 (null at EOF) |
| `ReceiveInt64()` | Read an int64 (null at EOF) |
| `ReceiveDouble()` | Read a double (null at EOF) |
| `ReceiveFloat()` | Read a float (null at EOF) |
| `ReceiveDateTime()` | Read a DateTime (null at EOF) |
| `ReceiveGuid()` | Read a GUID (null at EOF) |
| `ReceiveBytes()` | Read a byte array (null at EOF) |
| `ReceiveMessage()` | Read a ProtoMessage (null at EOF) |
| `ReceiveAllStrings()` | Read all strings as `IEnumerable<string>` |
| `ReceiveAllStringsAsync(CancellationToken)` | Read all as `IAsyncEnumerable<string>` |
| `ReceiveAllMessages()` | Read all as `IEnumerable<ProtoMessage>` |
| `ListenAsync(Func<string, Task>, CancellationToken)` | Callback per string |
| `ListenAsync(Func<ProtoMessage, Task>, CancellationToken)` | Callback per message |

### Example

```C#
await using var receiver = new ProtobufValueReceiver(networkStream, ProtoEncoding.UTF8);

// Read strings with emoji
await foreach (var text in receiver.ReceiveAllStringsAsync(ct))
    Console.WriteLine(text); // "Hello 🌍🎉", "こんにちは世界"

// Or use typed receivers
int? count = receiver.ReceiveInt32();
bool? flag = receiver.ReceiveBool();
```

## Stream Ownership

All transport classes accept an `ownsStream` / `ownsStreams` parameter:

- `true` (default): The transport disposes the underlying stream when disposed
- `false`: The caller retains ownership and is responsible for disposal

```C#
// Transport owns the stream
await using var sender = new ProtobufSender<T>(stream); // disposes stream

// Caller owns the stream
using var sender = new ProtobufSender<T>(stream, ownsStream: false);
// stream remains open after sender is disposed
```
