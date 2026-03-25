// ProtobuffEncoder — Simple Template
//
// This template walks you through the basics: defining contracts, encoding
// and decoding messages, streaming over a pipe, and declaring a service
// interface that you can later host over gRPC.
//
// Run with: dotnet run

using ProtobuffEncoder;

Console.WriteLine("ProtobuffEncoder — Simple Template\n");

// Start by encoding a request and decoding it back. The two contracts below
// (WeatherRequest and WeatherForecast) model a simple weather lookup.

var request = new WeatherRequest
{
    City = "London",
    Days = 3,
    IncludeWind = true
};

byte[] encoded = ProtobufEncoder.Encode(request);
Console.WriteLine($"Encoded WeatherRequest to {encoded.Length} bytes");

var decoded = ProtobufEncoder.Decode<WeatherRequest>(encoded);
Console.WriteLine($"Decoded: City={decoded.City}, Days={decoded.Days}, IncludeWind={decoded.IncludeWind}");

// Nested contracts work the same way. WeatherForecast contains a list of
// DayEntry items, each with its own fields. The encoder handles the nesting
// and repeated fields automatically.

var forecast = new WeatherForecast
{
    City = "London",
    Entries =
    [
        new DayEntry { Date = "2026-03-25", HighC = 14.5, LowC = 7.2, Condition = "Partly cloudy" },
        new DayEntry { Date = "2026-03-26", HighC = 16.0, LowC = 8.1, Condition = "Sunny" },
        new DayEntry { Date = "2026-03-27", HighC = 12.3, LowC = 6.9, Condition = "Rain" }
    ]
};

var forecastBytes = ProtobufEncoder.Encode(forecast);
var decodedForecast = ProtobufEncoder.Decode<WeatherForecast>(forecastBytes);
Console.WriteLine($"\nForecast for {decodedForecast.City}: {decodedForecast.Entries.Count} day(s)");
foreach (var day in decodedForecast.Entries)
    Console.WriteLine($"  {day.Date}  {day.LowC}–{day.HighC} C  {day.Condition}");

// Length-delimited streaming lets you write multiple messages to a single
// stream and read them back one by one. This is the foundation for transport
// layers like gRPC server-streaming.

Console.WriteLine("\nStreaming three forecasts into a MemoryStream...");

using var stream = new MemoryStream();
for (int i = 1; i <= 3; i++)
{
    var msg = new WeatherForecast { City = $"City-{i}" };
    ProtobufEncoder.WriteDelimitedMessage(msg, stream);
}

Console.WriteLine($"Wrote {stream.Length} bytes total");

stream.Position = 0;
foreach (var msg in ProtobufEncoder.ReadDelimitedMessages<WeatherForecast>(stream))
    Console.WriteLine($"  Read back: {msg.City}");

// A static (pre-compiled) encoder avoids repeated reflection lookups. Use it
// when you encode the same type many times in a hot path.

Console.WriteLine("\nStatic encoder round-trip...");
var staticMsg = ProtobufEncoder.CreateStaticMessage<WeatherRequest>();
var fastBytes = staticMsg.Encode(request);
var fastDecoded = staticMsg.Decode(fastBytes);
Console.WriteLine($"  {fastDecoded.City}, {fastDecoded.Days} day(s) — {fastBytes.Length} bytes");

// Below is a service interface. It does not run in this console app, but it
// shows the contract you would implement and host via the gRPC integration
// package. The [ProtoService] and [ProtoMethod] attributes are all you need;
// no .proto files or code generation required.

Console.WriteLine("\nService interface declared: IWeatherService");
Console.WriteLine("  GetForecast(WeatherRequest) -> WeatherForecast   [Unary]");
Console.WriteLine("  StreamForecasts(WeatherRequest) -> stream        [ServerStreaming]");

Console.WriteLine("\nDone.");
