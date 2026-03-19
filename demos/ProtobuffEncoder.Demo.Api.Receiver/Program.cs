using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddProtobufFormatters();

// Load .proto schemas — the Receiver has ZERO compile-time reference to Contracts.
// It decodes requests and builds responses purely from the .proto schema + ProtobufWriter.
string protoDir = Path.Combine(AppContext.BaseDirectory, "protos");
builder.Services.AddSingleton(_ => SchemaDecoder.FromDirectory(protoDir));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

SchemaDecoder decoder = app.Services.GetRequiredService<SchemaDecoder>();
app.Logger.LogInformation("Loaded proto schemas: {Messages}", string.Join(", ", decoder.RegisteredMessages));

// --- Weather endpoint: schema-only decode + raw protobuf response ---
app.MapPost("/api/weather", async (HttpContext ctx, SchemaDecoder schema) =>
{
    byte[] bytes = await ReadBodyAsync(ctx);
    DecodedMessage request = schema.Decode("WeatherRequest", bytes);

    string city = request.Get<string>("City") ?? "Unknown";
    int days = request["Days"] is long d ? (int)d : 3;
    bool includeHourly = request["IncludeHourly"] is true;

    app.Logger.LogInformation("Weather request: city={City}, days={Days}, hourly={Hourly}", city, days, includeHourly);

    string[] conditions = ["Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy", "Windy"];

    var forecasts = new List<ProtobufWriter>();
    for (int i = 0; i < days; i++)
    {
        var f = new ProtobufWriter();
        f.WriteString(1, DateTime.UtcNow.AddDays(i).ToString("yyyy-MM-dd"));
        f.WriteDouble(2, Math.Round(Random.Shared.NextDouble() * 15 - 5, 1));
        f.WriteDouble(3, Math.Round(Random.Shared.NextDouble() * 20 + 10, 1));
        f.WriteString(4, conditions[Random.Shared.Next(conditions.Length)]);
        f.WriteVarint(5, Random.Shared.Next(30, 95));
        if (includeHourly)
            f.WriteDouble(6, Math.Round(Random.Shared.NextDouble() * 50, 1));
        forecasts.Add(f);
    }

    var response = new ProtobufWriter();
    response.WriteString(1, city);
    response.WriteRepeatedMessage(2, forecasts);
    response.WriteFixed64(3, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    return Results.Bytes(response.ToByteArray(), ProtobufMediaType.Protobuf);
});

/*
 *
 service EquipmentGrpcService {
     rpc Create(CreateRequest) returns (CreatedEquipmentResponse);
     rpc GetById(GetByIdRequest) returns (CreatedEquipmentIdResponse);
     rpc GetByAreaId(GetEquipmentByAreaIdRequest) returns (CollectionByIdEquipmentResponse);
     rpc GetAll(GetAllEquipmentRequest) returns (CollectionEquipmentResponse);
     rpc Update(UpdateEquipmentRequest) returns (UpdatedEquipmentResponse);
     rpc Delete(DeleteEquipmentRequest) returns (RemovedEquipmentResponse);
   }

equipment.proto

message CreateEquipmentResponse:
   EquipmentBody equipmentBody

message CreateByIdEquipmentResponse:
   EquipmentBody equipmentBody

message CreateEquipmentResponse:
   repeated EquipmentBody equipmentBody: 1,

message EquipmentBody:
 string id
 AnyValue value 

 */


// --- Notification endpoint: schema-only decode + raw protobuf ACK ---
app.MapPost("/api/notifications", async (HttpContext ctx, SchemaDecoder schema) =>
{
    byte[] bytes = await ReadBodyAsync(ctx);
    DecodedMessage notification = schema.Decode("NotificationMessage", bytes);

    string source = notification.Get<string>("Source") ?? "";
    string text = notification.Get<string>("Text") ?? "";
    string level = notification.Get<string>("Level") ?? "Info";
    List<string> tags = notification.GetRepeated<string>("Tags");

    app.Logger.LogInformation(
        "[{Level}] from {Source}: {Text} (tags: {Tags})",
        level, source, text, string.Join(", ", tags));

    var ack = new ProtobufWriter();
    ack.WriteBool(1, true);
    ack.WriteString(2, Guid.NewGuid().ToString("N"));

    return Results.Bytes(ack.ToByteArray(), ProtobufMediaType.Protobuf);
});

// --- Dashboard API: schema info ---
app.MapGet("/api/schema", (SchemaDecoder schema) =>
{
    return Results.Ok(new
    {
        messages = schema.RegisteredMessages,
        enums = schema.RegisteredEnums,
        protoDirectory = protoDir
    });
});

// --- Dashboard API: message/enum detail ---
app.MapGet("/api/schema/{name}", (string name, SchemaDecoder schema) =>
{
    var msg = schema.GetMessage(name);
    if (msg is not null)
    {
        return Results.Ok(new
        {
            type = "message",
            msg.Name,
            fields = msg.Fields.Select(f => new
            {
                f.Name,
                number = f.FieldNumber,
                type = f.TypeName,
                f.IsRepeated,
                f.IsOptional,
                f.IsMap,
                f.MapKeyType,
                f.MapValueType
            })
        });
    }

    var enumDef = schema.GetEnum(name);
    if (enumDef is not null)
    {
        return Results.Ok(new
        {
            type = "enum",
            enumDef.Name,
            values = enumDef.Values.Select(v => new { v.Name, v.Number })
        });
    }

    return Results.NotFound();
});

// --- Dashboard API: raw proto source ---
app.MapGet("/api/proto-source", () =>
{
    var protoFiles = Directory.GetFiles(protoDir, "*.proto");
    if (protoFiles.Length == 0) return Results.NotFound();

    var content = string.Join("\n\n// --- next file ---\n\n",
        protoFiles.Select(File.ReadAllText));
    return Results.Text(content, "text/plain");
});

app.MapGet("/health", () => Results.Ok("Receiver is running"));

app.Run();

static async Task<byte[]> ReadBodyAsync(HttpContext ctx)
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    return ms.ToArray();
}
