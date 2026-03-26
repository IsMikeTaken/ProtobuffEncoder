// ProtobuffEncoder — Simple Template
//
// Walks through the basics: contracts, encode/decode, streaming, and
// declaring a service interface for gRPC.
//
// Contracts live in Contracts/, the service interface in Services/.
//
// Run with: dotnet run

using ProtobuffEncoder;
using ProtobuffEncoder.Template.Simple.Contracts;

Console.WriteLine("ProtobuffEncoder — Simple Template\n");

// Encode a request and decode it back.

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

// Nested contracts with repeated fields.

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

// Length-delimited streaming — write multiple messages, read them back.

Console.WriteLine("\nStreaming three forecasts into a MemoryStream...");

using var stream = new MemoryStream();
for (int i = 1; i <= 3; i++)
    ProtobufEncoder.WriteDelimitedMessage(new WeatherForecast { City = $"City-{i}" }, stream);

Console.WriteLine($"Wrote {stream.Length} bytes total");

stream.Position = 0;
foreach (var msg in ProtobufEncoder.ReadDelimitedMessages<WeatherForecast>(stream))
    Console.WriteLine($"  Read back: {msg.City}");

// Static (pre-compiled) encoder for hot paths.

Console.WriteLine("\nStatic encoder round-trip...");
var staticMsg = ProtobufEncoder.CreateStaticMessage<WeatherRequest>();
var fastBytes = staticMsg.Encode(request);
var fastDecoded = staticMsg.Decode(fastBytes);
Console.WriteLine($"  {fastDecoded.City}, {fastDecoded.Days} day(s) — {fastBytes.Length} bytes");

// The IWeatherService interface (in Services/) declares the gRPC contract.

Console.WriteLine("\nService: IWeatherService (see Services/IWeatherService.cs)");
Console.WriteLine("  GetForecast(WeatherRequest) -> WeatherForecast   [Unary]");
Console.WriteLine("  StreamForecasts(WeatherRequest) -> stream        [ServerStreaming]");

Console.WriteLine("\nDone.");
