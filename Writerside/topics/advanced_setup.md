# Advanced Setup

Advanced setup is designed for high-performance scenarios or when complex object hierarchies must be supported.

## Custom Contract Resolvers

If you need to serialize types from third-party libraries or apply non-standard metadata, you can implement a custom `IContractResolver`.

```csharp
public class MyResolver : IContractResolver
{
    public MessageDescriptor Resolve(Type type) => // custom logic here
}

// Register in DI
builder.Services.AddSingleton<IContractResolver, MyResolver>();
```

## Low-Level Manual Writing

For ultra-hot paths where you want to avoid almost all overhead, you can use the `ProtobufWriter` directly to write to any stream.

```csharp
app.MapPost("/high-perf", async (HttpContext context) => 
{
    await using var writer = new ProtobufWriter(context.Response.Body);
    writer.WriteString(1, "Direct String");
    writer.WriteInt32(2, 123);
    // No reflection, no intermediate object allocations
});
```

## Polymorphism ([ProtoInclude])

Support for inheritance is handled via the `[ProtoInclude]` attribute on base classes, similar to standard Protobuf-net patterns.

```csharp
[ProtoContract]
[ProtoInclude(10, typeof(DerivedType))]
public class BaseType { ... }
```

---

*For full source code, see [Program_Advanced.cs](file:///c:/Development/ProtobuffEncoder/demos/ProtobuffEncoder.Demo.Setup/Program_Advanced.cs)*
