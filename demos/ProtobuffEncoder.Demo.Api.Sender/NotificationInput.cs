namespace ProtobuffEncoder.Demo.Api.Sender;

record NotificationInput(string? Source, string? Text, string? Level, List<string>? Tags);