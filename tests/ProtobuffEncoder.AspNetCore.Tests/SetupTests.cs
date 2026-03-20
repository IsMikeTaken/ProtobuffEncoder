using Microsoft.Extensions.DependencyInjection;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.AspNetCore.Setup.Strategies;
using ProtobuffEncoder.AspNetCore.Tests.Fixtures;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.AspNetCore.Tests;

/// <summary>
/// Tests for the unified setup API: Options, Builder, ServiceCollection extensions.
/// </summary>
public class SetupTests
{
    #region Simple-Test Pattern — ProtobufEncoderOptions defaults

    [Fact]
    public void Options_DefaultInvalidMessageBehavior_IsSkip()
    {
        var options = new ProtobufEncoderOptions();
        Assert.Equal(InvalidMessageBehavior.Skip, options.DefaultInvalidMessageBehavior);
    }

    [Fact]
    public void Options_EnableMvcFormatters_DefaultsFalse()
    {
        var options = new ProtobufEncoderOptions();
        Assert.False(options.EnableMvcFormatters);
    }

    [Fact]
    public void Options_OnGlobalValidationFailure_DefaultsNull()
    {
        var options = new ProtobufEncoderOptions();
        Assert.Null(options.OnGlobalValidationFailure);
    }

    [Fact]
    public void Options_AllPropertiesSettable()
    {
        bool callbackCalled = false;
        var options = new ProtobufEncoderOptions
        {
            DefaultInvalidMessageBehavior = InvalidMessageBehavior.Throw,
            EnableMvcFormatters = true,
            OnGlobalValidationFailure = (_, _) => callbackCalled = true
        };

        Assert.Equal(InvalidMessageBehavior.Throw, options.DefaultInvalidMessageBehavior);
        Assert.True(options.EnableMvcFormatters);
        Assert.NotNull(options.OnGlobalValidationFailure);

        options.OnGlobalValidationFailure!(new object(), ValidationResult.Fail("test"));
        Assert.True(callbackCalled);
    }

    #endregion

    #region Process-Sequence Pattern — Builder fluent chaining

    [Fact]
    public void AddProtobuffEncoder_ReturnsBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder();

        Assert.NotNull(builder);
        Assert.IsType<ProtobufEncoderBuilder>(builder);
    }

    [Fact]
    public void AddProtobuffEncoder_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddProtobuffEncoder(opts =>
        {
            opts.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Throw;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ProtobufEncoderOptions>();

        Assert.Equal(InvalidMessageBehavior.Throw, options.DefaultInvalidMessageBehavior);
    }

    [Fact]
    public void AddProtobuffEncoder_RegistersBuilderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddProtobuffEncoder();

        var provider = services.BuildServiceProvider();
        var builder1 = provider.GetRequiredService<ProtobufEncoderBuilder>();
        var builder2 = provider.GetRequiredService<ProtobufEncoderBuilder>();

        Assert.Same(builder1, builder2);
    }

    [Fact]
    public void AddProtobuffEncoder_EnableMvcFormatters_AutoAddsRestStrategy()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder(opts =>
        {
            opts.EnableMvcFormatters = true;
        });

        Assert.Contains(builder.Strategies, s => s is RestFormatterStrategy);
    }

    [Fact]
    public void AddProtobuffEncoder_NoEnableMvc_NoRestStrategy()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder();

        Assert.DoesNotContain(builder.Strategies, s => s is RestFormatterStrategy);
    }

    #endregion

    #region Collection-Order Pattern — strategy registration order

    [Fact]
    public void Builder_WithRestFormatters_AddsRestStrategy()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder()
            .WithRestFormatters();

        Assert.Single(builder.Strategies);
        Assert.IsType<RestFormatterStrategy>(builder.Strategies[0]);
    }

    [Fact]
    public void Builder_FluentChaining_PreservesOrder()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder()
            .WithRestFormatters()
            .WithWebSocket(ws => ws.AddEndpoint<TestRequest, TestResponse>());

        Assert.Equal(2, builder.Strategies.Count);
        Assert.IsType<RestFormatterStrategy>(builder.Strategies[0]);
        Assert.IsType<WebSocketStrategy>(builder.Strategies[1]);
    }

    [Fact]
    public void Builder_AddTransport_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder();

        var result = builder.AddTransport(new RestFormatterStrategy());

        Assert.Same(builder, result);
    }

    #endregion

    #region Collection-Constraint Pattern — custom strategy

    [Fact]
    public void Builder_AddTransport_CustomStrategy_IsRegistered()
    {
        var services = new ServiceCollection();
        var builder = services.AddProtobuffEncoder();
        var custom = new FakeTransportStrategy();

        builder.AddTransport(custom);

        Assert.Contains(builder.Strategies, s => s is FakeTransportStrategy);
        Assert.True(custom.ServicesConfigured);
    }

    #endregion

    #region Process-Rule Pattern — ProtobufMediaType constant

    [Fact]
    public void ProtobufMediaType_HasCorrectValue()
    {
        Assert.Equal("application/x-protobuf", ProtobufMediaType.Protobuf);
    }

    #endregion

    #region Mock-Object Pattern — strategy invocation

    [Fact]
    public void WebSocketStrategy_AddEndpoint_RegistersConnectionManager()
    {
        var services = new ServiceCollection();
        services.AddProtobuffEncoder()
            .WithWebSocket(ws => ws.AddEndpoint<TestRequest, TestResponse>());

        var provider = services.BuildServiceProvider();

        // The WebSocket strategy should register WebSocketConnectionManager<TSend, TReceive>
        var manager = provider.GetService<WebSockets.WebSocketConnectionManager<TestRequest, TestResponse>>();
        Assert.NotNull(manager);
    }

    #endregion

    /// <summary>Fake strategy for testing custom transport registration.</summary>
    private class FakeTransportStrategy : IProtobufTransportStrategy
    {
        public bool ServicesConfigured { get; private set; }
        public bool EndpointsConfigured { get; private set; }

        public void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options)
            => ServicesConfigured = true;

        public void ConfigureEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints,
            ProtobufEncoderOptions options)
            => EndpointsConfigured = true;
    }
}
