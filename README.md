# ProtobuffEncoder

A high-performance Protobuf binary wire format encoder and decoder for .NET 8, 9, and 10.

## Features

- **High Performance**: Optimized for speed and low allocations.
- **Multi-Targeting**: Native support for .NET 8.0, 9.0, and 10.0.
- **Full Protobuf Support**: Scalars, nullable types, collections, maps, nested messages, oneof, inheritance, and more.
- **Streaming**: Built-in support for length-delimited streaming.
- **Static Message Delegates**: Pre-compiled encoders and decoders for maximum performance.
- **AOT Friendly**: Designed with AOT compatibility in mind.

## Quick Start

```csharp
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

[ProtoContract]
public class MyMessage
{
    [ProtoField(1)]
    public int Id { get; set; }

    [ProtoField(2)]
    public string Name { get; set; }
}

var message = new MyMessage { Id = 1, Name = "Hello Protobuf" };

// Encode
byte[] data = ProtobufEncoder.Encode(message);

// Decode
var decoded = ProtobufEncoder.Decode<MyMessage>(data);
```

## Installation

```bash
dotnet add package ProtobuffEncoder
```

## License

MIT
