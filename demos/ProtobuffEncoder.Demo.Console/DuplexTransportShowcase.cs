using System.Diagnostics;
using ProtobuffEncoder;
using ProtobuffEncoder.Console;
using ProtobuffEncoder.Transport;

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