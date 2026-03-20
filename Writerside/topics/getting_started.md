# Getting Started

## Installation

Add the core package to your project:

```bash
dotnet add package ProtobuffEncoder
```

For ASP.NET Core integration:

```bash
dotnet add package ProtobuffEncoder.AspNetCore
```

For gRPC support:

```bash
dotnet add package ProtobuffEncoder.Grpc
```

For WebSocket support:

```bash
dotnet add package ProtobuffEncoder.WebSockets
```

## Your First Contract

Mark any class with `[ProtoContract]` and its properties with `[ProtoField(N)]`:

```C#
using ProtobuffEncoder.Attributes;

[ProtoContract]
public class Person
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
    [ProtoField(3)] public string Email { get; set; } = "";
    [ProtoField(4)] public int Age { get; set; }
}
```

## Encode and Decode

```C#
using ProtobuffEncoder;

// Encode to bytes
var person = new Person { Id = 1, Name = "Alice", Email = "alice@example.com", Age = 30 };
byte[] bytes = ProtobufEncoder.Encode(person);

// Decode from bytes
Person decoded = ProtobufEncoder.Decode<Person>(bytes);

Console.WriteLine(decoded.Name); // "Alice"
```

## Async Streaming

Write and read length-delimited messages over any `Stream`:

```C#
// Write multiple messages
await using var file = File.Create("people.bin");
foreach (var p in people)
    await ProtobufEncoder.WriteDelimitedMessageAsync(p, file);

// Read them back
await using var reader = File.OpenRead("people.bin");
await foreach (var p in ProtobufEncoder.ReadDelimitedMessagesAsync<Person>(reader))
    Console.WriteLine(p.Name);
```

## Static Messages (Pre-compiled)

For hot-path serialization, create a `StaticMessage` that caches reflection lookups:

```C#
var msg = ProtobufEncoder.CreateStaticMessage<Person>();

// Fast encode/decode
byte[] bytes = msg.Encode(person);
Person decoded = msg.Decode(bytes);

// Delimited streaming
msg.WriteDelimited(person, stream);
Person? next = msg.ReadDelimited(stream);
```

## Generate .proto Schema

```C#
using ProtobuffEncoder.Schema;

string proto = ProtoSchemaGenerator.Generate(typeof(Person));
Console.WriteLine(proto);
```

Output:

```protobuf
syntax = "proto3";

package MyApp.Models;

message Person {
  int32 Id = 1;
  string Name = 2;
  string Email = 3;
  int32 Age = 4;
}
```

## Transport Layer

Use typed senders and receivers for structured streaming:

```C#
using ProtobuffEncoder.Transport;

// Sender
await using var sender = new ProtobufSender<Person>(networkStream);
await sender.SendAsync(person);
await sender.SendManyAsync(people);

// Receiver
await using var receiver = new ProtobufReceiver<Person>(networkStream);
await foreach (var p in receiver.ReceiveAllAsync())
    Console.WriteLine(p.Name);

// Bi-directional
await using var duplex = new ProtobufDuplexStream<Request, Response>(tcpStream);
var response = await duplex.SendAndReceiveAsync(request);
```

## ASP.NET Core Setup

```C#
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
})
.WithRestFormatters()
.WithWebSocket(ws => ws.AddEndpoint<Message, Message>())
.WithGrpc(grpc => grpc.AddService<MyGrpcServiceImpl>());

var app = builder.Build();
```

## Next Steps

- [Attributes Reference](attributes_reference.md) -- all attributes and their options
- [Serialization Deep Dive](serialization_deep_dive.md) -- wire format details
- [Transport Layer](transport_layer.md) -- streaming patterns
- [ASP.NET Core Integration](aspnetcore_integration.md) -- REST, WebSocket, gRPC setup
