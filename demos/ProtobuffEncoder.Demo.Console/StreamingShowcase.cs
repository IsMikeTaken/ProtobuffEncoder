using System.Diagnostics;
using ProtobuffEncoder;
using ProtobuffEncoder.Console;

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