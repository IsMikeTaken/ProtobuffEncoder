using System.Diagnostics;

namespace ProtobuffEncoder.Demo.Console;

public static class BasicOperationsShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("BasicEncodeDecode");
        if (!options.Silent) System.Console.WriteLine("--- Basic Encode/Decode ---");
        var sw = Stopwatch.StartNew();

        var person = new Person
        {
            Name = "Alice",
            Email = "alice@example.com",
            Age = 30,
            Type = ContactType.Work,
            Tags = ["developer", "lead"],
            LuckyNumbers = [7, 13, 42]
        };

        // Encode
        byte[] encoded = ProtobufEncoder.Encode(person);
        activity?.SetTag("bytes.encoded", encoded.Length);

        // Decode
        var decoded = ProtobufEncoder.Decode<Person>(encoded);

        sw.Stop();
        if (!options.Silent) System.Console.WriteLine($"  Success: {decoded.Name} | Size: {encoded.Length} bytes | Took: {sw.ElapsedMilliseconds}ms");

        // Static Compile
        using var staticActivity = tracer.StartActivity("StaticCompileEncode");
        var staticSw = Stopwatch.StartNew();

        var staticMsg = ProtobufEncoder.CreateStaticMessage<Person>();
        byte[] fast = staticMsg.Encode(person);
        var back = staticMsg.Decode(fast);

        staticSw.Stop();
        staticActivity?.SetTag("bytes.encoded", fast.Length);
        if (!options.Silent) System.Console.WriteLine($"  Static Success: {back.Name} | Size: {fast.Length} bytes | Took: {staticSw.ElapsedMilliseconds}ms");
    }
}