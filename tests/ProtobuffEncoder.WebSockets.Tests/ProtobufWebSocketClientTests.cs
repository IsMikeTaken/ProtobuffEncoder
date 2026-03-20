using System.Net.WebSockets;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for <see cref="ProtobufWebSocketClient{TSend, TReceive}"/> — managed client
/// with retry, lifecycle hooks, and EnsureConnected guard.
/// </summary>
public class ProtobufWebSocketClientTests
{
    private static ProtobufWebSocketClientOptions DefaultOptions(Uri? uri = null) => new()
    {
        ServerUri = uri ?? new Uri("ws://localhost:9999/ws/test"),
        RetryPolicy = RetryPolicy.None
    };

    #region Simple-Test Pattern — construction

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProtobufWebSocketClient<Heartbeat, Heartbeat>(null!));
    }

    [Fact]
    public async Task Constructor_ValidOptions_CreatesInstance()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        Assert.NotNull(client);
        Assert.False(client.IsConnected);
        Assert.Null(client.Stream);
    }

    #endregion

    #region Process-State Pattern — IsConnected and EnsureConnected

    [Fact]
    public async Task IsConnected_BeforeConnect_ReturnsFalse()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_ThrowsInvalidOperation()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(new Heartbeat { Timestamp = 1 }));

        Assert.Contains("ConnectAsync", ex.Message);
    }

    [Fact]
    public async Task ReceiveAsync_BeforeConnect_ThrowsInvalidOperation()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ReceiveAsync());
    }

    [Fact]
    public async Task SendAndReceiveAsync_BeforeConnect_ThrowsInvalidOperation()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAndReceiveAsync(new Heartbeat()));
    }

    [Fact]
    public async Task ListenAsync_BeforeConnect_ThrowsInvalidOperation()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListenAsync(_ => Task.CompletedTask));
    }

    [Fact]
    public async Task RunDuplexAsync_BeforeConnect_ThrowsInvalidOperation()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.RunDuplexAsync(
                AsyncEnumerable.Empty<Heartbeat>(),
                _ => Task.CompletedTask));
    }

    #endregion

    #region Process-Sequence Pattern — connect failure behavior

    [Fact]
    public async Task ConnectAsync_NoServer_ThrowsWithNoRetry()
    {
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://127.0.0.1:1/nonexistent"),
            RetryPolicy = RetryPolicy.None
        };

        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);

        // Should fail immediately since RetryPolicy.None means 0 retries
        await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync());
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_Cancellation_ThrowsOperationCanceled()
    {
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://192.0.2.1:1/blackhole"), // Non-routable
            RetryPolicy = RetryPolicy.Default
        };

        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);
        using var cts = new CancellationTokenSource(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ConnectAsync(cts.Token));
    }

    #endregion

    #region Service-Simulation Pattern — lifecycle hooks

    [Fact]
    public async Task ConnectAsync_OnErrorHook_CalledOnFailure()
    {
        var errorCaught = false;
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://127.0.0.1:1/fail"),
            RetryPolicy = RetryPolicy.None,
            OnError = ex => { errorCaught = true; return Task.CompletedTask; }
        };

        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);

        try { await client.ConnectAsync(); } catch { /* expected */ }

        Assert.True(errorCaught, "OnError should have been called");
    }

    [Fact]
    public async Task ConnectAsync_OnRetryHook_CalledWithAttemptAndDelay()
    {
        var retryAttempts = new List<(int Attempt, TimeSpan Delay)>();
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://127.0.0.1:1/fail"),
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = 2,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromMilliseconds(50),
                BackoffMultiplier = 2.0
            },
            OnRetry = (attempt, delay) =>
            {
                retryAttempts.Add((attempt, delay));
                return Task.CompletedTask;
            },
            OnError = _ => Task.CompletedTask
        };

        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);

        try { await client.ConnectAsync(); } catch { /* expected after max retries */ }

        Assert.Equal(2, retryAttempts.Count);
        Assert.Equal(1, retryAttempts[0].Attempt);
        Assert.Equal(2, retryAttempts[1].Attempt);

        // Verify exponential backoff: first delay = 10ms, second = 20ms
        Assert.Equal(TimeSpan.FromMilliseconds(10), retryAttempts[0].Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(20), retryAttempts[1].Delay);
    }

    #endregion

    #region Deadlock-Resolution Pattern — cancellation prevents hanging

    [Fact]
    public async Task ReceiveAllAsync_BeforeConnect_ThrowsImmediately()
    {
        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.ReceiveAllAsync())
            {
                // Should never reach here
                Assert.Fail("Should not enumerate when not connected");
            }
        });
    }

    #endregion

    #region Rollback Pattern — dispose cleanup

    [Fact]
    public async Task DisposeAsync_BeforeConnect_CompletesCleanly()
    {
        var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(DefaultOptions());

        var ex = await Record.ExceptionAsync(() => client.DisposeAsync().AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeAsync_AfterFailedConnect_CompletesCleanly()
    {
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://127.0.0.1:1/fail"),
            RetryPolicy = RetryPolicy.None,
            OnError = _ => Task.CompletedTask
        };

        var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);
        try { await client.ConnectAsync(); } catch { }

        var ex = await Record.ExceptionAsync(() => client.DisposeAsync().AsTask());
        Assert.Null(ex);
    }

    #endregion

    #region Process-Rule Pattern — CloseAsync calls OnDisconnect

    [Fact]
    public async Task CloseAsync_BeforeConnect_InvokesOnDisconnect()
    {
        var disconnected = false;
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://localhost:9999/test"),
            RetryPolicy = RetryPolicy.None,
            OnDisconnect = () => { disconnected = true; return Task.CompletedTask; }
        };

        await using var client = new ProtobufWebSocketClient<Heartbeat, Heartbeat>(options);
        await client.CloseAsync();

        Assert.True(disconnected);
    }

    #endregion

    #region Model-State Test Pattern — options are read correctly

    [Fact]
    public void ConfigureWebSocket_CallbackIsAvailable()
    {
        var configured = false;
        var options = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://localhost:9999/test"),
            ConfigureWebSocket = ws => { configured = true; }
        };

        Assert.NotNull(options.ConfigureWebSocket);
        options.ConfigureWebSocket(new ClientWebSocket());
        Assert.True(configured);
    }

    #endregion
}
