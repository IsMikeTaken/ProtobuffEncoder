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

// ==============================================================================
// SHOWCASE CLASSES
// ==============================================================================