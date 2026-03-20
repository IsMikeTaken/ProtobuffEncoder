namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for <see cref="RetryPolicy"/> — exponential backoff configuration.
/// </summary>
public class RetryPolicyTests
{
    #region Simple-Test Pattern — default values

    [Fact]
    public void Default_HasExpectedValues()
    {
        var policy = RetryPolicy.Default;

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.MaxDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
    }

    [Fact]
    public void None_HasZeroRetries()
    {
        var policy = RetryPolicy.None;

        Assert.Equal(0, policy.MaxRetries);
    }

    #endregion

    #region Parameter-Range Pattern — boundary values

    [Theory]
    [InlineData(0, 1.0)]   // 1s * 2^0 = 1s
    [InlineData(1, 2.0)]   // 1s * 2^1 = 2s
    [InlineData(2, 4.0)]   // 1s * 2^2 = 4s
    [InlineData(3, 8.0)]   // 1s * 2^3 = 8s
    [InlineData(4, 16.0)]  // 1s * 2^4 = 16s
    public void GetDelay_ExponentialGrowth(int attempt, double expectedSeconds)
    {
        var policy = RetryPolicy.Default;
        var delay = policy.GetDelay(attempt);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void GetDelay_AttemptZero_ReturnsInitialDelay()
    {
        var policy = new RetryPolicy { InitialDelay = TimeSpan.FromMilliseconds(500) };
        var delay = policy.GetDelay(0);

        Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
    }

    [Fact]
    public void GetDelay_NegativeAttempt_DoesNotThrow()
    {
        // Parameter-Range: weird input should not crash
        var policy = RetryPolicy.Default;
        var ex = Record.Exception(() => policy.GetDelay(-1));
        Assert.Null(ex);
    }

    #endregion

    #region Collection-Constraint Pattern — MaxDelay caps growth

    [Fact]
    public void GetDelay_CapsAtMaxDelay()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0
        };

        // Attempt 4: 1s * 2^4 = 16s → capped at 10s
        var delay = policy.GetDelay(4);
        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void GetDelay_VeryHighAttempt_NeverExceedsMaxDelay()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 3.0
        };

        for (int i = 0; i < 100; i++)
        {
            var delay = policy.GetDelay(i);
            Assert.True(delay <= policy.MaxDelay,
                $"Attempt {i}: delay {delay} exceeded max {policy.MaxDelay}");
        }
    }

    #endregion

    #region Constraint-Data Pattern — custom configurations

    [Fact]
    public void CustomPolicy_UsesCustomValues()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 10,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 1.5
        };

        Assert.Equal(10, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.MaxDelay);
        Assert.Equal(1.5, policy.BackoffMultiplier);
    }

    [Fact]
    public void Multiplier_Of_One_ProducesConstantDelay()
    {
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(2),
            BackoffMultiplier = 1.0,
            MaxDelay = TimeSpan.FromMinutes(1)
        };

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(i));
        }
    }

    #endregion

    #region Enumeration Pattern — delay sequence is monotonically non-decreasing

    [Fact]
    public void GetDelay_Sequence_IsMonotonicallyNonDecreasing()
    {
        var policy = RetryPolicy.Default;
        var previous = TimeSpan.Zero;

        for (int i = 0; i < 20; i++)
        {
            var current = policy.GetDelay(i);
            Assert.True(current >= previous,
                $"Delay at attempt {i} ({current}) < previous ({previous})");
            previous = current;
        }
    }

    #endregion

    #region Performance-Test Pattern — calculation speed

    [Fact]
    public void GetDelay_ManyCalculations_CompletesQuickly()
    {
        var policy = RetryPolicy.Default;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 1_000_000; i++)
            policy.GetDelay(i % 20);

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"1M delay calculations took {sw.ElapsedMilliseconds}ms, expected < 500ms");
    }

    #endregion

    #region Model-State Test Pattern — preset instances are singletons

    [Fact]
    public void Default_IsSameInstance()
    {
        Assert.Same(RetryPolicy.Default, RetryPolicy.Default);
    }

    [Fact]
    public void None_IsSameInstance()
    {
        Assert.Same(RetryPolicy.None, RetryPolicy.None);
    }

    #endregion
}
