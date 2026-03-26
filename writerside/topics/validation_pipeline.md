# Validation Pipeline

The validation pipeline provides a composable, rule-based validation system for protobuf messages. It can be applied to both outgoing (pre-send) and incoming (post-receive) messages.

## ValidationPipeline

```C#
public sealed class ValidationPipeline<T>
```

### API

| Method | Description |
|--------|-------------|
| `Add(IMessageValidator<T>)` | Add a custom validator |
| `Add(Func<T, ValidationResult>)` | Add a delegate-based rule |
| `Require(Func<T, bool>, string)` | Add a predicate rule |
| `Validate(T)` | Run all validators, return first failure or success |
| `ValidateOrThrow(T)` | Run all validators, throw on failure |
| `HasValidators` | Whether any validators are registered |

### Example {id="pipeline-example"}

```C#
var pipeline = new ValidationPipeline<OrderMessage>();

// Simple predicate rules
pipeline.Require(m => m.OrderId > 0, "OrderId must be positive");
pipeline.Require(m => !string.IsNullOrEmpty(m.CustomerName), "CustomerName is required");
pipeline.Require(m => m.Total >= 0, "Total cannot be negative");

// Delegate-based rule with custom logic
pipeline.Add(m =>
{
    if (m.Items.Count == 0)
        return ValidationResult.Fail("Order must have at least one item");
    return ValidationResult.Success;
});

// Validate
var result = pipeline.Validate(order);
if (!result.IsValid)
    Console.WriteLine($"Invalid: {result.ErrorMessage}");
```

## ValidationResult

```C#
public readonly struct ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public static ValidationResult Success { get; }
    public static ValidationResult Fail(string message);
}
```

## IMessageValidator

Custom validator interface:

```C#
public interface IMessageValidator<in T>
{
    ValidationResult Validate(T message);
}
```

### Example {id="validated-transport-example"}

```C#
public class OrderTotalValidator : IMessageValidator<OrderMessage>
{
    public ValidationResult Validate(OrderMessage message)
    {
        var computed = message.Items.Sum(i => i.Price * i.Quantity);
        return Math.Abs(computed - message.Total) < 0.01m
            ? ValidationResult.Success
            : ValidationResult.Fail($"Total mismatch: expected {computed}, got {message.Total}");
    }
}

pipeline.Add(new OrderTotalValidator());
```

## InvalidMessageBehavior

Controls what happens when a received message fails validation:

| Behavior | Description |
|----------|-------------|
| `Throw` | Throws `MessageValidationException` (default for raw receivers) |
| `Skip` | Silently skips invalid messages, continues to next (default for WebSocket) |
| `ReturnNull` | Returns `null` / ends the stream |

## ValidatedProtobufSender

Wraps a `ProtobufSender<T>` with outgoing validation. Invalid messages throw `MessageValidationException` and are never written to the stream.

```C#
await using var sender = new ValidatedProtobufSender<OrderMessage>(stream);

// Configure validation rules
sender.Validation.Require(m => m.OrderId > 0, "OrderId required");
sender.Validation.Require(m => m.Total >= 0, "Total must be non-negative");

// This throws if validation fails
sender.Send(invalidOrder); // MessageValidationException
```

## ValidatedProtobufReceiver

Wraps a `ProtobufReceiver<T>` with incoming validation and configurable failure behavior.

```C#
await using var receiver = new ValidatedProtobufReceiver<OrderMessage>(stream);

// Configure validation
receiver.Validation.Require(m => m.OrderId > 0, "OrderId required");

// Configure behavior for invalid messages
receiver.OnInvalid = InvalidMessageBehavior.Skip;

// Optional: handle rejected messages
receiver.MessageRejected += (message, result) =>
{
    logger.Warning($"Rejected: {result.ErrorMessage}");
};

// All invalid messages are silently skipped
await foreach (var order in receiver.ReceiveAllAsync(ct))
    ProcessOrder(order); // only valid orders reach here
```

## ValidatedDuplexStream

Combines bi-directional streaming with validation on both send and receive sides.

```C#
await using var duplex = new ValidatedDuplexStream<Request, Response>(stream);

// Send-side validation
duplex.SendValidation.Require(r => r.Id > 0, "Request Id required");

// Receive-side validation
duplex.ReceiveValidation.Require(r => r.StatusCode >= 200, "Invalid status");
duplex.OnInvalidReceive = InvalidMessageBehavior.Skip;

// Rejected message event
duplex.MessageRejected += (response, result) =>
{
    logger.Warning($"Bad response: {result.ErrorMessage}");
};

// Use normally - validation is transparent
var response = await duplex.SendAndReceiveAsync(request);
```

## ASP.NET Core Registration

To enable validation in an ASP.NET Core application, use the `AddProtobufValidation` extension on the `IServiceCollection` or within the `AddProtobufEncoder` builder:

```C#
// Simple setup
builder.Services.AddProtobufValidation();

// Advanced setup with builder
builder.Services.AddProtobufEncoder(options => { ... })
       .AddProtobufValidation();
```

This registers the necessary `IProtobufValidator` services and enables the tiered validation strategies for REST, WebSockets, and gRPC.

### ValidatedDuplexStream API

| Property / Method | Description |
|-------------------|-------------|
| `SendValidation` | `ValidationPipeline<TSend>` for outgoing messages |
| `ReceiveValidation` | `ValidationPipeline<TReceive>` for incoming messages |
| `OnInvalidReceive` | `InvalidMessageBehavior` for receive failures |
| `MessageRejected` | Event fired on rejected received messages |
| `SendAsync` / `Send` | Validated send |
| `ReceiveAsync` | Validated receive with behavior |
| `SendAndReceiveAsync` | Validated request-response |
| `RunDuplexAsync` | Validated concurrent bidirectional |
| `ProcessAsync` | Validated transform pipeline |

## MessageValidationException

Thrown when a message fails validation (when behavior is `Throw`):

```C#
public class MessageValidationException : Exception
{
    public object? InvalidMessage { get; }
    // message contains the validation error message
}
```

