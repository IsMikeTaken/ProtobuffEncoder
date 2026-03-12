namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Configures exponential backoff retry behavior for <see cref="ProtobufWebSocketClient{TSend, TReceive}"/>.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>Maximum number of reconnection attempts. 0 = no retry.</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>Delay before the first retry attempt.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum delay between retry attempts (caps exponential growth).</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Multiplier applied to the delay after each failed attempt.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>Default policy: 5 retries, 1s initial, 30s max, 2x backoff.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>No retries — fail immediately on disconnect.</summary>
    public static RetryPolicy None { get; } = new() { MaxRetries = 0 };

    /// <summary>Calculates the delay for a given attempt number (0-based).</summary>
    internal TimeSpan GetDelay(int attempt)
    {
        var delay = InitialDelay * Math.Pow(BackoffMultiplier, attempt);
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
