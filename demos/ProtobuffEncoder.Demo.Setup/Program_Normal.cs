using ProtobuffEncoder.Demo.Setup.Models;
using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.WebSockets;
using ProtobuffEncoder.Validation;

var builder = WebApplication.CreateBuilder(args);

// --- NORMAL SETUP ---
// 1. Add ProtobuffEncoder with Options
builder.Services.AddProtobuffEncoder(options => 
{
    options.UseCamelCase = true; // Example of a potentially existing option
});

// 2. Add Validation Rules
// Example: Validate that Name is not empty and Value is positive
builder.Services.AddProtobufValidation(registry => 
{
    registry.AddRule<DemoRequest>(req => !string.IsNullOrEmpty(req.Name), "Name cannot be empty");
    registry.AddRule<DemoRequest>(req => req.Value >= 0, "Value must be positive");
});

// 3. Add REST with Formatters (Normal usage)
builder.Services.AddControllers()
    .AddProtobufFormatters();

var app = builder.Build();

// ... use validated senders/receivers in endpoints ...
app.MapPost("/api/normal/validated", (DemoRequest request, IProtobufValidator validator) => 
{
    var result = validator.Validate(request);
    if (!result.IsValid) return Results.BadRequest(result.Errors);
    
    return Results.Ok(new DemoResponse { Message = "Validated!" });
});

app.Run();
