using ProtobuffEncoder.Demo.Setup.Models;
using ProtobuffEncoder.Contracts;
using ProtobuffEncoder.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --- ADVANCED SETUP ---
// 1. Custom Contract Resolver for obscure types or external libraries
builder.Services.AddSingleton<IContractResolver, MyAdvancedResolver>();

// 2. Manual Writer Usage for super high performance
// (Showcasing how to bypass normal reflection-based paths)

var app = builder.Build();

app.MapPost("/api/advanced/manual", (HttpContext context) => 
{
    // High-performance manual writing directly to response stream
    var writer = new ProtobufWriter(context.Response.Body);
    writer.WriteString(1, "High-Performance Response");
    writer.WriteInt32(2, 42);
    return Task.CompletedTask;
});

app.Run();

public class MyAdvancedResolver : IContractResolver
{
    // Implementation that handles special types or custom naming
    public MessageDescriptor Resolve(Type type) => null; // Dummy placeholder
}
