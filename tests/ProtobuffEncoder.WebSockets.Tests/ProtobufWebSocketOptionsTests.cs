using ProtobuffEncoder.Transport;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for <see cref="ProtobufWebSocketOptions{TSend, TReceive}"/> and
/// <see cref="ProtobufWebSocketClientOptions"/> — configuration and validation rules.
/// </summary>
public class ProtobufWebSocketOptionsTests
{
    #region Model-State Test Pattern — server options defaults

    [Fact]
    public void ServerOptions_Defaults_AreNull()
    {
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>();

        Assert.Null(opts.OnConnect);
        Assert.Null(opts.OnDisconnect);
        Assert.Null(opts.OnError);
        Assert.Null(opts.OnMessage);
        Assert.Null(opts.ConfigureSendValidation);
        Assert.Null(opts.ConfigureReceiveValidation);
        Assert.Null(opts.OnMessageRejected);
    }

    [Fact]
    public void ServerOptions_InvalidReceiveDefault_IsSkip()
    {
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>();

        Assert.Equal(InvalidMessageBehavior.Skip, opts.OnInvalidReceive);
    }

    #endregion

    #region Model-State Test Pattern — client options defaults

    [Fact]
    public void ClientOptions_RetryPolicy_DefaultsToDefault()
    {
        var opts = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://localhost:5000/test")
        };

        Assert.Same(RetryPolicy.Default, opts.RetryPolicy);
    }

    [Fact]
    public void ClientOptions_Hooks_DefaultToNull()
    {
        var opts = new ProtobufWebSocketClientOptions
        {
            ServerUri = new Uri("ws://localhost:5000/test")
        };

        Assert.Null(opts.OnConnect);
        Assert.Null(opts.OnDisconnect);
        Assert.Null(opts.OnError);
        Assert.Null(opts.OnRetry);
        Assert.Null(opts.ConfigureWebSocket);
    }

    #endregion

    #region Process-Rule Pattern — validation pipeline integration

    [Fact]
    public void ServerOptions_ConfigureSendValidation_IsInvocable()
    {
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>
        {
            ConfigureSendValidation = pipeline =>
            {
                pipeline.Require(m => m.Timestamp > 0, "Timestamp must be positive");
            }
        };

        var pipeline = new ValidationPipeline<Heartbeat>();
        opts.ConfigureSendValidation!(pipeline);

        var valid = pipeline.Validate(new Heartbeat { Timestamp = 100 });
        var invalid = pipeline.Validate(new Heartbeat { Timestamp = 0 });

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
        Assert.Contains("Timestamp", invalid.ErrorMessage);
    }

    [Fact]
    public void ServerOptions_ConfigureReceiveValidation_IsInvocable()
    {
        var opts = new ProtobufWebSocketOptions<ChatMessage, ChatReply>
        {
            ConfigureReceiveValidation = pipeline =>
            {
                pipeline.Require(m => !string.IsNullOrEmpty(m.From), "From is required");
                pipeline.Require(m => !string.IsNullOrEmpty(m.Body), "Body is required");
            }
        };

        var pipeline = new ValidationPipeline<ChatReply>();
        opts.ConfigureReceiveValidation!(pipeline);

        var valid = pipeline.Validate(new ChatReply { From = "srv", Body = "hi" });
        Assert.True(valid.IsValid);

        var invalid = pipeline.Validate(new ChatReply { From = "", Body = "hi" });
        Assert.False(invalid.IsValid);
        Assert.Contains("From", invalid.ErrorMessage);
    }

    #endregion

    #region Constraint-Data Pattern — validation chaining

    [Fact]
    public void ValidationPipeline_MultipleRules_FirstFailureWins()
    {
        var pipeline = new ValidationPipeline<Heartbeat>();
        pipeline.Require(m => m.Timestamp > 0, "Must be positive");
        pipeline.Require(m => m.Timestamp < 1_000_000, "Must be reasonable");

        // Both fail — first rule result is returned
        var result = pipeline.Validate(new Heartbeat { Timestamp = 0 });
        Assert.False(result.IsValid);
        Assert.Equal("Must be positive", result.ErrorMessage);
    }

    [Fact]
    public void ValidationPipeline_NoValidators_AlwaysValid()
    {
        var pipeline = new ValidationPipeline<Heartbeat>();

        Assert.False(pipeline.HasValidators);
        var result = pipeline.Validate(new Heartbeat());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidationPipeline_ValidateOrThrow_ThrowsOnInvalid()
    {
        var pipeline = new ValidationPipeline<Heartbeat>();
        pipeline.Require(m => m.Timestamp > 0, "Timestamp required");

        Assert.Throws<MessageValidationException>(
            () => pipeline.ValidateOrThrow(new Heartbeat { Timestamp = 0 }));
    }

    [Fact]
    public void ValidationPipeline_ValidateOrThrow_PassesOnValid()
    {
        var pipeline = new ValidationPipeline<Heartbeat>();
        pipeline.Require(m => m.Timestamp > 0, "Timestamp required");

        var ex = Record.Exception(
            () => pipeline.ValidateOrThrow(new Heartbeat { Timestamp = 42 }));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidationPipeline_DelegateValidator_CustomLogic()
    {
        var pipeline = new ValidationPipeline<ChatMessage>();
        pipeline.Add(msg =>
        {
            if (string.IsNullOrEmpty(msg.User))
                return ValidationResult.Fail("User is empty");
            if (msg.Text.Length > 500)
                return ValidationResult.Fail("Text too long");
            return ValidationResult.Success;
        });

        Assert.True(pipeline.Validate(new ChatMessage { User = "a", Text = "hi" }).IsValid);
        Assert.False(pipeline.Validate(new ChatMessage { User = "", Text = "hi" }).IsValid);
        Assert.False(pipeline.Validate(new ChatMessage { User = "a", Text = new string('x', 501) }).IsValid);
    }

    #endregion

    #region View-State Test Pattern — InvalidMessageBehavior enum coverage

    [Theory]
    [InlineData(InvalidMessageBehavior.Skip)]
    [InlineData(InvalidMessageBehavior.Throw)]
    [InlineData(InvalidMessageBehavior.ReturnNull)]
    public void OnInvalidReceive_AcceptsAllBehaviors(InvalidMessageBehavior behavior)
    {
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>
        {
            OnInvalidReceive = behavior
        };

        Assert.Equal(behavior, opts.OnInvalidReceive);
    }

    #endregion

    #region Process-Rule Pattern — lifecycle hooks wiring

    [Fact]
    public async Task ServerOptions_OnConnect_IsInvocable()
    {
        var connected = false;
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>
        {
            OnConnect = conn =>
            {
                connected = true;
                Assert.NotNull(conn);
                return Task.CompletedTask;
            }
        };

        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "test");
        await opts.OnConnect!(conn);

        Assert.True(connected);
    }

    [Fact]
    public async Task ServerOptions_OnMessage_ReceivesConnectionAndMessage()
    {
        string? receivedUser = null;
        var opts = new ProtobufWebSocketOptions<ChatReply, ChatMessage>
        {
            OnMessage = (conn, msg) =>
            {
                receivedUser = msg.User;
                return Task.CompletedTask;
            }
        };

        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<ChatReply, ChatMessage>(ws, "test");
        await opts.OnMessage!(conn, new ChatMessage { User = "alice", Text = "hello" });

        Assert.Equal("alice", receivedUser);
    }

    [Fact]
    public async Task ServerOptions_OnError_ReceivesException()
    {
        Exception? receivedEx = null;
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>
        {
            OnError = (conn, ex) =>
            {
                receivedEx = ex;
                return Task.CompletedTask;
            }
        };

        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "test");
        var testEx = new InvalidOperationException("test error");
        await opts.OnError!(conn, testEx);

        Assert.Same(testEx, receivedEx);
    }

    [Fact]
    public async Task ServerOptions_OnMessageRejected_ReceivesValidationResult()
    {
        ValidationResult? rejectedResult = null;
        var opts = new ProtobufWebSocketOptions<Heartbeat, Heartbeat>
        {
            OnMessageRejected = (conn, msg, result) =>
            {
                rejectedResult = result;
                return Task.CompletedTask;
            }
        };

        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "test");
        var failResult = ValidationResult.Fail("bad message");
        await opts.OnMessageRejected!(conn, new Heartbeat(), failResult);

        Assert.NotNull(rejectedResult);
        Assert.False(rejectedResult!.IsValid);
        Assert.Equal("bad message", rejectedResult.ErrorMessage);
    }

    #endregion

    #region Fluent Builder Pattern — ValidationPipeline chaining

    [Fact]
    public void ValidationPipeline_FluentChaining_ReturnsSameInstance()
    {
        var pipeline = new ValidationPipeline<Heartbeat>();

        var result = pipeline
            .Require(m => m.Timestamp > 0, "positive")
            .Require(m => m.Timestamp < 1_000_000, "bounded")
            .Add(m => ValidationResult.Success);

        Assert.Same(pipeline, result);
        Assert.True(pipeline.HasValidators);
    }

    #endregion
}
