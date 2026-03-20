# Console Demo

This project demonstrates the core capabilities of the `ProtobuffEncoder` library, including:
- Basic serialization and deserialization of simple and nested objects.
- Streaming multiple messages over a single stream using length-delimited framing.
- Bi-directional (duplex) communication using `ProtobufDuplexStream`.
- Message validation using `ValidationPipeline` and validated senders/receivers.
- **New**: Complex aggregate serialization with the `OrderAggregateShowcase`.

## How to Run

1. Navigate to the demo directory:
   ```pwsh
   cd demos/ProtobuffEncoder.Demo.Console
   ```
2. Run the project:
   ```pwsh
   dotnet run
   ```

## Options

- `--verbose`: Enable detailed trace logging.
- `--silent`: Disable standard output (only trace logs if enabled).
