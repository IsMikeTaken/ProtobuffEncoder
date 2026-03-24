using Microsoft.Extensions.DependencyInjection;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Extensions for registering protobuf validation rules in the DI container.
/// </summary>
public static class ProtobufValidationServiceCollectionExtensions
{
    /// <summary>
    /// Adds protobuf validation support and configures the validation registry.
    /// </summary>
    public static IServiceCollection AddProtobufValidation(
        this IServiceCollection services,
        Action<ProtobufValidationRegistry> configure)
    {
        var registry = new ProtobufValidationRegistry(services);
        configure(registry);
        
        services.AddSingleton<IProtobufValidator, ProtobufValidator>();
        
        return services;
    }
}

/// <summary>
/// Registry for mapping message types to their validation pipelines.
/// </summary>
public class ProtobufValidationRegistry
{
    private readonly IServiceCollection _services;

    public ProtobufValidationRegistry(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a validation rule for a specific message type.
    /// </summary>
    public ProtobufValidationRegistry AddRule<T>(Func<T, bool> predicate, string errorMessage)
    {
        _services.AddSingleton<IMessageValidator<T>>(new DelegateValidator<T>(msg => 
            predicate(msg) ? ValidationResult.Success : ValidationResult.Fail(errorMessage)));
        return this;
    }
}

/// <summary>
/// Service interface for validating messages using registered pipelines.
/// </summary>
public interface IProtobufValidator
{
    ValidationResult Validate<T>(T message);
}

/// <summary>
/// Default implementation of <see cref="IProtobufValidator"/> that resolves
/// all registered <see cref="IMessageValidator{T}"/> for a type.
/// </summary>
public class ProtobufValidator : IProtobufValidator
{
    private readonly IServiceProvider _serviceProvider;

    public ProtobufValidator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ValidationResult Validate<T>(T message)
    {
        var validators = _serviceProvider.GetServices<IMessageValidator<T>>();
        foreach (var validator in validators)
        {
            var result = validator.Validate(message);
            if (!result.IsValid) return result;
        }
        return ValidationResult.Success;
    }
}
