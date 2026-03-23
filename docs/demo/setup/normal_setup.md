# Normal Setup

Normal setup involves basic customization of the serialization process and adding validation rules to your pipeline.

## Customizing Options

You can configure global serialization options such as naming conventions or formatting during DI registration.

```csharp
builder.Services.AddProtobuffEncoder(options => 
{
    options.UseCamelCase = true;
});
```

## Adding Validation

The validation pipeline allows you to enforce rules on your models before they are processed by your business logic.

```csharp
builder.Services.AddProtobufValidation(registry => 
{
    registry.AddRule<DemoRequest>(req => !string.IsNullOrEmpty(req.Name), "Name is required");
    registry.AddRule<DemoRequest>(req => req.Value > 0, "Value must be positive");
});
```

## Client-Side Support

The library provides extensions for `HttpClient` to simplify sending and receiving protobuf messages.

```csharp
var client = new HttpClient();
var response = await client.PostProtobufAsync(url, myRequest);
var result = await response.Content.ReadAsProtobufAsync<MyResponse>();
```

---

*For full source code, see [Program_Normal.cs](file:///c:/Development/ProtobuffEncoder/demos/ProtobuffEncoder.Demo.Setup/Program_Normal.cs)*
