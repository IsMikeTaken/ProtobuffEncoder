using FakeItEasy;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for ValidationPipeline, ValidationResult, DelegateValidator,
/// ValidatedProtobufSender, ValidatedProtobufReceiver, and
/// ValidatedDuplexStream with FakeItEasy mocks. AAA pattern throughout.
/// </summary>
public class ValidationTests
{
    [Fact]
    public void ValidationResult_Success_IsValid()
    {
        // Arrange & Act
        var result = ValidationResult.Success;

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidationResult_Fail_HasError()
    {
        // Arrange & Act
        var result = ValidationResult.Fail("something broke");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("something broke", result.ErrorMessage);
    }

    [Fact]
    public void DelegateValidator_PassingRule_ReturnsSuccess()
    {
        // Arrange
        var validator = new DelegateValidator<SimpleMessage>(
            msg => ValidationResult.Success);

        // Act
        var result = validator.Validate(new SimpleMessage { Id = 1 });

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void DelegateValidator_FailingRule_ReturnsFail()
    {
        // Arrange
        var validator = new DelegateValidator<SimpleMessage>(
            msg => msg.Id <= 0 ? ValidationResult.Fail("Id must be positive") : ValidationResult.Success);

        // Act
        var result = validator.Validate(new SimpleMessage { Id = -1 });

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Id must be positive", result.ErrorMessage);
    }

    [Fact]
    public void Pipeline_NoValidators_ReturnsSuccess()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>();

        // Act
        var result = pipeline.Validate(new SimpleMessage());

        // Assert
        Assert.True(result.IsValid);
        Assert.False(pipeline.HasValidators);
    }

    [Fact]
    public void Pipeline_AllPass_ReturnsSuccess()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Add(msg => ValidationResult.Success)
            .Add(msg => ValidationResult.Success);

        // Act
        var result = pipeline.Validate(new SimpleMessage { Id = 1, Name = "ok" });

        // Assert
        Assert.True(result.IsValid);
        Assert.True(pipeline.HasValidators);
    }

    [Fact]
    public void Pipeline_FirstFails_ReturnsFirstError()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Add(msg => ValidationResult.Fail("first error"))
            .Add(msg => ValidationResult.Fail("second error")); // should not be reached

        // Act
        var result = pipeline.Validate(new SimpleMessage());

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("first error", result.ErrorMessage);
    }

    [Fact]
    public void Pipeline_SecondFails_ReturnsSecondError()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Add(msg => ValidationResult.Success)
            .Add(msg => ValidationResult.Fail("second rule failed"));

        // Act
        var result = pipeline.Validate(new SimpleMessage());

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("second rule failed", result.ErrorMessage);
    }

    [Fact]
    public void Pipeline_Require_PredicateTrue_Passes()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Require(msg => msg.Id > 0, "Id must be positive");

        // Act
        var result = pipeline.Validate(new SimpleMessage { Id = 5 });

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Pipeline_Require_PredicateFalse_Fails()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Require(msg => msg.Id > 0, "Id must be positive");

        // Act
        var result = pipeline.Validate(new SimpleMessage { Id = 0 });

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Id must be positive", result.ErrorMessage);
    }

    [Fact]
    public void Pipeline_ValidateOrThrow_Invalid_ThrowsException()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Require(msg => !string.IsNullOrEmpty(msg.Name), "Name required");

        // Act & Assert
        var ex = Assert.Throws<MessageValidationException>(
            () => pipeline.ValidateOrThrow(new SimpleMessage { Name = "" }));
        Assert.Equal("Name required", ex.Message);
    }

    [Fact]
    public void Pipeline_ValidateOrThrow_Valid_NoException()
    {
        // Arrange
        var pipeline = new ValidationPipeline<SimpleMessage>()
            .Require(msg => msg.Id > 0, "Id needed");

        // Act & Assert — no exception
        pipeline.ValidateOrThrow(new SimpleMessage { Id = 1 });
    }

    [Fact]
    public void Pipeline_AddIMessageValidator_ViaFakeItEasy()
    {
        // Arrange
        var fakeValidator = A.Fake<IMessageValidator<SimpleMessage>>();
        A.CallTo(() => fakeValidator.Validate(A<SimpleMessage>._))
            .Returns(ValidationResult.Fail("fake error"));

        var pipeline = new ValidationPipeline<SimpleMessage>().Add(fakeValidator);

        // Act
        var result = pipeline.Validate(new SimpleMessage());

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("fake error", result.ErrorMessage);
        A.CallTo(() => fakeValidator.Validate(A<SimpleMessage>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Pipeline_FakeValidator_CalledMultipleTimes()
    {
        // Arrange
        var fakeValidator = A.Fake<IMessageValidator<SimpleMessage>>();
        A.CallTo(() => fakeValidator.Validate(A<SimpleMessage>._))
            .Returns(ValidationResult.Success);

        var pipeline = new ValidationPipeline<SimpleMessage>().Add(fakeValidator);

        // Act
        pipeline.Validate(new SimpleMessage { Id = 1 });
        pipeline.Validate(new SimpleMessage { Id = 2 });
        pipeline.Validate(new SimpleMessage { Id = 3 });

        // Assert
        A.CallTo(() => fakeValidator.Validate(A<SimpleMessage>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public void MessageValidationException_ContainsMessage()
    {
        // Arrange & Act
        var msg = new SimpleMessage { Id = 99 };
        var ex = new MessageValidationException("test error", msg);

        // Assert
        Assert.Equal("test error", ex.Message);
        Assert.Same(msg, ex.InvalidMessage);
    }

    [Fact]
    public void MessageValidationException_NullMessage_Works()
    {
        // Arrange & Act
        var ex = new MessageValidationException("no msg");

        // Assert
        Assert.Equal("no msg", ex.Message);
        Assert.Null(ex.InvalidMessage);
    }

    [Fact]
    public void ValidatedSender_ValidMessage_Sends()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var sender = new ValidatedProtobufSender<SimpleMessage>(stream, ownsStream: false);
        sender.Validation.Require(m => m.Id > 0, "Id required");

        // Act — valid message
        sender.Send(new SimpleMessage { Id = 5, Name = "ok" });

        // Assert — data written
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void ValidatedSender_InvalidMessage_Throws()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var sender = new ValidatedProtobufSender<SimpleMessage>(stream, ownsStream: false);
        sender.Validation.Require(m => m.Id > 0, "Id must be > 0");

        // Act & Assert
        Assert.Throws<MessageValidationException>(
            () => sender.Send(new SimpleMessage { Id = 0 }));
    }

    [Fact]
    public async Task ValidatedSender_AsyncValid_Sends()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var sender = new ValidatedProtobufSender<SimpleMessage>(stream, ownsStream: false);
        sender.Validation.Require(m => !string.IsNullOrEmpty(m.Name), "Name required");

        // Act
        await sender.SendAsync(new SimpleMessage { Id = 1, Name = "async-valid" });

        // Assert
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task ValidatedSender_AsyncInvalid_Throws()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var sender = new ValidatedProtobufSender<SimpleMessage>(stream, ownsStream: false);
        sender.Validation.Require(m => !string.IsNullOrEmpty(m.Name), "Name required");

        // Act & Assert
        await Assert.ThrowsAsync<MessageValidationException>(
            () => sender.SendAsync(new SimpleMessage { Id = 1, Name = "" }));
    }

    [Fact]
    public void ValidatedReceiver_ValidMessages_Receives()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 1, Name = "valid" }, stream);
        stream.Position = 0;
        using var receiver = new ValidatedProtobufReceiver<SimpleMessage>(stream, ownsStream: false);
        receiver.Validation.Require(m => m.Id > 0, "Id required");

        // Act
        var msg = receiver.Receive();

        // Assert
        Assert.NotNull(msg);
        Assert.Equal(1, msg.Id);
    }

    [Fact]
    public void ValidatedReceiver_InvalidMessage_ThrowByDefault()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 0, Name = "bad" }, stream);
        stream.Position = 0;
        using var receiver = new ValidatedProtobufReceiver<SimpleMessage>(stream, ownsStream: false);
        receiver.Validation.Require(m => m.Id > 0, "Id must be positive");

        // Act & Assert
        Assert.Throws<MessageValidationException>(() => receiver.Receive());
    }

    [Fact]
    public void ValidatedReceiver_InvalidMessage_SkipBehavior_SkipsAndContinues()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 0, Name = "bad" }, stream);
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 5, Name = "good" }, stream);
        stream.Position = 0;

        using var receiver = new ValidatedProtobufReceiver<SimpleMessage>(stream, ownsStream: false);
        receiver.Validation.Require(m => m.Id > 0, "Id positive");
        receiver.OnInvalid = InvalidMessageBehavior.Skip;

        SimpleMessage? rejected = null;
        receiver.MessageRejected += (msg, _) => rejected = msg;

        // Act
        var result = receiver.Receive();

        // Assert — skipped bad, returned good
        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        Assert.NotNull(rejected);
        Assert.Equal(0, rejected!.Id);
    }

    [Fact]
    public void ValidatedReceiver_InvalidMessage_ReturnNullBehavior()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = -1, Name = "bad" }, stream);
        stream.Position = 0;

        using var receiver = new ValidatedProtobufReceiver<SimpleMessage>(stream, ownsStream: false);
        receiver.Validation.Require(m => m.Id >= 0, "Non-negative");
        receiver.OnInvalid = InvalidMessageBehavior.ReturnNull;

        // Act
        var result = receiver.Receive();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidatedReceiver_ReceiveAll_FiltersInvalid()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 1, Name = "a" }, stream);
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 0, Name = "b" }, stream);
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 2, Name = "c" }, stream);
        stream.Position = 0;

        using var receiver = new ValidatedProtobufReceiver<SimpleMessage>(stream, ownsStream: false);
        receiver.Validation.Require(m => m.Id > 0, "Id positive");
        receiver.OnInvalid = InvalidMessageBehavior.Skip;

        // Act
        var results = receiver.ReceiveAll().ToList();

        // Assert — message with Id=0 was skipped
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal(2, results[1].Id);
    }
}
