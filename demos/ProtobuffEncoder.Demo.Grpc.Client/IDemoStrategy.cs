namespace ProtobuffEncoder.Demo.Grpc.Client;

public interface IDemoStrategy
{
    string DisplayName { get; }
    Task ExecuteAsync();
}
