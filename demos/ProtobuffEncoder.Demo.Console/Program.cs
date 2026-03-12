using ProtobuffEncoder;
using ProtobuffEncoder.Console;
using ProtobuffEncoder.Transport;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// --- 1. Global Setup, CLI Args, & Telemetry ---
var options = CliParser.Parse(args);

// Define our global tracer
var tracer = new ActivitySource("ProtobufEncoder.Showcase");

// Attach an ActivityListener to dump trace spans to the console if Verbose
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "ProtobufEncoder.Showcase",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity =>
    {
        if (options.Verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[TRACE] {activity.OperationName} | Duration: {activity.Duration.TotalMilliseconds}ms | Tags: {string.Join(", ", activity.Tags.Select(t => $"{t.Key}={t.Value}"))}");
            Console.ResetColor();
        }
    }
};
ActivitySource.AddActivityListener(listener);

using var cts = new CancellationTokenSource();
var token = cts.Token;

Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n[System] Shutdown requested. Cancelling operations...");
};

try
{
    Log($"Starting Protobuf Showcase (Verbose: {options.Verbose}, Silent: {options.Silent})\n", options);

    // --- 2. Execute Modular Showcases ---

    await BasicOperationsShowcase.RunAsync(tracer, token, options);
    await StreamingShowcase.RunAsync(tracer, token, options);
    await DuplexTransportShowcase.RunAsync(tracer, token, options);
    await ValidatedTransportShowcase.RunAsync(tracer, token, options);

    // --- 3. Keep Alive Block ---
    Log("\n------------------------------------------------", options);
    Log("All showcases executed.", options);
    Log("Console is now acting as an async host.", options);
    Log("Press Ctrl+C to exit gracefully.", options);
    Log("------------------------------------------------", options);

    await Task.Delay(Timeout.Infinite, token);
}
catch (TaskCanceledException)
{
    Console.WriteLine("\nApplication shut down successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nUnhandled Exception: {ex.Message}");
}

// --- Helper to respect Silent mode for standard logs ---
static void Log(string message, CliOptions opts)
{
    if (!opts.Silent)
    {
        Console.WriteLine(message);
    }
}


// ==============================================================================
// CLI PARSER CLASSES
// ==============================================================================

public class CliOptions
{
    public bool Verbose { get; set; }
    public bool Silent { get; set; }
}

public static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "-s":
                case "--silent":
                    options.Silent = true;
                    break;
            }
        }

        return options;
    }
}

// ==============================================================================
// SHOWCASE CLASSES
// ==============================================================================

public static class BasicOperationsShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("BasicEncodeDecode");
        if (!options.Silent) Console.WriteLine("--- Basic Encode/Decode ---");
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
        if (!options.Silent) Console.WriteLine($"  Success: {decoded.Name} | Size: {encoded.Length} bytes | Took: {sw.ElapsedMilliseconds}ms");

        // Static Compile
        using var staticActivity = tracer.StartActivity("StaticCompileEncode");
        var staticSw = Stopwatch.StartNew();

        var staticMsg = ProtobufEncoder.CreateStaticMessage<Person>();
        byte[] fast = staticMsg.Encode(person);
        var back = staticMsg.Decode(fast);

        staticSw.Stop();
        staticActivity?.SetTag("bytes.encoded", fast.Length);
        if (!options.Silent) Console.WriteLine($"  Static Success: {back.Name} | Size: {fast.Length} bytes | Took: {staticSw.ElapsedMilliseconds}ms");
    }
}

public static class StreamingShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("AsyncStreaming");
        if (!options.Silent) Console.WriteLine("\n--- Async Streamed Messages ---");
        var sw = Stopwatch.StartNew();

        await using var asyncStream = new MemoryStream();

        // Simulate ongoing writes
        var writeActivity = tracer.StartActivity("StreamWrites");
        await ProtobufEncoder.WriteDelimitedMessageAsync(new Person { Name = "Dave", Age = 35 }, asyncStream, token);
        await ProtobufEncoder.WriteDelimitedMessageAsync(new Person { Name = "Eve", Age = 28 }, asyncStream, token);
        writeActivity?.SetTag("bytes.written", asyncStream.Position);
        writeActivity?.Dispose();

        asyncStream.Position = 0;

        // Read dynamically as they "arrive"
        int msgCount = 0;
        await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<Person>(asyncStream).WithCancellation(token))
        {
            msgCount++;
            if (!options.Silent) Console.WriteLine($"  Received: {msg.Name}, age={msg.Age}");
        }

        sw.Stop();
        activity?.SetTag("messages.processed", msgCount);
        activity?.SetTag("bytes.total", asyncStream.Length);
        if (!options.Silent) Console.WriteLine($"  Stream processing complete. {msgCount} msgs | {asyncStream.Length} bytes | Took: {sw.ElapsedMilliseconds}ms");
    }
}

public static class DuplexTransportShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("BiDirectionalStreaming");
        if (!options.Silent) Console.WriteLine("\n--- Bi-Directional Streaming ---");
        var sw = Stopwatch.StartNew();

        await using var sendPipe = new MemoryStream();
        await using var recvPipe = new MemoryStream();

        // Pre-fill server response to simulate incoming traffic
        ProtobufEncoder.WriteDelimitedMessage(new Person { Name = "Server-Alice", Age = 30 }, recvPipe);
        ProtobufEncoder.WriteDelimitedMessage(new Person { Name = "Server-Bob", Age = 25 }, recvPipe);
        recvPipe.Position = 0;

        await using var duplex = new ProtobufDuplexStream<Person>(sendPipe, recvPipe, ownsStreams: false);

        // Send Phase
        using var sendActivity = tracer.StartActivity("DuplexSend");
        await duplex.SendAsync(new Person { Name = "Client-Request-1", Age = 1 }, token);
        await duplex.SendAsync(new Person { Name = "Client-Request-2", Age = 2 }, token);
        sendActivity?.SetTag("bytes.sent", sendPipe.Length);
        sendActivity?.Dispose();

        // Receive Phase
        using var recvActivity = tracer.StartActivity("DuplexReceive");
        int recvCount = 0;
        await foreach (var response in duplex.ReceiveAllAsync().WithCancellation(token))
        {
            recvCount++;
            if (!options.Silent) Console.WriteLine($"  Server Replied: {response.Name}");
        }
        recvActivity?.SetTag("messages.received", recvCount);
        recvActivity?.SetTag("bytes.received", recvPipe.Length);

        sw.Stop();
        if (!options.Silent) Console.WriteLine($"  Duplex exchange complete | Sent: {sendPipe.Length}b | Recv: {recvPipe.Length}b | Took: {sw.ElapsedMilliseconds}ms");
    }
}

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