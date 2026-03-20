using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.Benchmarks;

// ═══════════════════════════════════════════════════
// Core Encode / Decode
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class EncoderBenchmarks
{
    private SimpleMessage _smallMessage = null!;
    private AllScalarsMessage _largeMessage = null!;
    private byte[] _smallBytes = null!;
    private byte[] _largeBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallMessage = new SimpleMessage { Id = 1, Name = "Small Message" };
        _largeMessage = new AllScalarsMessage
        {
            IntValue = 123,
            StringValue = new string('A', 1000),
            ByteArrayValue = new byte[1000]
        };
        _smallBytes = ProtobufEncoder.Encode(_smallMessage);
        _largeBytes = ProtobufEncoder.Encode(_largeMessage);
    }

    [Benchmark(Baseline = true)]
    public byte[] Encode_Small() => ProtobufEncoder.Encode(_smallMessage);

    [Benchmark]
    public SimpleMessage Decode_Small() => ProtobufEncoder.Decode<SimpleMessage>(_smallBytes);

    [Benchmark]
    public byte[] Encode_Large() => ProtobufEncoder.Encode(_largeMessage);

    [Benchmark]
    public AllScalarsMessage Decode_Large() => ProtobufEncoder.Decode<AllScalarsMessage>(_largeBytes);
}

// ═══════════════════════════════════════════════════
// Collections
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class CollectionBenchmarks
{
    private ListMessage _listMessage = null!;
    private byte[] _listBytes = null!;
    private MapMessage _mapMessage = null!;
    private byte[] _mapBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _listMessage = new ListMessage
        {
            Numbers = Enumerable.Range(0, 100).ToList(),
            Tags = Enumerable.Range(0, 50).Select(i => $"tag-{i}").ToList()
        };
        _listBytes = ProtobufEncoder.Encode(_listMessage);

        _mapMessage = new MapMessage
        {
            Entries = Enumerable.Range(0, 100)
                .ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };
        _mapBytes = ProtobufEncoder.Encode(_mapMessage);
    }

    [Benchmark]
    public byte[] Encode_List() => ProtobufEncoder.Encode(_listMessage);

    [Benchmark]
    public ListMessage Decode_List() => ProtobufEncoder.Decode<ListMessage>(_listBytes);

    [Benchmark]
    public byte[] Encode_Map() => ProtobufEncoder.Encode(_mapMessage);

    [Benchmark]
    public MapMessage Decode_Map() => ProtobufEncoder.Decode<MapMessage>(_mapBytes);
}

// ═══════════════════════════════════════════════════
// Static Message (pre-compiled delegates)
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class StaticMessageBenchmarks
{
    private StaticMessage<SimpleMessage> _static = null!;
    private SimpleMessage _message = null!;
    private byte[] _bytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _static = ProtobufEncoder.CreateStaticMessage<SimpleMessage>();
        _message = new SimpleMessage { Id = 42, Name = "Static Benchmark" };
        _bytes = _static.Encode(_message);
    }

    [Benchmark]
    public byte[] StaticEncode() => _static.Encode(_message);

    [Benchmark]
    public SimpleMessage StaticDecode() => _static.Decode(_bytes);

    [Benchmark]
    public byte[] DynamicEncode() => ProtobufEncoder.Encode(_message);

    [Benchmark]
    public SimpleMessage DynamicDecode() => ProtobufEncoder.Decode<SimpleMessage>(_bytes);
}

// ═══════════════════════════════════════════════════
// Streaming (delimited messages)
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class StreamingBenchmarks
{
    private SimpleMessage _message = null!;
    private byte[] _delimitedBatch = null!;

    [GlobalSetup]
    public void Setup()
    {
        _message = new SimpleMessage { Id = 1, Name = "Stream" };

        using var ms = new MemoryStream();
        for (int i = 0; i < 100; i++)
            ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = i, Name = "msg" }, ms);
        _delimitedBatch = ms.ToArray();
    }

    [Benchmark]
    public void WriteDelimited_100()
    {
        using var ms = new MemoryStream();
        for (int i = 0; i < 100; i++)
            ProtobufEncoder.WriteDelimitedMessage(_message, ms);
    }

    [Benchmark]
    public int ReadDelimited_100()
    {
        using var ms = new MemoryStream(_delimitedBatch);
        return ProtobufEncoder.ReadDelimitedMessages<SimpleMessage>(ms).Count();
    }

    [Benchmark]
    public void SenderReceiver_RoundTrip()
    {
        using var ms = new MemoryStream();
        using var sender = new ProtobufSender<SimpleMessage>(ms, ownsStream: false);
        sender.Send(_message);

        ms.Position = 0;
        using var receiver = new ProtobufReceiver<SimpleMessage>(ms, ownsStream: false);
        receiver.Receive();
    }
}

// ═══════════════════════════════════════════════════
// Validation pipeline
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class ValidationBenchmarks
{
    private ValidationPipeline<SimpleMessage> _pipeline = null!;
    private SimpleMessage _validMessage = null!;
    private SimpleMessage _invalidMessage = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pipeline = new ValidationPipeline<SimpleMessage>();
        _pipeline.Require(m => m.Id > 0, "Id required");
        _pipeline.Require(m => !string.IsNullOrEmpty(m.Name), "Name required");
        _pipeline.Require(m => m.Name.Length <= 100, "Name too long");

        _validMessage = new SimpleMessage { Id = 1, Name = "Valid" };
        _invalidMessage = new SimpleMessage { Id = 0, Name = "" };
    }

    [Benchmark]
    public ValidationResult Validate_Valid() => _pipeline.Validate(_validMessage);

    [Benchmark]
    public ValidationResult Validate_Invalid() => _pipeline.Validate(_invalidMessage);
}

// ═══════════════════════════════════════════════════
// Schema generation
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class SchemaGenerationBenchmarks
{
    [Benchmark]
    public string Generate_SimpleMessage()
        => ProtoSchemaGenerator.Generate(typeof(SimpleMessage));

    [Benchmark]
    public string Generate_NestedMessage()
        => ProtoSchemaGenerator.Generate(typeof(NestedMessage));

    [Benchmark]
    public string Generate_AllScalars()
        => ProtoSchemaGenerator.Generate(typeof(AllScalarsMessage));
}

// ═══════════════════════════════════════════════════
// Payload scaling
// ═══════════════════════════════════════════════════

[MemoryDiagnoser]
[ShortRunJob]
public class PayloadScalingBenchmarks
{
    private AllScalarsMessage _small = null!;
    private AllScalarsMessage _medium = null!;
    private AllScalarsMessage _large = null!;

    [GlobalSetup]
    public void Setup()
    {
        _small = new AllScalarsMessage { StringValue = new string('A', 100), ByteArrayValue = new byte[100] };
        _medium = new AllScalarsMessage { StringValue = new string('A', 10_000), ByteArrayValue = new byte[10_000] };
        _large = new AllScalarsMessage { StringValue = new string('A', 100_000), ByteArrayValue = new byte[100_000] };
    }

    [Benchmark(Baseline = true)]
    public byte[] Encode_100B() => ProtobufEncoder.Encode(_small);

    [Benchmark]
    public byte[] Encode_10KB() => ProtobufEncoder.Encode(_medium);

    [Benchmark]
    public byte[] Encode_100KB() => ProtobufEncoder.Encode(_large);
}

// ═══════════════════════════════════════════════════
// Entry point
// ═══════════════════════════════════════════════════

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run(new[]
        {
            typeof(EncoderBenchmarks),
            typeof(CollectionBenchmarks),
            typeof(StaticMessageBenchmarks),
            typeof(StreamingBenchmarks),
            typeof(ValidationBenchmarks),
            typeof(SchemaGenerationBenchmarks),
            typeof(PayloadScalingBenchmarks)
        });
    }
}

// ═══════════════════════════════════════════════════
// Benchmark models
// ═══════════════════════════════════════════════════

[ProtoContract]
public class SimpleMessage
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
}

[ProtoContract]
public class AllScalarsMessage
{
    [ProtoField(1)] public int IntValue { get; set; }
    [ProtoField(2)] public string StringValue { get; set; } = "";
    [ProtoField(3)] public byte[] ByteArrayValue { get; set; } = [];
}

[ProtoContract]
public class ListMessage
{
    [ProtoField(1)] public List<int> Numbers { get; set; } = [];
    [ProtoField(2)] public List<string> Tags { get; set; } = [];
}

[ProtoContract]
public class MapMessage
{
    [ProtoMap]
    [ProtoField(1)] public Dictionary<string, string> Entries { get; set; } = new();
}

[ProtoContract]
public class NestedMessage
{
    [ProtoField(1)] public string Title { get; set; } = "";
    [ProtoField(2)] public InnerMessage Inner { get; set; } = new();
}

[ProtoContract]
public class InnerMessage
{
    [ProtoField(1)] public int Value { get; set; }
    [ProtoField(2)] public string Detail { get; set; } = "";
}
