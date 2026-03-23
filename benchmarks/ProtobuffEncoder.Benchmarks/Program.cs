using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.Benchmarks;

// ═══════════════════════════════════════════════════
// Multi-TFM Configuration
// ═══════════════════════════════════════════════════

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Validators;

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // 1. The Baseline Job (Statistical Rigor & Enterprise Environment)
        var baseJob = Job.Default
            .WithMaxRelativeError(0.01) // Strict 1% error margin
            .WithMinIterationCount(15)  // Ensure enough data points for statistical validity
            .WithMaxIterationCount(100) // Put a cap on execution time
            .WithWarmupCount(5)         // Give the JIT enough time to optimize hot paths
            .WithGcServer(true)         // Force Server GC (Standard for enterprise web/api apps)
            .WithGcConcurrent(true);    // Background GC enabled

        // 2. Runtimes & Platforms (With corrected IDs)
        // .NET 10
        AddJob(baseJob.WithRuntime(CoreRuntime.Core10_0).WithPlatform(Platform.X64).WithId("x64.Net10"));
        AddJob(baseJob.WithRuntime(CoreRuntime.Core10_0).WithPlatform(Platform.X86).WithId("x86.Net10"));

        // .NET 9
        AddJob(baseJob.WithRuntime(CoreRuntime.Core90).WithPlatform(Platform.X64).WithId("x64.Net9"));
        AddJob(baseJob.WithRuntime(CoreRuntime.Core90).WithPlatform(Platform.X86).WithId("x86.Net9"));

        // .NET 8 (Current LTS)
        AddJob(baseJob.WithRuntime(CoreRuntime.Core80).WithPlatform(Platform.X64).WithId("x64.Net8"));
        AddJob(baseJob.WithRuntime(CoreRuntime.Core80).WithPlatform(Platform.X86).WithId("x86.Net8"));

        // 3. Enterprise Diagnosers
        AddDiagnoser(MemoryDiagnoser.Default);     // Non-negotiable: Tracks byte allocations & GC collections
        AddDiagnoser(ThreadingDiagnoser.Default);  // Crucial: Tracks lock contentions in concurrent code

        // 4. Statistical Columns for the Report
        AddColumn(StatisticColumn.Median);
        AddColumn(StatisticColumn.Min);
        AddColumn(StatisticColumn.Max);
        AddColumn(RankColumn.Arabic); // Easily rank the fastest to slowest setups

        // 5. Exporters (Artifact Generation)
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default); // Excellent for attaching to Jira tickets or CI/CD dashboards

        // 6. Safeguards
        AddValidator(JitOptimizationsValidator.FailOnError); // Refuses to run if you accidentally leave it in Debug mode
    }
}

// ═══════════════════════════════════════════════════
// 1. Core Encode / Decode
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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
            LongValue = 9876543210L,
            DoubleValue = 3.14159265,
            FloatValue = 2.71828f,
            BoolValue = true,
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
// 2. Collections (List + Map)
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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
// 3. Static Message (pre-compiled delegates)
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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
// 4. Streaming (length-delimited messages)
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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
// 5. Duplex Stream
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class DuplexStreamBenchmarks
{
    private SimpleMessage _message = null!;

    [GlobalSetup]
    public void Setup()
    {
        _message = new SimpleMessage { Id = 1, Name = "Duplex" };
    }

    [Benchmark]
    public void DuplexStream_SendAndReceive()
    {
        var sendStream = new MemoryStream();
        var receiveStream = new MemoryStream();

        using var duplex = new ProtobufDuplexStream<SimpleMessage, SimpleMessage>(
            sendStream, receiveStream, ownsStreams: false);
        duplex.Send(_message);

        // Read back from sendStream
        sendStream.Position = 0;
        using var reader = new ProtobufReceiver<SimpleMessage>(sendStream, ownsStream: false);
        reader.Receive();
    }

    [Benchmark]
    public void DuplexStream_SendMany_10()
    {
        var sendStream = new MemoryStream();
        var receiveStream = new MemoryStream();
        using var duplex = new ProtobufDuplexStream<SimpleMessage, SimpleMessage>(
            sendStream, receiveStream, ownsStreams: false);

        for (int i = 0; i < 10; i++)
            duplex.Send(_message);
    }
}

// ═══════════════════════════════════════════════════
// 6. Validation Pipeline
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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

    [Benchmark]
    public void ValidatedSender_Send()
    {
        using var ms = new MemoryStream();
        using var sender = new ValidatedProtobufSender<SimpleMessage>(ms, ownsStream: false);
        sender.Validation.Require(m => m.Id > 0, "Id required");
        sender.Send(_validMessage);
    }
}

// ═══════════════════════════════════════════════════
// 7. Schema Generation
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
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

    [Benchmark]
    public string Generate_WithOneOf()
        => ProtoSchemaGenerator.Generate(typeof(OneOfMessage));

    [Benchmark]
    public string Generate_WithMap()
        => ProtoSchemaGenerator.Generate(typeof(MapMessage));
}

// ═══════════════════════════════════════════════════
// 8. Schema Parsing + SchemaDecoder
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SchemaParsingBenchmarks
{
    private string _protoContent = null!;
    private SchemaDecoder _decoder = null!;
    private byte[] _encodedMessage = null!;

    [GlobalSetup]
    public void Setup()
    {
        _protoContent = ProtoSchemaGenerator.Generate(typeof(SimpleMessage));
        _decoder = SchemaDecoder.FromProtoContent(_protoContent);
        _encodedMessage = ProtobufEncoder.Encode(new SimpleMessage { Id = 42, Name = "parse-bench" });
    }

    [Benchmark]
    public ProtoFile Parse_Proto()
        => ProtoSchemaParser.Parse(_protoContent);

    [Benchmark]
    public DecodedMessage SchemaDecoder_Decode()
        => _decoder.Decode("SimpleMessage", _encodedMessage);
}

// ═══════════════════════════════════════════════════
// 9. ProtobufWriter (low-level)
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ProtobufWriterBenchmarks
{
    [Benchmark]
    public byte[] Writer_SimpleMessage()
    {
        var w = new ProtobufWriter();
        w.WriteVarint(1, 42);
        w.WriteString(2, "ProtobufWriter benchmark");
        return w.ToByteArray();
    }

    [Benchmark]
    public byte[] Writer_NestedMessage()
    {
        var inner = new ProtobufWriter();
        inner.WriteVarint(1, 100);
        inner.WriteString(2, "nested-detail");

        var outer = new ProtobufWriter();
        outer.WriteString(1, "outer-title");
        outer.WriteMessage(2, inner);
        return outer.ToByteArray();
    }

    [Benchmark]
    public byte[] Writer_MapField()
    {
        var w = new ProtobufWriter();
        w.WriteStringStringMap(1,
            Enumerable.Range(0, 10).Select(i => new KeyValuePair<string, string>($"k{i}", $"v{i}")));
        return w.ToByteArray();
    }

    [Benchmark]
    public byte[] Writer_PackedVarints()
    {
        var w = new ProtobufWriter();
        w.WritePackedVarints(1, Enumerable.Range(0, 100).Select(i => (long)i));
        return w.ToByteArray();
    }
}

// ═══════════════════════════════════════════════════
// 10. Payload Scaling
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class PayloadScalingBenchmarks
{
    private AllScalarsMessage _small = null!;
    private AllScalarsMessage _medium = null!;
    private AllScalarsMessage _large = null!;
    private byte[] _smallBytes = null!;
    private byte[] _mediumBytes = null!;
    private byte[] _largeBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _small = new AllScalarsMessage { StringValue = new string('A', 100), ByteArrayValue = new byte[100] };
        _medium = new AllScalarsMessage { StringValue = new string('A', 10_000), ByteArrayValue = new byte[10_000] };
        _large = new AllScalarsMessage { StringValue = new string('A', 100_000), ByteArrayValue = new byte[100_000] };
        _smallBytes = ProtobufEncoder.Encode(_small);
        _mediumBytes = ProtobufEncoder.Encode(_medium);
        _largeBytes = ProtobufEncoder.Encode(_large);
    }

    [Benchmark(Baseline = true)]
    public byte[] Encode_100B() => ProtobufEncoder.Encode(_small);

    [Benchmark]
    public byte[] Encode_10KB() => ProtobufEncoder.Encode(_medium);

    [Benchmark]
    public byte[] Encode_100KB() => ProtobufEncoder.Encode(_large);

    [Benchmark]
    public AllScalarsMessage Decode_100B() => ProtobufEncoder.Decode<AllScalarsMessage>(_smallBytes);

    [Benchmark]
    public AllScalarsMessage Decode_10KB() => ProtobufEncoder.Decode<AllScalarsMessage>(_mediumBytes);

    [Benchmark]
    public AllScalarsMessage Decode_100KB() => ProtobufEncoder.Decode<AllScalarsMessage>(_largeBytes);
}

// ═══════════════════════════════════════════════════
// 11. Nested / Complex Objects
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class NestedObjectBenchmarks
{
    private NestedMessage _shallow = null!;
    private DeepNestedMessage _deep = null!;
    private byte[] _shallowBytes = null!;
    private byte[] _deepBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _shallow = new NestedMessage { Title = "shallow", Inner = new InnerMessage { Value = 42, Detail = "detail" } };
        _deep = new DeepNestedMessage
        {
            Name = "root",
            Level1 = new Level1Message
            {
                Value = 1,
                Level2 = new Level2Message
                {
                    Value = 2,
                    Level3 = new Level3Message { Value = 3, Data = "deep-leaf" }
                }
            }
        };
        _shallowBytes = ProtobufEncoder.Encode(_shallow);
        _deepBytes = ProtobufEncoder.Encode(_deep);
    }

    [Benchmark]
    public byte[] Encode_Shallow() => ProtobufEncoder.Encode(_shallow);

    [Benchmark]
    public NestedMessage Decode_Shallow() => ProtobufEncoder.Decode<NestedMessage>(_shallowBytes);

    [Benchmark]
    public byte[] Encode_Deep_3Levels() => ProtobufEncoder.Encode(_deep);

    [Benchmark]
    public DeepNestedMessage Decode_Deep_3Levels() => ProtobufEncoder.Decode<DeepNestedMessage>(_deepBytes);
}

// ═══════════════════════════════════════════════════
// 12. OneOf Encoding
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class OneOfBenchmarks
{
    private OneOfMessage _withEmail = null!;
    private OneOfMessage _withPhone = null!;
    private byte[] _emailBytes = null!;
    private byte[] _phoneBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _withEmail = new OneOfMessage { Id = 1, Email = "test@example.com" };
        _withPhone = new OneOfMessage { Id = 2, Phone = "+31612345678" };
        _emailBytes = ProtobufEncoder.Encode(_withEmail);
        _phoneBytes = ProtobufEncoder.Encode(_withPhone);
    }

    [Benchmark]
    public byte[] Encode_OneOf_Email() => ProtobufEncoder.Encode(_withEmail);

    [Benchmark]
    public byte[] Encode_OneOf_Phone() => ProtobufEncoder.Encode(_withPhone);

    [Benchmark]
    public OneOfMessage Decode_OneOf_Email() => ProtobufEncoder.Decode<OneOfMessage>(_emailBytes);

    [Benchmark]
    public OneOfMessage Decode_OneOf_Phone() => ProtobufEncoder.Decode<OneOfMessage>(_phoneBytes);
}

// ═══════════════════════════════════════════════════
// 13. Inheritance (ProtoInclude)
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class InheritanceBenchmarks
{
    private DerivedAnimal _dog = null!;
    private byte[] _dogBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dog = new DerivedAnimal { Name = "Rex", Sound = "Woof", LegCount = 4 };
        _dogBytes = ProtobufEncoder.Encode(_dog);
    }

    [Benchmark]
    public byte[] Encode_DerivedType() => ProtobufEncoder.Encode(_dog);

    [Benchmark]
    public DerivedAnimal Decode_DerivedType() => ProtobufEncoder.Decode<DerivedAnimal>(_dogBytes);
}

// ═══════════════════════════════════════════════════
// 14. Async Streaming
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class AsyncStreamingBenchmarks
{
    private SimpleMessage _message = null!;
    private byte[] _encodedAsync = null!;

    [GlobalSetup]
    public void Setup()
    {
        _message = new SimpleMessage { Id = 1, Name = "AsyncBench" };
        _encodedAsync = ProtobufEncoder.Encode(_message);
    }

    [Benchmark]
    public async Task EncodeAsync()
    {
        using var ms = new MemoryStream();
        await ProtobufEncoder.EncodeAsync(_message, ms);
    }

    [Benchmark]
    public async Task<SimpleMessage> DecodeAsync()
    {
        using var ms = new MemoryStream(_encodedAsync);
        return await ProtobufEncoder.DecodeAsync<SimpleMessage>(ms);
    }

    [Benchmark]
    public async Task WriteDelimitedAsync_50()
    {
        using var ms = new MemoryStream();
        for (int i = 0; i < 50; i++)
            await ProtobufEncoder.WriteDelimitedMessageAsync(_message, ms);
    }
}

// ═══════════════════════════════════════════════════
// 15. ContractResolver Caching
// ═══════════════════════════════════════════════════

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ContractResolverBenchmarks
{
    [Benchmark]
    public byte[] FirstCall_NewType_Encode()
    {
        // ContractResolver caches after first call; this measures cached path
        return ProtobufEncoder.Encode(new SimpleMessage { Id = 1, Name = "resolve" });
    }

    [Benchmark]
    public byte[] CachedResolve_AllScalars()
    {
        return ProtobufEncoder.Encode(new AllScalarsMessage { IntValue = 1, StringValue = "test" });
    }

    [Benchmark]
    public byte[] CachedResolve_Nested()
    {
        return ProtobufEncoder.Encode(new NestedMessage
        {
            Title = "t",
            Inner = new InnerMessage { Value = 1, Detail = "d" }
        });
    }
}

// ═══════════════════════════════════════════════════
// Entry point
// ═══════════════════════════════════════════════════

public class Program
{
    public static void Main(string[] args)
    {
        var types = new[]
        {
            typeof(EncoderBenchmarks),
            typeof(CollectionBenchmarks),
            typeof(StaticMessageBenchmarks),
            typeof(StreamingBenchmarks),
            typeof(DuplexStreamBenchmarks),
            typeof(ValidationBenchmarks),
            typeof(SchemaGenerationBenchmarks),
            typeof(SchemaParsingBenchmarks),
            typeof(ProtobufWriterBenchmarks),
            typeof(PayloadScalingBenchmarks),
            typeof(NestedObjectBenchmarks),
            typeof(OneOfBenchmarks),
            typeof(InheritanceBenchmarks),
            typeof(AsyncStreamingBenchmarks),
            typeof(ContractResolverBenchmarks)
        };

        foreach (var type in types)
            BenchmarkRunner.Run(type);
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
    [ProtoField(4)] public long LongValue { get; set; }
    [ProtoField(5)] public double DoubleValue { get; set; }
    [ProtoField(6)] public float FloatValue { get; set; }
    [ProtoField(7)] public bool BoolValue { get; set; }
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

[ProtoContract]
public class DeepNestedMessage
{
    [ProtoField(1)] public string Name { get; set; } = "";
    [ProtoField(2)] public Level1Message Level1 { get; set; } = new();
}

[ProtoContract]
public class Level1Message
{
    [ProtoField(1)] public int Value { get; set; }
    [ProtoField(2)] public Level2Message Level2 { get; set; } = new();
}

[ProtoContract]
public class Level2Message
{
    [ProtoField(1)] public int Value { get; set; }
    [ProtoField(2)] public Level3Message Level3 { get; set; } = new();
}

[ProtoContract]
public class Level3Message
{
    [ProtoField(1)] public int Value { get; set; }
    [ProtoField(2)] public string Data { get; set; } = "";
}

[ProtoContract]
public class OneOfMessage
{
    [ProtoField(1)] public int Id { get; set; }

    [ProtoOneOf("contact")]
    [ProtoField(2)] public string? Email { get; set; }

    [ProtoOneOf("contact")]
    [ProtoField(3)] public string? Phone { get; set; }
}

[ProtoContract(IncludeBaseFields = true)]
[ProtoInclude(10, typeof(DerivedAnimal))]
public class BaseAnimal
{
    [ProtoField(1)] public string Name { get; set; } = "";
    [ProtoField(2)] public string Sound { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class DerivedAnimal : BaseAnimal
{
    [ProtoField(3)] public int LegCount { get; set; }
}
