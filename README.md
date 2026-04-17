# ProtobuffEncoder

A high-performance Protobuf binary wire format encoder and decoder for .NET 8, 9, and 10.

## Features

- High-performance core encoding, decoding, and streaming primitives.
- Multi-targeting for .NET 8.0, 9.0, and 10.0.
- Schema generation and parsing for messages, enums, maps, oneof, and services.
- ASP.NET Core integration for REST, WebSocket, and gRPC transports.
- Fluent setup API with configuration-bound `ProtobufEncoderOptions` support.

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
    public string Name { get; set; } = "";
}

var message = new MyMessage { Id = 1, Name = "Hello Protobuf" };
byte[] data = ProtobufEncoder.Encode(message);
var decoded = ProtobufEncoder.Decode<MyMessage>(data);
```

## ASP.NET Core Setup

```csharp
using ProtobuffEncoder.AspNetCore.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(builder.Configuration)
    .WithRestFormatters();
```

The configuration overload binds the default `ProtobuffEncoder` section while preserving the existing
fluent builder for transport registration.

## Installation

```bash
dotnet add package ProtobuffEncoder
```

## License

MIT
