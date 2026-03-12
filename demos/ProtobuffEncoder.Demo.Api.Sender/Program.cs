using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ReceiverApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5100");
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// --- Request weather from the Receiver API ---
app.MapGet("/api/send-weather", async (
    string city,
    int days,
    bool? includeHourly,
    IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("ReceiverApi");

    var request = new WeatherRequest
    {
        City = city,
        Days = days,
        IncludeHourly = includeHourly ?? false
    };

    var response = await client.PostProtobufAsync<WeatherRequest, WeatherResponse>("/api/weather", request);

    // Return as JSON for easy inspection
    return Results.Ok(new
    {
        response.City,
        response.GeneratedAtUtc,
        Forecasts = response.Forecasts.Select(forecast => new
        {
            forecast.Date,
            forecast.TemperatureMin,
            forecast.TemperatureMax,
            forecast.Condition,
            forecast.HumidityPercent,
            forecast.WindSpeed
        })
    });
});

// --- Send a notification to the Receiver API ---
app.MapPost("/api/send-notification", async (
    HttpContext ctx,
    IHttpClientFactory httpClientFactory) =>
{
    // Accept a JSON body for convenience, then forward as protobuf
    var json = await ctx.Request.ReadFromJsonAsync<NotificationInput>();
    if (json is null) return Results.BadRequest("Invalid JSON body");

    var client = httpClientFactory.CreateClient("ReceiverApi");

    var notification = new NotificationMessage
    {
        Source = json.Source ?? "SenderApi",
        Text = json.Text ?? "",
        Level = Enum.TryParse<NotificationLevel>(json.Level, true, out var lvl) ? lvl : NotificationLevel.Info,
        TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Tags = json.Tags ?? []
    };

    var ack = await client.PostProtobufAsync<NotificationMessage, AckResponse>(
        "/api/notifications", notification);

    return Results.Ok(new
    {
        ack.Accepted,
        ack.MessageId,
        SentNotification = new
        {
            notification.Source,
            notification.Text,
            notification.Level,
            notification.Tags
        }
    });
});

// --- Health check ---
app.MapGet("/health", () => Results.Ok("Sender is running"));

app.Run();