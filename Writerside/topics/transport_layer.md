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
