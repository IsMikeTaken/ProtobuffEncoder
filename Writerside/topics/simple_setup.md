# Simple Setup

The simplest way to integrate ProtobuffEncoder into an ASP.NET Core application is to use the default extension methods.

## Service Registration

Add the protobuf formatters to your MVC controllers or Minimal APIs:

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. REST Support
builder.Services.AddControllers()
    .AddProtobufFormatters();

// 2. WebSocket Support (Optional)
builder.Services.AddProtobufWebSocketEndpoint<Req, Res>();

// 3. gRPC Support (Optional)
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc.AddService<MyService>());
```

## Usage

### Minimal API
Minimal APIs automatically use the registered formatters if the `Content-Type` is set to `application/x-protobuf`.

```csharp
app.MapPost("/echo", (MyMessage msg) => msg);
```

### Controllers
Decorate your action methods normally; the `ProtobufInputFormatter` and `ProtobufOutputFormatter` will handle the rest.

```csharp
[ApiController]
[Route("[controller]")]
public class MyController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] MyMessage msg) => Ok(msg);
}
```

### WebSockets
Map your protobuf WebSocket endpoints using the extension methods:

```csharp
app.MapProtobufWebSocket<Res, Req>("/ws/echo");
```

---

*For full source code, see [Program_Simple.cs](file:///c:/Development/ProtobuffEncoder/demos/ProtobuffEncoder.Demo.Setup/Program_Simple.cs)*
