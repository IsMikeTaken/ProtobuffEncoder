using System.Diagnostics;
using ProtobuffEncoder.Console;
using ProtobuffEncoder.Transport;

public static class ValidatedTransportShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("ValidatedTransport");
        if (!options.Silent) Console.WriteLine("\n--- Validated Transport ---");
        var sw = Stopwatch.StartNew();

        await using var validStream = new MemoryStream();
        await using var validSender = new ValidatedProtobufSender<Person>(validStream, ownsStream: false);

        validSender.Validation
            .Require(p => !string.IsNullOrEmpty(p.Name), "Name is required")
            .Require(p => p.Age >= 0, "Age must be non-negative");

        // Valid send
        await validSender.SendAsync(new Person { Name = "ValidPerson", Age = 25 }, token);

        // Invalid send
        try
        {
            using var errorActivity = tracer.StartActivity("InvalidSendAttempt");
            await validSender.SendAsync(new Person { Name = "", Age = 25 }, token);
        }
        catch (MessageValidationException ex)
        {
            if (!options.Silent) Console.WriteLine($"  [Validation Blocked] {ex.Message}");
        }

        sw.Stop();
        activity?.SetTag("bytes.written", validStream.Length);
        if (!options.Silent) Console.WriteLine($"  Validated logic executed | Processed: {validStream.Length} bytes | Took: {sw.ElapsedMilliseconds}ms");
    }
}